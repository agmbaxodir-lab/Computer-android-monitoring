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

public class FullWorkflowIntegrationTests
{
    private static (AppDbContext db, IConfiguration cfg, PolicyEngine policyEngine) CreateTestEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"Agent:EnrollmentToken", "super-secret-enrollment-token-2026"}
        }).Build();
        var policyEngine = new PolicyEngine(db);
        return (db, cfg, policyEngine);
    }

    [Fact]
    public async Task Complete_EndToEnd_Agent_Lifecycle_Windows_And_Android()
    {
        var (db, cfg, policyEngine) = CreateTestEnvironment();
        await using (db)
        {
            // Seed default application definitions
            var telegramApp = new AppDefinition { Id = Guid.NewGuid(), Name = "Telegram", ProcessNames = new[] { "Telegram.exe", "org.telegram.messenger" } };
            db.Applications.Add(telegramApp);
            await db.SaveChangesAsync();

            // 1. REJECT INVALID ENROLLMENT TOKEN
            var regController = new AgentController(db, cfg, policyEngine)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var invalidReg = await regController.Register(new RegisterRequest("wrong-token", "device-x", null, null, null));
            Assert.IsType<UnauthorizedResult>(invalidReg);

            // 2. ENROLL WINDOWS AGENT
            var winReg = await regController.Register(new RegisterRequest(
                EnrollmentToken: "super-secret-enrollment-token-2026",
                Hostname: "CORP-WIN-001",
                Username: "alice",
                OsVersion: "Windows 11 Pro 23H2",
                AgentVersion: "1.0.0",
                Platform: "Windows"
            ));
            var winOk = Assert.IsType<OkObjectResult>(winReg);
            var winJson = JsonSerializer.Serialize(winOk.Value);
            using var winDoc = JsonDocument.Parse(winJson);
            var winDeviceId = Guid.Parse(winDoc.RootElement.GetProperty("deviceId").GetString()!);
            var winSecret = winDoc.RootElement.GetProperty("deviceSecret").GetString()!;

            // 3. ENROLL ANDROID AGENT
            var andReg = await regController.Register(new RegisterRequest(
                EnrollmentToken: "super-secret-enrollment-token-2026",
                Hostname: "Google-Pixel-8",
                Username: "bob",
                OsVersion: "Android 14 (API 34)",
                AgentVersion: "1.0.0",
                Platform: "Android",
                DeviceModel: "Pixel 8 Pro"
            ));
            var andOk = Assert.IsType<OkObjectResult>(andReg);
            var andJson = JsonSerializer.Serialize(andOk.Value);
            using var andDoc = JsonDocument.Parse(andJson);
            var andDeviceId = Guid.Parse(andDoc.RootElement.GetProperty("deviceId").GetString()!);
            var andSecret = andDoc.RootElement.GetProperty("deviceSecret").GetString()!;

            // 4. HEARTBEAT WITH PENDING NOTIFICATION
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                TargetType = "Device",
                TargetId = andDeviceId,
                Title = "Security Update Required",
                Message = "Please update corporate agent.",
                Status = "Pending"
            });
            await db.SaveChangesAsync();

            var andContext = new DefaultHttpContext();
            andContext.Request.Headers["X-Device-Id"] = andDeviceId.ToString();
            andContext.Request.Headers["X-Device-Secret"] = andSecret;

            var andController = new AgentController(db, cfg, policyEngine)
            {
                ControllerContext = new ControllerContext { HttpContext = andContext }
            };

            var hbResult = await andController.Heartbeat(new HeartbeatRequest(
                AgentVersion: "1.0.0",
                Username: "bob",
                CpuPercent: 1.5f,
                MemoryMb: 45.2f,
                QueueSize: 2
            ));
            var hbOk = Assert.IsType<OkObjectResult>(hbResult);
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var hbDoc = JsonDocument.Parse(JsonSerializer.Serialize(hbOk.Value, jsonOptions));
            var notifications = hbDoc.RootElement.GetProperty("pendingNotifications").EnumerateArray().ToList();
            Assert.Single(notifications);
            Assert.Equal("Security Update Required", notifications[0].GetProperty("title").GetString());

            // Verify notification status transitioned to Delivered
            var notifDb = await db.Notifications.FirstAsync(n => n.TargetId == andDeviceId);
            Assert.Equal("Delivered", notifDb.Status);

            // 5. INGEST FILE EVENTS BATCH (EXTENDED EVENTS: CREATED, MODIFIED, DOWNLOADED)
            var eventId1 = Guid.NewGuid();
            var eventId2 = Guid.NewGuid();
            var eventBatch = new List<EventDto>
            {
                new(
                    EventId: eventId1,
                    DeviceId: andDeviceId,
                    Username: "bob",
                    Application: "Telegram",
                    ProcessName: "org.telegram.messenger",
                    File: new FileInfoDto("passport.pdf", ".pdf", "application/pdf", 204800, "112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00", "/sdcard/Download/passport.pdf"),
                    EventType: "DOWNLOADED",
                    Timestamp: DateTimeOffset.UtcNow,
                    Confidence: 1.0f,
                    Platform: "Android"
                ),
                new(
                    EventId: eventId2,
                    DeviceId: andDeviceId,
                    Username: "bob",
                    Application: null,
                    ProcessName: "org.telegram.messenger",
                    File: new FileInfoDto("notes.txt", ".txt", "text/plain", 120, null, "/sdcard/Documents/notes.txt"),
                    EventType: "MODIFIED",
                    Timestamp: DateTimeOffset.UtcNow,
                    Confidence: 0.85f,
                    Platform: "Android"
                )
            };

            var eventRes = await andController.Events(eventBatch);
            var eventOk = Assert.IsType<OkObjectResult>(eventRes);
            var evDoc = JsonDocument.Parse(JsonSerializer.Serialize(eventOk.Value));
            Assert.Equal(2, evDoc.RootElement.GetProperty("accepted").GetInt32());
            Assert.Equal(0, evDoc.RootElement.GetProperty("duplicates").GetInt32());

            // 6. IDEMPOTENCY TEST: RESEND SAME BATCH
            var resendRes = await andController.Events(eventBatch);
            var resendOk = Assert.IsType<OkObjectResult>(resendRes);
            var resendDoc = JsonDocument.Parse(JsonSerializer.Serialize(resendOk.Value));
            Assert.Equal(0, resendDoc.RootElement.GetProperty("accepted").GetInt32());
            Assert.Equal(2, resendDoc.RootElement.GetProperty("duplicates").GetInt32());

            // 7. QUERY DASHBOARD & DEVICES STATS
            var dashController = new DashboardController(db);
            var dashRes = await dashController.Statistics();
            var dashOk = Assert.IsType<OkObjectResult>(dashRes);
            var dashDoc = JsonDocument.Parse(JsonSerializer.Serialize(dashOk.Value));
            Assert.Equal(2, dashDoc.RootElement.GetProperty("totalDevices").GetInt32());
            Assert.Equal(1, dashDoc.RootElement.GetProperty("windowsDevices").GetInt32());
            Assert.Equal(1, dashDoc.RootElement.GetProperty("androidDevices").GetInt32());

            // 8. QUERY FILE EVENTS WITH PLATFORM & SEARCH FILTER
            var feController = new FileEventsController(db);
            var feRes = await feController.List(null, null, null, platform: "Android", search: "passport", null, null, null, null, null, null);
            var feOk = Assert.IsType<OkObjectResult>(feRes);
            var feDoc = JsonDocument.Parse(JsonSerializer.Serialize(feOk.Value));
            Assert.Equal(1, feDoc.RootElement.GetProperty("total").GetInt32());

            // 9. REJECT UNREGISTERED / WRONG SECRET REQUEST
            var fakeContext = new DefaultHttpContext();
            fakeContext.Request.Headers["X-Device-Id"] = andDeviceId.ToString();
            fakeContext.Request.Headers["X-Device-Secret"] = "incorrect-secret";
            var fakeController = new AgentController(db, cfg, policyEngine)
            {
                ControllerContext = new ControllerContext { HttpContext = fakeContext }
            };
            var fakeRes = await fakeController.Heartbeat(new HeartbeatRequest(null, null, null, null, 0));
            Assert.IsType<UnauthorizedResult>(fakeRes);

            // 10. SUSPEND DEVICE AND VERIFY ACCESS IS BLOCKED
            var devController = new DevicesController(db, new AuditLogger(db))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            await devController.SetStatus(andDeviceId, JsonDocument.Parse("\"Suspended\"").RootElement);
            var suspendedRes = await andController.Heartbeat(new HeartbeatRequest(null, null, null, null, 0));
            // To'xtatilgan qurilma uchun 403 (401 emas): agent "tanilmadim" va "bloklandim"ni farqlay olishi uchun
            var forbidden = Assert.IsType<ObjectResult>(suspendedRes);
            Assert.Equal(403, forbidden.StatusCode);
        }
    }
}
