using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Services;

/// <summary>
/// Service for retrieving weather data with cache-aside pattern.
/// </summary>
public interface IWeatherDataService
{
    /// <summary>
    /// Gets the cached rankings, triggering manual data load if stale or missing.
    /// </summary>
    Task<CachedRankings> GetRankingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets weather data for specific coordinates on a given date.
    /// </summary>
    Task<WeatherSnapshot> GetWeatherForCoordinatesAsync(
        Coordinates location,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets weather data for a district on a given date.
    /// </summary>
    Task<WeatherSnapshot> GetWeatherForDistrictAsync(
        District district,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
