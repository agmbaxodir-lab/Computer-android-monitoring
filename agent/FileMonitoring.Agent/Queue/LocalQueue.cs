using System.Security.Cryptography;
using System.Text.Json;
using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Queue;

/// <summary>SQLite navbat. Payload DPAPI bilan shifrlanadi; event_id PRIMARY KEY => lokal dublikat yo'q.</summary>
public sealed class LocalQueue
{
    private readonly string _cs;
    private readonly object _lock = new();

    public LocalQueue(IOptions<AgentOptions> o)
    {
        Directory.CreateDirectory(o.Value.DataDir);
        _cs = $"Data Source={Path.Combine(o.Value.DataDir, "queue.db")}";
        Exec("CREATE TABLE IF NOT EXISTS events (event_id TEXT PRIMARY KEY, payload BLOB NOT NULL, created_at INTEGER NOT NULL)");
    }

    public void Enqueue(EventDto e)
    {
        var blob = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(e, Json.Options), null, DataProtectionScope.LocalMachine);
        lock (_lock)
        {
            using var c = new SqliteConnection(_cs); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO events(event_id,payload,created_at) VALUES($i,$p,$t)";
            cmd.Parameters.AddWithValue("$i", e.EventId.ToString()); cmd.Parameters.AddWithValue("$p", blob);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
    }

    public List<EventDto> Peek(int max)
    {
        var list = new List<EventDto>();
        lock (_lock)
        {
            using var c = new SqliteConnection(_cs); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT payload FROM events ORDER BY created_at LIMIT $m";
            cmd.Parameters.AddWithValue("$m", max);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                try
                {
                    var raw = ProtectedData.Unprotect((byte[])r[0], null, DataProtectionScope.LocalMachine);
                    var e = JsonSerializer.Deserialize<EventDto>(raw, Json.Options);
                    if (e is not null) list.Add(e);
                }
                catch (CryptographicException) { }
            }
        }
        return list;
    }

    public void Delete(IEnumerable<Guid> ids)
    {
        lock (_lock)
        {
            using var c = new SqliteConnection(_cs); c.Open();
            using var tx = c.BeginTransaction();
            foreach (var id in ids)
            {
                using var cmd = c.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM events WHERE event_id=$i"; cmd.Parameters.AddWithValue("$i", id.ToString());
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            using var c = new SqliteConnection(_cs); c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM events";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private void Exec(string sql)
    {
        using var c = new SqliteConnection(_cs); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
