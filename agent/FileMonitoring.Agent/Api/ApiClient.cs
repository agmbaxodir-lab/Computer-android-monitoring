using System.Net;
using System.Net.Http.Json;
using FileMonitoring.Agent.Config;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Queue;
using Microsoft.Extensions.Options;

namespace FileMonitoring.Agent.Api;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly DeviceIdentity _id;
    private readonly AgentOptions _o;
    private readonly ILogger<ApiClient> _log;

    public ApiClient(HttpClient http, DeviceIdentity id, IOptions<AgentOptions> o, ILogger<ApiClient> log)
    {
        _http = http; _id = id; _o = o.Value; _log = log;
        var uri = new Uri(_o.ServerUrl);
        if (uri.Scheme != Uri.UriSchemeHttps && !_o.AllowInsecureHttp)
            throw new InvalidOperationException("ServerUrl must be HTTPS (AllowInsecureHttp faqat development uchun).");
        _http.BaseAddress = uri; _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> RegisterAsync(string hostname, string? user, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/v1/agent/register",
                new { enrollmentToken = _o.EnrollmentToken, hostname, username = user,
                      osVersion = Environment.OSVersion.VersionString, agentVersion = "1.0.0" }, Json.Options, ct);
            if (!resp.IsSuccessStatusCode) { _log.LogWarning("Register failed: {S}", resp.StatusCode); return false; }
            var r = await resp.Content.ReadFromJsonAsync<RegisterResponse>(Json.Options, ct);
            if (r is null) return false;
            _id.Save(r.DeviceId, r.DeviceSecret); return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { _log.LogWarning("Register error: {M}", e.Message); return false; }
    }

    public async Task<HeartbeatResponse?> HeartbeatAsync(object body, CancellationToken ct)
    {
        try
        {
            using var req = Auth(new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/heartbeat") { Content = JsonContent.Create(body, options: Json.Options) });
            var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<HeartbeatResponse>(Json.Options, ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { _log.LogWarning("Heartbeat offline: {M}", e.Message); return null; }
    }

    public Task<SendResult> SendEventsAsync(IReadOnlyList<EventDto> batch, CancellationToken ct) => PostAsync("api/v1/agent/events", batch, ct);

    public async Task<ConfigResponse?> GetConfigAsync(CancellationToken ct)
    {
        try
        {
            using var req = Auth(new HttpRequestMessage(HttpMethod.Get, "api/v1/agent/config"));
            var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ConfigResponse>(Json.Options, ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }

    private async Task<SendResult> PostAsync(string url, object body, CancellationToken ct)
    {
        try
        {
            using var req = Auth(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json.Options) });
            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return SendResult.Ok;
            if (resp.StatusCode == HttpStatusCode.BadRequest) { _log.LogError("Server rejected payload (400)"); return SendResult.Reject; }
            _log.LogWarning("{Url} -> {S}", url, resp.StatusCode); return SendResult.Retry;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { _log.LogWarning("{Url} offline: {M}", url, e.Message); return SendResult.Retry; }
    }

    private HttpRequestMessage Auth(HttpRequestMessage r)
    {
        r.Headers.Add("X-Device-Id", _id.DeviceId.ToString()); r.Headers.Add("X-Device-Secret", _id.Secret);
        return r;
    }
}
