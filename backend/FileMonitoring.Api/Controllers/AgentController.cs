using System.Security.Cryptography;
using System.Text;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FileMonitoring.Api.Controllers;

public record RegisterRequest(string EnrollmentToken, string Hostname, string? Username, string? OsVersion, string? AgentVersion, string? Platform = "Windows", string? DeviceModel = null);
public record HeartbeatRequest(string? AgentVersion, string? Username, float? CpuPercent, float? MemoryMb, int? QueueSize);
public record FileInfoDto(string Name, string? Extension, string? MimeType, long Size, string? Sha256, string? Path = null, string? Source = null, string? Destination = null);
public record EventDto(Guid EventId, Guid DeviceId, string? Username, string? Application, string? ProcessName, FileInfoDto File, string EventType, DateTimeOffset Timestamp, float Confidence, string? Platform = null);

[ApiController, Route("api/v1/agent"), EnableRateLimiting("agent")]
public class AgentController(AppDbContext db, IConfiguration cfg, FileMonitoring.Api.Services.PolicyEngine policyEngine) : ControllerBase
{
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FILE_SENT", "CREATED", "MODIFIED", "RENAMED", "DELETED", "COPIED", "COPIED_IN", "COPIED_OUT", "MOVED", "DOWNLOADED", "UPLOADED", "SHARED", "OPENED"
    };

    // Authenticate() qurilma topilib, siri to'g'ri bo'lsa-yu, lekin Suspended/Deleted bo'lsa true bo'ladi.
    // Shunda agent "tanilmadim" (401 -> qayta ro'yxatdan o'tadi) va "bloklandim" (403 -> kutadi) holatlarini farqlay oladi.
    private bool _forbidden;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest r)
    {
        var expected = cfg["Agent:EnrollmentToken"] ?? "";
        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(r.EnrollmentToken ?? ""), Encoding.UTF8.GetBytes(expected)))
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
        var d = await Authenticate(); if (d is null) return Denied();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        d.LastHeartbeatAt = DateTimeOffset.UtcNow; d.IpAddress = ip;
        d.AgentVersion = r.AgentVersion ?? d.AgentVersion; d.Username = r.Username ?? d.Username;
        db.Heartbeats.Add(new AgentHeartbeat { DeviceId = d.Id, AgentVersion = r.AgentVersion, IpAddress = ip, CpuPercent = r.CpuPercent, MemoryMb = r.MemoryMb, QueueSize = r.QueueSize });
        await db.SaveChangesAsync();
        var pending = await db.Notifications.Where(n => n.Status == "Pending" &&
            (n.TargetType == "All" || (n.TargetType == "Device" && n.TargetId == d.Id))).OrderBy(n => n.CreatedAt).ToListAsync();
        foreach (var n in pending) { n.Status = "Delivered"; n.DeliveredAt = DateTimeOffset.UtcNow; }
        if (pending.Count > 0) await db.SaveChangesAsync();
        return Ok(new { serverTime = DateTimeOffset.UtcNow, pendingNotifications = pending.Select(n => new { n.Id, n.Title, n.Message }) });
    }

    /// <summary>
    /// Agent yuborgan hodisalarni bazaga yozadi.
    /// - Autentifikatsiyadan o'tgan qurilma ishonchli manba: eventdagi DeviceId e'tiborga olinmaydi
    ///   (agent ro'yxatdan o'tishdan oldin Guid.Empty bilan event yaratishi mumkin edi — avval butun batch rad etilardi).
    /// - Yaroqsiz eventlar alohida rad etiladi, yaroqlilari saqlanadi.
    /// - Faqat "unique event_id" xatosi jim o'tkaziladi; boshqa DB xatolari 500 qaytaradi (agent navbatni o'chirmaydi, qayta urinadi).
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> Events(List<EventDto> batch)
    {
        var d = await Authenticate(); if (d is null) return Denied();
        if (batch is null || batch.Count is 0 or > 500) return BadRequest(new { error = "Batch size must be 1..500" });

        var now = DateTimeOffset.UtcNow;
        var valid = new List<EventDto>();
        var rejected = new List<object>();
        foreach (var e in batch)
        {
            var reason = Validate(e);
            if (reason is null) valid.Add(e); else rejected.Add(new { eventId = e?.EventId, reason });
        }
        if (valid.Count == 0) return BadRequest(new { error = "No valid events in batch", rejected });

        var ids = valid.Select(e => e.EventId).ToList();
        var seen = (await db.FileEvents.Where(e => ids.Contains(e.EventId)).Select(e => e.EventId).ToListAsync()).ToHashSet();
        var allApps = await db.Applications.ToListAsync();
        var appsByName = allApps.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var added = 0; var toEvaluate = new List<FileEvent>();
        foreach (var e in valid.Where(e => seen.Add(e.EventId))) // true = yangi event
        {
            AppDefinition? a = null;
            if (!string.IsNullOrWhiteSpace(e.Application))
                appsByName.TryGetValue(e.Application, out a);
            if (a is null && !string.IsNullOrWhiteSpace(e.ProcessName))
                a = allApps.FirstOrDefault(app => app.ProcessNames.Any(p => string.Equals(p, e.ProcessName, StringComparison.OrdinalIgnoreCase)));

            var eventPlatform = !string.IsNullOrWhiteSpace(e.Platform) ? e.Platform : d.Platform;
            var ts = e.Timestamp == default || e.Timestamp > now.AddMinutes(5) ? now : e.Timestamp; // soat noto'g'ri bo'lgan kompyuterlar uchun
            var sha = e.File.Sha256?.Trim().ToLowerInvariant();
            if (sha is not { Length: 64 }) sha = null; // char(64) ustuniga sig'maydigan qiymat butun batchni yiqitmasin
            var fe = new FileEvent
            {
                Id = Guid.NewGuid(), EventId = e.EventId, DeviceId = d.Id, ApplicationId = a?.Id, Platform = eventPlatform,
                EventType = e.EventType.ToUpperInvariant(), ProcessName = e.ProcessName, OsUsername = e.Username,
                FileName = e.File.Name, FilePath = e.File.Path, Source = e.File.Source, Destination = e.File.Destination, FileExtension = e.File.Extension?.ToLowerInvariant(),
                MimeType = e.File.MimeType, FileSize = e.File.Size, Sha256 = sha,
                Timestamp = ts, Confidence = e.Confidence
            };
            db.FileEvents.Add(fe); toEvaluate.Add(fe);
            added++;
        }

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Parallel so'rov xuddi shu event_id ni yozib ulgurgan: bizdagilarni qaytarib, faqat yangilarini qayta saqlaymiz.
            foreach (var en in db.ChangeTracker.Entries<FileEvent>().Where(x => x.State == EntityState.Added).ToList())
                en.State = EntityState.Detached;
            var existing = (await db.FileEvents.Where(x => ids.Contains(x.EventId)).Select(x => x.EventId).ToListAsync()).ToHashSet();
            var again = toEvaluate.Where(x => !existing.Contains(x.EventId)).ToList();
            toEvaluate.Clear(); added = 0;
            foreach (var fe in again) { db.FileEvents.Add(fe); toEvaluate.Add(fe); added++; }
            await db.SaveChangesAsync();
        }

        // Policy xatosi hodisalar saqlanganiga ta'sir qilmasligi kerak.
        try
        {
            foreach (var fe in toEvaluate)
            {
                var appName = allApps.FirstOrDefault(a => a.Id == fe.ApplicationId)?.Name;
                await policyEngine.EvaluateAsync(fe, appName);
            }
        }
        catch (Exception ex)
        {
            HttpContext?.RequestServices?.GetService<ILogger<AgentController>>()?.LogError(ex, "Policy evaluation failed (events saved)");
        }
        return Ok(new { accepted = added, duplicates = valid.Count - added, rejected = rejected.Count, rejectedItems = rejected });
    }

    [HttpGet("config")]
    public async Task<IActionResult> Config()
    {
        if (await Authenticate() is null) return Denied();
        var apps = await db.Applications.Where(a => a.Enabled).Select(a => new { name = a.Name, processNames = a.ProcessNames, enabled = a.Enabled }).ToListAsync();
        // heartbeatSeconds=15: admin yuborgan bildirishnoma ~15 soniyada kompyuterga yetib boradi
        return Ok(new { applications = apps, heartbeatSeconds = 15, configPollSeconds = 300 });
    }

    private static string? Validate(EventDto? e)
    {
        if (e is null) return "event is null";
        if (e.EventId == Guid.Empty) return "eventId is empty";
        if (string.IsNullOrWhiteSpace(e.EventType) || !AllowedEventTypes.Contains(e.EventType)) return "unknown eventType";
        if (e.Confidence is < 0 or > 1) return "confidence must be 0..1";
        if (e.File is null || string.IsNullOrWhiteSpace(e.File.Name)) return "file.name is required";
        if (e.File.Size < 0) return "file.size must be >= 0";
        return null;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private IActionResult Denied() =>
        _forbidden ? StatusCode(StatusCodes.Status403Forbidden, new { error = "Device is suspended or deleted" }) : Unauthorized();

    private async Task<Device?> Authenticate()
    {
        _forbidden = false;
        if (!Guid.TryParse(Request.Headers["X-Device-Id"], out var id)) return null;
        var secret = Request.Headers["X-Device-Secret"].ToString();
        if (secret.Length == 0) return null;
        var d = await db.Devices.FindAsync(id);
        if (d is null) return null;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), d.SecretHash)) return null;
        if (d.Status != "Active") { _forbidden = true; return null; }
        return d;
    }
}
