using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Services;

/// <summary>
/// Domain service for ranking districts by weather conditions.
/// </summary>
public sealed class DistrictRankingService
{
    /// <summary>
    /// Computes rankings for districts based on average temperature and PM2.5.
    /// Districts are ranked by coolest first, with PM2.5 as tie-breaker.
    /// </summary>
    /// <param name="districtWeatherData">Dictionary of district to average weather snapshot.</param>
    /// <returns>List of ranked districts ordered from rank 1 (best) to N.</returns>
    public IReadOnlyList<RankedDistrict> ComputeRankings(
        IReadOnlyDictionary<District, WeatherSnapshot> districtWeatherData)
    {
        ArgumentNullException.ThrowIfNull(districtWeatherData);

        if (districtWeatherData.Count == 0)
        {
            return [];
        }

        var generatedAt = DateTime.UtcNow;

        // Sort by temperature ascending (coolest first),
        // then by PM2.5 ascending (cleanest first) as tie-breaker
        var ranked = districtWeatherData
            .OrderBy(kvp => kvp.Value.Temperature.Celsius)
            .ThenBy(kvp => kvp.Value.PM25.Value)
            .Select((kvp, index) => new RankedDistrict(
                rank: index + 1,
                district: kvp.Key,
                avgTemperature: kvp.Value.Temperature,
                avgPM25: kvp.Value.PM25,
                generatedAt: generatedAt))
            .ToList();

        return ranked;
    }

    /// <summary>
    /// Gets the top N ranked districts.
    /// </summary>
    /// <param name="districtWeatherData">Dictionary of district to average weather snapshot.</param>
    /// <param name="count">Number of top districts to return (default 10).</param>
    /// <returns>Top N ranked districts.</returns>
    public IReadOnlyList<RankedDistrict> GetTopDistricts(
        IReadOnlyDictionary<District, WeatherSnapshot> districtWeatherData,
        int count = 10)
    {
        var allRankings = ComputeRankings(districtWeatherData);
        return [.. allRankings.Take(count)];
    }
}
