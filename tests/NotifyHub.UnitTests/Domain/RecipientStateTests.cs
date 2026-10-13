using NotifyHub.Domain.Common;
using NotifyHub.Domain.Notifications;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

/// <summary>Per-recipient isolation of dismissal and acknowledgement (shared-computer scenarios).</summary>
public sealed class RecipientStateTests
{
    [Fact]
    public void Dismissing_normal_notice_hides_it_for_that_recipient_only()
    {
        var notice = Published();
        var userA = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        var userB = NotificationRecipientState.Create(notice, RecipientFor(1002), T0);

        var outcome = userA.RecordDismissal(notice, T0.AddMinutes(5), T0.AddMinutes(5));

        Assert.Equal(DismissalOutcome.Suppressed, outcome);
        Assert.False(userA.ShouldPresent(notice, T0.AddMinutes(6)));
        Assert.False(userA.ShouldPresent(notice, T0.AddDays(3)));
        Assert.True(userB.ShouldPresent(notice, T0.AddMinutes(6)));
        Assert.Equal(NotificationStatus.Published, notice.Status);
    }

    [Fact]
    public void Repeated_dismissal_keeps_first_timestamp()
    {
        var notice = Published();
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        state.RecordDismissal(notice, T0.AddMinutes(5), T0.AddMinutes(5));
        var second = state.RecordDismissal(notice, T0.AddMinutes(9), T0.AddMinutes(9));

        Assert.Equal(DismissalOutcome.NoChange, second);
        Assert.Equal(T0.AddMinutes(5), state.DismissedAt);
    }

    [Fact]
    public void Closing_critical_notice_does_not_acknowledge_or_suppress_it()
    {
        var notice = Published(Critical(new ReminderPolicy(30, 8, true)));
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        state.RecordPresented(notice, T0, T0);

        var outcome = state.RecordDismissal(notice, T0.AddMinutes(1), T0.AddMinutes(1));

        Assert.Equal(DismissalOutcome.Deferred, outcome);
        Assert.False(state.IsAcknowledged);
        Assert.Null(state.DismissedAt);
        Assert.Equal(T0.AddMinutes(1), state.LastClosedAt);
        Assert.False(state.ShouldPresent(notice, T0.AddMinutes(30)));
        Assert.True(state.ShouldPresent(notice, T0.AddMinutes(31)));
    }

    [Fact]
    public void Unacknowledged_critical_notice_reappears_after_reminder_interval()
    {
        var notice = Published(Critical(new ReminderPolicy(60, 8, true)));
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        Assert.True(state.ShouldPresent(notice, T0));
        state.RecordPresented(notice, T0, T0);
        Assert.False(state.ShouldPresent(notice, T0.AddMinutes(59)));
        Assert.True(state.ShouldPresent(notice, T0.AddMinutes(60)));
        Assert.Equal(T0.AddMinutes(60), state.NextReminderAt);
    }

    [Fact]
    public void Acknowledgement_stops_reminders_for_that_recipient_only()
    {
        var notice = Published(Critical());
        var userA = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        var userB = NotificationRecipientState.Create(notice, RecipientFor(1002), T0);

        var acknowledged = userA.RecordAcknowledgement(notice, DeviceId, T0.AddMinutes(2), T0.AddMinutes(2));

        Assert.True(acknowledged);
        Assert.Equal(T0.AddMinutes(2), userA.AcknowledgedAt);
        Assert.Equal(1, userA.AcknowledgedRevision);
        Assert.Equal(DeviceId, userA.AcknowledgedOnDeviceId);
        Assert.Null(userA.NextReminderAt);
        Assert.False(userA.ShouldPresent(notice, T0.AddDays(1)));

        Assert.False(userB.IsAcknowledged);
        Assert.True(userB.ShouldPresent(notice, T0.AddDays(1)));
        Assert.Equal(NotificationStatus.Published, notice.Status);
    }

    [Fact]
    public void Retried_acknowledgement_is_idempotent()
    {
        var notice = Published(Critical());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        Assert.True(state.RecordAcknowledgement(notice, DeviceId, T0.AddMinutes(2), T0.AddMinutes(2)));
        Assert.False(state.RecordAcknowledgement(notice, DeviceId, T0.AddMinutes(3), T0.AddMinutes(3)));
        Assert.Equal(T0.AddMinutes(2), state.AcknowledgedAt);
    }

