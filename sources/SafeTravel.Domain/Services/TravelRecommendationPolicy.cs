using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Services;

/// <summary>
/// Domain service that evaluates whether travel to a destination is recommended.
/// A destination is recommended only if it is BOTH cooler AND has cleaner air.
/// </summary>
public sealed class TravelRecommendationPolicy
{
    /// <summary>
    /// Evaluates whether travel from origin to destination is recommended.
    /// </summary>
    /// <param name="originWeather">Weather conditions at origin location.</param>
    /// <param name="destinationWeather">Weather conditions at destination.</param>
    /// <param name="destinationName">Name of the destination for message generation.</param>
    /// <returns>A recommendation result with decision and explanation.</returns>
    public RecommendationResult Evaluate(
        WeatherSnapshot originWeather,
        WeatherSnapshot destinationWeather,
        string destinationName)
    {
        ArgumentNullException.ThrowIfNull(originWeather);
        ArgumentNullException.ThrowIfNull(destinationWeather);
        ArgumentNullException.ThrowIfNull(destinationName);

        var isCooler = destinationWeather.Temperature.IsCoolerThan(originWeather.Temperature);
        var isCleaner = destinationWeather.PM25.IsCleanerThan(originWeather.PM25);

        if (isCooler && isCleaner)
        {
            var tempDiff = originWeather.Temperature.Celsius - destinationWeather.Temperature.Celsius;
            var pm25Improvement = CalculatePM25Improvement(originWeather.PM25, destinationWeather.PM25);

            var reason = $"{destinationName} is {tempDiff:F1}°C cooler and has " +
                        $"{pm25Improvement:F0}% better air quality than your location.";

            return RecommendationResult.Recommended(reason);
        }

        return RecommendationResult.NotRecommended(BuildNegativeReason(
            originWeather, destinationWeather, destinationName, isCooler, isCleaner));
    }

    private static double CalculatePM25Improvement(PM25Level origin, PM25Level destination)
    {
        if (origin.Value == 0) return 0;
        return ((origin.Value - destination.Value) / origin.Value) * 100;
    }

    private static string BuildNegativeReason(
        WeatherSnapshot origin,
        WeatherSnapshot destination,
        string destinationName,
        bool isCooler,
        bool isCleaner)
    {
        var issues = new List<string>();

        if (!isCooler)
        {
            var tempDiff = destination.Temperature.Celsius - origin.Temperature.Celsius;
            if (tempDiff > 0)
            {
                issues.Add($"{tempDiff:F1}°C warmer");
            }
            else
            {
                issues.Add("same temperature");
            }
        }

        if (!isCleaner)
        {
            var pm25Diff = destination.PM25.Value - origin.PM25.Value;
            if (pm25Diff > 0)
            {
                issues.Add($"{pm25Diff:F1} μg/m³ higher PM2.5");
            }
            else
            {
                issues.Add("similar air quality");
            }
        }

        return $"{destinationName} is {string.Join(" and has ", issues)} compared to your location.";
    }
}
