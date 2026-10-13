using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Assets;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", t =>
        {
            t.HasEnumCheck<TenantStatus>("tenants", "status");
            t.HasCheckConstraint("ck_tenants_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND char_length(slug) BETWEEN 3 AND 63");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Tenant.NameMaxLength);
        builder.Property(x => x.Slug).HasMaxLength(Tenant.SlugMaxLength);
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_branding", t =>
            t.HasCheckConstraint("ck_tenant_branding_version_positive", "branding_version >= 1"));
        builder.HasKey(x => x.TenantId);
        builder.Property(x => x.ThemeJson).IsJsonb();
        builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantBranding>(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LogoAssetId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_branding_logo_asset");
    }
}
