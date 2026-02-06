using SafeTravel.Domain.Models;

namespace SafeTravel.Domain.Interfaces;

public sealed record CachedRankings(
    IReadOnlyList<RankedDistrict> Rankings,
    DateTime GeneratedAt,
    DateTime ExpiresAt);

public sealed record CachedDistrictForecast(
    string DistrictId,
    IReadOnlyList<WeatherSnapshot> Forecasts,
    DateTime GeneratedAt);

public sealed record CacheMetadata(
    DateTime LastUpdated,
    bool IsHealthy,
    int DistrictsCached);

public interface IWeatherDataCache
{
    Task<CachedRankings?> GetRankingsAsync(CancellationToken cancellationToken = default);
    Task SetRankingsAsync(CachedRankings rankings, CancellationToken cancellationToken = default);
    Task<CachedDistrictForecast?> GetDistrictForecastAsync(string districtId, CancellationToken cancellationToken = default);
    Task SetDistrictForecastAsync(string districtId, CachedDistrictForecast forecast, CancellationToken cancellationToken = default);
    Task<CacheMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default);
}
