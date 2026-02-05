namespace SafeTravel.Application.DTOs;

/// <summary>
/// Response for travel recommendation endpoint.
/// </summary>
public sealed record TravelRecommendationResponse(
    bool IsRecommended,
    string Reason,
    string DestinationDistrict,
    WeatherComparisonDto Comparison,
    DateOnly TravelDate);
