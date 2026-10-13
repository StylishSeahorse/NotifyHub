using NotifyHub.Domain.Common;
using NotifyHub.Domain.Notifications;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

public sealed class NotificationLifecycleTests
{
    [Fact]
    public void New_draft_is_revision_one_of_its_own_series()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        Assert.Equal(NotificationStatus.Draft, n.Status);
        Assert.Equal(1, n.Revision);
        Assert.Equal(n.Id, n.SeriesId);
        Assert.Null(n.SupersedesNotificationId);
        Assert.Equal(TimeSpan.Zero, n.CreatedAt.Offset);
    }

    [Fact]
    public void Timestamps_are_normalised_to_utc()
    {
        var local = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(1));
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, local);

        Assert.Equal(TimeSpan.Zero, n.CreatedAt.Offset);
        Assert.Equal(T0, n.CreatedAt);
    }

    [Fact]
    public void Publish_now_activates_draft()
    {
        var n = Draft();

        n.PublishNow(Admin, T0);

        Assert.Equal(NotificationStatus.Published, n.Status);
        Assert.Equal(T0, n.PublishedAt);
        Assert.Equal(T0, n.StartsAt);
        Assert.Equal(Admin, n.PublishedBy);
        Assert.True(n.IsActiveAt(T0));
    }

    [Fact]
    public void Publish_requires_at_least_one_target()
    {
        var n = Notification.CreateDraft(TenantA, Normal(), Admin, T0);

        var ex = Assert.Throws<DomainException>(() => n.PublishNow(Admin, T0));
        Assert.Equal("notification.no_targets", ex.Code);
    }

    [Fact]
    public void Publish_rejects_expiry_in_the_past()
    {
        var n = Draft();
        n.SetWindow(null, T0.AddMinutes(-1), T0.AddMinutes(-10));

        var ex = Assert.Throws<DomainException>(() => n.PublishNow(Admin, T0));
        Assert.Equal("notification.already_expired", ex.Code);
    }

    [Fact]
    public void Publish_now_with_future_start_is_rejected()
    {
        var n = Draft();
        n.SetWindow(T0.AddHours(1), null, T0);

        var ex = Assert.Throws<DomainException>(() => n.PublishNow(Admin, T0));
        Assert.Equal("notification.start_in_future", ex.Code);
    }

    [Fact]
    public void Window_expiry_must_follow_start()
    {
        var n = Draft();

        var ex = Assert.Throws<DomainException>(() => n.SetWindow(T0.AddHours(2), T0.AddHours(1), T0));
        Assert.Equal("notification.invalid_window", ex.Code);
    }

    [Fact]
    public void Schedule_then_activate_when_due()
    {
        var n = Draft();
        n.SetWindow(T0.AddHours(1), T0.AddHours(3), T0);

        n.Schedule(Admin, T0);
        Assert.Equal(NotificationStatus.Scheduled, n.Status);
        Assert.False(n.IsActiveAt(T0.AddMinutes(30)));

        Assert.False(n.ActivateIfDue(T0.AddMinutes(59)));
        Assert.Equal(NotificationStatus.Scheduled, n.Status);

        Assert.True(n.ActivateIfDue(T0.AddHours(1)));
        Assert.Equal(NotificationStatus.Published, n.Status);
        Assert.Equal(T0.AddHours(1), n.PublishedAt);
        Assert.Equal(Admin, n.PublishedBy);
    }

    [Fact]
    public void Schedule_requires_future_start()
    {
        var n = Draft();

        var ex = Assert.Throws<DomainException>(() => n.Schedule(Admin, T0));
        Assert.Equal("notification.start_not_in_future", ex.Code);
    }

    [Fact]
    public void Delayed_scheduler_past_expiry_expires_without_ever_publishing()
    {
        var n = Draft();
        n.SetWindow(T0.AddHours(1), T0.AddHours(2), T0);
        n.Schedule(Admin, T0);

        Assert.True(n.ActivateIfDue(T0.AddHours(5)));

        Assert.Equal(NotificationStatus.Expired, n.Status);
        Assert.Null(n.PublishedAt);
        Assert.False(n.WasActiveAt(T0.AddHours(1.5)));
    }

    [Fact]
    public void Unschedule_returns_to_editable_draft()
    {
        var n = Draft();
        n.SetWindow(T0.AddHours(1), null, T0);
        n.Schedule(Admin, T0);

        n.Unschedule(T0);
        n.UpdateContent(Normal("Changed"), T0);

        Assert.Equal(NotificationStatus.Draft, n.Status);
        Assert.Equal("Changed", n.Title);
    }

    [Fact]
    public void Published_content_is_immutable()
    {
        var n = Published();

        Assert.Equal("notification.invalid_transition", Assert.Throws<DomainException>(() => n.UpdateContent(Normal("x"), T0)).Code);
        Assert.Equal("notification.invalid_transition", Assert.Throws<DomainException>(() => n.SetWindow(null, T0.AddDays(1), T0)).Code);
        Assert.Equal("notification.invalid_transition", Assert.Throws<DomainException>(() => n.ReplaceTargets([NotificationTargetSpec.AllDevices], T0)).Code);
    }

    [Fact]
    public void Withdraw_published_notification()
    {
        var n = Published();

        n.Withdraw(Admin, T0.AddHours(1));

        Assert.Equal(NotificationStatus.Withdrawn, n.Status);
        Assert.Equal(T0.AddHours(1), n.WithdrawnAt);
        Assert.False(n.IsActiveAt(T0.AddHours(2)));
        Assert.True(n.WasActiveAt(T0.AddMinutes(30)));
        Assert.False(n.WasActiveAt(T0.AddHours(1)));
    }

    [Fact]
    public void Withdraw_scheduled_notification_cancels_it()
    {
        var n = Draft();
        n.SetWindow(T0.AddHours(1), null, T0);
        n.Schedule(Admin, T0);

        n.Withdraw(Admin, T0);

        Assert.Equal(NotificationStatus.Withdrawn, n.Status);
        Assert.False(n.ActivateIfDue(T0.AddHours(2)));
    }

    [Theory]
    [InlineData(NotificationStatus.Draft)]
    [InlineData(NotificationStatus.Withdrawn)]
    [InlineData(NotificationStatus.Expired)]
    public void Withdraw_is_rejected_from_non_active_states(NotificationStatus status)
    {
        var n = InState(status);

        var ex = Assert.Throws<DomainException>(() => n.Withdraw(Admin, T0.AddDays(2)));
        Assert.Equal("notification.invalid_transition", ex.Code);
    }

    [Fact]
    public void Expire_when_due()
    {
        var n = Published(expiresAt: T0.AddHours(2));

        Assert.False(n.ExpireIfDue(T0.AddHours(1)));
        Assert.True(n.IsActiveAt(T0.AddHours(1)));
        Assert.False(n.IsActiveAt(T0.AddHours(2)));

        Assert.True(n.ExpireIfDue(T0.AddHours(2)));
        Assert.Equal(NotificationStatus.Expired, n.Status);
        Assert.False(n.ExpireIfDue(T0.AddHours(3)));
    }

    [Fact]
    public void Notification_without_expiry_never_expires()
    {
        var n = Published();

        Assert.False(n.ExpireIfDue(T0.AddYears(5)));
        Assert.True(n.IsActiveAt(T0.AddYears(5)));
    }

    [Theory]
    [InlineData(NotificationStatus.Published)]
    [InlineData(NotificationStatus.Withdrawn)]
    [InlineData(NotificationStatus.Expired)]
    public void Only_drafts_can_be_published_or_scheduled(NotificationStatus status)
    {
        var n = InState(status);

        Assert.Throws<DomainException>(() => n.PublishNow(Admin, T0.AddDays(2)));
        Assert.Throws<DomainException>(() => n.Schedule(Admin, T0.AddDays(2)));
    }

    [Fact]
    public void Revision_shares_series_and_supersedes_previous_on_publish()
    {
        var v1 = Published(Critical());
        var v2 = Notification.CreateRevision(v1, Critical(), Admin, T0.AddHours(1));

        Assert.Equal(NotificationStatus.Draft, v2.Status);
        Assert.Equal(v1.SeriesId, v2.SeriesId);
        Assert.Equal(2, v2.Revision);
        Assert.Equal(v1.Id, v2.SupersedesNotificationId);
        Assert.Equal(v1.Targets.Select(t => t.ToSpec()), v2.Targets.Select(t => t.ToSpec()));
        Assert.Equal(NotificationStatus.Published, v1.Status);

        v2.PublishNow(Admin, T0.AddHours(1), superseded: v1);

        Assert.Equal(NotificationStatus.Published, v2.Status);
        Assert.Equal(NotificationStatus.Withdrawn, v1.Status);
        Assert.Equal(v2.Id, v1.SupersededByNotificationId);
    }

    [Fact]
    public void Revision_requires_superseded_notification_at_activation()
    {
        var v1 = Published();
        var v2 = Notification.CreateRevision(v1, Normal("v2"), Admin, T0);

        var ex = Assert.Throws<DomainException>(() => v2.PublishNow(Admin, T0));
        Assert.Equal("notification.superseded_required", ex.Code);
    }

    [Fact]
    public void Superseded_revision_cannot_be_revised_twice()
    {
        var v1 = Published();
        var v2 = Notification.CreateRevision(v1, Normal("v2"), Admin, T0);
        v2.PublishNow(Admin, T0, v1);

        Assert.Throws<DomainException>(() => Notification.CreateRevision(v1, Normal("v2b"), Admin, T0));
    }

    [Fact]
    public void Drafts_are_not_revised()
    {
        var ex = Assert.Throws<DomainException>(() => Notification.CreateRevision(Draft(), Normal(), Admin, T0));
        Assert.Equal("notification.revision_not_allowed", ex.Code);
    }

    [Fact]
    public void Non_revision_rejects_superseded_argument()
    {
        var other = Published();
        var n = Draft();

        Assert.Throws<DomainException>(() => n.PublishNow(Admin, T0, other));
    }

    private static Notification InState(NotificationStatus status)
    {
        switch (status)
        {
            case NotificationStatus.Draft:
                return Draft();
            case NotificationStatus.Published:
                return Published();
            case NotificationStatus.Withdrawn:
                var w = Published();
                w.Withdraw(Admin, T0.AddHours(1));
                return w;
            case NotificationStatus.Expired:
                var e = Published(expiresAt: T0.AddHours(1));
                e.ExpireIfDue(T0.AddHours(1));
                return e;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
