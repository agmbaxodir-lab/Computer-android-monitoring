namespace FileMonitoring.Api.Domain;

public class User { public Guid Id { get; set; } public string Username { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string Role { get; set; } = "User"; public bool IsActive { get; set; } = true; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public class RefreshToken { public Guid Id { get; set; } public Guid UserId { get; set; } public string TokenHash { get; set; } = ""; public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public class Device { public Guid Id { get; set; } public string Hostname { get; set; } = ""; public string? Username { get; set; } public string? IpAddress { get; set; } public string? OsVersion { get; set; } public string? AgentVersion { get; set; } public string Platform { get; set; } = "Windows"; public string? DeviceModel { get; set; } public byte[] SecretHash { get; set; } = []; public string Status { get; set; } = "Active"; public DateTimeOffset? LastHeartbeatAt { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public class AppDefinition { public Guid Id { get; set; } public string Name { get; set; } = ""; public string[] ProcessNames { get; set; } = []; public bool Enabled { get; set; } = true; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public class FileEvent
{
    public Guid Id { get; set; } public Guid EventId { get; set; } public Guid DeviceId { get; set; } public Guid? UserId { get; set; } public Guid? ApplicationId { get; set; }
    public string Platform { get; set; } = "Windows";
    public string EventType { get; set; } = "FILE_SENT"; public string? ProcessName { get; set; } public string? OsUsername { get; set; }
    public string FileName { get; set; } = ""; public string? FilePath { get; set; } public string? Source { get; set; } public string? Destination { get; set; } public string? FileExtension { get; set; } public string? MimeType { get; set; } public long FileSize { get; set; }
    public string? Sha256 { get; set; } public DateTimeOffset Timestamp { get; set; } public float Confidence { get; set; } public string Status { get; set; } = "New"; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class AgentHeartbeat { public long Id { get; set; } public Guid DeviceId { get; set; } public string? AgentVersion { get; set; } public string? IpAddress { get; set; } public float? CpuPercent { get; set; } public float? MemoryMb { get; set; } public int? QueueSize { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
