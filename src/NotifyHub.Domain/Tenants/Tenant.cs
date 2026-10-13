using System.Text.RegularExpressions;
using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Tenants;

public enum TenantStatus
{
    Active,
    Suspended,
    Deactivated,
}

/// <summary>An organisation or customer. The root of the tenant isolation boundary.</summary>
public sealed partial class Tenant
{
    public const int NameMaxLength = 200;
    public const int SlugMaxLength = 63;

    private Tenant()
    {
        Name = null!;
        Slug = null!;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>URL-safe unique identifier: lowercase letters, digits and single hyphens, 3–63 characters.</summary>
    public string Slug { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tenant Create(string name, string slug, DateTimeOffset now)
    {
        var utcNow = Guard.Utc(now);
        return new Tenant
        {
            Id = Ids.New(),
            Name = Guard.Text(name, "Tenant name", NameMaxLength),
            Slug = ValidateSlug(slug),
            Status = TenantStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public bool IsActive => Status == TenantStatus.Active;

    public void Rename(string name, DateTimeOffset now)
    {
        Name = Guard.Text(name, "Tenant name", NameMaxLength);
        UpdatedAt = Guard.Utc(now);
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status == TenantStatus.Deactivated)
        {
            throw new DomainException("tenant.deactivated", "A deactivated tenant cannot be suspended.");
        }

        Status = TenantStatus.Suspended;
        UpdatedAt = Guard.Utc(now);
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status == TenantStatus.Deactivated)
        {
            throw new DomainException("tenant.deactivated", "A deactivated tenant cannot be reactivated.");
        }

        Status = TenantStatus.Active;
        UpdatedAt = Guard.Utc(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        Status = TenantStatus.Deactivated;
        UpdatedAt = Guard.Utc(now);
    }

    public static string ValidateSlug(string? slug)
    {
        var value = slug?.Trim() ?? string.Empty;
        if (value.Length is < 3 or > SlugMaxLength || !SlugPattern().IsMatch(value))
        {
            throw new DomainException(
                "tenant.invalid_slug",
                "Slug must be 3–63 characters of lowercase letters, digits and single hyphens, and must start and end with a letter or digit.");
        }

        return value;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
