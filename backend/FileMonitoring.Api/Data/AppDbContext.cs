using FileMonitoring.Api.Domain;
using Microsoft.EntityFrameworkCore;
namespace FileMonitoring.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> o) : DbContext(o)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AppDefinition> Applications => Set<AppDefinition>();
    public DbSet<FileEvent> FileEvents => Set<FileEvent>();
    public DbSet<AgentHeartbeat> Heartbeats => Set<AgentHeartbeat>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyRule> PolicyRules => Set<PolicyRule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppDefinition>().ToTable("applications");
        b.Entity<AgentHeartbeat>().ToTable("agent_heartbeats");
        b.Entity<FileEvent>().Property(e => e.Sha256).HasColumnType("char(64)");
        b.Entity<Policy>().HasMany(p => p.Rules).WithOne().HasForeignKey(r => r.PolicyId);
        b.Entity<AuditLog>().Property(a => a.Details).HasColumnType("jsonb");
    }
}
