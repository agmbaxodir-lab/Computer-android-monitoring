using System.Diagnostics;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Detection;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Notify;
using FileMonitoring.Agent.Queue;

namespace FileMonitoring.Agent.Workers;

/// <summary>Registration, heartbeat, config sync va navbatni yuborish (exponential backoff + jitter).</summary>
public sealed class SyncWorker(ApiClient api, DeviceIdentity id, LocalQueue queue, AppCatalog catalog, NotifyPipeServer notify, ILogger<SyncWorker> log) : BackgroundService
{
    private int _failures;
    private TimeSpan _cpuPrev; private DateTime _cpuAt = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        DateTime nextHb = DateTime.MinValue, nextCfg = DateTime.MinValue, nextFlush = DateTime.MinValue;
        int hbSec = 15, cfgSec = 300;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;

            // Server bu qurilmani tanimayapti (baza tozalangan / qayta yaratilgan): eski identifikatorni tashlab, qayta ro'yxatdan o'tamiz.
            if (api.AuthFailed && id.IsRegistered)
            {
                log.LogWarning("Server qurilmani tanimadi (401). Qayta ro'yxatdan o'tiladi.");
                id.Clear(); api.ClearAuthFailed();
                nextFlush = DateTime.MinValue; nextHb = DateTime.MinValue; nextCfg = DateTime.MinValue;
            }

            if (!id.IsRegistered)
            {
                if (now < nextFlush) continue;
                if (!await api.RegisterAsync(Environment.MachineName, Native.ConsoleUser(), ct)) { _failures++; nextFlush = now.AddSeconds(Math.Min(30, 2 * _failures)); continue; }
                _failures = 0; log.LogInformation("Registered as {Id}", id.DeviceId);
            }
            if (now >= nextCfg)
            {
                var cfg = await api.GetConfigAsync(ct);
                if (cfg is not null)
                {
                    // Server ro'yxati bo'sh bo'lsa lokal (appsettings) ro'yxat saqlanadi — aks holda agent hech narsani kuzatmay qo'yardi.
                    if (cfg.Applications.Count > 0) catalog.Update(cfg.Applications);
                    hbSec = Math.Clamp(cfg.HeartbeatSeconds, 5, 300); cfgSec = Math.Max(30, cfg.ConfigPollSeconds); nextCfg = now.AddSeconds(cfgSec);
                }
                else nextCfg = now.AddSeconds(30);
            }
            if (now >= nextHb)
            {
                var p = Process.GetCurrentProcess();
                var elapsed = Math.Max(1, (now - _cpuAt).TotalMilliseconds);
                var cpu = (p.TotalProcessorTime - _cpuPrev).TotalMilliseconds / (elapsed * Environment.ProcessorCount) * 100;
                _cpuPrev = p.TotalProcessorTime; _cpuAt = now;
                await HeartbeatAndNotifyAsync((float)cpu, (float)(p.WorkingSet64 / 1048576.0), ct);
                nextHb = now.AddSeconds(hbSec);
            }
            if (now >= nextFlush) { await FlushAsync(ct); nextFlush = _failures == 0 ? now.AddSeconds(3) : now + Backoff(); }
        }
    }

    private async Task HeartbeatAndNotifyAsync(float cpu, float mem, CancellationToken ct)
    {
        var r = await api.HeartbeatAsync(new { agentVersion = "1.1.0", username = Native.ConsoleUser(), cpuPercent = cpu, memoryMb = mem, queueSize = queue.Count() }, ct);
        if (r is null) return;
        foreach (var n in r.PendingNotifications)
        {
            log.LogInformation("Bildirishnoma olindi: {Title}", n.Title);
            notify.Enqueue(n.Title, n.Message);
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var deviceId = id.DeviceId;
        if (deviceId is null) return;
        while (!ct.IsCancellationRequested)
        {
            var batch = queue.Peek(100);
            if (batch.Count == 0) { _failures = 0; return; }
            // Ro'yxatdan o'tishdan oldin yaratilgan eventlarda DeviceId = Guid.Empty bo'ladi (yoki qurilma qayta ro'yxatdan o'tgan) —
            // yuborish paytida joriy DeviceId bilan almashtiramiz.
            var toSend = batch.Select(e => e.DeviceId == deviceId.Value ? e : e with { DeviceId = deviceId.Value }).ToList();
            var r = await api.SendEventsAsync(toSend, ct);
            if (r == SendResult.Retry) { _failures++; return; }
            queue.Delete(batch.Select(e => e.EventId)); // Ok yoki Reject (zaharli batch navbatni bloklamasin)
            if (r == SendResult.Ok) log.LogInformation("{N} ta hodisa serverga yuborildi", batch.Count);
            _failures = 0;
        }
    }

    private TimeSpan Backoff()
    {
        var sec = Math.Min(300, Math.Pow(2, Math.Min(_failures, 8)));
        return TimeSpan.FromSeconds(sec + Random.Shared.NextDouble() * sec * 0.2);
    }
}
