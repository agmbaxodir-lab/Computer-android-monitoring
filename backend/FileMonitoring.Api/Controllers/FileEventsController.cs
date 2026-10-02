using FileMonitoring.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

[ApiController, Route("api/v1/file-events"), Authorize]
public class FileEventsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? deviceId,
        [FromQuery] string? platform, [FromQuery] string? search,
        [FromQuery] string? application, [FromQuery] string? extension, [FromQuery] long? minSize, [FromQuery] long? maxSize,
        [FromQuery] string? eventType, [FromQuery] double? minConfidence, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (page < 1) page = 1;
        var baseQ = from e in db.FileEvents.AsNoTracking()
                    join a in db.Applications.AsNoTracking() on e.ApplicationId equals a.Id into aj
                    from a in aj.DefaultIfEmpty()
                    join dv in db.Devices.AsNoTracking() on e.DeviceId equals dv.Id into dj
                    from dv in dj.DefaultIfEmpty()
                    select new { e, AppName = a == null ? null : a.Name, DeviceName = dv == null ? null : dv.Hostname };
        if (from.HasValue) baseQ = baseQ.Where(x => x.e.Timestamp >= from);
        if (to.HasValue) baseQ = baseQ.Where(x => x.e.Timestamp <= to);
        if (deviceId.HasValue) baseQ = baseQ.Where(x => x.e.DeviceId == deviceId);
        if (!string.IsNullOrWhiteSpace(platform)) baseQ = baseQ.Where(x => x.e.Platform.ToLower() == platform.ToLower());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            baseQ = baseQ.Where(x => x.e.FileName.ToLower().Contains(s) || (x.e.FilePath != null && x.e.FilePath.ToLower().Contains(s)) || (x.AppName != null && x.AppName.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(application))
        {
            var app = application.Trim().ToLower();
            baseQ = baseQ.Where(x => x.AppName != null && x.AppName.ToLower().Contains(app));
        }
        if (!string.IsNullOrWhiteSpace(extension))
        {
            var ext = extension.Trim().ToLowerInvariant();
            if (!ext.StartsWith('.')) ext = "." + ext; // "pdf" ham, ".pdf" ham ishlaydi
            baseQ = baseQ.Where(x => x.e.FileExtension == ext);
        }
        if (minSize.HasValue) baseQ = baseQ.Where(x => x.e.FileSize >= minSize);
        if (maxSize.HasValue) baseQ = baseQ.Where(x => x.e.FileSize <= maxSize);
        if (!string.IsNullOrWhiteSpace(eventType)) { var et = eventType.Trim().ToUpperInvariant(); baseQ = baseQ.Where(x => x.e.EventType == et); }
        if (minConfidence.HasValue) baseQ = baseQ.Where(x => x.e.Confidence >= minConfidence);

        var total = await baseQ.CountAsync();
        var items = await baseQ.OrderByDescending(x => x.e.Timestamp).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.e.Id, x.e.EventId, x.e.DeviceId, x.DeviceName, x.e.Platform, x.e.OsUsername, Application = x.AppName, x.e.ProcessName,
                x.e.FileName, x.e.FilePath, x.e.Source, x.e.Destination, x.e.FileExtension, x.e.MimeType, x.e.FileSize, x.e.Sha256, x.e.EventType, x.e.Timestamp, x.e.Confidence, x.e.Status })
            .ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var e = await db.FileEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return e is null ? NotFound() : Ok(e);
    }
}
