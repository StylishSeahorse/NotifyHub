using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Identity;

public enum UserStatus
{
    Active,
    Disabled,
}

/// <summary>
/// A portal principal (administrator, publisher or auditor). Identity is the pair
/// (<see cref="IdentityProvider"/>, <see cref="ExternalSubject"/>), never the email address, which can change
/// or be reassigned. Users are global; access to a tenant is granted only through <see cref="TenantMembership"/>.
/// Windows session users who receive notifications are modelled separately as <see cref="Recipient"/>.
/// </summary>
public sealed class User
{
    public const int ProviderMaxLength = 200;
    public const int SubjectMaxLength = 256;
    public const int EmailMaxLength = 320;
    public const int DisplayNameMaxLength = 200;

    private User()
    {
        IdentityProvider = null!;
        ExternalSubject = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Issuer identifier, e.g. an OpenID Connect issuer URL, or "local" for local accounts.</summary>
    public string IdentityProvider { get; private set; }

    /// <summary>The provider's stable, non-reassignable subject identifier (OIDC "sub").</summary>
    public string ExternalSubject { get; private set; }

    public string? Email { get; private set; }

    public string? DisplayName { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(string identityProvider, string externalSubject, string? email, string? displayName, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        var user = new User
        {
            Id = Ids.New(),
            IdentityProvider = Guard.Text(identityProvider, "Identity provider", ProviderMaxLength),
            ExternalSubject = Guard.Text(externalSubject, "External subject", SubjectMaxLength),
            Status = UserStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        user.UpdateProfile(email, displayName, now);
        return user;
    }

    public bool IsActive => Status == UserStatus.Active;

    public void UpdateProfile(string? email, string? displayName, DateTimeOffset now)
    {
        var normalisedEmail = Guard.OptionalText(email, "Email", EmailMaxLength);
        if (normalisedEmail is not null && (!normalisedEmail.Contains('@', StringComparison.Ordinal) || normalisedEmail.Any(char.IsWhiteSpace)))
        {
            throw new DomainException("user.invalid_email", "Email address is not valid.");
        }

        Email = normalisedEmail;
        DisplayName = Guard.OptionalText(displayName, "Display name", DisplayNameMaxLength);
        UpdatedAt = Guard.Utc(now);
    }

    public void Disable(DateTimeOffset now)
    {
        Status = UserStatus.Disabled;
        UpdatedAt = Guard.Utc(now);
    }

    public void Enable(DateTimeOffset now)
    {
        Status = UserStatus.Active;
        UpdatedAt = Guard.Utc(now);
    }
}
