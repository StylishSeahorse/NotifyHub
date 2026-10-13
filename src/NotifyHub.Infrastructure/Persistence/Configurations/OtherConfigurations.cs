using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Assets;
using NotifyHub.Domain.Auditing;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        var mimeTypes = string.Join(", ", Asset.AllowedMimeTypes.Order(StringComparer.Ordinal).Select(m => $"'{m}'"));
        builder.ToTable("assets", t =>
        {
            t.HasCheckConstraint("ck_assets_mime_type", $"mime_type IN ({mimeTypes})");
            t.HasCheckConstraint("ck_assets_size", $"size_bytes > 0 AND size_bytes <= {Asset.MaxSizeBytes}");
            t.HasCheckConstraint("ck_assets_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasTenantScopedKey("assets");
        builder.Property(x => x.StorageKey).HasMaxLength(100);
        builder.Property(x => x.MimeType).HasMaxLength(50);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events", t =>
        {
            t.HasEnumCheck<AuditActorType>("audit_events", "actor_type");
            t.HasCheckConstraint("ck_audit_events_actor", "actor_type = 'System' OR actor_id IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Action).HasMaxLength(AuditEvent.ActionMaxLength);
        builder.Property(x => x.EntityType).HasMaxLength(AuditEvent.EntityTypeMaxLength);
        builder.Property(x => x.CorrelationId).HasMaxLength(AuditEvent.CorrelationIdMaxLength);
        builder.Property(x => x.Metadata).IsJsonb();

        // actor_id is polymorphic (user, device or recipient) and is intentionally not a foreign key so
        // that audit history survives deletion of the actor.
        builder.HasIndex(x => new { x.TenantId, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeatRecord>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeatRecord> builder)
    {
        builder.ToTable("worker_heartbeats");
        builder.HasKey(x => new { x.WorkerName, x.InstanceId });
        builder.Property(x => x.WorkerName).HasMaxLength(100);
        builder.Property(x => x.Version).HasMaxLength(50);
        builder.HasIndex(x => new { x.WorkerName, x.LastHeartbeatAt });
    }
}
