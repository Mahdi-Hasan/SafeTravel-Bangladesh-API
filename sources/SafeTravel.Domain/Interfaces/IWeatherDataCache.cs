using SafeTravel.Domain.Models;

namespace SafeTravel.Domain.Interfaces;

/// <summary>
/// Cache data models for rankings.
/// </summary>
public sealed record CachedRankings(
    IReadOnlyList<RankedDistrict> Rankings,
    DateTime GeneratedAt,
    DateTime ExpiresAt);

/// <summary>
/// Cache data models for district forecasts.
/// </summary>
public sealed record CachedDistrictForecast(
    string DistrictId,
    IReadOnlyList<WeatherSnapshot> Forecasts,
    DateTime GeneratedAt);

/// <summary>
/// Metadata about the cache state.
/// </summary>
public sealed record CacheMetadata(
    DateTime LastUpdated,
    bool IsHealthy,
    int DistrictsCached);

/// <summary>
/// Interface for weather data cache operations.
/// </summary>
public interface IWeatherDataCache
{
    /// <summary>
    /// Gets the cached district rankings.
    /// </summary>
    CachedRankings? GetRankings();

    /// <summary>
    /// Sets the district rankings in cache.
    /// </summary>
    void SetRankings(CachedRankings rankings);

    /// <summary>
    /// Gets the cached forecast for a specific district.
    /// </summary>
    CachedDistrictForecast? GetDistrictForecast(string districtId);

    /// <summary>
    /// Sets the forecast for a specific district.
    /// </summary>
    void SetDistrictForecast(string districtId, CachedDistrictForecast forecast);

    /// <summary>
    /// Gets metadata about the current cache state.
    /// </summary>
    CacheMetadata? GetMetadata();
}
