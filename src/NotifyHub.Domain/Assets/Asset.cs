using System.Text.RegularExpressions;
using NotifyHub.Domain.Common;

namespace NotifyHub.Domain.Assets;

/// <summary>
/// An uploaded branding asset. The storage key is generated from server-side identifiers only, so no
/// client-supplied file name or path ever reaches the storage layer. File content is verified against
/// <see cref="MimeType"/> (magic-byte sniffing) by the infrastructure layer before an asset is created.
/// </summary>
public sealed partial class Asset : ITenantOwned
{
    public const long MaxSizeBytes = 2 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedMimeTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    private Asset()
    {
        StorageKey = null!;
        MimeType = null!;
        Sha256 = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string StorageKey { get; private set; }

    public string MimeType { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>Lowercase hexadecimal SHA-256 of the stored content.</summary>
    public string Sha256 { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Asset Create(Guid tenantId, string mimeType, long sizeBytes, string sha256, Guid? createdBy, DateTimeOffset now)
    {
        Guard.NotEmpty(tenantId, "Tenant");
        if (!AllowedMimeTypes.Contains(mimeType))
        {
            throw new DomainException("asset.unsupported_type", "Only PNG, JPEG and WebP images are accepted.");
        }

        if (sizeBytes is <= 0 or > MaxSizeBytes)
        {
            throw new DomainException("asset.invalid_size", $"Assets must be between 1 byte and {MaxSizeBytes} bytes.");
        }

        if (sha256 is null || !Sha256Pattern().IsMatch(sha256))
        {
            throw new DomainException("asset.invalid_hash", "SHA-256 must be 64 lowercase hexadecimal characters.");
        }

        var id = Ids.New();
        return new Asset
        {
            Id = id,
            TenantId = tenantId,
            StorageKey = $"{tenantId:N}/{id:N}",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            CreatedBy = createdBy,
            CreatedAt = Guard.Utc(now),
        };
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
