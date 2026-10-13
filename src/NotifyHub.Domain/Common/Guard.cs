namespace NotifyHub.Domain.Common;

internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("validation.required", $"{name} is required.");
        }

        return value;
    }

    public static string Text(string? value, string name, int maxLength, int minLength = 1)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < minLength)
        {
            throw new DomainException("validation.required", $"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException("validation.too_long", $"{name} must be at most {maxLength} characters.");
        }

        if (trimmed.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
        {
            throw new DomainException("validation.invalid_characters", $"{name} contains control characters.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, string name, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Text(value, name, maxLength);
    }

    /// <summary>Normalises a timestamp to UTC (offset zero). PostgreSQL timestamptz columns require UTC values.</summary>
    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();

    public static DateTimeOffset? Utc(DateTimeOffset? value) => value?.ToUniversalTime();
}
