using LiteBus.Queries.Abstractions;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Queries;
using SafeTravel.Application.Services;
using SafeTravel.Domain;

namespace SafeTravel.Application.Handlers;

/// <summary>
/// Handles the GetTop10DistrictsQuery using cache-first logic.
/// </summary>
public sealed class GetTop10DistrictsHandler : IQueryHandler<GetTop10DistrictsQuery, Top10DistrictsResponse>
{

    private readonly IWeatherDataService _weatherDataService;

    public GetTop10DistrictsHandler(IWeatherDataService weatherDataService)
    {
        _weatherDataService = weatherDataService ?? throw new ArgumentNullException(nameof(weatherDataService));
    }

    public async Task<Top10DistrictsResponse> HandleAsync(
        GetTop10DistrictsQuery query,
        CancellationToken cancellationToken = default)
    {
        var rankings = await _weatherDataService.GetRankingsAsync(cancellationToken);

        var top10 = rankings.Rankings
            .Take(SafeTravelConstants.TopDistrictsCount)
            .Select(r => new RankedDistrictDto(
                Rank: r.Rank,
                DistrictName: r.District.Name,
                AvgTemperature: Math.Round(r.AvgTemperature.Celsius, 1),
                AvgPM25: Math.Round(r.AvgPM25.Value, 1),
                AirQualityCategory: r.AvgPM25.GetAirQualityCategory().ToString()))
            .ToList();

        var isStale = DateTime.UtcNow - rankings.GeneratedAt > SafeTravelConstants.CacheStalenessThreshold;
        var forecastEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(SafeTravelConstants.ForecastDays - 1);

        var metadata = new ResponseMetadata(
            GeneratedAt: rankings.GeneratedAt,
            ForecastPeriod: $"{DateOnly.FromDateTime(rankings.GeneratedAt):yyyy-MM-dd} to {forecastEnd:yyyy-MM-dd}",
            IsStale: isStale);

        return new Top10DistrictsResponse(top10, metadata);
    }
}
