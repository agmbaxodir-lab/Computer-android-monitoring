using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

public record CreateAlertDto(Guid? FileEventId, string Severity, string Message);

[ApiController, Route("api/v1/alerts"), Authorize]
public class AlertsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = db.Alerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateAlertDto dto)
    {
        var a = new Alert { FileEventId = dto.FileEventId, Severity = dto.Severity, Message = dto.Message };
        db.Alerts.Add(a); await db.SaveChangesAsync();
        return Ok(a);
    }

    [HttpPatch("{id:guid}/status"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] string status)
    {
        var a = await db.Alerts.FindAsync(id); if (a is null) return NotFound();
        a.Status = status; await db.SaveChangesAsync();
        return Ok(a);
    }
}
