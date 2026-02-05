namespace SafeTravel.Domain.Exceptions;

/// <summary>
/// Base exception for all SafeTravel domain-specific errors.
/// </summary>
public abstract class SafeTravelDomainException : Exception
{
    protected SafeTravelDomainException(string message) : base(message) { }

    protected SafeTravelDomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
