using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FileMonitoring.Api.Controllers;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FileMonitoring.Api.Tests;

/// <summary>Fayl hodisalari bazaga yozilishi va bildirishnoma yuborish tuzatishlari uchun testlar.</summary>
public class IngestionAndNotificationTests
{
    private const string Secret = "unit-test-secret-value-1234567890";

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(AgentController ctl, Device device)> CreateAgentAsync(AppDbContext db)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(), Hostname = "LOGISTIKA", Platform = "Windows", Status = "Active",
            SecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(Secret))
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Device-Id"] = device.Id.ToString();
        http.Request.Headers["X-Device-Secret"] = Secret;
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Agent:EnrollmentToken"] = "t" }).Build();
        var ctl = new AgentController(db, cfg, new PolicyEngine(db)) { ControllerContext = new ControllerContext { HttpContext = http } };
        return (ctl, device);
    }

    private static EventDto Ev(Guid deviceId, string type = "DOWNLOADED", string name = "hisobot.pdf") =>
        new(Guid.NewGuid(), deviceId, "user", "Telegram", "Telegram.exe",
            new FileInfoDto(name, ".pdf", "application/pdf", 2048, null, @"C:\Users\user\Downloads\Telegram Desktop\" + name),
            type, DateTimeOffset.UtcNow, 0.9f);

    [Fact]
    public async Task Event_with_empty_DeviceId_is_stored_under_authenticated_device()
    {
        // Agent ro'yxatdan o'tishdan oldin yaratgan eventlarda DeviceId = Guid.Empty bo'ladi — avval butun batch 400 bilan rad etilardi.
        await using var db = CreateDb();
        var (ctl, device) = await CreateAgentAsync(db);

        var res = await ctl.Events([Ev(Guid.Empty)]);

        Assert.IsType<OkObjectResult>(res);
        var saved = await db.FileEvents.SingleAsync();
        Assert.Equal(device.Id, saved.DeviceId);
        Assert.Equal(@"C:\Users\user\Downloads\Telegram Desktop\hisobot.pdf", saved.FilePath);
    }

    [Fact]
    public async Task One_invalid_event_does_not_block_valid_ones()
    {
        await using var db = CreateDb();
        var (ctl, device) = await CreateAgentAsync(db);
        var bad = Ev(device.Id) with { EventType = "NOT_A_TYPE" };

        var res = await ctl.Events([Ev(device.Id, "FILE_SENT"), bad, Ev(device.Id, "DOWNLOADED", "b.pdf")]);

        var ok = Assert.IsType<OkObjectResult>(res);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(2, doc.RootElement.GetProperty("accepted").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("rejected").GetInt32());
        Assert.Equal(2, await db.FileEvents.CountAsync());
    }

    [Fact]
    public async Task Transfer_source_and_destination_are_persisted()
    {
        await using var db = CreateDb();
        var (ctl, device) = await CreateAgentAsync(db);

        var ev = Ev(device.Id, "COPIED_IN", "passport.pdf") with
        {
            File = new FileInfoDto(
                "passport.pdf", ".pdf", "application/pdf", 2048, null,
                @"C:\Users\user\Downloads\passport.pdf",
                @"\\server\share\passport.pdf",
                @"C:\Users\user\Downloads\passport.pdf")
        };

        var res = await ctl.Events([ev]);
        Assert.IsType<OkObjectResult>(res);

        var saved = await db.FileEvents.SingleAsync();
        Assert.Equal("COPIED_IN", saved.EventType);
        Assert.Equal(@"\\server\share\passport.pdf", saved.Source);
        Assert.Equal(@"C:\Users\user\Downloads\passport.pdf", saved.Destination);
    }

    [Fact]
    public async Task Future_timestamp_is_clamped_not_rejected()
    {
        await using var db = CreateDb();
        var (ctl, device) = await CreateAgentAsync(db);

        await ctl.Events([Ev(device.Id) with { Timestamp = DateTimeOffset.UtcNow.AddHours(3) }]);

        var saved = await db.FileEvents.SingleAsync();
        Assert.True(saved.Timestamp <= DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Unknown_device_gets_401_but_suspended_device_gets_403()
    {
        await using var db = CreateDb();
        var (ctl, device) = await CreateAgentAsync(db);

        device.Status = "Suspended"; await db.SaveChangesAsync();
        var suspended = Assert.IsType<ObjectResult>(await ctl.Events([Ev(device.Id)]));
        Assert.Equal(403, suspended.StatusCode);

        db.Devices.Remove(device); await db.SaveChangesAsync();
        Assert.IsType<UnauthorizedResult>(await ctl.Events([Ev(device.Id)]));
    }

    private static NotificationsController Notif(AppDbContext db) =>
        new(db, new AuditLogger(db)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Fact]
    public async Task Notification_to_All_creates_one_row_per_active_device()
    {
        await using var db = CreateDb();
        db.Devices.AddRange(
            new Device { Id = Guid.NewGuid(), Hostname = "A", Status = "Active" },
            new Device { Id = Guid.NewGuid(), Hostname = "B", Status = "Active" },
            new Device { Id = Guid.NewGuid(), Hostname = "C", Status = "Suspended" });
        await db.SaveChangesAsync();

        var res = await Notif(db).Create(new CreateNotificationDto("All", null, "Test", "Salom"));

        Assert.IsType<OkObjectResult>(res);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.All(db.Notifications, n => Assert.Equal("Device", n.TargetType));
    }

    [Fact]
    public async Task Notification_to_Device_requires_existing_device_and_text()
    {
        await using var db = CreateDb();
        var device = new Device { Id = Guid.NewGuid(), Hostname = "LOGISTIKA", Status = "Active" };
        db.Devices.Add(device); await db.SaveChangesAsync();
        var ctl = Notif(db);

        Assert.IsType<BadRequestObjectResult>(await ctl.Create(new CreateNotificationDto("Device", null, "T", "M")));           // TargetId yo'q
        Assert.IsType<NotFoundObjectResult>(await ctl.Create(new CreateNotificationDto("Device", Guid.NewGuid(), "T", "M")));   // qurilma yo'q
        Assert.IsType<BadRequestObjectResult>(await ctl.Create(new CreateNotificationDto("Device", device.Id, " ", "M")));      // sarlavha bo'sh
        Assert.IsType<BadRequestObjectResult>(await ctl.Create(new CreateNotificationDto("Nobody", null, "T", "M")));           // noto'g'ri tur
        Assert.IsType<OkObjectResult>(await ctl.Create(new CreateNotificationDto("Device", device.Id, "Test qilyapmiz", "Testing")));
        Assert.Equal("Pending", (await db.Notifications.SingleAsync()).Status);
    }
}
