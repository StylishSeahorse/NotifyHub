namespace NotifyHub.Domain.Common;

/// <summary>
/// Marks an entity that belongs to exactly one tenant. Tenant ownership is immutable once assigned and
/// is enforced in the database by composite (tenant_id, id) foreign keys.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
