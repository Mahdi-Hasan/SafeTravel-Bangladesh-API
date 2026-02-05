namespace SafeTravel.Application.DTOs;

/// <summary>
/// Request for travel recommendation endpoint.
/// </summary>
public sealed record TravelRecommendationRequest(
    double Latitude,
    double Longitude,
    string DestinationDistrict,
    DateOnly TravelDate);
