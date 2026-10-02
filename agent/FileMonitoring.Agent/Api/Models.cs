using FileMonitoring.Agent.Config;
namespace FileMonitoring.Agent.Api;

public record FileInfoDto(string Name, string? Extension, string? MimeType, long Size, string? Sha256, string? Path = null);
public record EventDto(Guid EventId, Guid DeviceId, string? Username, string Application, string? ProcessName,
    FileInfoDto File, string EventType, DateTimeOffset Timestamp, double Confidence);
public record RegisterResponse(Guid DeviceId, string DeviceSecret);
public record ConfigResponse(List<AppConfig> Applications, int HeartbeatSeconds, int ConfigPollSeconds);
public record PendingNotificationDto(Guid Id, string Title, string Message);
public record HeartbeatResponse(DateTimeOffset ServerTime, List<PendingNotificationDto> PendingNotifications);
public enum SendResult { Ok, Retry, Reject }
