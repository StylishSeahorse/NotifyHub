namespace NotifyHub.Contracts.V1;

/// <summary>Health endpoint payload. Contains statuses only — never configuration, exception text or secrets.</summary>
public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, HealthEntryResponse> Checks);

public sealed record HealthEntryResponse(string Status, double DurationMs);
