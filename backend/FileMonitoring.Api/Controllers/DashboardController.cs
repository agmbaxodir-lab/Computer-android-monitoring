using FileMonitoring.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

[ApiController, Route("api/v1/dashboard"), Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics()
    {
        var now = DateTimeOffset.UtcNow;
        var totalDevices = await db.Devices.CountAsync();
        var online = await db.Devices.CountAsync(d => d.LastHeartbeatAt != null && d.LastHeartbeatAt > now.AddMinutes(-3));
        var windowsDevices = await db.Devices.CountAsync(d => d.Platform.ToLower() == "windows");
        var androidDevices = await db.Devices.CountAsync(d => d.Platform.ToLower() == "android");
        var eventsToday = await db.FileEvents.CountAsync(e => e.Timestamp >= now.Date);
        var eventsWeek = await db.FileEvents.CountAsync(e => e.Timestamp >= now.AddDays(-7));
        var byApp = await (from e in db.FileEvents
                            join a in db.Applications on e.ApplicationId equals a.Id into aj from a in aj.DefaultIfEmpty()
                            group e by (a == null ? "Unknown" : a.Name) into g
                            select new { application = g.Key, count = g.Count() }).OrderByDescending(x => x.count).Take(10).ToListAsync();
        var byExt = await db.FileEvents.GroupBy(e => e.FileExtension ?? "(none)")
            .Select(g => new { extension = g.Key, count = g.Count() }).OrderByDescending(x => x.count).Take(10).ToListAsync();
        var recentEvents = await db.FileEvents.AsNoTracking().OrderByDescending(e => e.Timestamp).Take(10).ToListAsync();
        var recentAlerts = await db.Alerts.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync();
        return Ok(new { totalDevices, onlineDevices = online, offlineDevices = totalDevices - online,
            windowsDevices, androidDevices,
            eventsToday, eventsWeek, byApplication = byApp, byExtension = byExt, recentEvents, recentAlerts });
    }
}
