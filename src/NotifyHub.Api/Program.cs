using Microsoft.AspNetCore.Authorization;
using NotifyHub.Api.Health;
using NotifyHub.Api.Infrastructure;
using NotifyHub.Infrastructure;
using NotifyHub.Infrastructure.Persistence;

if (args is ["healthcheck", ..])
{
    return await LocalHealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>("Api:MaxRequestBodyBytes", 1024 * 1024);
});

builder.Services.AddNotifyHubInfrastructure(builder.Configuration);
builder.Services.AddNotifyHubHealthChecks();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    // Behind the reverse proxy the request scheme/host seen by the API is internal; emit no server URLs so
    // clients resolve paths relative to wherever the document was fetched.
    document.Servers?.Clear();
    document.Info.Title = "NotifyHub API";
    return Task.CompletedTask;
}));

// Deny by default: every request requires an authenticated principal unless its endpoint explicitly opts
// out. Administrator (OIDC/cookie) and device-credential schemes are added in later phases; until then the
// fail-closed default scheme authenticates nobody.
builder.Services.AddAuthentication(UnauthenticatedHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, UnauthenticatedHandler>(UnauthenticatedHandler.SchemeName, null);
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

if (args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
{
    // Deployment step: apply migrations and exit without starting the web server.
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
    return 0;
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapNotifyHubHealthEndpoints();
if (app.Configuration.GetValue("Api:EnableOpenApi", true))
{
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
}

await app.RunAsync();
return 0;
