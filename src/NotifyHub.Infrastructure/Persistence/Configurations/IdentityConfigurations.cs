using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Domain.Devices;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Tenants;

namespace NotifyHub.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t => t.HasEnumCheck<UserStatus>("users", "status"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.IdentityProvider).HasMaxLength(User.ProviderMaxLength);
        builder.Property(x => x.ExternalSubject).HasMaxLength(User.SubjectMaxLength);
        builder.Property(x => x.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(x => x.DisplayName).HasMaxLength(User.DisplayNameMaxLength);

        // Identity is (provider, subject). Email is deliberately not unique and not an identity key.
        builder.HasIndex(x => new { x.IdentityProvider, x.ExternalSubject }).IsUnique();
        builder.HasIndex(x => x.Email);
    }
}

internal sealed class PlatformAdministratorConfiguration : IEntityTypeConfiguration<PlatformAdministrator>
{
    public void Configure(EntityTypeBuilder<PlatformAdministrator> builder)
    {
        builder.ToTable("platform_administrators");
        builder.HasKey(x => x.UserId);
        builder.HasOne<User>().WithOne().HasForeignKey<PlatformAdministrator>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", t =>
        {
            t.HasEnumCheck<TenantRole>("tenant_memberships", "role");
            t.HasEnumCheck<MembershipStatus>("tenant_memberships", "status");
        });

        // One membership per (tenant, user).
        builder.HasKey(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => x.UserId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        builder.ToTable("recipients", t =>
        {
            t.HasEnumCheck<RecipientIdentityKind>("recipients", "kind");
            t.HasEnumCheck<RecipientStatus>("recipients", "status");
            t.HasCheckConstraint("ck_recipients_local_device", "(kind = 'LocalAccount') = (local_device_id IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasTenantScopedKey("recipients");
        builder.Property(x => x.Authority).HasMaxLength(Recipient.AuthorityMaxLength);
        builder.Property(x => x.Subject).HasMaxLength(Recipient.SubjectMaxLength);
        builder.Property(x => x.DisplayName).HasMaxLength(Recipient.DisplayNameMaxLength);

        builder.HasIndex(x => new { x.TenantId, x.Kind, x.Authority, x.Subject }).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LocalDeviceId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recipients_local_device");
    }
}
