namespace NotifyHub.Domain.Notifications;

public enum NotificationSeverity
{
    Info,
    Warning,
    Critical,
}

public enum DisplayMode
{
    /// <summary>Native Windows toast. Falls back to a branded popup when toasts are unavailable (see docs).</summary>
    Toast,

    /// <summary>Custom branded WPF window.</summary>
    Popup,
}

/// <summary>Global lifecycle. Independent of any recipient's state.</summary>
public enum NotificationStatus
{
    Draft,
    Scheduled,
    Published,
    Withdrawn,
    Expired,
}

public enum NotificationTargetType
{
    /// <summary>Every eligible device in the tenant, evaluated at delivery time.</summary>
    AllDevices,
    DeviceGroup,
    Device,
    Recipient,
}

public enum DeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
}

/// <summary>Client-reported recipient events. Each is recorded separately; a display is not proof of reading.</summary>
public enum NotificationEventType
{
    DeliveryAttempted,
    Delivered,
    Displayed,
    DisplayFailed,
    Dismissed,
    Acknowledged,
}
