using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FileMonitoring.Agent.Detection;

internal static class Native
{
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr h, int sessionId, int infoClass, out IntPtr buf, out int bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr p);
    [DllImport("kernel32.dll")] private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string dev, StringBuilder target, int max);

    public static string? SessionUser(int pid)
    {
        try
        {
            var sid = Process.GetProcessById(pid).SessionId;
            if (!WTSQuerySessionInformation(IntPtr.Zero, sid, 5 /*WTSUserName*/, out var p, out _)) return null;
            try { return Marshal.PtrToStringUni(p); } finally { WTSFreeMemory(p); }
        }
        catch { return null; }
    }

    /// <summary>Hozir kompyuter oldida o'tirgan (konsol sessiyasidagi) foydalanuvchi. Servis SYSTEM ostida ishlagani uchun Environment.UserName yaramaydi.</summary>
    public static string? ConsoleUser()
    {
        try
        {
            var sid = WTSGetActiveConsoleSessionId();
            if (sid == 0xFFFFFFFF) return null;
            if (!WTSQuerySessionInformation(IntPtr.Zero, (int)sid, 5 /*WTSUserName*/, out var p, out _)) return null;
            try { var u = Marshal.PtrToStringUni(p); return string.IsNullOrWhiteSpace(u) ? null : u; } finally { WTSFreeMemory(p); }
        }
        catch { return null; }
    }

    /// <summary>\Device\HarddiskVolumeN\... -> C:\... (ETW ba'zan device yo'l qaytaradi)</summary>
    public static Dictionary<string, string> BuildVolumeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in DriveInfo.GetDrives())
        {
            var letter = d.Name.TrimEnd('\\');
            var sb = new StringBuilder(512);
            if (QueryDosDevice(letter, sb, sb.Capacity) > 0) map[sb.ToString().Split('\0')[0]] = letter;
        }
        return map;
    }
}
