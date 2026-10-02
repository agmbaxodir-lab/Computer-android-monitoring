using System.Collections.Concurrent;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Queue;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Detection;

/// <summary>
/// QABUL QILINGAN fayllarni (DOWNLOADED) aniqlaydi. Avval agent faqat yuborilgan fayllarni (FILE_SENT) ko'rardi,
/// qabul qilinganlari umuman qayd etilmasdi.
///
/// Ikki signal birlashtiriladi:
///  1) ETW: messenger jarayoni foydalanuvchi papkasidagi faylga YOZGAN (NoteWrite)  -> ishonch 0.95
///  2) Papka nomi: yangi fayl "Telegram Desktop", "WhatsApp" kabi messenger papkasida paydo bo'lgan   -> ishonch 0.80
///     (ETW ishlamasa ham ishlaydi)
/// Fayl yozilishi tugagach (3 soniya jim) bitta DOWNLOADED hodisa navbatga qo'yiladi.
/// Fayl mazmuni o'qilmaydi/yuborilmaydi — faqat nom, yo'l, hajm va SHA-256.
/// </summary>
public sealed class ReceiveWatcher(IOptions<AgentOptions> opt, AppCatalog catalog, DeviceIdentity id, LocalQueue queue, ILogger<ReceiveWatcher> log) : BackgroundService
{
    private sealed class PendingFile { public DateTime Last; public bool Created; }
    private sealed record WriterInfo(string App, string Proc, DateTime At);

    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(3);
    private static readonly HashSet<string> TempExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tmp", ".temp", ".part", ".partial", ".crdownload", ".download", ".opdownload", ".lock", ".lnk"
    };
    private static readonly string[] UserSubFolders = ["Downloads", "Documents", "Desktop", "Pictures", "Videos", "Music"];
    private static readonly string[] SkipProfiles = ["Default", "Default User", "All Users", "defaultuser0"];

    private readonly ConcurrentDictionary<string, PendingFile> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WriterInfo> _writers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>ETW (DetectionWorker) chaqiradi: messenger jarayoni shu faylga yozdi.</summary>
    public void NoteWrite(int pid, string app, string proc, string path)
    {
        _writers[path] = new WriterInfo(app, proc, DateTime.UtcNow);
        Touch(path, created: false);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        RefreshWatchers();
        var nextScan = DateTime.UtcNow.AddSeconds(60);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (DateTime.UtcNow >= nextScan) { RefreshWatchers(); nextScan = DateTime.UtcNow.AddSeconds(60); } // yangi foydalanuvchi profillari
                await FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { foreach (var w in _watchers.Values) w.Dispose(); _watchers.Clear(); }
    }

    private void Touch(string path, bool created)
    {
        if (_pending.Count > 5000 && !_pending.ContainsKey(path)) return; // xotira himoyasi
        var now = DateTime.UtcNow;
        _pending.AddOrUpdate(path,
            _ => new PendingFile { Last = now, Created = created },
            (_, p) => { p.Last = now; p.Created |= created; return p; });
    }

    // ---------------- kuzatuvchilar ----------------

    private void RefreshWatchers()
    {
        foreach (var root in WatchRoots())
        {
            if (_watchers.ContainsKey(root)) continue;
            try
            {
                var w = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };
                w.Created += (_, e) => Touch(e.FullPath, created: true);
                w.Changed += (_, e) => Touch(e.FullPath, created: false);
                w.Renamed += (_, e) => Touch(e.FullPath, created: true); // "x.part" -> "x.pdf" ko'rinishidagi yakunlash
                w.Error += (_, e) => log.LogWarning(e.GetException(), "FileSystemWatcher xatosi: {Root}", root);
                w.EnableRaisingEvents = true;
                _watchers[root] = w;
                log.LogInformation("Qabul qilingan fayllar kuzatilmoqda: {Root}", root);
            }
            catch (Exception ex) { log.LogWarning(ex, "Papkani kuzatib bo'lmadi: {Root}", root); }
        }
    }

    private IEnumerable<string> WatchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in opt.Value.WatchPaths) if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) roots.Add(p);

        // Servis LocalSystem ostida ishlaydi: Environment.SpecialFolder foydalanuvchi papkalarini bermaydi, shuning uchun C:\Users\* ni o'zimiz aylanamiz.
        try
        {
            var sysRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            var usersDir = Path.Combine(sysRoot, "Users");
            if (Directory.Exists(usersDir))
            {
                foreach (var profile in Directory.EnumerateDirectories(usersDir))
                {
                    if (SkipProfiles.Contains(Path.GetFileName(profile), StringComparer.OrdinalIgnoreCase)) continue;
                    foreach (var sub in UserSubFolders)
                    {
                        var p = Path.Combine(profile, sub);
                        if (Directory.Exists(p)) roots.Add(p);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log.LogWarning(ex, "Foydalanuvchi papkalarini o'qib bo'lmadi"); }
        return roots;
    }

    // ---------------- baholash ----------------

    private async Task FlushAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _pending.ToArray())
        {
            if (now - kv.Value.Last < Settle) continue; // fayl hali yozilmoqda
            if (!_pending.TryRemove(kv.Key, out var p)) continue;
            try { await EvaluateAsync(kv.Key, p, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogDebug(ex, "Fayl baholanmadi: {Path}", kv.Key); }
        }
        foreach (var k in _recent.Where(x => now - x.Value > TimeSpan.FromMinutes(5)).Select(x => x.Key).ToList()) _recent.Remove(k);
        foreach (var k in _writers.Where(x => now - x.Value.At > TimeSpan.FromMinutes(5)).Select(x => x.Key).ToList()) _writers.TryRemove(k, out _);
    }

    private async Task EvaluateAsync(string path, PendingFile p, CancellationToken ct)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext.Length == 0 || TempExt.Contains(ext) || name.StartsWith("~$") || name.StartsWith('.')) return;

        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length == 0) return; // papka yoki o'chib ketgan vaqtinchalik fayl

        var now = DateTime.UtcNow;
        string? app; string? proc = null; double conf;
        if (_writers.TryGetValue(path, out var w) && now - w.At < TimeSpan.FromMinutes(2))
        { app = w.App; proc = w.Proc; conf = 0.95; }
        else if (p.Created && AppByFolder(path) is { } folderApp)
        { app = folderApp; conf = 0.80; } // faqat YANGI fayl: messenger papkasidagi eski faylni foydalanuvchi tahrirlasa hodisa chiqmaydi
        else return;

        if (!catalog.IsEnabled(app) || conf < opt.Value.MinConfidence) return;

        var sha = await FileMeta.Sha256Async(path, ct);
        var key = $"{app}|{sha ?? path}";
        if (_recent.TryGetValue(key, out var t) && now - t < TimeSpan.FromMinutes(5)) return; // bir xil fayl uchun dublikat
        _recent[key] = now;

        queue.Enqueue(new EventDto(Guid.NewGuid(), id.DeviceId ?? Guid.Empty, Native.ConsoleUser(), app, proc,
            new FileInfoDto(fi.Name, ext, FileMeta.MimeOf(ext), fi.Length, sha, path), "DOWNLOADED", DateTimeOffset.UtcNow, conf));
        log.LogInformation("DOWNLOADED {App} {File} conf={C}", app, fi.Name, conf);
    }

    /// <summary>Fayl joylashgan papkalar nomidan messengerni taxmin qiladi (fayl nomi emas, faqat papkalar).</summary>
    internal static string? AppByFolder(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        foreach (var seg in dir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg.StartsWith("Telegram", StringComparison.OrdinalIgnoreCase)) return "Telegram";
            if (seg.StartsWith("WhatsApp", StringComparison.OrdinalIgnoreCase)) return "WhatsApp";
            if (seg.StartsWith("Discord", StringComparison.OrdinalIgnoreCase)) return "Discord";
            if (seg.StartsWith("Microsoft Teams", StringComparison.OrdinalIgnoreCase)) return "Microsoft Teams";
            if (seg.Equals("imo", StringComparison.OrdinalIgnoreCase) || seg.StartsWith("imo ", StringComparison.OrdinalIgnoreCase)) return "imo";
        }
        return null;
    }
}
