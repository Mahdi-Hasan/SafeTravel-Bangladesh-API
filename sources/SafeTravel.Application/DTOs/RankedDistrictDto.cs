namespace SafeTravel.Application.DTOs;

/// <summary>
/// Represents a ranked district for API response.
/// </summary>
public sealed record RankedDistrictDto(
    int Rank,
    string DistrictName,
    double AvgTemperature,
    double AvgPM25,
    string AirQualityCategory);
