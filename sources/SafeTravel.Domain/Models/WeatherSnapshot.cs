using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Models;

/// <summary>
/// Represents weather conditions at a specific time.
/// Used for both current conditions and forecasts.
/// </summary>
public sealed record WeatherSnapshot
{
    public DateOnly Date { get; }
    public Temperature Temperature { get; }
    public PM25Level PM25 { get; }
    public DateTime RecordedAt { get; }

    public WeatherSnapshot(DateOnly date, Temperature temperature, PM25Level pm25, DateTime recordedAt)
    {
        Date = date;
        Temperature = temperature ?? throw new ArgumentNullException(nameof(temperature));
        PM25 = pm25 ?? throw new ArgumentNullException(nameof(pm25));
        RecordedAt = recordedAt;
    }

    /// <summary>
    /// Creates a WeatherSnapshot from raw values.
    /// </summary>
    public static WeatherSnapshot Create(DateOnly date, double temperatureCelsius, double pm25Value)
    {
        return new WeatherSnapshot(
            date,
            Temperature.FromCelsius(temperatureCelsius),
            PM25Level.Create(pm25Value),
            DateTime.UtcNow);
    }

    public override string ToString() => $"{Date}: {Temperature}, {PM25}";
}
