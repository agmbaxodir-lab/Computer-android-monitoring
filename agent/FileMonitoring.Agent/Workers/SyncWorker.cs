using System.Diagnostics;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Detection;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Notify;
using FileMonitoring.Agent.Queue;
using System.Text.Json;

namespace FileMonitoring.Agent.Workers;

/// <summary>Registration, heartbeat, config sync va navbatni yuborish (exponential backoff + jitter).</summary>
public sealed class SyncWorker(ApiClient api, DeviceIdentity id, LocalQueue queue, AppCatalog catalog, NotifyPipeServer notify, ILogger<SyncWorker> log) : BackgroundService
{
    private int _failures;
    private TimeSpan _cpuPrev; private DateTime _cpuAt = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        DateTime nextHb = DateTime.MinValue, nextCfg = DateTime.MinValue, nextFlush = DateTime.MinValue;
        int hbSec = 60, cfgSec = 300;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;
            if (!id.IsRegistered)
            {
                if (now < nextFlush) continue;
                if (!await api.RegisterAsync(Environment.MachineName, null, ct)) { nextFlush = now + Backoff(); continue; }
                _failures = 0; log.LogInformation("Registered as {Id}", id.DeviceId);
            }
            if (now >= nextCfg)
            {
                var cfg = await api.GetConfigAsync(ct);
                if (cfg is not null) { catalog.Update(cfg.Applications); hbSec = cfg.HeartbeatSeconds; cfgSec = cfg.ConfigPollSeconds; nextCfg = now.AddSeconds(cfgSec); }
                else nextCfg = now.AddSeconds(30);
            }
            if (now >= nextHb)
            {
                var p = Process.GetCurrentProcess();
                var cpu = (p.TotalProcessorTime - _cpuPrev).TotalMilliseconds / ((now - _cpuAt).TotalMilliseconds * Environment.ProcessorCount) * 100;
                _cpuPrev = p.TotalProcessorTime; _cpuAt = now;
                await HeartbeatAndNotifyAsync((float)cpu, (float)(p.WorkingSet64 / 1048576.0), ct);
                nextHb = now.AddSeconds(hbSec);
            }
            if (now >= nextFlush) { await FlushAsync(ct); nextFlush = _failures == 0 ? now.AddSeconds(5) : now + Backoff(); }
        }
    }

    private async Task HeartbeatAndNotifyAsync(float cpu, float mem, CancellationToken ct)
    {
        var r = await api.HeartbeatAsync(new { agentVersion = "1.0.0", username = (string?)null, cpuPercent = cpu, memoryMb = mem, queueSize = queue.Count() }, ct);
        if (r is null) return;
        foreach (var n in r.PendingNotifications) notify.Enqueue(n.Title, n.Message);
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var batch = queue.Peek(100);
            if (batch.Count == 0) { _failures = 0; return; }
            var r = await api.SendEventsAsync(batch, ct);
            if (r == SendResult.Retry) { _failures++; return; }
            queue.Delete(batch.Select(e => e.EventId)); // Ok yoki Reject (zaharli batch navbatni bloklamasin)
            _failures = 0;
        }
    }

    private TimeSpan Backoff()
    {
        var sec = Math.Min(300, Math.Pow(2, Math.Min(_failures, 8)));
        return TimeSpan.FromSeconds(sec + Random.Shared.NextDouble() * sec * 0.2);
    }
}
