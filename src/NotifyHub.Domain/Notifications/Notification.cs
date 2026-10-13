using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Notification aggregate. Owns the global lifecycle and the requested audience.
/// <para>
/// Lifecycle: Draft → Scheduled → Published, or Draft → Published. Scheduled → Draft (unschedule).
/// Scheduled or Published → Withdrawn. Published → Expired. Withdrawn and Expired are terminal.
/// </para>
/// <para>
/// Only drafts are editable. Published content is immutable: a material change is made by creating a new
/// revision (<see cref="CreateRevision"/>), which shares the <see cref="SeriesId"/>, increments
/// <see cref="Revision"/>, and withdraws the revision it supersedes when it becomes active.
/// </para>
/// Recipient state (dismissal, acknowledgement) is held separately in <see cref="NotificationRecipientState"/>
/// and never changes the global status.
/// </summary>
public sealed class Notification : ITenantOwned
{
    public const int MaxTargets = 500;

    private readonly List<NotificationTarget> _targets = [];

    private Notification()
    {
        Title = null!;
        Body = null!;
        ReminderPolicy = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Identifier shared by every revision of the same logical notice (the first revision's ID).</summary>
    public Guid SeriesId { get; private set; }

    public int Revision { get; private set; }

    public Guid? SupersedesNotificationId { get; private set; }

    public Guid? SupersededByNotificationId { get; private set; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public NotificationSeverity Severity { get; private set; }

    public DisplayMode DisplayMode { get; private set; }

    public bool RequiresAcknowledgement { get; private set; }

    public ReminderPolicy ReminderPolicy { get; private set; }

    public NotificationStatus Status { get; private set; }

    /// <summary>Requested start of the display window. For scheduled notices this is the publication time.</summary>
    public DateTimeOffset? StartsAt { get; private set; }

    /// <summary>End of the display window. Clients apply it locally while offline.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? PublishedBy { get; private set; }

    /// <summary>When the notice actually became active (immediate publication or scheduled activation).</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public Guid? WithdrawnBy { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    public IReadOnlyCollection<NotificationTarget> Targets => _targets.AsReadOnly();

    public static Notification CreateDraft(Guid tenantId, NotificationContent content, Guid createdBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        var utcNow = Guard.Utc(now);
        var id = Ids.New();
        var notification = new Notification
        {
            Id = id,
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            SeriesId = id,
            Revision = 1,
            Status = NotificationStatus.Draft,
            CreatedBy = Guard.NotEmpty(createdBy, "Creator"),
            CreatedAt = utcNow,
        };
        notification.ApplyContent(content, utcNow);
        return notification;
    }

    /// <summary>
    /// Creates a new draft revision of a scheduled or published notice. The original is not modified until
    /// the revision becomes active, at which point the original is withdrawn as superseded.
    /// </summary>
    public static Notification CreateRevision(Notification previous, NotificationContent content, Guid createdBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (previous.Status is not (NotificationStatus.Scheduled or NotificationStatus.Published))
        {
            throw new DomainException(
                "notification.revision_not_allowed",
                $"Only scheduled or published notifications can be revised; this one is {previous.Status}. Edit drafts directly.");
        }

        if (previous.SupersededByNotificationId is not null)
        {
            throw new DomainException("notification.already_superseded", "This revision has already been superseded.");
        }

        var revision = CreateDraft(previous.TenantId, content, createdBy, now);
        revision.SeriesId = previous.SeriesId;
        revision.Revision = previous.Revision + 1;
        revision.SupersedesNotificationId = previous.Id;
        revision.ExpiresAt = previous.ExpiresAt;
        foreach (var target in previous._targets)
        {
            revision._targets.Add(NotificationTarget.Create(revision.TenantId, revision.Id, target.ToSpec()));
        }

        return revision;
    }

    public void UpdateContent(NotificationContent content, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureStatus(NotificationStatus.Draft, "edit");
        ApplyContent(content, Guard.Utc(now));
    }

    public void SetWindow(DateTimeOffset? startsAt, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        EnsureStatus(NotificationStatus.Draft, "edit");
        var start = Guard.Utc(startsAt);
        var end = Guard.Utc(expiresAt);
        if (start is not null && end is not null && end <= start)
        {
            throw new DomainException("notification.invalid_window", "Expiry must be after the start time.");
        }

        StartsAt = start;
        ExpiresAt = end;
        UpdatedAt = Guard.Utc(now);
    }

    public void ReplaceTargets(IEnumerable<NotificationTargetSpec> targets, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(targets);
        EnsureStatus(NotificationStatus.Draft, "edit");

        var specs = targets.ToList();
        if (specs.Count > MaxTargets)
        {
            throw new DomainException("target.too_many", $"A notification can have at most {MaxTargets} targets.");
        }

        foreach (var spec in specs)
        {
            ArgumentNullException.ThrowIfNull(spec);
            if (!Enum.IsDefined(spec.TargetType))
            {
                throw new DomainException("target.invalid_type", "Unknown target type.");
            }

            var needsId = spec.TargetType != NotificationTargetType.AllDevices;
            if (needsId && (spec.TargetId is null || spec.TargetId == Guid.Empty))
            {
                throw new DomainException("target.missing_reference", $"A {spec.TargetType} target requires an identifier.");
            }

            if (!needsId && spec.TargetId is not null)
            {
                throw new DomainException("target.unexpected_reference", "An all-devices target must not reference an entity.");
            }
        }

        if (specs.Any(s => s.TargetType == NotificationTargetType.AllDevices) && specs.Count > 1)
        {
            throw new DomainException("target.all_devices_exclusive", "An all-devices target cannot be combined with other targets.");
        }

        if (specs.Distinct().Count() != specs.Count)
        {
            throw new DomainException("target.duplicate", "Targets must not contain duplicates.");
        }

        _targets.Clear();
        _targets.AddRange(specs.Select(s => NotificationTarget.Create(TenantId, Id, s)));
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>Publishes a draft immediately. If this is a revision, <paramref name="superseded"/> must be the revision it replaces.</summary>
    public void PublishNow(Guid actorId, DateTimeOffset now, Notification? superseded = null)
    {
        EnsureStatus(NotificationStatus.Draft, "publish");
        var utcNow = Guard.Utc(now);
        EnsurePublishable(utcNow);
        if (StartsAt > utcNow)
        {
            throw new DomainException("notification.start_in_future", "The start time is in the future; schedule the notification instead.");
        }

        ValidateSuperseded(superseded);
        StartsAt ??= utcNow;
        Activate(Guard.NotEmpty(actorId, "Actor"), utcNow, superseded);
    }

    /// <summary>Schedules a draft for publication at <see cref="StartsAt"/>.</summary>
    public void Schedule(Guid actorId, DateTimeOffset now)
    {
        EnsureStatus(NotificationStatus.Draft, "schedule");
        var utcNow = Guard.Utc(now);
        EnsurePublishable(utcNow);
        if (StartsAt is null || StartsAt <= utcNow)
        {
            throw new DomainException("notification.start_not_in_future", "A scheduled notification needs a start time in the future.");
        }

        PublishedBy = Guard.NotEmpty(actorId, "Actor");
        ScheduledAt = utcNow;
        Status = NotificationStatus.Scheduled;
        UpdatedAt = utcNow;
    }

    /// <summary>Returns a scheduled notification to draft so it can be edited.</summary>
    public void Unschedule(DateTimeOffset now)
    {
        EnsureStatus(NotificationStatus.Scheduled, "unschedule");
        Status = NotificationStatus.Draft;
        ScheduledAt = null;
        PublishedBy = null;
        UpdatedAt = Guard.Utc(now);
    }

    /// <summary>
    /// Called by the scheduler. Activates a scheduled notification whose start time has passed. If the
    /// scheduler was delayed past the expiry time, the notice moves straight to Expired and is never shown.
    /// </summary>
    /// <returns>True if the status changed.</returns>
    public bool ActivateIfDue(DateTimeOffset now, Notification? superseded = null)
    {
        var utcNow = Guard.Utc(now);
        if (Status != NotificationStatus.Scheduled || StartsAt > utcNow)
        {
            return false;
        }

        ValidateSuperseded(superseded);
        if (ExpiresAt <= utcNow)
        {
            Status = NotificationStatus.Expired;
            ExpiredAt = utcNow;
            UpdatedAt = utcNow;
            return true;
        }

        Activate(PublishedBy!.Value, utcNow, superseded);
        return true;
    }

    /// <summary>Called by the scheduler. Expires a published notice whose expiry time has passed.</summary>
    /// <returns>True if the status changed.</returns>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        if (Status != NotificationStatus.Published || ExpiresAt is null || ExpiresAt > utcNow)
        {
            return false;
        }

        Status = NotificationStatus.Expired;
        ExpiredAt = utcNow;
        UpdatedAt = utcNow;
        return true;
    }

    public void Withdraw(Guid actorId, DateTimeOffset now)
    {
        if (Status is not (NotificationStatus.Scheduled or NotificationStatus.Published))
        {
            throw InvalidTransition("withdraw");
        }

        var utcNow = Guard.Utc(now);
        Status = NotificationStatus.Withdrawn;
        WithdrawnBy = Guard.NotEmpty(actorId, "Actor");
        WithdrawnAt = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>True if the notice is currently published and inside its display window.</summary>
    public bool IsActiveAt(DateTimeOffset instant) => Status == NotificationStatus.Published && WasActiveAt(instant);

    /// <summary>
    /// True if the notice was published and inside its display window at <paramref name="instant"/>,
    /// regardless of later withdrawal or expiry. Used to evaluate events recorded by offline clients.
    /// </summary>
    public bool WasActiveAt(DateTimeOffset instant)
    {
        var utc = Guard.Utc(instant);
        if (PublishedAt is null || utc < PublishedAt)
        {
            return false;
        }

        if (WithdrawnAt is not null && utc >= WithdrawnAt)
        {
            return false;
        }

        if (ExpiresAt is not null && utc >= ExpiresAt)
        {
            return false;
        }

        return ExpiredAt is null || utc < ExpiredAt;
    }

    private void Activate(Guid actorId, DateTimeOffset utcNow, Notification? superseded)
    {
        Status = NotificationStatus.Published;
        PublishedBy = actorId;
        PublishedAt = utcNow;
        UpdatedAt = utcNow;

        if (superseded is not null)
        {
            if (superseded.Status is NotificationStatus.Scheduled or NotificationStatus.Published)
            {
                superseded.Withdraw(actorId, utcNow);
            }

            superseded.SupersededByNotificationId = Id;
            superseded.UpdatedAt = utcNow;
        }
    }

    private void ValidateSuperseded(Notification? superseded)
    {
        if (SupersedesNotificationId is null)
        {
            if (superseded is not null)
            {
                throw new DomainException("notification.not_a_revision", "This notification does not supersede another.");
            }

            return;
        }

        if (superseded is null || superseded.Id != SupersedesNotificationId || superseded.TenantId != TenantId)
        {
            throw new DomainException("notification.superseded_required", "The superseded revision must be supplied when activating a revision.");
        }

        if (superseded.SupersededByNotificationId is not null && superseded.SupersededByNotificationId != Id)
        {
            throw new DomainException("notification.already_superseded", "The previous revision has already been superseded by another revision.");
        }
    }

    private void EnsurePublishable(DateTimeOffset utcNow)
    {
        if (_targets.Count == 0)
        {
            throw new DomainException("notification.no_targets", "A notification needs at least one target before it can be published.");
        }

        if (ExpiresAt is not null && ExpiresAt <= utcNow)
        {
            throw new DomainException("notification.already_expired", "The expiry time has already passed.");
        }
    }

    private void ApplyContent(NotificationContent content, DateTimeOffset utcNow)
    {
        Title = content.Title;
        Body = content.Body;
        Severity = content.Severity;
        DisplayMode = content.DisplayMode;
        RequiresAcknowledgement = content.RequiresAcknowledgement;
        ReminderPolicy = content.ReminderPolicy;
        UpdatedAt = utcNow;
    }

    private void EnsureStatus(NotificationStatus required, string operation)
    {
        if (Status != required)
        {
            throw InvalidTransition(operation);
        }
    }

    private DomainException InvalidTransition(string operation) =>
        new("notification.invalid_transition", $"Cannot {operation} a notification in state {Status}.");
}
