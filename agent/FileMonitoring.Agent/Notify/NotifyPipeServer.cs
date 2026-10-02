using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FileMonitoring.Agent.Notify;

/// <summary>Toast matnini (title/message) FileMonitoring.Agent.Notifier jarayoniga uzatadi.</summary>
public sealed class NotifyPipeServer(ILogger<NotifyPipeServer> log) : BackgroundService
{
    private readonly Channel<(string Title, string Message)> _ch = Channel.CreateUnbounded<(string, string)>();
    public void Enqueue(string title, string message) => _ch.Writer.TryWrite((title, message));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream("FileMonitoringAgentNotifyPipe", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                await using var writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true };
                while (server.IsConnected && !ct.IsCancellationRequested)
                {
                    var (title, msg) = await _ch.Reader.ReadAsync(ct);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { Title = title, Message = msg }));
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { await Task.Delay(2000, ct).ContinueWith(_ => { }); } // notifier hali ulanmagan
        }
    }
}