    [Fact]
    public void Closing_after_acknowledgement_changes_nothing()
    {
        var notice = Published(Critical());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        state.RecordAcknowledgement(notice, DeviceId, T0.AddMinutes(2), T0.AddMinutes(2));

        Assert.Equal(DismissalOutcome.NoChange, state.RecordDismissal(notice, T0.AddMinutes(3), T0.AddMinutes(3)));
        Assert.Null(state.NextReminderAt);
    }

    [Fact]
    public void Acknowledgement_is_rejected_for_notices_that_do_not_require_it()
    {
        var notice = Published(Normal());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        var ex = Assert.Throws<DomainException>(() => state.RecordAcknowledgement(notice, DeviceId, T0, T0));
        Assert.Equal("notification.acknowledgement_not_required", ex.Code);
    }

    [Fact]
    public void Offline_acknowledgement_made_before_withdrawal_is_preserved()
    {
        var notice = Published(Critical());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        notice.Withdraw(Admin, T0.AddHours(1));

        // Acknowledged at +30 min while offline; uploaded at +2 h after the withdrawal.
        Assert.True(state.RecordAcknowledgement(notice, DeviceId, T0.AddMinutes(30), T0.AddHours(2)));
        Assert.Equal(T0.AddMinutes(30), state.AcknowledgedAt);
    }

    [Fact]
    public void Acknowledgement_made_after_withdrawal_is_rejected()
    {
        var notice = Published(Critical());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);
        notice.Withdraw(Admin, T0.AddHours(1));

        var ex = Assert.Throws<DomainException>(() => state.RecordAcknowledgement(notice, DeviceId, T0.AddHours(1.5), T0.AddHours(2)));
        Assert.Equal("notification.not_active", ex.Code);
        Assert.False(state.IsAcknowledged);
    }

    [Fact]
    public void Acknowledgement_after_expiry_is_rejected()
    {
        var notice = Published(Critical(), expiresAt: T0.AddHours(1));
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        Assert.Throws<DomainException>(() => state.RecordAcknowledgement(notice, DeviceId, T0.AddHours(1), T0.AddHours(1)));
    }

    [Fact]
    public void Future_client_timestamps_are_clamped_to_server_receipt_time()
    {
        var notice = Published(Critical());
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        state.RecordAcknowledgement(notice, DeviceId, T0.AddDays(30), T0.AddMinutes(5));

        Assert.Equal(T0.AddMinutes(5), state.AcknowledgedAt);
    }

    [Fact]
    public void Withdrawn_or_expired_notices_are_never_presented()
    {
        var withdrawn = Published(Critical());
        var s1 = NotificationRecipientState.Create(withdrawn, RecipientFor(1001), T0);
        withdrawn.Withdraw(Admin, T0.AddMinutes(1));

        var expired = Published(Normal(), expiresAt: T0.AddHours(1));
        var s2 = NotificationRecipientState.Create(expired, RecipientFor(1001), T0);

        Assert.False(s1.ShouldPresent(withdrawn, T0.AddMinutes(2)));
        Assert.False(s2.ShouldPresent(expired, T0.AddHours(1)));
    }

    [Fact]
    public void State_cannot_link_recipient_and_notification_from_different_tenants()
    {
        var notice = Published();
        var foreignRecipient = RecipientFor(1001, TenantB);

        var ex = Assert.Throws<DomainException>(() => NotificationRecipientState.Create(notice, foreignRecipient, T0));
        Assert.Equal("tenant.mismatch", ex.Code);
    }

    [Fact]
    public void State_rejects_operations_with_another_notification()
    {
        var notice = Published();
        var other = Published();
        var state = NotificationRecipientState.Create(notice, RecipientFor(1001), T0);

        Assert.Throws<DomainException>(() => state.RecordDismissal(other, T0, T0));
    }

    [Fact]
    public void Draft_cannot_be_dismissed()
    {
        var draft = Draft();
        var state = NotificationRecipientState.Create(draft, RecipientFor(1001), T0);

        Assert.Throws<DomainException>(() => state.RecordDismissal(draft, T0, T0));
    }
}
