using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using FileMonitoring.Agent.Queue;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileMonitoring.Agent.Tests;

public class LocalQueueTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fma_test_" + Guid.NewGuid());
    private LocalQueue NewQueue() => new(Options.Create(new AgentOptions { DataDir = _dir }));

    [Fact]
    public void Enqueue_then_peek_returns_same_event()
    {
        var q = NewQueue();
        var e = Sample();
        q.Enqueue(e);
        var peeked = q.Peek(10);
        Assert.Single(peeked);
        Assert.Equal(e.EventId, peeked[0].EventId);
    }

    [Fact]
    public void Duplicate_event_id_is_stored_once()
    {
        var q = NewQueue();
        var e = Sample();
        q.Enqueue(e); q.Enqueue(e); // ikkinchisi INSERT OR IGNORE bilan e'tiborsiz qoldiriladi
        Assert.Equal(1, q.Count());
    }

    [Fact]
    public void Delete_removes_only_specified_ids()
    {
        var q = NewQueue();
        var e1 = Sample(); var e2 = Sample();
        q.Enqueue(e1); q.Enqueue(e2);
        q.Delete([e1.EventId]);
        Assert.Equal(1, q.Count());
    }

    private static EventDto Sample() => new(Guid.NewGuid(), Guid.NewGuid(), "user", "Telegram", "Telegram.exe",
        new FileInfoDto("report.pdf", ".pdf", "application/pdf", 1024, "deadbeef"), "FILE_SENT", DateTimeOffset.UtcNow, 0.9);

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
}
