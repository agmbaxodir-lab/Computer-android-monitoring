using System.Security.Claims;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Controllers;

/// <summary>TargetType: "All" (barcha faol qurilmalar) yoki "Device" (bitta qurilma, TargetId = qurilma GUID).</summary>
public record CreateNotificationDto(string TargetType, Guid? TargetId, string Title, string Message);

[ApiController, Route("api/v1/notifications"), Authorize(Roles = "Admin")]
public class NotificationsController(AppDbContext db, AuditLogger audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (page < 1) page = 1;
        var total = await db.Notifications.CountAsync();
        var items = await (from n in db.Notifications.AsNoTracking()
                           join d in db.Devices.AsNoTracking() on n.TargetId equals (Guid?)d.Id into dj
                           from d in dj.DefaultIfEmpty()
                           orderby n.CreatedAt descending
                           select new
                           {
                               n.Id, n.TargetType, n.TargetId,
                               TargetName = n.TargetType == "All" ? "Barcha qurilmalar" : (d == null ? null : d.Hostname),
                               n.Title, n.Message, n.Status, n.CreatedAt, n.DeliveredAt
                           }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>
    /// "All" bo'lsa har bir faol qurilma uchun alohida yozuv yaratiladi — shunda har bir kompyuter xabarni
    /// o'z heartbeat'ida oladi va har birining holati (Pending/Delivered) alohida ko'rinadi.
    /// (Avval bitta "All" yozuv birinchi heartbeat qilgan qurilmaga berilib "Delivered" bo'lib qolardi, qolganlar olmasdi.)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateNotificationDto dto)
    {
        var type = (dto.TargetType ?? "").Trim();
        var title = (dto.Title ?? "").Trim();
        var message = (dto.Message ?? "").Trim();
        if (title.Length == 0) return BadRequest(new { error = "Sarlavha bo'sh bo'lmasligi kerak" });
        if (message.Length == 0) return BadRequest(new { error = "Xabar matni bo'sh bo'lmasligi kerak" });
        if (title.Length > 200) return BadRequest(new { error = "Sarlavha 200 belgidan oshmasligi kerak" });
        if (message.Length > 2000) return BadRequest(new { error = "Xabar 2000 belgidan oshmasligi kerak" });

        var uid = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null;
        var created = new List<Notification>();

        if (type.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var deviceIds = await db.Devices.AsNoTracking().Where(d => d.Status == "Active").Select(d => d.Id).ToListAsync();
            if (deviceIds.Count == 0) return BadRequest(new { error = "Faol qurilma yo'q — xabar yuboriladigan kompyuter topilmadi" });
            foreach (var id in deviceIds)
                created.Add(new Notification { TargetType = "Device", TargetId = id, Title = title, Message = message, CreatedBy = uid });
        }
        else if (type.Equals("Device", StringComparison.OrdinalIgnoreCase))
        {
            if (dto.TargetId is null) return BadRequest(new { error = "Qurilmani tanlang (TargetId kerak)" });
            var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dto.TargetId);
            if (device is null || device.Status == "Deleted") return NotFound(new { error = "Qurilma topilmadi" });
            created.Add(new Notification { TargetType = "Device", TargetId = device.Id, Title = title, Message = message, CreatedBy = uid });
        }
        else return BadRequest(new { error = "TargetType All yoki Device bo'lishi kerak" });

        db.Notifications.AddRange(created);
        await db.SaveChangesAsync();
        await audit.LogAsync(User.Identity?.Name ?? "?", "notification.create", "notification", created[0].Id.ToString(),
            new { type, dto.TargetId, title, recipients = created.Count });
        return Ok(new { created = created.Count, ids = created.Select(n => n.Id) });
    }
}
