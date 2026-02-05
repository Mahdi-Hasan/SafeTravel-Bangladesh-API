namespace SafeTravel.Application.DTOs;

/// <summary>
/// Weather comparison data between origin and destination.
/// </summary>
public sealed record WeatherComparisonDto(
    double OriginTemperature,
    double OriginPM25,
    string OriginAirQuality,
    double DestinationTemperature,
    double DestinationPM25,
    string DestinationAirQuality);
