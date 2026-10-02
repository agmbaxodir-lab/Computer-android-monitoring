using FileMonitoring.Agent.Detection;
using Xunit;

namespace FileMonitoring.Agent.Tests;

public class ReceiveWatcherTests
{
    [Theory]
    [InlineData(@"C:\Users\ali\Downloads\Telegram Desktop\hisobot.pdf", "Telegram")]
    [InlineData(@"C:\Users\ali\Downloads\WhatsApp\kontrakt.docx", "WhatsApp")]
    [InlineData(@"D:\Discord\a.zip", "Discord")]
    [InlineData(@"C:\Users\ali\Documents\imo\photo.jpg", "imo")]
    public void Messenger_folders_are_recognised(string path, string expectedApp) =>
        Assert.Equal(expectedApp, ReceiveWatcher.AppByFolder(path));

    [Theory]
    [InlineData(@"C:\Users\ali\Downloads\hisobot.pdf")]
    [InlineData(@"C:\Users\ali\Documents\memo\notes.txt")]          // "memo" papkasi "imo" bilan adashmasligi kerak
    [InlineData(@"C:\Users\ali\Downloads\Telegram-hisobot.pdf")]    // fayl nomida Telegram bo'lishi papkaga tegishli emas
    public void Ordinary_paths_are_not_attributed(string path) =>
        Assert.Null(ReceiveWatcher.AppByFolder(path));
}
