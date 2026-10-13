namespace NotifyHub.Worker;

internal static class LivenessProbe
{
    public static int Run(string? livenessFilePath, TimeSpan maxAge)
    {
        if (string.IsNullOrWhiteSpace(livenessFilePath) || !File.Exists(livenessFilePath))
        {
            return 1;
        }

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(livenessFilePath);
        return age <= maxAge ? 0 : 1;
    }
}
