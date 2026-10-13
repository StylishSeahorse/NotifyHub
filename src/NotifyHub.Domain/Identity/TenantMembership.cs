using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Identity;

/// <summary>Tenant-level roles. Platform administration is deliberately not a tenant role.</summary>
public enum TenantRole
{
    TenantAdministrator,
    Publisher,
    Auditor,
}

public enum MembershipStatus
{
    Active,
    Suspended,
}

/// <summary>Grants a <see cref="User"/> a single role within one tenant.</summary>
public sealed class TenantMembership : ITenantOwned
{
    private TenantMembership()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantRole Role { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TenantMembership Create(Guid tenantId, Guid userId, TenantRole role, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        return new TenantMembership
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            UserId = Guard.NotEmpty(userId, "User"),
            Role = role,
            Status = MembershipStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public bool IsActive => Status == MembershipStatus.Active;

    public void ChangeRole(TenantRole role, DateTimeOffset now)
    {
        Role = role;
        UpdatedAt = Guard.Utc(now);
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = MembershipStatus.Suspended;
        UpdatedAt = Guard.Utc(now);
    }

    public void Reactivate(DateTimeOffset now)
    {
        Status = MembershipStatus.Active;
        UpdatedAt = Guard.Utc(now);
    }
}
