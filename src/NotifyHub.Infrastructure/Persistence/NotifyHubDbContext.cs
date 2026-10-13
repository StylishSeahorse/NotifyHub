using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Assets;
using NotifyHub.Domain.Auditing;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence;

public sealed class NotifyHubDbContext(DbContextOptions<NotifyHubDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantBranding> TenantBranding => Set<TenantBranding>();

    public DbSet<User> Users => Set<User>();

    public DbSet<PlatformAdministrator> PlatformAdministrators => Set<PlatformAdministrator>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<Recipient> Recipients => Set<Recipient>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();

    public DbSet<DeviceGroupMember> DeviceGroupMembers => Set<DeviceGroupMember>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationTarget> NotificationTargets => Set<NotificationTarget>();

    public DbSet<NotificationRecipientState> NotificationRecipientStates => Set<NotificationRecipientState>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    internal DbSet<WorkerHeartbeatRecord> WorkerHeartbeats => Set<WorkerHeartbeatRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotifyHubDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as text (readable, stable under reordering) and constrained by CHECK constraints.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
