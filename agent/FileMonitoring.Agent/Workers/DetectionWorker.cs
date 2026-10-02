using System.Collections.Concurrent;
using System.Diagnostics;
using FileMonitoring.Agent.Detection;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Queue;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace FileMonitoring.Agent.Workers;

/// <summary>
/// ETW (NT Kernel Logger): FileIO Read + TCP Send. Faqat konfiguratsiyadagi ilovalar jarayonlari kuzatiladi.
/// Klaviatura, ekran, mikrofon, xabar mazmuni — umuman o'qilmaydi.
/// </summary>
public sealed class DetectionWorker(AppCatalog catalog, DeviceIdentity id, LocalQueue queue, IServiceProvider sp, ILogger<DetectionWorker> log) : BackgroundService
{
    private readonly ConcurrentDictionary<int, (string? app, string proc, DateTime exp)> _pids = new();
    private static readonly string[] SystemRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
    ];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var corr = (Correlator)sp.GetService(typeof(Correlator))!;
        var vol = Native.BuildVolumeMap();
        TraceEventSession? session = null;
        var etw = Task.Run(() =>
        {
            try
            {
                session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIOInit | KernelTraceEventParser.Keywords.FileIO | KernelTraceEventParser.Keywords.NetworkTCPIP);
                session.Source.Kernel.FileIORead += e =>
                {
                    var (app, proc) = Resolve(e.ProcessID); if (app is null) return;
                    var path = Normalize(e.FileName, vol); if (path is null || !IsUserFile(path)) return;
                    corr.OnRead(e.ProcessID, app, proc, path, e.IoSize);
                };
                session.Source.Kernel.TcpIpSend += e => { if (Resolve(e.ProcessID).app is not null) corr.OnNetSend(e.ProcessID, e.size); };
                session.Source.Kernel.TcpIpSendIPV6 += e => { if (Resolve(e.ProcessID).app is not null) corr.OnNetSend(e.ProcessID, e.size); };
                session.Source.Process();
            }
            catch (Exception ex) { log.LogError(ex, "ETW session failed (admin/LocalSystem huquqi kerak, boshqa 'NT Kernel Logger' egasi bo'lishi mumkin)"); }
        }, ct);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try { while (await timer.WaitForNextTickAsync(ct)) await corr.FlushAsync(ct); }
        catch (OperationCanceledException) { }
        finally { session?.Dispose(); await etw.WaitAsync(TimeSpan.FromSeconds(5)).ContinueWith(_ => { }); }
    }

    private (string? app, string proc) Resolve(int pid)
    {
        if (_pids.TryGetValue(pid, out var v) && v.exp > DateTime.UtcNow) return (v.app, v.proc);
        string proc = ""; string? app = null;
        try { proc = Process.GetProcessById(pid).ProcessName + ".exe"; app = catalog.Match(proc); } catch { }
        _pids[pid] = (app, proc, DateTime.UtcNow.AddSeconds(app is null ? 30 : 300)); // PID reuse uchun TTL
        return (app, proc);
    }

    private static string? Normalize(string? p, Dictionary<string, string> vol)
    {
        if (string.IsNullOrEmpty(p)) return null;
        if (p.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (dev, letter) in vol) if (p.StartsWith(dev + "\\", StringComparison.OrdinalIgnoreCase)) return letter + p[dev.Length..];
            return null;
        }
        return p.Length > 2 && p[1] == ':' ? p : null;
    }

    /// <summary>Ilovaning o'z cache/data papkalari va tizim yo'llari chiqarib tashlanadi.</summary>
    private static bool IsUserFile(string p) =>
        !p.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase) && !p.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase) &&
        !SystemRoots.Any(r => !string.IsNullOrEmpty(r) && p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) &&
        Path.HasExtension(p);
}
