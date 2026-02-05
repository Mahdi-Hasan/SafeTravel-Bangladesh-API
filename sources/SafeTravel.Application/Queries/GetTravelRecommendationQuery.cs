using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;

namespace SafeTravel.Application.Queries;

/// <summary>
/// Query to get a travel recommendation for a specific destination.
/// </summary>
public sealed record GetTravelRecommendationQuery(
    TravelRecommendationRequest Request) : IQuery<TravelRecommendationResponse>;

