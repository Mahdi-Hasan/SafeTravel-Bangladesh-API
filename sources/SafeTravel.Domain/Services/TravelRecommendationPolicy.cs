using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Services;

/// <summary>
/// Domain service that evaluates whether travel to a destination is recommended.
/// A destination is recommended only if it is BOTH cooler AND has cleaner air.
/// </summary>
/// <example>
/// Example outcomes based on different conditions:
/// 
/// Case 1 - Recommended (cooler + cleaner):
///   Origin: 32°C, PM2.5: 80 μg/m³
///   Destination: 25°C, PM2.5: 40 μg/m³
///   Result: "Sylhet is 7.0°C cooler and has 50% better air quality than your location."
/// 
/// Case 2 - Not recommended (warmer):
///   Origin: 28°C, PM2.5: 50 μg/m³
///   Destination: 35°C, PM2.5: 30 μg/m³
///   Result: "Rajshahi is 7.0°C warmer compared to your location."
/// 
/// Case 3 - Not recommended (worse air quality):
///   Origin: 35°C, PM2.5: 40 μg/m³
///   Destination: 28°C, PM2.5: 90 μg/m³
///   Result: "Dhaka is 50.0 μg/m³ higher PM2.5 compared to your location."
/// 
/// Case 4 - Not recommended (both warmer and worse air):
///   Origin: 28°C, PM2.5: 40 μg/m³
///   Destination: 33°C, PM2.5: 85 μg/m³
///   Result: "Chittagong is 5.0°C warmer and has 45.0 μg/m³ higher PM2.5 compared to your location."
/// 
/// Case 5 - Not recommended (same temperature):
///   Origin: 30°C, PM2.5: 50 μg/m³
///   Destination: 30°C, PM2.5: 70 μg/m³
///   Result: "Khulna is same temperature and has 20.0 μg/m³ higher PM2.5 compared to your location."
/// 
/// Case 6 - Not recommended (similar air quality):
///   Origin: 32°C, PM2.5: 60 μg/m³
///   Destination: 35°C, PM2.5: 60 μg/m³
///   Result: "Barisal is 3.0°C warmer and has similar air quality compared to your location."
/// </example>
/// 
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

    /// <summary>
    /// Calculates the percentage improvement in PM2.5 air quality from origin to destination.
    /// </summary>
    /// <remarks>
    /// Uses the formula: ((origin - destination) / origin) * 100
    /// A positive result indicates improvement (destination has lower PM2.5).
    /// Returns 0 if origin has no PM2.5 to avoid division by zero.
    /// </remarks>
    private static double CalculatePM25Improvement(PM25Level origin, PM25Level destination)
    {
        // Guard against division by zero when origin has no PM2.5
        if (origin.Value == 0) return 0;

        // Calculate percentage change: positive = improvement, negative = worse
        return ((origin.Value - destination.Value) / origin.Value) * 100;
    }

    /// <summary>
    /// Builds the reason message when a destination is not recommended.
    /// </summary>
    /// <remarks>
    /// The message structure varies based on which conditions fail:
    /// - Not cooler only: "{name} is {diff}°C warmer compared to your location."
    /// - Not cleaner only: "{name} is {diff} μg/m³ higher PM2.5 compared to your location."
    /// - Both fail: "{name} is {tempDiff}°C warmer and has {pm25Diff} μg/m³ higher PM2.5 compared to your location."
    /// - Same/similar values: Uses "same temperature" or "similar air quality" instead of numeric differences.
    /// </remarks>
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
