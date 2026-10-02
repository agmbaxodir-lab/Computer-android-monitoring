using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using System.Text.Json;
namespace FileMonitoring.Api.Services;

public class AuditLogger(AppDbContext db)
{
    public async Task LogAsync(string actor, string action, string? entity = null, string? entityId = null, object? details = null, string? ip = null)
    {
        db.AuditLogs.Add(new AuditLog { Actor = actor, Action = action, Entity = entity, EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details), IpAddress = ip });
        await db.SaveChangesAsync();
    }
}
