using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Services;

/// <summary>
/// Domain service for aggregating hourly weather data into daily/weekly averages.
/// Focuses on 14:00 (2 PM) values as specified in requirements.
/// </summary>
public sealed class WeatherAggregator
{
    private const int TargetHour = 14; // 2 PM

    /// <summary>
    /// Aggregates hourly weather and air quality data into a 7-day average snapshot.
    /// </summary>
    /// <param name="hourlyWeather">Hourly temperature data.</param>
    /// <param name="hourlyAirQuality">Hourly PM2.5 data.</param>
    /// <param name="days">Number of days to aggregate (default 7).</param>
    /// <returns>Average weather snapshot for the period.</returns>
    public WeatherSnapshot AggregateToAverage(
        IReadOnlyList<HourlyWeatherData> hourlyWeather,
        IReadOnlyList<HourlyAirQualityData> hourlyAirQuality,
        int days = 7)
    {
        ArgumentNullException.ThrowIfNull(hourlyWeather);
        ArgumentNullException.ThrowIfNull(hourlyAirQuality);

        var temps2PM = ExtractValuesAtHour(hourlyWeather, TargetHour);
        var pm252PM = ExtractValuesAtHour(hourlyAirQuality, TargetHour);

        if (temps2PM.Count == 0 || pm252PM.Count == 0)
        {
            throw InsufficientDataException.NotEnoughDataPoints(1, 0);
        }

        var avgTemp = temps2PM.Average();
        var avgPM25 = pm252PM.Average();

        return WeatherSnapshot.Create(
            DateOnly.FromDateTime(DateTime.UtcNow),
            avgTemp,
            avgPM25);
    }

    /// <summary>
    /// Gets weather snapshot for a specific date from hourly data.
    /// </summary>
    /// <param name="hourlyWeather">Hourly temperature data.</param>
    /// <param name="hourlyAirQuality">Hourly PM2.5 data.</param>
    /// <param name="targetDate">The date to get weather for.</param>
    /// <returns>Weather snapshot at 2 PM for the specified date.</returns>
    public WeatherSnapshot GetSnapshotForDate(
        IReadOnlyList<HourlyWeatherData> hourlyWeather,
        IReadOnlyList<HourlyAirQualityData> hourlyAirQuality,
        DateOnly targetDate)
    {
        ArgumentNullException.ThrowIfNull(hourlyWeather);
        ArgumentNullException.ThrowIfNull(hourlyAirQuality);

        var weatherAt2PM = hourlyWeather
            .FirstOrDefault(h =>
                DateOnly.FromDateTime(h.Time) == targetDate &&
                h.Time.Hour == TargetHour);

        var airQualityAt2PM = hourlyAirQuality
            .FirstOrDefault(h =>
                DateOnly.FromDateTime(h.Time) == targetDate &&
                h.Time.Hour == TargetHour);

        if (weatherAt2PM is null || airQualityAt2PM is null)
        {
            throw InsufficientDataException.NotEnoughDataPoints(1, 0);
        }

        return WeatherSnapshot.Create(targetDate, weatherAt2PM.TemperatureCelsius, airQualityAt2PM.PM25);
    }

    /// <summary>
    /// Extracts daily temperature values at the target hour.
    /// </summary>
    public IReadOnlyList<double> ExtractValuesAtHour(
        IReadOnlyList<HourlyWeatherData> hourlyData,
        int hour)
    {
        return hourlyData
            .Where(h => h.Time.Hour == hour)
            .Select(h => h.TemperatureCelsius)
            .ToList();
    }

    /// <summary>
    /// Extracts daily PM2.5 values at the target hour.
    /// </summary>
    public IReadOnlyList<double> ExtractValuesAtHour(
        IReadOnlyList<HourlyAirQualityData> hourlyData,
        int hour)
    {
        return hourlyData
            .Where(h => h.Time.Hour == hour)
            .Select(h => h.PM25)
            .ToList();
    }
}
