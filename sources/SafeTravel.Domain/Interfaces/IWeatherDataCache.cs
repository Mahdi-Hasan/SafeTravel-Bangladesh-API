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
    CachedRankings? GetRankings();
    void SetRankings(CachedRankings rankings);
    CachedDistrictForecast? GetDistrictForecast(string districtId);
    void SetDistrictForecast(string districtId, CachedDistrictForecast forecast);
    CacheMetadata? GetMetadata();
}
