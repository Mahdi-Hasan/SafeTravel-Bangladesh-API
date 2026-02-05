using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Interfaces;

public sealed record BulkWeatherResponse(
    IReadOnlyDictionary<Coordinates, IReadOnlyList<HourlyWeatherData>> Data);

public sealed record HourlyWeatherData(DateTime Time, double TemperatureCelsius);

public sealed record BulkAirQualityResponse(
    IReadOnlyDictionary<Coordinates, IReadOnlyList<HourlyAirQualityData>> Data);

public sealed record HourlyAirQualityData(DateTime Time, double PM25);

public interface IOpenMeteoClient
{
    Task<BulkWeatherResponse> GetBulkForecastAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default);

    Task<BulkAirQualityResponse> GetBulkAirQualityAsync(
        IEnumerable<Coordinates> locations,
        int days,
        CancellationToken cancellationToken = default);
}
