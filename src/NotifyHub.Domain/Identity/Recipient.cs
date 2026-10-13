using System.Text.RegularExpressions;
using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Identity;

/// <summary>How a Windows session user's identity is anchored. See ADR 0004.</summary>
public enum RecipientIdentityKind
{
    /// <summary>Authority = AD domain SID; subject = user SID within that domain.</summary>
    ActiveDirectory,

    /// <summary>Authority = Entra tenant ID (GUID); subject = user object ID (GUID).</summary>
    EntraId,

    /// <summary>Authority = the device the local account lives on; subject = local account SID. Device-scoped.</summary>
    LocalAccount,
}

public enum RecipientStatus
{
    Active,
    Disabled,
}

/// <summary>
/// A tenant-scoped notification recipient: a person who logs on to an enrolled Windows device. Per-user
/// dismissal and acknowledgement state is keyed to this record. A display name or username is never used
/// as identity; the stable key is (tenant, kind, authority, subject).
/// </summary>
public sealed partial class Recipient : ITenantOwned
{
    public const int AuthorityMaxLength = 200;
    public const int SubjectMaxLength = 200;
    public const int DisplayNameMaxLength = 256;

    private Recipient()
    {
        Authority = null!;
        Subject = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public RecipientIdentityKind Kind { get; private set; }

    public string Authority { get; private set; }

    public string Subject { get; private set; }

    /// <summary>Set only for <see cref="RecipientIdentityKind.LocalAccount"/>: local SIDs are meaningful only on one machine.</summary>
    public Guid? LocalDeviceId { get; private set; }

    /// <summary>Informational only (e.g. DOMAIN\user or UPN). Never used for identity or authorisation.</summary>
    public string? DisplayName { get; private set; }

    public RecipientStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Recipient ForActiveDirectory(Guid tenantId, string domainSid, string userSid, string? displayName, DateTimeOffset now)
    {
        var authority = ValidateSid(domainSid, "Domain SID");
        var subject = ValidateSid(userSid, "User SID");
        if (!subject.StartsWith(authority + "-", StringComparison.Ordinal)
            || !uint.TryParse(subject.AsSpan(authority.Length + 1), out _))
        {
            throw new DomainException("recipient.sid_domain_mismatch", "User SID does not belong to the specified domain.");
        }

        return Create(tenantId, RecipientIdentityKind.ActiveDirectory, authority, subject, null, displayName, now);
    }

    public static Recipient ForEntraId(Guid tenantId, Guid entraTenantId, Guid objectId, string? displayName, DateTimeOffset now)
    {
        Guard.NotEmpty(entraTenantId, "Entra tenant ID");
        Guard.NotEmpty(objectId, "Entra object ID");
        return Create(tenantId, RecipientIdentityKind.EntraId, entraTenantId.ToString("D"), objectId.ToString("D"), null, displayName, now);
    }

    public static Recipient ForLocalAccount(Guid tenantId, Guid deviceId, string accountSid, string? displayName, DateTimeOffset now)
    {
        Guard.NotEmpty(deviceId, "Device");
        var subject = ValidateSid(accountSid, "Account SID");
        return Create(tenantId, RecipientIdentityKind.LocalAccount, deviceId.ToString("D"), subject, deviceId, displayName, now);
    }

    public bool IsActive => Status == RecipientStatus.Active;

    public void UpdateDisplayName(string? displayName, DateTimeOffset now)
    {
        DisplayName = Guard.OptionalText(displayName, "Display name", DisplayNameMaxLength);
        UpdatedAt = Guard.Utc(now);
    }

    public void Disable(DateTimeOffset now)
    {
        Status = RecipientStatus.Disabled;
        UpdatedAt = Guard.Utc(now);
    }

    public static string ValidateSid(string? sid, string name)
    {
        var value = sid?.Trim().ToUpperInvariant() ?? string.Empty;
        if (value.Length > SubjectMaxLength || !SidPattern().IsMatch(value))
        {
            throw new DomainException("recipient.invalid_sid", $"{name} is not a valid Windows security identifier.");
        }

        return value;
    }

    private static Recipient Create(Guid tenantId, RecipientIdentityKind kind, string authority, string subject, Guid? localDeviceId, string? displayName, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        return new Recipient
        {
            Id = Ids.New(),
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            Kind = kind,
            Authority = authority,
            Subject = subject,
            LocalDeviceId = localDeviceId,
            DisplayName = Guard.OptionalText(displayName, "Display name", DisplayNameMaxLength),
            Status = RecipientStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    // S-1-<authority>-<sub-authority>... with 1 to 15 sub-authorities.
    [GeneratedRegex(@"^S-1-\d{1,13}(-\d{1,10}){1,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex SidPattern();
}
