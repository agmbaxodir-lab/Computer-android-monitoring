using System.Text.Json;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

[ApiController, Route("api/v1/devices"), Authorize]
public class DevicesController(AppDbContext db, AuditLogger auditLogger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? platform = null,
        [FromQuery] string? search = null,
        [FromQuery] bool includeDeleted = false)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.Devices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(d => d.Status.ToLower() == status.ToLower());
        }
        else if (!includeDeleted)
        {
            q = q.Where(d => d.Status != "Deleted");
        }

        if (!string.IsNullOrWhiteSpace(platform)) q = q.Where(d => d.Platform.ToLower() == platform.ToLower());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(d => d.Hostname.ToLower().Contains(s) || (d.Username != null && d.Username.ToLower().Contains(s)) || (d.IpAddress != null && d.IpAddress.ToLower().Contains(s)));
        }
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.LastHeartbeatAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new { d.Id, d.Hostname, d.Username, d.IpAddress, d.OsVersion, d.AgentVersion, d.Platform, d.DeviceModel, d.Status, d.LastHeartbeatAt,
                Online = d.LastHeartbeatAt != null && d.LastHeartbeatAt > DateTimeOffset.UtcNow.AddMinutes(-3) })
            .ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var d = await db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return d is null ? NotFound() : Ok(new { d.Id, d.Hostname, d.Username, d.IpAddress, d.OsVersion, d.AgentVersion, d.Platform, d.DeviceModel, d.Status, d.LastHeartbeatAt,
            Online = d.LastHeartbeatAt != null && d.LastHeartbeatAt > DateTimeOffset.UtcNow.AddMinutes(-3) });
    }

    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> Events(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.FileEvents.AsNoTracking().Where(e => e.DeviceId == id);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(e => e.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPatch("{id:guid}/status"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] JsonElement body)
    {
        var status = body.ValueKind switch
        {
            JsonValueKind.String => body.GetString(),
            JsonValueKind.Object when body.TryGetProperty("status", out var p) => p.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(status) || status is not ("Active" or "Suspended" or "Deleted"))
            return BadRequest(new { error = "status must be Active, Suspended, or Deleted" });

        var d = await db.Devices.FindAsync(id);
        if (d is null) return NotFound();

        var oldStatus = d.Status;
        d.Status = status;
        await db.SaveChangesAsync();

        var actor = User.Identity?.Name ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var action = status switch
        {
            "Active" => "DeviceEnabled",
            "Suspended" => "DeviceSuspended",
            _ => "DeviceDeactivated"
        };
        await auditLogger.LogAsync(actor, action, "Device", id.ToString(), new { hostname = d.Hostname, oldStatus, newStatus = status }, ip);

        return Ok(new { d.Id, d.Status });
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool hard = false)
    {
        var d = await db.Devices.FindAsync(id);
        if (d is null) return NotFound();

        var actor = User.Identity?.Name ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (hard)
        {
            var hasEvents = await db.FileEvents.AnyAsync(e => e.DeviceId == id);
            if (hasEvents)
            {
                d.Status = "Deleted";
                await db.SaveChangesAsync();
                await auditLogger.LogAsync(actor, "DeviceSoftDeleted", "Device", id.ToString(), new { hostname = d.Hostname, reason = "Preserving existing file events history" }, ip);
                return Ok(new { d.Id, status = d.Status, message = "Device soft-deleted to preserve file events history" });
            }

            db.Devices.Remove(d);
            await db.SaveChangesAsync();
            await auditLogger.LogAsync(actor, "DeviceHardDeleted", "Device", id.ToString(), new { hostname = d.Hostname }, ip);
            return NoContent();
        }

        d.Status = "Deleted";
        await db.SaveChangesAsync();
        await auditLogger.LogAsync(actor, "DeviceDeleted", "Device", id.ToString(), new { hostname = d.Hostname, previousStatus = d.Status }, ip);
        return Ok(new { d.Id, status = d.Status });
    }
}
