using NotifyHub.Domain.Common;
using NotifyHub.Domain.Identity;

namespace NotifyHub.Domain.Notifications;

public enum DismissalOutcome
{
    /// <summary>A normal notice is now hidden for this recipient (and only this recipient).</summary>
    Suppressed,

    /// <summary>A notice requiring acknowledgement was closed; it will be re-presented per its reminder policy.</summary>
    Deferred,

    /// <summary>No change: already dismissed or acknowledged.</summary>
    NoChange,
}

/// <summary>
/// One recipient's state for one notification. Keyed by (tenant, notification, recipient) so that a
/// dismissal or acknowledgement can never affect any other recipient. Never modifies the notification's
/// global status.
/// <para>
/// Client-supplied event times are untrusted: they are clamped so that they cannot be later than the
/// time the server received them.
/// </para>
/// </summary>
public sealed class NotificationRecipientState : ITenantOwned
{
    private NotificationRecipientState()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid NotificationId { get; private set; }

    public Guid RecipientId { get; private set; }

    /// <summary>Set when a normal notice is dismissed. Suppresses it for this recipient across logons and devices.</summary>
    public DateTimeOffset? DismissedAt { get; private set; }

    /// <summary>Most recent close of a notice requiring acknowledgement. Does not suppress it.</summary>
    public DateTimeOffset? LastClosedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    /// <summary>The notification revision that was acknowledged.</summary>
    public int? AcknowledgedRevision { get; private set; }

    public Guid? AcknowledgedOnDeviceId { get; private set; }

    public DateTimeOffset? FirstPresentedAt { get; private set; }

    public DateTimeOffset? LastPresentedAt { get; private set; }

    public int PresentationCount { get; private set; }

    /// <summary>Earliest time an unacknowledged notice should be re-presented. Null means present when eligible.</summary>
    public DateTimeOffset? NextReminderAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationRecipientState Create(Notification notification, Recipient recipient, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(recipient);
        if (notification.TenantId != recipient.TenantId)
        {
            throw new DomainException("tenant.mismatch", "Recipient and notification belong to different tenants.");
        }

        var utcNow = Guard.Utc(now);
        return new NotificationRecipientState
        {
            TenantId = notification.TenantId,
            NotificationId = notification.Id,
            RecipientId = recipient.Id,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public bool IsAcknowledged => AcknowledgedAt is not null;

    /// <summary>Whether the notice should currently be shown to this recipient.</summary>
    public bool ShouldPresent(Notification notification, DateTimeOffset now)
    {
        EnsureSameNotification(notification);
        var utcNow = Guard.Utc(now);
        if (!notification.IsActiveAt(utcNow))
        {
            return false;
        }

        if (notification.RequiresAcknowledgement)
        {
            return !IsAcknowledged && (NextReminderAt is null || NextReminderAt <= utcNow);
        }

        return DismissedAt is null;
    }

    public void RecordPresented(Notification notification, DateTimeOffset occurredAt, DateTimeOffset receivedAt)
    {
        EnsureSameNotification(notification);
        var at = Clamp(occurredAt, receivedAt);
        FirstPresentedAt ??= at;
        if (LastPresentedAt is null || at > LastPresentedAt)
        {
            LastPresentedAt = at;
        }

        PresentationCount++;
        if (notification.RequiresAcknowledgement && !IsAcknowledged)
        {
            NextReminderAt = at + notification.ReminderPolicy.Interval;
        }

        UpdatedAt = Guard.Utc(receivedAt);
    }

    public DismissalOutcome RecordDismissal(Notification notification, DateTimeOffset occurredAt, DateTimeOffset receivedAt)
    {
        EnsureSameNotification(notification);
        if (notification.PublishedAt is null)
        {
            throw new DomainException("notification.never_published", "Cannot dismiss a notification that was never published.");
        }

        var at = Clamp(occurredAt, receivedAt);
        if (notification.RequiresAcknowledgement)
        {
            if (IsAcknowledged)
            {
                return DismissalOutcome.NoChange;
            }

            // Closing is not acknowledgement: record it and defer the next reminder, but never suppress.
            LastClosedAt = LastClosedAt is null || at > LastClosedAt ? at : LastClosedAt;
            NextReminderAt = at + notification.ReminderPolicy.Interval;
            UpdatedAt = Guard.Utc(receivedAt);
            return DismissalOutcome.Deferred;
        }

        if (DismissedAt is not null)
        {
            return DismissalOutcome.NoChange;
        }

        DismissedAt = at;
        UpdatedAt = Guard.Utc(receivedAt);
        return DismissalOutcome.Suppressed;
    }

    /// <summary>
    /// Records an explicit acknowledgement by the authenticated recipient. Accepted only if the notice was
    /// active when the acknowledgement occurred, so an offline acknowledgement made before a withdrawal is
    /// preserved but one made afterwards is rejected.
    /// </summary>
    /// <returns>True if this call acknowledged the notice; false if it was already acknowledged.</returns>
    public bool RecordAcknowledgement(Notification notification, Guid deviceId, DateTimeOffset occurredAt, DateTimeOffset receivedAt)
    {
        EnsureSameNotification(notification);
        if (!notification.RequiresAcknowledgement)
        {
            throw new DomainException("notification.acknowledgement_not_required", "This notification does not accept acknowledgements.");
        }

        if (IsAcknowledged)
        {
            return false;
        }

        var at = Clamp(occurredAt, receivedAt);
        if (!notification.WasActiveAt(at))
        {
            throw new DomainException("notification.not_active", "The notification was not active when the acknowledgement was made.");
        }

        AcknowledgedAt = at;
        AcknowledgedRevision = notification.Revision;
        AcknowledgedOnDeviceId = Guard.NotEmpty(deviceId, "Device");
        NextReminderAt = null;
        UpdatedAt = Guard.Utc(receivedAt);
        return true;
    }

    private static DateTimeOffset Clamp(DateTimeOffset occurredAt, DateTimeOffset receivedAt)
    {
        var occurred = Guard.Utc(occurredAt);
        var received = Guard.Utc(receivedAt);
        return occurred > received ? received : occurred;
    }

    private void EnsureSameNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.Id != NotificationId || notification.TenantId != TenantId)
        {
            throw new DomainException("notification.mismatch", "State does not belong to this notification.");
        }
    }
}
