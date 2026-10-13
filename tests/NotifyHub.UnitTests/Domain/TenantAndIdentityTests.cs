using NotifyHub.Domain.Common;
using NotifyHub.Domain.Identity;
using NotifyHub.Domain.Tenants;
using static NotifyHub.UnitTests.Domain.TestData;

namespace NotifyHub.UnitTests.Domain;

public sealed class TenantAndIdentityTests
{
    [Theory]
    [InlineData("contoso")]
    [InlineData("contoso-ltd")]
    [InlineData("a1b")]
    public void Valid_slugs_are_accepted(string slug)
    {
        Assert.Equal(slug, Tenant.Create("Contoso", slug, T0).Slug);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Contoso")]
    [InlineData("-contoso")]
    [InlineData("contoso-")]
    [InlineData("con--toso")]
    [InlineData("con_toso")]
    [InlineData("../etc")]
    [InlineData("")]
    public void Invalid_slugs_are_rejected(string slug)
    {
        Assert.Equal("tenant.invalid_slug", Assert.Throws<DomainException>(() => Tenant.Create("Contoso", slug, T0)).Code);
    }

    [Fact]
    public void Deactivated_tenant_cannot_be_reactivated()
    {
        var tenant = Tenant.Create("Contoso", "contoso", T0);
        tenant.Deactivate(T0);

        Assert.Throws<DomainException>(() => tenant.Reactivate(T0));
        Assert.False(tenant.IsActive);
    }

    [Fact]
    public void Branding_version_increments_on_update()
    {
        var branding = TenantBranding.CreateDefault(TenantA, T0);

        branding.Update("""{"primaryColor":"#003366"}""", null, T0.AddMinutes(1));

        Assert.Equal(2, branding.BrandingVersion);
    }

    [Fact]
    public void User_identity_is_provider_and_subject_not_email()
    {
        var user = User.Create("https://login.example.com/", "sub-123", "admin@example.com", "Admin", T0);

        user.UpdateProfile("renamed@example.com", "Admin", T0);

        Assert.Equal(("https://login.example.com/", "sub-123"), (user.IdentityProvider, user.ExternalSubject));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a b@example.com")]
    public void Invalid_email_is_rejected(string email)
    {
        Assert.Throws<DomainException>(() => User.Create("local", "x", email, null, T0));
    }

    [Fact]
    public void Active_directory_recipient_is_anchored_to_sid()
    {
        var r = Recipient.ForActiveDirectory(TenantA, DomainSid, $"{DomainSid}-1105", "CONTOSO\\alice", T0);

        Assert.Equal(RecipientIdentityKind.ActiveDirectory, r.Kind);
        Assert.Equal(DomainSid, r.Authority);
        Assert.Equal($"{DomainSid}-1105", r.Subject);
        Assert.Null(r.LocalDeviceId);
    }

    [Fact]
    public void Sid_is_normalised_to_upper_case()
    {
        var r = Recipient.ForActiveDirectory(TenantA, DomainSid.ToLowerInvariant(), $"{DomainSid}-1105".ToLowerInvariant(), null, T0);

        Assert.StartsWith("S-1-5-21-", r.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void User_sid_must_belong_to_the_stated_domain()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Recipient.ForActiveDirectory(TenantA, DomainSid, "S-1-5-21-1-2-3-1105", null, T0));
        Assert.Equal("recipient.sid_domain_mismatch", ex.Code);
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("CONTOSO\\alice")]
    [InlineData("S-1-")]
    [InlineData("S-1-5-21-abc")]
    [InlineData("S-1-5-21-1-2-3-4'; DROP TABLE recipients;--")]
    public void Usernames_and_malformed_sids_are_not_identities(string sid)
    {
        Assert.Throws<DomainException>(() => Recipient.ForLocalAccount(TenantA, DeviceId, sid, null, T0));
    }

    [Fact]
    public void Local_account_recipient_is_scoped_to_its_device()
    {
        var r = Recipient.ForLocalAccount(TenantA, DeviceId, "S-1-5-21-111-222-333-1001", ".\\kiosk", T0);

        Assert.Equal(RecipientIdentityKind.LocalAccount, r.Kind);
        Assert.Equal(DeviceId, r.LocalDeviceId);
        Assert.Equal(DeviceId.ToString("D"), r.Authority);
    }

    [Fact]
    public void Entra_recipient_uses_tenant_and_object_ids()
    {
        var tid = Guid.NewGuid();
        var oid = Guid.NewGuid();

        var r = Recipient.ForEntraId(TenantA, tid, oid, "alice@contoso.com", T0);

        Assert.Equal((tid.ToString("D"), oid.ToString("D")), (r.Authority, r.Subject));
        Assert.Throws<DomainException>(() => Recipient.ForEntraId(TenantA, Guid.Empty, oid, null, T0));
    }

    [Fact]
    public void Membership_requires_tenant_and_user()
    {
        Assert.Throws<DomainException>(() => TenantMembership.Create(Guid.Empty, Admin, TenantRole.Publisher, T0));
        Assert.Throws<DomainException>(() => TenantMembership.Create(TenantA, Guid.Empty, TenantRole.Publisher, T0));
    }
}
