using System.Security.Cryptography;

namespace FileMonitoring.Agent.Detection;

internal static class FileMeta
{
    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel", [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip", [".rar"] = "application/vnd.rar", [".7z"] = "application/x-7z-compressed",
        [".txt"] = "text/plain", [".csv"] = "text/csv", [".json"] = "application/json",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".gif"] = "image/gif",
        [".mp4"] = "video/mp4", [".mp3"] = "audio/mpeg", [".exe"] = "application/vnd.microsoft.portable-executable"
    };
    public static string MimeOf(string ext) => Mime.TryGetValue(ext, out var m) ? m : "application/octet-stream";

    public static async Task<string?> Sha256Async(string path, CancellationToken ct)
    {
        try
        {
            await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, true);
            return Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
