namespace SafeTravel.Domain.Exceptions;

/// <summary>
/// Thrown when a date range is invalid (e.g., end before start, outside 7-day window).
/// </summary>
public sealed class InvalidDateRangeException : SafeTravelDomainException
{
    public InvalidDateRangeException(string message) : base(message) { }

    public static InvalidDateRangeException EndBeforeStart() =>
        new("End date must be greater than or equal to start date.");

    public static InvalidDateRangeException ExceedsMaximumDays(int maxDays) =>
        new($"Date range cannot exceed {maxDays} days.");

    public static InvalidDateRangeException DateInPast() =>
        new("Travel date cannot be in the past.");

    public static InvalidDateRangeException OutsideForecastWindow(int maxDays) =>
        new($"Travel date must be within the next {maxDays} days.");
}
