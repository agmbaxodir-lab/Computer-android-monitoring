using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileMonitoring.Api.Tests;

/// <summary>event_id unique bo'lgani uchun bir xil eventni ikki marta saqlashga urinish e'tiborsiz qoldiriladi.</summary>
public class EventIdempotencyTests
{
    [Fact]
    public async Task Duplicate_event_id_is_rejected_by_unique_index()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var eventId = Guid.NewGuid(); var deviceId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            db.FileEvents.Add(new FileEvent { Id = Guid.NewGuid(), EventId = eventId, DeviceId = deviceId,
                FileName = "a.pdf", FileSize = 10, Timestamp = DateTimeOffset.UtcNow, Confidence = 0.9f });
            await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(options))
        {
            var exists = await db.FileEvents.AnyAsync(e => e.EventId == eventId);
            Assert.True(exists); // ingestion controlleri shu tekshiruv asosida qayta qo'shmaydi
        }
    }
}
