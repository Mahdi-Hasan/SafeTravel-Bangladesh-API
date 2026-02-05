namespace SafeTravel.Application.DTOs;

/// <summary>
/// Metadata about the API response for transparency.
/// </summary>
public sealed record ResponseMetadata(
    DateTime GeneratedAt,
    string ForecastPeriod,
    bool IsStale);
