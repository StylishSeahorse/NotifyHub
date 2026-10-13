using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices", t =>
        {
            t.HasEnumCheck<DeviceStatus>("devices", "status");
            t.HasCheckConstraint("ck_devices_revoked_at", "status <> 'Revoked' OR revoked_at IS NOT NULL");
            t.HasCheckConstraint("ck_devices_active_has_key", "status NOT IN ('Active', 'Quarantined') OR device_key_id IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasTenantScopedKey("devices");
        builder.Property(x => x.DeviceName).HasMaxLength(Device.NameMaxLength);
        builder.Property(x => x.DeviceKeyId).HasMaxLength(Device.KeyIdMaxLength);
        builder.Property(x => x.OperatingSystem).HasMaxLength(Device.OperatingSystemMaxLength);
        builder.Property(x => x.AgentVersion).HasMaxLength(Device.AgentVersionMaxLength);

        // Credential identifiers are globally unique: device authentication resolves the tenant from the key.
        builder.HasIndex(x => x.DeviceKeyId).IsUnique().HasFilter("device_key_id IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.Status, x.LastSeenAt });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeviceGroupConfiguration : IEntityTypeConfiguration<DeviceGroup>
{
    public void Configure(EntityTypeBuilder<DeviceGroup> builder)
    {
        builder.ToTable("device_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasTenantScopedKey("device_groups");
        builder.Property(x => x.Name).HasMaxLength(DeviceGroup.NameMaxLength);
        builder.Property(x => x.NormalizedName).HasMaxLength(DeviceGroup.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(DeviceGroup.DescriptionMaxLength);
        builder.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeviceGroupMemberConfiguration : IEntityTypeConfiguration<DeviceGroupMember>
{
    public void Configure(EntityTypeBuilder<DeviceGroupMember> builder)
    {
        builder.ToTable("device_group_members");
        builder.HasKey(x => new { x.TenantId, x.DeviceGroupId, x.DeviceId });
        builder.HasIndex(x => new { x.TenantId, x.DeviceId });
        builder.HasOne<DeviceGroup>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceGroupId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_device_group_members_group");
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_device_group_members_device");
    }
}
