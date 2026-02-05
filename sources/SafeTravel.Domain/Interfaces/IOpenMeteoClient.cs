using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Interfaces;

/// <summary>
/// Response model for bulk weather forecast data.
/// </summary>
public sealed record BulkWeatherResponse(
    IReadOnlyDictionary<Coordinates, IReadOnlyList<HourlyWeatherData>> Data);

/// <summary>
/// Hourly weather data from Open-Meteo API.
/// </summary>
public sealed record HourlyWeatherData(
    DateTime Time,
    double TemperatureCelsius);

/// <summary>
/// Response model for bulk air quality data.
/// </summary>
public sealed record BulkAirQualityResponse(
    IReadOnlyDictionary<Coordinates, IReadOnlyList<HourlyAirQualityData>> Data);

/// <summary>
/// Hourly air quality data from Open-Meteo API.
/// </summary>
public sealed record HourlyAirQualityData(
    DateTime Time,
    double PM25);

/// <summary>
/// Client interface for Open-Meteo API operations.
/// </summary>
public interface IOpenMeteoClient
{
    /// <summary>
    /// Gets bulk weather forecast for multiple locations.
    /// </summary>
    /// <param name="locations">Geographic coordinates to fetch.</param>
    /// <param name="days">Number of forecast days (1-7).</param>
    /// <returns>Weather data grouped by location.</returns>
    Task<BulkWeatherResponse> GetBulkForecastAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bulk air quality data for multiple locations.
    /// </summary>
    /// <param name="locations">Geographic coordinates to fetch.</param>
    /// <param name="days">Number of forecast days (1-7).</param>
    /// <returns>Air quality data grouped by location.</returns>
    Task<BulkAirQualityResponse> GetBulkAirQualityAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default);
}
