using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Notifications;
using NotifyHub.IntegrationTests.Infrastructure;

namespace NotifyHub.IntegrationTests.Persistence;

public sealed class NotificationPersistenceTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private TenantSeed _a = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _a = await SeedTenantAsync("persist");
    }

    [Fact]
    public async Task Notification_round_trips_with_targets_policy_and_utc_timestamps()
    {
        var ct = TestContext.Current.CancellationToken;
        var policy = new ReminderPolicy(15, 12, false);
        Guid id;
        await using (var db = NewContext())
        {
            var n = Notification.CreateDraft(
                _a.TenantId,
                new NotificationContent("Title <b>", "Body\nline 2", NotificationSeverity.Critical, DisplayMode.Popup, true, policy),
                _a.AdminId,
                T0);
            n.SetWindow(T0.AddHours(1), T0.AddDays(1), T0);
            n.ReplaceTargets([NotificationTargetSpec.Group(_a.GroupId), NotificationTargetSpec.ForRecipient(_a.RecipientAId)], T0);
            n.Schedule(_a.AdminId, T0);
            db.Add(n);
            await db.SaveChangesAsync(ct);
            id = n.Id;
        }

        await using (var db = NewContext())
        {
            var loaded = await db.Notifications.Include(x => x.Targets).SingleAsync(x => x.Id == id, ct);

            Assert.Equal(NotificationStatus.Scheduled, loaded.Status);
            Assert.Equal("Title <b>", loaded.Title);
            Assert.Equal("Body\nline 2", loaded.Body);
            Assert.Equal(policy, loaded.ReminderPolicy);
            Assert.Equal(T0.AddHours(1), loaded.StartsAt);
            Assert.Equal(TimeSpan.Zero, loaded.StartsAt!.Value.Offset);
            Assert.Equal(2, loaded.Targets.Count);
            Assert.Contains(loaded.Targets, t => t.DeviceGroupId == _a.GroupId);
            Assert.Contains(loaded.Targets, t => t.RecipientId == _a.RecipientAId);

            // The scheduler activates it later in a separate unit of work.
            Assert.True(loaded.ActivateIfDue(T0.AddHours(1)));
            await db.SaveChangesAsync(ct);
        }

        await using (var db = NewContext())
        {
            Assert.Equal(NotificationStatus.Published, (await db.Notifications.SingleAsync(x => x.Id == id, ct)).Status);
        }
    }

    [Fact]
    public async Task Replacing_targets_on_a_draft_persists_the_new_set()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid id;
        await using (var db = NewContext())
        {
            var n = Notification.CreateDraft(_a.TenantId, new NotificationContent("t", "b", NotificationSeverity.Info, DisplayMode.Toast, false), _a.AdminId, T0);
            n.ReplaceTargets([NotificationTargetSpec.ForDevice(_a.DeviceId)], T0);
            db.Add(n);
            await db.SaveChangesAsync(ct);
            id = n.Id;
        }

        await using (var db = NewContext())
        {
            var n = await db.Notifications.Include(x => x.Targets).SingleAsync(x => x.Id == id, ct);
            n.ReplaceTargets([NotificationTargetSpec.AllDevices], T0);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = NewContext())
        {
            var targets = await db.NotificationTargets.Where(t => t.NotificationId == id).ToListAsync(ct);
            Assert.Single(targets);
            Assert.Equal(NotificationTargetType.AllDevices, targets[0].TargetType);
        }
    }

    [Fact]
    public async Task Dismissal_persists_for_one_user_and_not_another_across_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedPublishedNotificationAsync(_a, critical: false);

        // Session 1: user A dismisses on the shared device.
        await using (var db = NewContext())
        {
            var n = await db.Notifications.SingleAsync(x => x.Id == id, ct);
            var userA = await db.Recipients.SingleAsync(r => r.Id == _a.RecipientAId, ct);
            var state = NotificationRecipientState.Create(n, userA, T0);
            state.RecordDismissal(n, T0.AddMinutes(5), T0.AddMinutes(5));
            db.Add(state);
            await db.SaveChangesAsync(ct);
        }

        // Later sessions: A logs on again; B logs on to the same device.
        await using (var db = NewContext())
        {
            var n = await db.Notifications.SingleAsync(x => x.Id == id, ct);
            var stateA = await db.NotificationRecipientStates.SingleAsync(s => s.NotificationId == id && s.RecipientId == _a.RecipientAId, ct);
            Assert.False(stateA.ShouldPresent(n, T0.AddHours(8)));

            Assert.False(await db.NotificationRecipientStates.AnyAsync(s => s.NotificationId == id && s.RecipientId == _a.RecipientBId, ct));
            var userB = await db.Recipients.SingleAsync(r => r.Id == _a.RecipientBId, ct);
            Assert.True(NotificationRecipientState.Create(n, userB, T0.AddHours(8)).ShouldPresent(n, T0.AddHours(8)));
            Assert.Equal(NotificationStatus.Published, n.Status);
        }
    }

    [Fact]
    public async Task Acknowledgement_by_one_user_does_not_acknowledge_for_another()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await SeedPublishedNotificationAsync(_a, critical: true);

        await using (var db = NewContext())
        {
            var n = await db.Notifications.SingleAsync(x => x.Id == id, ct);
            var userA = await db.Recipients.SingleAsync(r => r.Id == _a.RecipientAId, ct);
            var userB = await db.Recipients.SingleAsync(r => r.Id == _a.RecipientBId, ct);
            var stateA = NotificationRecipientState.Create(n, userA, T0);
            var stateB = NotificationRecipientState.Create(n, userB, T0);
            stateA.RecordAcknowledgement(n, _a.DeviceId, T0.AddMinutes(1), T0.AddMinutes(1));
            stateB.RecordDismissal(n, T0.AddMinutes(1), T0.AddMinutes(1));
            db.AddRange(stateA, stateB);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = NewContext())
        {
            var states = await db.NotificationRecipientStates.Where(s => s.NotificationId == id).ToDictionaryAsync(s => s.RecipientId, ct);
            Assert.NotNull(states[_a.RecipientAId].AcknowledgedAt);
            Assert.Equal(_a.DeviceId, states[_a.RecipientAId].AcknowledgedOnDeviceId);
            Assert.Null(states[_a.RecipientBId].AcknowledgedAt);
            Assert.NotNull(states[_a.RecipientBId].NextReminderAt);

            var unresolved = await db.NotificationRecipientStates.CountAsync(s => s.NotificationId == id && s.AcknowledgedAt == null, ct);
            Assert.Equal(1, unresolved);
        }
    }

    [Fact]
    public async Task Concurrent_edits_are_detected_with_optimistic_concurrency()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid id;
        await using (var db = NewContext())
        {
            var n = Notification.CreateDraft(_a.TenantId, new NotificationContent("v0", "b", NotificationSeverity.Info, DisplayMode.Toast, false), _a.AdminId, T0);
            db.Add(n);
            await db.SaveChangesAsync(ct);
            id = n.Id;
        }

        await using var editor1 = NewContext();
        await using var editor2 = NewContext();
        var copy1 = await editor1.Notifications.SingleAsync(x => x.Id == id, ct);
        var copy2 = await editor2.Notifications.SingleAsync(x => x.Id == id, ct);

        copy1.UpdateContent(new NotificationContent("v1", "b", NotificationSeverity.Info, DisplayMode.Toast, false), T0);
        await editor1.SaveChangesAsync(ct);

        copy2.UpdateContent(new NotificationContent("v2", "b", NotificationSeverity.Info, DisplayMode.Toast, false), T0);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => editor2.SaveChangesAsync(ct));

        await using var check = NewContext();
        Assert.Equal("v1", (await check.Notifications.SingleAsync(x => x.Id == id, ct)).Title);
    }

    [Fact]
    public async Task Publishing_a_revision_withdraws_the_previous_one_atomically()
    {
        var ct = TestContext.Current.CancellationToken;
        var v1Id = await SeedPublishedNotificationAsync(_a, critical: true);
        Guid v2Id;
        await using (var db = NewContext())
        {
            var v1 = await db.Notifications.Include(x => x.Targets).SingleAsync(x => x.Id == v1Id, ct);
            var v2 = Notification.CreateRevision(v1, new NotificationContent("Critical v2", "Updated guidance.", NotificationSeverity.Critical, DisplayMode.Popup, true), _a.AdminId, T0.AddHours(1));
            v2.PublishNow(_a.AdminId, T0.AddHours(1), v1);
            db.Add(v2);
            await db.SaveChangesAsync(ct);
            v2Id = v2.Id;
        }

        await using (var db = NewContext())
        {
            var series = await db.Notifications.Include(x => x.Targets).Where(x => x.Id == v1Id || x.Id == v2Id).ToDictionaryAsync(x => x.Id, ct);
            Assert.Equal(NotificationStatus.Withdrawn, series[v1Id].Status);
            Assert.Equal(v2Id, series[v1Id].SupersededByNotificationId);
            Assert.Equal(NotificationStatus.Published, series[v2Id].Status);
            Assert.Equal(2, series[v2Id].Revision);
            Assert.Equal(series[v1Id].SeriesId, series[v2Id].SeriesId);
            Assert.Single(series[v2Id].Targets);
        }
    }
}
