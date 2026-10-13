using NotifyHub.Domain.Common;
using NotifyHub.Domain.Notifications;

namespace NotifyHub.UnitTests.Domain;

public sealed class ContentAndPolicyTests
{
    [Theory]
    [InlineData("", "body")]
    [InlineData("   ", "body")]
    [InlineData("title", "")]
    public void Title_and_body_are_required(string title, string body)
    {
        Assert.Throws<DomainException>(() => new NotificationContent(title, body, NotificationSeverity.Info, DisplayMode.Toast, false));
    }

    [Fact]
    public void Content_length_is_bounded()
    {
        Assert.Throws<DomainException>(() => new NotificationContent(new string('a', 201), "b", NotificationSeverity.Info, DisplayMode.Toast, false));
        Assert.Throws<DomainException>(() => new NotificationContent("t", new string('a', 4001), NotificationSeverity.Info, DisplayMode.Toast, false));
    }

    [Fact]
    public void Control_characters_are_rejected_but_line_breaks_allowed()
    {
        Assert.Throws<DomainException>(() => new NotificationContent("bad\u0007title", "b", NotificationSeverity.Info, DisplayMode.Toast, false));
        var ok = new NotificationContent("t", "line one\r\nline two\tend", NotificationSeverity.Info, DisplayMode.Toast, false);
        Assert.Contains('\n', ok.Body);
    }

    [Fact]
    public void Markup_is_stored_verbatim_as_inert_text()
    {
        var content = new NotificationContent("<script>alert(1)</script>", "<b>bold</b>", NotificationSeverity.Info, DisplayMode.Toast, false);

        Assert.Equal("<script>alert(1)</script>", content.Title);
    }

    [Fact]
    public void Undefined_enum_values_are_rejected()
    {
        Assert.Throws<DomainException>(() => new NotificationContent("t", "b", (NotificationSeverity)42, DisplayMode.Toast, false));
        Assert.Throws<DomainException>(() => new NotificationContent("t", "b", NotificationSeverity.Info, (DisplayMode)42, false));
    }

    [Fact]
    public void Default_reminder_policy_is_bounded()
    {
        var policy = ReminderPolicy.Default;

        Assert.Equal(TimeSpan.FromHours(1), policy.Interval);
        Assert.Equal(8, policy.MaxPresentationsPerDay);
        Assert.True(policy.RepresentAtLogon);
    }

    [Theory]
    [InlineData(4, 8)]
    [InlineData(0, 8)]
    [InlineData(-5, 8)]
    [InlineData(ReminderPolicy.MaximumIntervalMinutes + 1, 8)]
    [InlineData(60, 0)]
    [InlineData(60, ReminderPolicy.MaximumPresentationsPerDay + 1)]
    public void Reminder_storms_are_prevented(int intervalMinutes, int maxPerDay)
    {
        Assert.Throws<DomainException>(() => new ReminderPolicy(intervalMinutes, maxPerDay, true));
    }

    [Fact]
    public void Reminder_policy_has_value_equality()
    {
        Assert.Equal(new ReminderPolicy(30, 4, false), new ReminderPolicy(30, 4, false));
    }
}
