namespace SafeTravel.Domain.Exceptions;

/// <summary>
/// Thrown when weather data cannot be retrieved from external sources.
/// </summary>
public sealed class WeatherDataUnavailableException : SafeTravelDomainException
{
    public WeatherDataUnavailableException(string message) : base(message) { }

    public WeatherDataUnavailableException(string message, Exception innerException) 
        : base(message, innerException) { }

    public static WeatherDataUnavailableException CacheEmpty() =>
        new("Weather data cache is empty and external API is unavailable.");

    public static WeatherDataUnavailableException ExternalApiFailure(Exception innerException) =>
        new("Failed to retrieve weather data from external API.", innerException);
}
