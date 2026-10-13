using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Tenants;

/// <summary>
/// Per-tenant branding. <see cref="ThemeJson"/> holds the data-driven theme document (validated by the
/// application layer against a strict schema in Phase 4); it is never interpreted as markup or code.
/// <see cref="BrandingVersion"/> increments on every change so agents can invalidate cached branding.
/// </summary>
public sealed class TenantBranding : ITenantOwned
{
    public const int ThemeJsonMaxLength = 16 * 1024;

    private TenantBranding()
    {
        ThemeJson = null!;
    }

    public Guid TenantId { get; private set; }

    public Guid? LogoAssetId { get; private set; }

    public string ThemeJson { get; private set; }

    public long BrandingVersion { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TenantBranding CreateDefault(Guid tenantId, DateTimeOffset now) => new()
    {
        TenantId = Guard.NotEmpty(tenantId, "Tenant"),
        ThemeJson = "{}",
        BrandingVersion = 1,
        UpdatedAt = Guard.Utc(now),
    };

    public void Update(string themeJson, Guid? logoAssetId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(themeJson) || themeJson.Length > ThemeJsonMaxLength)
        {
            throw new DomainException("branding.invalid_theme", $"Theme must be a non-empty JSON document of at most {ThemeJsonMaxLength} characters.");
        }

        ThemeJson = themeJson;
        LogoAssetId = logoAssetId;
        BrandingVersion++;
        UpdatedAt = Guard.Utc(now);
    }
}
