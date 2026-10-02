using System.Collections.Concurrent;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Detection;

/// <summary>
/// Heuristik korrelyatsiya: messenger jarayoni FOYDALANUVCHI faylini deyarli to'liq o'qigan
/// (+ shu vaqtda tarmoqqa fayl hajmiga yaqin ma'lumot yuborgan) bo'lsa -> potential FILE_SENT.
/// Bu 100% aniq emas: confidence evristik baho.
/// </summary>
public sealed class Correlator(IOptions<AgentOptions> opt, Action<EventDto> emit, Func<Guid> deviceId, ILogger<Correlator> log)
{
    private sealed class Cand { public int Pid; public string App = "", Proc = "", Path = ""; public long Bytes; public DateTime First, Last; }
    private readonly ConcurrentDictionary<(int, string), Cand> _c = new();
    private readonly ConcurrentDictionary<int, Queue<(DateTime t, long n)>> _net = new();
    private readonly Dictionary<string, DateTime> _recent = new();
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(3);

    public void OnRead(int pid, string app, string proc, string path, int ioSize)
    {
        var now = DateTime.UtcNow;
        var c = _c.GetOrAdd((pid, path), _ => new Cand { Pid = pid, App = app, Proc = proc, Path = path, First = now });
        Interlocked.Add(ref c.Bytes, ioSize); c.Last = now;
    }

    public void OnNetSend(int pid, int size)
    {
        var q = _net.GetOrAdd(pid, _ => new Queue<(DateTime, long)>());
        lock (q) { q.Enqueue((DateTime.UtcNow, size)); while (q.Count > 0 && q.Peek().t < DateTime.UtcNow.AddSeconds(-90)) q.Dequeue(); }
    }

    private long Sent(int pid, DateTime from, DateTime to)
    {
        if (!_net.TryGetValue(pid, out var q)) return 0;
        lock (q) return q.Where(x => x.t >= from && x.t <= to).Sum(x => x.n);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _c.ToArray())
        {
            var c = kv.Value;
            if (now - c.Last < Idle && now - c.First < TimeSpan.FromMinutes(2)) continue;
            if (!_c.TryRemove(kv.Key, out _)) continue;
            await EvaluateAsync(c, ct);
        }
        foreach (var k in _recent.Where(x => now - x.Value > TimeSpan.FromMinutes(2)).Select(x => x.Key).ToList()) _recent.Remove(k);
    }

    private async Task EvaluateAsync(Cand c, CancellationToken ct)
    {
        var fi = new FileInfo(c.Path);
        if (!fi.Exists || fi.Length == 0) return;                 // vaqtinchalik fayl o'chib ketgan
        var ratio = Math.Min(1.0, (double)c.Bytes / fi.Length);
        if (ratio < 0.9) return;                                  // preview/thumbnail: to'liq o'qilmagan
        var conf = 0.75;
        if (fi.Length >= 65536)                                   // kichik fayllarda tarmoq signali ishonchsiz
        {
            var sent = Sent(c.Pid, c.First.AddSeconds(-2), c.Last.AddSeconds(15));
            conf += sent >= fi.Length * 0.8 ? 0.2 : -0.1;
        }
        conf = Math.Min(0.95, conf);
        if (conf < opt.Value.MinConfidence) return;

        var sha = await FileMeta.Sha256Async(c.Path, ct);
        var key = $"{c.App}|{sha ?? c.Path}";
        if (_recent.TryGetValue(key, out var t) && DateTime.UtcNow - t < TimeSpan.FromSeconds(60)) return; // lokal dublikat
        _recent[key] = DateTime.UtcNow;

        var ext = fi.Extension.ToLowerInvariant();
        emit(new EventDto(Guid.NewGuid(), deviceId(), Native.SessionUser(c.Pid), c.App, c.Proc,
            new FileInfoDto(fi.Name, ext, FileMeta.MimeOf(ext), fi.Length, sha), "FILE_SENT", new DateTimeOffset(c.Last, TimeSpan.Zero), Math.Round(conf, 2)));
        log.LogInformation("FILE_SENT candidate {App} {File} conf={C}", c.App, fi.Name, conf);
    }
}
