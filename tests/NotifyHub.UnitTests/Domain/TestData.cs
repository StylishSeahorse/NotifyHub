using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Notifications;

namespace NotifyHub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
    public static readonly Guid TenantA = Guid.CreateVersion7();
    public static readonly Guid TenantB = Guid.CreateVersion7();
    public static readonly Guid Admin = Guid.CreateVersion7();
    public static readonly Guid DeviceId = Guid.CreateVersion7();
    public const string DomainSid = "S-1-5-21-1004336348-1177238915-682003330";

    public static NotificationContent Normal(string title = "Maintenance tonight") =>
        new(title, "Systems will be unavailable from 22:00.", NotificationSeverity.Info, DisplayMode.Toast, requiresAcknowledgement: false);

    public static NotificationContent Critical(ReminderPolicy? policy = null) =>
        new("Security incident", "Read and acknowledge the incident guidance.", NotificationSeverity.Critical, DisplayMode.Popup, requiresAcknowledgement: true, policy);

    public static Notification Draft(NotificationContent? content = null, Guid? tenantId = null)
    {
        var n = Notification.CreateDraft(tenantId ?? TenantA, content ?? Normal(), Admin, T0);
        n.ReplaceTargets([NotificationTargetSpec.AllDevices], T0);
        return n;
    }

    public static Notification Published(NotificationContent? content = null, DateTimeOffset? expiresAt = null)
    {
        var n = Draft(content);
        if (expiresAt is not null)
        {
            n.SetWindow(null, expiresAt, T0);
        }

        n.PublishNow(Admin, T0);
        return n;
    }

    public static Recipient RecipientFor(int rid, Guid? tenantId = null) =>
        Recipient.ForActiveDirectory(tenantId ?? TenantA, DomainSid, $"{DomainSid}-{rid}", $"CONTOSO\\user{rid}", T0);
}
