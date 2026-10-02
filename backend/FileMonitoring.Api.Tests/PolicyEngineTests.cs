using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileMonitoring.Api.Tests;

public class PolicyEngineTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Triggers_alert_when_extension_matches()
    {
        await using var db = NewDb();
        var policy = new Policy { Id = Guid.NewGuid(), Name = "Block exe", Enabled = true,
            Rules = [new PolicyRule { Id = Guid.NewGuid(), Field = "extension", Operator = "eq", Value = ".exe" }] };
        db.Policies.Add(policy); await db.SaveChangesAsync();

        var fe = new FileEvent { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), DeviceId = Guid.NewGuid(),
            FileName = "setup.exe", FileExtension = ".exe", FileSize = 1024, Timestamp = DateTimeOffset.UtcNow, Confidence = 0.9f };

        var engine = new PolicyEngine(db);
        await engine.EvaluateAsync(fe, "Telegram");

        Assert.Single(await db.Alerts.ToListAsync());
    }

    [Fact]
    public async Task Does_not_trigger_when_no_rule_matches()
    {
        await using var db = NewDb();
        var policy = new Policy { Id = Guid.NewGuid(), Name = "Big files", Enabled = true,
            Rules = [new PolicyRule { Id = Guid.NewGuid(), Field = "size", Operator = "gt", Value = "100000000" }] };
        db.Policies.Add(policy); await db.SaveChangesAsync();

        var fe = new FileEvent { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), DeviceId = Guid.NewGuid(),
            FileName = "note.txt", FileExtension = ".txt", FileSize = 2048, Timestamp = DateTimeOffset.UtcNow, Confidence = 0.8f };

        var engine = new PolicyEngine(db);
        await engine.EvaluateAsync(fe, "Discord");

        Assert.Empty(await db.Alerts.ToListAsync());
    }

    [Fact]
    public async Task Disabled_policy_is_ignored()
    {
        await using var db = NewDb();
        var policy = new Policy { Id = Guid.NewGuid(), Name = "Disabled", Enabled = false,
            Rules = [new PolicyRule { Id = Guid.NewGuid(), Field = "extension", Operator = "eq", Value = ".pdf" }] };
        db.Policies.Add(policy); await db.SaveChangesAsync();

        var fe = new FileEvent { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), DeviceId = Guid.NewGuid(),
            FileName = "doc.pdf", FileExtension = ".pdf", FileSize = 2048, Timestamp = DateTimeOffset.UtcNow, Confidence = 0.8f };

        var engine = new PolicyEngine(db);
        await engine.EvaluateAsync(fe, "WhatsApp");

        Assert.Empty(await db.Alerts.ToListAsync());
    }
}
