using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Identity;

/// <summary>
/// Grants platform-wide administration (tenant provisioning, operational settings). Stored separately from
/// tenant memberships so that no tenant-level operation can create or elevate a platform administrator.
/// </summary>
public sealed class PlatformAdministrator
{
    private PlatformAdministrator()
    {
    }

    public Guid UserId { get; private set; }

    public Guid? GrantedBy { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public static PlatformAdministrator Grant(Guid userId, Guid? grantedBy, DateTimeOffset now) => new()
    {
        UserId = Guard.NotEmpty(userId, "User"),
        GrantedBy = grantedBy,
        GrantedAt = Guard.Utc(now),
    };
}
