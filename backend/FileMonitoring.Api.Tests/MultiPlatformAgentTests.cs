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

public class MultiPlatformAgentTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfig()
    {
        var myConfig = new Dictionary<string, string?>
        {
            {"Agent:EnrollmentToken", "test-token-123"}
        };
        return new ConfigurationBuilder().AddInMemoryCollection(myConfig).Build();
    }

    [Fact]
    public async Task Android_Device_Can_Register_With_Platform_And_DeviceModel()
    {
        await using var db = CreateDb();
        var cfg = CreateConfig();
        var policyEngine = new PolicyEngine(db);
        var controller = new AgentController(db, cfg, policyEngine)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var regReq = new RegisterRequest(
            EnrollmentToken: "test-token-123",
            Hostname: "pixel7-android",
            Username: "john_doe",
            OsVersion: "Android 14 (API 34)",
            AgentVersion: "1.0.0",
            Platform: "Android",
            DeviceModel: "Google Pixel 7"
        );

        var result = await controller.Register(regReq);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var device = await db.Devices.FirstOrDefaultAsync(d => d.Hostname == "pixel7-android");
        Assert.NotNull(device);
        Assert.Equal("Android", device.Platform);
        Assert.Equal("Google Pixel 7", device.DeviceModel);
        Assert.Equal("Android 14 (API 34)", device.OsVersion);
    }

    [Fact]
    public async Task Agent_Can_Ingest_Android_File_Events_With_Extended_Types()
    {
        await using var db = CreateDb();
        var cfg = CreateConfig();
        var policyEngine = new PolicyEngine(db);

        // Register device
        var deviceId = Guid.NewGuid();
        var secret = "test-secret-at-least-32-bytes-long-string==";
        var secretHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));

        var device = new Device
        {
            Id = deviceId,
            Hostname = "galaxy-s24",
            Platform = "Android",
            DeviceModel = "Samsung Galaxy S24",
            SecretHash = secretHash,
            Status = "Active"
        };
        db.Devices.Add(device);

        var app = new AppDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Telegram (Android)",
            ProcessNames = new[] { "org.telegram.messenger" },
            Enabled = true
        };
        db.Applications.Add(app);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Device-Id"] = deviceId.ToString();
        httpContext.Request.Headers["X-Device-Secret"] = secret;

        var controller = new AgentController(db, cfg, policyEngine)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var batch = new List<EventDto>
        {
            new(
                EventId: Guid.NewGuid(),
                DeviceId: deviceId,
                Username: "android_user",
                Application: "Telegram (Android)",
                ProcessName: "org.telegram.messenger",
                File: new FileInfoDto("contract.pdf", ".pdf", "application/pdf", 1024, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "/sdcard/Download/contract.pdf"),
                EventType: "CREATED",
                Timestamp: DateTimeOffset.UtcNow,
                Confidence: 1.0f,
                Platform: "Android"
            ),
            new(
                EventId: Guid.NewGuid(),
                DeviceId: deviceId,
                Username: "android_user",
                Application: null,
                ProcessName: "org.telegram.messenger",
                File: new FileInfoDto("shared_photo.jpg", ".jpg", "image/jpeg", 2048, null, "/sdcard/Pictures/shared_photo.jpg"),
                EventType: "SHARED",
                Timestamp: DateTimeOffset.UtcNow,
                Confidence: 0.95f,
                Platform: "Android"
            )
        };

        var result = await controller.Events(batch);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var events = await db.FileEvents.Where(e => e.DeviceId == deviceId).ToListAsync();
        Assert.Equal(2, events.Count);

        var createdEvent = events.First(e => e.EventType == "CREATED");
        Assert.Equal("contract.pdf", createdEvent.FileName);
        Assert.Equal("/sdcard/Download/contract.pdf", createdEvent.FilePath);
        Assert.Equal("Android", createdEvent.Platform);

        var sharedEvent = events.First(e => e.EventType == "SHARED");
        Assert.Equal("shared_photo.jpg", sharedEvent.FileName);
        Assert.Equal("/sdcard/Pictures/shared_photo.jpg", sharedEvent.FilePath);
        Assert.Equal("Android", sharedEvent.Platform);
        Assert.Equal(app.Id, sharedEvent.ApplicationId); // Matched by package name org.telegram.messenger!
    }

    [Fact]
    public async Task DevicesController_Filters_By_Platform()
    {
        await using var db = CreateDb();
        db.Devices.Add(new Device { Id = Guid.NewGuid(), Hostname = "win-pc-1", Platform = "Windows", Status = "Active" });
        db.Devices.Add(new Device { Id = Guid.NewGuid(), Hostname = "pixel-android", Platform = "Android", Status = "Active" });
        await db.SaveChangesAsync();

        var controller = new DevicesController(db, new AuditLogger(db));
        var resWindows = await controller.List(platform: "Windows");
        var okWin = Assert.IsType<OkObjectResult>(resWindows);
        var winJson = System.Text.Json.JsonSerializer.Serialize(okWin.Value);
        using var winDoc = System.Text.Json.JsonDocument.Parse(winJson);
        Assert.Equal(1, winDoc.RootElement.GetProperty("total").GetInt32());

        var resAndroid = await controller.List(platform: "Android");
        var okAnd = Assert.IsType<OkObjectResult>(resAndroid);
        var andJson = System.Text.Json.JsonSerializer.Serialize(okAnd.Value);
        using var andDoc = System.Text.Json.JsonDocument.Parse(andJson);
        Assert.Equal(1, andDoc.RootElement.GetProperty("total").GetInt32());
    }
}
