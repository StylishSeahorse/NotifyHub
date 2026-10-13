using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Notifications;

/// <summary>
/// Controls how often an unacknowledged notice that requires acknowledgement is re-presented. Bounded on
/// both axes so that a misconfiguration cannot cause a reminder storm. Quiet hours and escalation are added
/// in Phase 2 (they require tenant time-zone configuration).
/// </summary>
public sealed record ReminderPolicy
{
    public const int MinimumIntervalMinutes = 5;
    public const int MaximumIntervalMinutes = 7 * 24 * 60;
    public const int MaximumPresentationsPerDay = 48;

    public ReminderPolicy(int intervalMinutes, int maxPresentationsPerDay, bool representAtLogon)
    {
        if (intervalMinutes is < MinimumIntervalMinutes or > MaximumIntervalMinutes)
        {
            throw new DomainException(
                "reminder.invalid_interval",
                $"Reminder interval must be between {MinimumIntervalMinutes} minutes and {MaximumIntervalMinutes} minutes.");
        }

        if (maxPresentationsPerDay is < 1 or > MaximumPresentationsPerDay)
        {
            throw new DomainException(
                "reminder.invalid_frequency",
                $"Maximum presentations per day must be between 1 and {MaximumPresentationsPerDay}.");
        }

        IntervalMinutes = intervalMinutes;
        MaxPresentationsPerDay = maxPresentationsPerDay;
        RepresentAtLogon = representAtLogon;
    }

    /// <summary>One hour between reminders, at most eight per day, and re-presented at each logon.</summary>
    public static ReminderPolicy Default { get; } = new(60, 8, true);

    public int IntervalMinutes { get; }

    public int MaxPresentationsPerDay { get; }

    public bool RepresentAtLogon { get; }

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);
}
