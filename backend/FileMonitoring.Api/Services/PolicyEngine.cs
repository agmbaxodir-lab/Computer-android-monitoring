using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Services;

/// <summary>
/// Extensible policy engine: field/operator/value qoidalari FileEvent ustida baholanadi.
/// Qo'llab-quvvatlanadigan field'lar: application, extension, size, confidence, process.
/// Qo'llab-quvvatlanadigan operator'lar: eq, neq, gt, gte, lt, lte, contains.
/// Yangi field qo'shish uchun faqat ResolveField'ga bitta case qo'shiladi.
/// </summary>
public class PolicyEngine(AppDbContext db)
{
    public async Task EvaluateAsync(FileEvent e, string? appName)
    {
        var policies = await db.Policies.Include(p => p.Rules).Where(p => p.Enabled).ToListAsync();
        foreach (var p in policies)
        {
            if (p.Rules.Count == 0) continue;
            if (!p.Rules.All(r => Matches(r, e, appName))) continue;
            db.Alerts.Add(new Alert { FileEventId = e.Id, PolicyId = p.Id, Severity = SeverityOf(p), Message = $"Policy '{p.Name}' triggered by {e.FileName}" });
        }
        await db.SaveChangesAsync();
    }

    private static string SeverityOf(Policy p) => p.Rules.Any(r => r.Field == "size" && long.TryParse(r.Value, out var v) && v > 100_000_000) ? "High" : "Medium";

    private static bool Matches(PolicyRule r, FileEvent e, string? appName)
    {
        object? left = r.Field.ToLowerInvariant() switch
        {
            "application" => appName, "extension" => e.FileExtension, "size" => e.FileSize,
            "confidence" => (double)e.Confidence, "process" => e.ProcessName, _ => null
        };
        if (left is null) return false;
        return r.Operator.ToLowerInvariant() switch
        {
            "eq" => string.Equals(left.ToString(), r.Value, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(left.ToString(), r.Value, StringComparison.OrdinalIgnoreCase),
            "contains" => left.ToString()?.Contains(r.Value, StringComparison.OrdinalIgnoreCase) == true,
            "gt" => Num(left) > Num(r.Value), "gte" => Num(left) >= Num(r.Value),
            "lt" => Num(left) < Num(r.Value), "lte" => Num(left) <= Num(r.Value),
            _ => false
        };
    }
    private static double Num(object v) => double.TryParse(v.ToString(), out var d) ? d : 0;
}
