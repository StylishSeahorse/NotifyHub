using NotifyHub.Infrastructure;
using NotifyHub.Worker;

// "healthcheck": container health probe; succeeds if the liveness file was written recently.
if (args is ["healthcheck", ..])
{
    return LivenessProbe.Run(Environment.GetEnvironmentVariable("Worker__Heartbeat__LivenessFilePath"), TimeSpan.FromMinutes(2));
}

// Background worker host. Phase 1 provides liveness reporting; scheduled publication, expiry, reminders,
// retries and retention jobs are added in Phase 2 as additional hosted services in this process.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNotifyHubInfrastructure(builder.Configuration);
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection("Worker:Heartbeat"));
builder.Services.AddHostedService<HeartbeatService>();

using var host = builder.Build();
await host.RunAsync();
return 0;
