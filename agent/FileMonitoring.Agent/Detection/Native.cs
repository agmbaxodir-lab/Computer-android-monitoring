using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FileMonitoring.Agent.Detection;

internal static class Native
{
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr h, int sessionId, int infoClass, out IntPtr buf, out int bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr p);
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
