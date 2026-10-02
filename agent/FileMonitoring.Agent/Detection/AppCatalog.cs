using FileMonitoring.Agent.Config;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Detection;

/// <summary>Config orqali boshqariladigan ilovalar ro'yxati (appsettings -> server config bilan almashtiriladi).</summary>
public sealed class AppCatalog
{
    private volatile Dictionary<string, string> _byProcess = new(StringComparer.OrdinalIgnoreCase);
    public AppCatalog(IOptions<AgentOptions> o) => Update(o.Value.Applications);

    public void Update(IEnumerable<AppConfig> apps)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in apps.Where(a => a.Enabled)) foreach (var p in a.ProcessNames) d[p] = a.Name;
        _byProcess = d;
    }

    public bool IsEnabled(string appName) => _byProcess.Values.Any(v => string.Equals(v, appName, StringComparison.OrdinalIgnoreCase));

    public string? Match(string exeName) => _byProcess.TryGetValue(exeName, out var n) ? n : null;
}
