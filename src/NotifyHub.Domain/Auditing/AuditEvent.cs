using System.Text.RegularExpressions;
using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Auditing;

public enum AuditActorType
{
    User,
    Device,
    Recipient,
    System,
}

/// <summary>
/// Append-only record of a privileged or security-relevant action. <see cref="TenantId"/> is null only for
/// platform-level actions. The database rejects updates to audit rows.
/// </summary>
public sealed partial class AuditEvent
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 100;
    public const int MetadataMaxLength = 8192;
    public const int CorrelationIdMaxLength = 64;

    private AuditEvent()
    {
        Action = null!;
        EntityType = null!;
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    /// <summary>Dotted lowercase action name, e.g. "notification.published".</summary>
    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string? CorrelationId { get; private set; }

    /// <summary>JSON object with action-specific detail. Must never contain secrets or credential material.</summary>
    public string? Metadata { get; private set; }

    public static AuditEvent Record(
        Guid? tenantId,
        AuditActorType actorType,
        Guid? actorId,
        string action,
        string entityType,
        Guid? entityId,
        DateTimeOffset occurredAt,
        string? correlationId = null,
        string? metadata = null)
    {
        if (!Enum.IsDefined(actorType))
        {
            throw new DomainException("audit.invalid_actor", "Unknown actor type.");
        }

        if (actorType != AuditActorType.System && actorId is null)
        {
            throw new DomainException("audit.actor_required", "Non-system audit events must identify the actor.");
        }

        if (action is null || action.Length > ActionMaxLength || !ActionPattern().IsMatch(action))
        {
            throw new DomainException("audit.invalid_action", "Action must be a dotted lowercase identifier.");
        }

        if (metadata is not null && metadata.Length > MetadataMaxLength)
        {
            throw new DomainException("audit.metadata_too_large", $"Audit metadata must be at most {MetadataMaxLength} characters.");
        }

        return new AuditEvent
        {
            Id = Ids.New(),
            TenantId = tenantId,
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            EntityType = Guard.Text(entityType, "Entity type", EntityTypeMaxLength),
            EntityId = entityId,
            OccurredAt = Guard.Utc(occurredAt),
            CorrelationId = Guard.OptionalText(correlationId, "Correlation ID", CorrelationIdMaxLength),
            Metadata = metadata,
        };
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ActionPattern();
}
