using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FileMonitoring.Agent.Config;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Identity;

/// <summary>Device ID + secret DPAPI (LocalMachine) bilan shifrlangan holda saqlanadi.</summary>
public sealed class DeviceIdentity
{
    private record Stored(Guid DeviceId, string Secret);
    private readonly string _path;
    public Guid? DeviceId { get; private set; }
    public string? Secret { get; private set; }
    public bool IsRegistered => DeviceId.HasValue && Secret is not null;

    public DeviceIdentity(IOptions<AgentOptions> o)
    {
        Directory.CreateDirectory(o.Value.DataDir);
        _path = Path.Combine(o.Value.DataDir, "identity.bin");
        if (!File.Exists(_path)) return;
        try
        {
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.LocalMachine);
            var s = JsonSerializer.Deserialize<Stored>(raw);
            if (s is not null) { DeviceId = s.DeviceId; Secret = s.Secret; }
        }
        catch (CryptographicException) { /* buzilgan fayl: qayta ro'yxatdan o'tiladi */ }
    }

    /// <summary>Server qurilmani tanimasa (masalan baza qayta yaratilgan) — eski identifikatorni o'chirib, qayta ro'yxatdan o'tamiz.</summary>
    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch (IOException) { }
        DeviceId = null; Secret = null;
    }

    public void Save(Guid id, string secret)
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(new Stored(id, secret));
        File.WriteAllBytes(_path, ProtectedData.Protect(raw, null, DataProtectionScope.LocalMachine));
        DeviceId = id; Secret = secret;
    }
}
