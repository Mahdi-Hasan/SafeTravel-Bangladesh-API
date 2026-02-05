namespace SafeTravel.Application.DTOs;

/// <summary>
/// Response containing the top 10 districts for travel.
/// </summary>
public sealed record Top10DistrictsResponse(
    IReadOnlyList<RankedDistrictDto> Districts,
    ResponseMetadata Metadata);
