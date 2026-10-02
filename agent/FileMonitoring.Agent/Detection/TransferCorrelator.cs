using System.Collections.Concurrent;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using FileMonitoring.Agent.Identity;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Detection;

/// <summary>
/// Windows ETW FileIO Read/Write juftliklaridan USB va SMB/network share nusxalashni aniqlaydi.
/// Fayl mazmunini o'qimaydi; faqat yo'l, hajm va SHA-256 metadata'sini saqlaydi.
/// </summary>
public sealed class TransferCorrelator(
    IOptions<AgentOptions> opt,
    Action<EventDto> emit,
    Func<Guid> deviceId,
    ILogger<TransferCorrelator> log)
{
    private sealed record Access(int Pid, string App, string Proc, string Path, long Bytes, DateTime At, bool Read);
    private readonly ConcurrentDictionary<int, ConcurrentQueue<Access>> _access = new();
    private readonly ConcurrentDictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);

    public void OnRead(int pid, string? app, string proc, string path, long bytes)
        => Add(new Access(pid, app ?? proc, proc, path, bytes, DateTime.UtcNow, true));

    public void OnWrite(int pid, string? app, string proc, string path, long bytes)
        => Add(new Access(pid, app ?? proc, proc, path, bytes, DateTime.UtcNow, false));

    private void Add(Access a)
    {
        if (a.Bytes <= 0 || !IsTransferEndpoint(a.Path)) return;
        _access.GetOrAdd(a.Pid, _ => new ConcurrentQueue<Access>()).Enqueue(a);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _access.ToArray())
        {
            var q = pair.Value;
            var all = q.ToArray();
            while (q.TryPeek(out var first) && now - first.At > TimeSpan.FromSeconds(20)) q.TryDequeue(out _);

            var reads = all.Where(x => x.Read && now - x.At <= TimeSpan.FromSeconds(15)).ToList();
            var writes = all.Where(x => !x.Read && now - x.At >= TimeSpan.FromSeconds(3) && now - x.At <= TimeSpan.FromSeconds(15)).ToList();

            foreach (var w in writes)
            {
                var r = reads
                    .Where(x => x.Path.Equals(w.Path, StringComparison.OrdinalIgnoreCase) == false)
                    .Where(x => string.Equals(Path.GetFileName(x.Path), Path.GetFileName(w.Path), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => Math.Abs((x.At - w.At).TotalSeconds))
                    .FirstOrDefault();
                if (r is null) continue;

                var inCopy = IsExternal(r.Path) && IsLocalUserFile(w.Path);
                var outCopy = IsLocalUserFile(r.Path) && IsExternal(w.Path);
                if (!inCopy && !outCopy) continue;

                var type = inCopy ? "COPIED_IN" : "COPIED_OUT";
                var source = r.Path;
                var destination = w.Path;
                var key = $"{type}|{source}|{destination}";
                if (_recent.TryGetValue(key, out var seen) && now - seen < TimeSpan.FromMinutes(5)) continue;

                var target = inCopy ? destination : source;
                var fi = new FileInfo(target);
                var size = fi.Exists ? fi.Length : Math.Max(r.Bytes, w.Bytes);
                if (size <= 0) continue;

                var ext = Path.GetExtension(Path.GetFileName(target)).ToLowerInvariant();
                var sha = fi.Exists ? await FileMeta.Sha256Async(target, ct) : null;
                var conf = Math.Clamp((Math.Min(r.Bytes, w.Bytes) >= Math.Max(1, size * 0.8) ? 0.95 : 0.80), opt.Value.MinConfidence, 0.99);

                emit(new EventDto(
                    Guid.NewGuid(), deviceId(), Native.SessionUser(w.Pid), r.App, r.Proc,
                    new FileInfoDto(Path.GetFileName(target), ext, FileMeta.MimeOf(ext), size, sha, target, source, destination),
                    type, new DateTimeOffset(w.At, TimeSpan.Zero), conf));
                _recent[key] = now;
                log.LogInformation("{Type} {Source} -> {Destination}", type, source, destination);
            }
        }

        foreach (var k in _recent.Where(x => now - x.Value > TimeSpan.FromMinutes(5)).Select(x => x.Key).ToList())
            _recent.TryRemove(k, out _);
    }

    private static bool IsExternal(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrWhiteSpace(root) && DriveInfo.GetDrives()
                .FirstOrDefault(d => d.Name.Equals(root, StringComparison.OrdinalIgnoreCase)) is { DriveType: DriveType.Removable or DriveType.Network };
        }
        catch { return false; }
    }

    private static bool IsLocalUserFile(string path)
    {
        if (IsExternal(path) || path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) || !Path.IsPathFullyQualified(path))
            return false;
        if (!Path.HasExtension(path)) return false;
        return !path.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(@"\Program Files\", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(@"\Program Files (x86)\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransferEndpoint(string path) =>
        IsExternal(path) || IsLocalUserFile(path);
}
