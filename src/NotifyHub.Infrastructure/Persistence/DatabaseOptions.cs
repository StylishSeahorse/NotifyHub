namespace NotifyHub.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "NotifyHub";

    /// <summary>Command timeout in seconds for ordinary queries.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
