using System.Security.Claims;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

public record CreateNotificationDto(string TargetType, Guid? TargetId, string Title, string Message);

[ApiController, Route("api/v1/notifications"), Authorize(Roles = "Admin")]
public class NotificationsController(AppDbContext db, AuditLogger audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await db.Notifications.CountAsync();
        var items = await db.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationDto dto)
    {
        if (dto.TargetType is not ("Device" or "User" or "All")) return BadRequest(new { error = "TargetType must be Device/User/All" });
        if (dto.TargetType != "All" && dto.TargetId is null) return BadRequest(new { error = "TargetId is required unless TargetType=All" });
        var uid = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null;
        var n = new Notification { TargetType = dto.TargetType, TargetId = dto.TargetId, Title = dto.Title, Message = dto.Message, CreatedBy = uid };
        db.Notifications.Add(n); await db.SaveChangesAsync();
        await audit.LogAsync(User.Identity?.Name ?? "?", "notification.create", "notification", n.Id.ToString(), dto);
        return Ok(n);
    }
}
