using System.Security.Cryptography;
using System.Text;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FileMonitoring.Api.Controllers;

public record RegisterRequest(string EnrollmentToken, string Hostname, string? Username, string? OsVersion, string? AgentVersion, string? Platform = "Windows", string? DeviceModel = null);
public record HeartbeatRequest(string? AgentVersion, string? Username, float? CpuPercent, float? MemoryMb, int? QueueSize);
public record FileInfoDto(string Name, string? Extension, string? MimeType, long Size, string? Sha256, string? Path = null);
public record EventDto(Guid EventId, Guid DeviceId, string? Username, string? Application, string? ProcessName, FileInfoDto File, string EventType, DateTimeOffset Timestamp, float Confidence, string? Platform = null);

[ApiController, Route("api/v1/agent"), EnableRateLimiting("agent")]
public class AgentController(AppDbContext db, IConfiguration cfg, FileMonitoring.Api.Services.PolicyEngine policyEngine) : ControllerBase
{
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FILE_SENT", "CREATED", "MODIFIED", "RENAMED", "DELETED", "COPIED", "MOVED", "DOWNLOADED", "UPLOADED", "SHARED", "OPENED"
    };

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest r)
    {
        var expected = cfg["Agent:EnrollmentToken"] ?? "";
        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(r.EnrollmentToken), Encoding.UTF8.GetBytes(expected)))
            return Unauthorized();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var platform = string.IsNullOrWhiteSpace(r.Platform) ? "Windows" : r.Platform;
        var d = new Device
        {
            Id = Guid.NewGuid(), Hostname = r.Hostname, Username = r.Username, OsVersion = r.OsVersion, AgentVersion = r.AgentVersion,
            Platform = platform, DeviceModel = r.DeviceModel,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), SecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret))
        };
        db.Devices.Add(d); await db.SaveChangesAsync();
        return Ok(new { deviceId = d.Id, deviceSecret = secret });
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(HeartbeatRequest r)
    {
        var d = await Authenticate(); if (d is null) return Unauthorized();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        d.LastHeartbeatAt = DateTimeOffset.UtcNow; d.IpAddress = ip;
        d.AgentVersion = r.AgentVersion ?? d.AgentVersion; d.Username = r.Username ?? d.Username;
        db.Heartbeats.Add(new AgentHeartbeat { DeviceId = d.Id, AgentVersion = r.AgentVersion, IpAddress = ip, CpuPercent = r.CpuPercent, MemoryMb = r.MemoryMb, QueueSize = r.QueueSize });
        await db.SaveChangesAsync();
        var pending = await db.Notifications.Where(n => n.Status == "Pending" &&
            (n.TargetType == "All" || (n.TargetType == "Device" && n.TargetId == d.Id))).ToListAsync();
        foreach (var n in pending) { n.Status = "Delivered"; n.DeliveredAt = DateTimeOffset.UtcNow; }
        if (pending.Count > 0) await db.SaveChangesAsync();
        return Ok(new { serverTime = DateTimeOffset.UtcNow, pendingNotifications = pending.Select(n => new { n.Id, n.Title, n.Message }) });
    }

    [HttpPost("events")]
    public async Task<IActionResult> Events(List<EventDto> batch)
    {
        var d = await Authenticate(); if (d is null) return Unauthorized();
        if (batch.Count is 0 or > 500) return BadRequest(new { error = "Batch size must be 1..500" });
        if (batch.Any(e => e.DeviceId != d.Id || !AllowedEventTypes.Contains(e.EventType) || e.Confidence is < 0 or > 1 || string.IsNullOrWhiteSpace(e.File?.Name) || e.File.Size < 0))
            return BadRequest(new { error = "Invalid event in batch" });
        var now = DateTimeOffset.UtcNow; // replay protection
        if (batch.Any(e => e.Timestamp > now.AddMinutes(5) || e.Timestamp < now.AddDays(-30)))
            return BadRequest(new { error = "Timestamp out of range" });

        var ids = batch.Select(e => e.EventId).ToList();
        var seen = (await db.FileEvents.Where(e => ids.Contains(e.EventId)).Select(e => e.EventId).ToListAsync()).ToHashSet();
        var allApps = await db.Applications.ToListAsync();
        var appsByName = allApps.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        var added = 0; var toEvaluate = new List<FileEvent>();
        foreach (var e in batch.Where(e => seen.Add(e.EventId))) // true = yangi event
        {
            AppDefinition? a = null;
            if (!string.IsNullOrWhiteSpace(e.Application))
                appsByName.TryGetValue(e.Application, out a);
            if (a is null && !string.IsNullOrWhiteSpace(e.ProcessName))
                a = allApps.FirstOrDefault(app => app.ProcessNames.Any(p => string.Equals(p, e.ProcessName, StringComparison.OrdinalIgnoreCase)));

            var eventPlatform = !string.IsNullOrWhiteSpace(e.Platform) ? e.Platform : d.Platform;
            var fe = new FileEvent
            {
                Id = Guid.NewGuid(), EventId = e.EventId, DeviceId = d.Id, ApplicationId = a?.Id, Platform = eventPlatform,
                EventType = e.EventType.ToUpperInvariant(), ProcessName = e.ProcessName, OsUsername = e.Username,
                FileName = e.File.Name, FilePath = e.File.Path, FileExtension = e.File.Extension?.ToLowerInvariant(),
                MimeType = e.File.MimeType, FileSize = e.File.Size, Sha256 = e.File.Sha256?.ToLowerInvariant(),
                Timestamp = e.Timestamp, Confidence = e.Confidence
            };
            db.FileEvents.Add(fe); toEvaluate.Add(fe);
            added++;
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* parallel duplicate (unique event_id) — idempotent */ }
        foreach (var fe in toEvaluate)
        {
            var appName = allApps.FirstOrDefault(a => a.Id == fe.ApplicationId)?.Name;
            await policyEngine.EvaluateAsync(fe, appName);
        }
        return Ok(new { accepted = added, duplicates = batch.Count - added });
    }

    [HttpGet("config")]
    public async Task<IActionResult> Config()
    {
        if (await Authenticate() is null) return Unauthorized();
        var apps = await db.Applications.Where(a => a.Enabled).Select(a => new { name = a.Name, processNames = a.ProcessNames, enabled = a.Enabled }).ToListAsync();
        return Ok(new { applications = apps, heartbeatSeconds = 60, configPollSeconds = 300 });
    }

    private async Task<Device?> Authenticate()
    {
        if (!Guid.TryParse(Request.Headers["X-Device-Id"], out var id)) return null;
        var secret = Request.Headers["X-Device-Secret"].ToString();
        var d = await db.Devices.FindAsync(id);
        if (d is null || d.Status != "Active" || secret.Length == 0) return null;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), d.SecretHash) ? d : null;
    }
}
