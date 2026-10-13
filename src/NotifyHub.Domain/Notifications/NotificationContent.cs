using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Editable content of a notification. Title and body are plain text; they are rendered as inert text by
/// every client and are never interpreted as HTML, markup or script.
/// </summary>
public sealed record NotificationContent
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;

    public NotificationContent(
        string title,
        string body,
        NotificationSeverity severity,
        DisplayMode displayMode,
        bool requiresAcknowledgement,
        ReminderPolicy? reminderPolicy = null)
    {
        Title = Guard.Text(title, "Title", TitleMaxLength);
        Body = Guard.Text(body, "Body", BodyMaxLength);
        Severity = Enum.IsDefined(severity) ? severity : throw new DomainException("validation.invalid_severity", "Unknown severity.");
        DisplayMode = Enum.IsDefined(displayMode) ? displayMode : throw new DomainException("validation.invalid_display_mode", "Unknown display mode.");
        RequiresAcknowledgement = requiresAcknowledgement;
        ReminderPolicy = reminderPolicy ?? ReminderPolicy.Default;
    }

    public string Title { get; }

    public string Body { get; }

    public NotificationSeverity Severity { get; }

    public DisplayMode DisplayMode { get; }

    public bool RequiresAcknowledgement { get; }

    public ReminderPolicy ReminderPolicy { get; }
}
