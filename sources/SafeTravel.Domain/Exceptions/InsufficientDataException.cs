namespace SafeTravel.Domain.Exceptions;

/// <summary>
/// Thrown when there is not enough data to perform a calculation.
/// </summary>
public sealed class InsufficientDataException : SafeTravelDomainException
{
    public InsufficientDataException(string message) : base(message) { }

    public static InsufficientDataException NoWeatherData(string districtName) =>
        new($"No weather data available for district '{districtName}'.");

    public static InsufficientDataException NotEnoughDataPoints(int required, int actual) =>
        new($"At least {required} data points required, but only {actual} available.");
}
