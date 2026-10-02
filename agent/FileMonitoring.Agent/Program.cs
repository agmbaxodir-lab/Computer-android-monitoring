using FileMonitoring.Agent.Api;
using FileMonitoring.Agent.Config;
using FileMonitoring.Agent.Detection;
using FileMonitoring.Agent.Identity;
using FileMonitoring.Agent.Notify;
using FileMonitoring.Agent.Queue;
using FileMonitoring.Agent.Workers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "FileMonitoringAgent");
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddSingleton<DeviceIdentity>();
builder.Services.AddSingleton<LocalQueue>();
builder.Services.AddSingleton<AppCatalog>();
builder.Services.AddHttpClient<ApiClient>();
builder.Services.AddSingleton(sp => new Correlator(sp.GetRequiredService<IOptions<AgentOptions>>(),
    sp.GetRequiredService<LocalQueue>().Enqueue, () => sp.GetRequiredService<DeviceIdentity>().DeviceId ?? Guid.Empty,
    sp.GetRequiredService<ILogger<Correlator>>()));
builder.Services.AddSingleton(sp => new TransferCorrelator(sp.GetRequiredService<IOptions<AgentOptions>>(),
    sp.GetRequiredService<LocalQueue>().Enqueue, () => sp.GetRequiredService<DeviceIdentity>().DeviceId ?? Guid.Empty,
    sp.GetRequiredService<ILogger<TransferCorrelator>>()));
builder.Services.AddSingleton<ReceiveWatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReceiveWatcher>());
builder.Services.AddSingleton<NotifyPipeServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<NotifyPipeServer>());
builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddHostedService<DetectionWorker>();
await builder.Build().RunAsync();
