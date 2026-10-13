using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", t =>
        {
            t.HasEnumCheck<NotificationStatus>("notifications", "status");
            t.HasEnumCheck<NotificationSeverity>("notifications", "severity");
            t.HasEnumCheck<DisplayMode>("notifications", "display_mode");
            t.HasCheckConstraint("ck_notifications_revision_positive", "revision >= 1");
            t.HasCheckConstraint("ck_notifications_window", "expires_at IS NULL OR starts_at IS NULL OR expires_at > starts_at");
            t.HasCheckConstraint("ck_notifications_scheduled_has_start", "status <> 'Scheduled' OR starts_at IS NOT NULL");
            t.HasCheckConstraint("ck_notifications_published_at", "status NOT IN ('Published') OR published_at IS NOT NULL");
            t.HasCheckConstraint("ck_notifications_withdrawn_at", "status <> 'Withdrawn' OR withdrawn_at IS NOT NULL");
            t.HasCheckConstraint("ck_notifications_expired_at", "status <> 'Expired' OR expired_at IS NOT NULL");
            t.HasCheckConstraint("ck_notifications_revision_lineage", "(revision = 1) = (supersedes_notification_id IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasTenantScopedKey("notifications");
        builder.Property(x => x.Title).HasMaxLength(NotificationContent.TitleMaxLength);
        builder.Property(x => x.Body).HasMaxLength(NotificationContent.BodyMaxLength);
        builder.Property(x => x.ReminderPolicy)
            .HasColumnType("jsonb")
            .HasConversion(ReminderPolicyJson.Converter, ReminderPolicyJson.Comparer);

        // Optimistic concurrency on PostgreSQL's system row version.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.HasMany(x => x.Targets).WithOne()
            .HasForeignKey(x => new { x.TenantId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notification_targets_notification");
        builder.Navigation(x => x.Targets).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.WithdrawnBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SupersedesNotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_supersedes");
        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SupersededByNotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notifications_superseded_by");

        builder.HasIndex(x => new { x.TenantId, x.SeriesId, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });

        // Scheduler scans: due scheduled notices and due expiries.
        builder.HasIndex(x => x.StartsAt).HasDatabaseName("ix_notifications_due_schedule").HasFilter("status = 'Scheduled'");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_notifications_due_expiry").HasFilter("status = 'Published' AND expires_at IS NOT NULL");

        // Active notices per tenant (device sync).
        builder.HasIndex(x => new { x.TenantId, x.PublishedAt }).HasDatabaseName("ix_notifications_active").HasFilter("status = 'Published'");
    }
}

internal sealed class NotificationTargetConfiguration : IEntityTypeConfiguration<NotificationTarget>
{
    public void Configure(EntityTypeBuilder<NotificationTarget> builder)
    {
        builder.ToTable("notification_targets", t =>
        {
            t.HasEnumCheck<NotificationTargetType>("notification_targets", "target_type");
            t.HasCheckConstraint(
                "ck_notification_targets_reference",
                """
                (target_type = 'AllDevices' AND device_group_id IS NULL AND device_id IS NULL AND recipient_id IS NULL)
                OR (target_type = 'DeviceGroup' AND device_group_id IS NOT NULL AND device_id IS NULL AND recipient_id IS NULL)
                OR (target_type = 'Device' AND device_group_id IS NULL AND device_id IS NOT NULL AND recipient_id IS NULL)
                OR (target_type = 'Recipient' AND device_group_id IS NULL AND device_id IS NULL AND recipient_id IS NOT NULL)
                """);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.TargetId);

        builder.HasIndex(x => new { x.NotificationId, x.TargetType, x.DeviceGroupId, x.DeviceId, x.RecipientId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_notification_targets_unique_target");
        builder.HasIndex(x => new { x.TenantId, x.DeviceGroupId }).HasFilter("device_group_id IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.DeviceId }).HasFilter("device_id IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.RecipientId }).HasFilter("recipient_id IS NOT NULL");

        builder.HasOne<DeviceGroup>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceGroupId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_targets_device_group");
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_targets_device");
        builder.HasOne<Recipient>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RecipientId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_targets_recipient");
    }
}

internal sealed class NotificationRecipientStateConfiguration : IEntityTypeConfiguration<NotificationRecipientState>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientState> builder)
    {
        builder.ToTable("notification_recipient_states", t =>
        {
            t.HasCheckConstraint("ck_notification_recipient_states_presentations", "presentation_count >= 0");
            t.HasCheckConstraint(
                "ck_notification_recipient_states_ack",
                "(acknowledged_at IS NULL) = (acknowledged_revision IS NULL) AND (acknowledged_at IS NULL) = (acknowledged_on_device_id IS NULL)");
        });

        // Uniqueness of (tenant, notification, recipient) is the primary key: one state row per person per notice.
        builder.HasKey(x => new { x.TenantId, x.NotificationId, x.RecipientId });
        builder.HasIndex(x => new { x.TenantId, x.RecipientId });
        builder.HasIndex(x => new { x.TenantId, x.NotificationId })
            .HasDatabaseName("ix_notification_recipient_states_unacknowledged")
            .HasFilter("acknowledged_at IS NULL");

        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notification_recipient_states_notification");
        builder.HasOne<Recipient>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RecipientId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_recipient_states_recipient");
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AcknowledgedOnDeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_recipient_states_ack_device");
    }
}

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries", t =>
        {
            t.HasEnumCheck<DeliveryStatus>("notification_deliveries", "delivery_status");
            t.HasCheckConstraint("ck_notification_deliveries_attempts", "attempt_count >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.TenantId, x.NotificationId, x.DeviceId, x.RecipientId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_notification_deliveries_target");
        builder.HasIndex(x => new { x.TenantId, x.DeviceId });

        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notification_deliveries_notification");
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_deliveries_device");
        builder.HasOne<Recipient>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RecipientId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_deliveries_recipient");
    }
}

internal sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("notification_events", t => t.HasEnumCheck<NotificationEventType>("notification_events", "event_type"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(NotificationEvent.IdempotencyKeyMaxLength);
        builder.Property(x => x.EventMetadata).IsJsonb();

        // Idempotency: a device's retried upload of the same event is rejected by this constraint.
        builder.HasIndex(x => new { x.TenantId, x.DeviceId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NotificationId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.RecipientId, x.OccurredAt }).HasFilter("recipient_id IS NOT NULL");

        builder.HasOne<Notification>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.NotificationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_notification_events_notification");
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.DeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_events_device");
        builder.HasOne<Recipient>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RecipientId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_notification_events_recipient");
    }
}

internal static class ReminderPolicyJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<ReminderPolicy, string> Converter = new(
        policy => Serialize(policy),
        json => Deserialize(json));

    public static readonly ValueComparer<ReminderPolicy> Comparer = new(
        (a, b) => Equals(a, b),
        policy => policy.GetHashCode(),
        policy => policy);

    private static string Serialize(ReminderPolicy policy) =>
        JsonSerializer.Serialize(new Dto(policy.IntervalMinutes, policy.MaxPresentationsPerDay, policy.RepresentAtLogon), Options);

    private static ReminderPolicy Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, Options) ?? throw new InvalidOperationException("Stored reminder policy is null.");
        return new ReminderPolicy(dto.IntervalMinutes, dto.MaxPresentationsPerDay, dto.RepresentAtLogon);
    }

    private sealed record Dto(int IntervalMinutes, int MaxPresentationsPerDay, bool RepresentAtLogon);
}
