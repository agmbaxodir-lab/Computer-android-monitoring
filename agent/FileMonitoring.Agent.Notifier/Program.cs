using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Forms;

namespace FileMonitoring.Agent.Notifier;

/// <summary>
/// Foydalanuvchi sessiyasida ishlaydigan yengil yordamchi. Windows Service 0-sessiyada ishlaydi
/// va u yerdan to'g'ridan-to'g'ri toast ko'rsata olmaydi, shuning uchun bu alohida jarayon
/// (logon'da ishga tushadi) named pipe orqali serviсdan xabar oladi va balloon/toast ko'rsatadi.
/// Faqat title+message ko'rsatadi; boshqa hech qanday ma'lumot o'qimaydi yoki yubormaydi.
/// </summary>
internal static class Program
{
    private record Msg(string Title, string Message);

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var icon = new NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Visible = true, Text = "File Monitoring Agent" };
        var cts = new CancellationTokenSource();
        var listener = Task.Run(() => ListenLoop(icon, cts.Token));
        Application.ApplicationExit += (_, _) => cts.Cancel();
        Application.Run();
    }

    private static async Task ListenLoop(NotifyIcon icon, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(".", "FileMonitoringAgentNotifyPipe", PipeDirection.In);
                await pipe.ConnectAsync(5000, ct);
                using var reader = new StreamReader(pipe);
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break; // service qayta ishga tushganda qayta ulanamiz
                    var msg = JsonSerializer.Deserialize<Msg>(line);
                    if (msg is not null) icon.BeginInvoke(() => icon.ShowBalloonTip(10000, msg.Title, msg.Message, ToolTipIcon.Warning));
                }
            }
            catch (Exception) { await Task.Delay(3000, ct).ContinueWith(_ => { }); } // pipe hali yo'q — qayta urinish
        }
    }
}
