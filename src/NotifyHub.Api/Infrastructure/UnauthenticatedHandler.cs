using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NotifyHub.Api.Infrastructure;

/// <summary>
/// Fail-closed default authentication scheme. It never authenticates anyone; challenges produce 401 and
/// forbids produce 403. It exists so that the deny-by-default authorisation policy responds correctly
/// before the administrator (OIDC/cookie) and device-credential schemes are introduced, and it remains the
/// default for requests that present no recognised credentials.
/// </summary>
internal sealed class UnauthenticatedHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Unauthenticated";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
