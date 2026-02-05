using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;

namespace SafeTravel.Application.Queries;

/// <summary>
/// Query to retrieve the top 10 districts for travel based on weather conditions.
/// </summary>
public sealed record GetTop10DistrictsQuery : IQuery<Top10DistrictsResponse>;

