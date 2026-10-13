namespace NotifyHub.Domain.Common;

/// <summary>
/// Raised when an operation would violate a business invariant. <see cref="Code"/> is a stable,
/// machine-readable identifier that the API maps to a problem-details response.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
