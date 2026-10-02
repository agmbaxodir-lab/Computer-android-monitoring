using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FileMonitoring.Agent.Notify;

/// <summary>Toast matnini (title/message) FileMonitoring.Agent.Notifier jarayoniga uzatadi.</summary>
public sealed class NotifyPipeServer(ILogger<NotifyPipeServer> log) : BackgroundService
{
    public const string PipeName = "FileMonitoringAgentNotifyPipe";
    private readonly Channel<(string Title, string Message)> _ch = Channel.CreateUnbounded<(string, string)>();
    public void Enqueue(string title, string message) => _ch.Writer.TryWrite((title, message));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Servis LocalSystem ostida, Notifier esa oddiy foydalanuvchi sifatida ishlaydi: oddiy foydalanuvchiga pipe'ga ulanishga ruxsat beramiz.
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = NamedPipeServerStreamAcl.Create(PipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
                await server.WaitForConnectionAsync(ct);
                log.LogInformation("Notifier ulandi");
                await using var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true };
                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    var (title, msg) = await _ch.Reader.ReadAsync(ct);
                    try { await writer.WriteLineAsync(JsonSerializer.Serialize(new { Title = title, Message = msg })); }
                    catch (IOException) { _ch.Writer.TryWrite((title, msg)); break; } // Notifier uzilgan: xabar yo'qolmasin, qayta ulanganda yetkaziladi
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { await Task.Delay(2000, ct).ContinueWith(_ => { }); }
        }
    }
}
