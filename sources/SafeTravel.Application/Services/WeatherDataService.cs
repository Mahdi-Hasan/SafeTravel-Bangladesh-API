using SafeTravel.Domain;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.Services;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Services;

/// <summary>
/// Implements cache-aside pattern for weather data retrieval.
/// Uses SafeTravelConstants for staleness threshold with manual data loader fallback.
/// </summary>
public sealed class WeatherDataService : IWeatherDataService
{
    private readonly IWeatherDataCache _cache;
    private readonly IOpenMeteoClient _openMeteoClient;
    private readonly IDistrictRepository _districtRepository;
    private readonly DistrictRankingService _rankingService;
    private readonly WeatherAggregator _weatherAggregator;

    public WeatherDataService(
        IWeatherDataCache cache,
        IOpenMeteoClient openMeteoClient,
        IDistrictRepository districtRepository,
        DistrictRankingService rankingService,
        WeatherAggregator weatherAggregator)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _openMeteoClient = openMeteoClient ?? throw new ArgumentNullException(nameof(openMeteoClient));
        _districtRepository = districtRepository ?? throw new ArgumentNullException(nameof(districtRepository));
        _rankingService = rankingService ?? throw new ArgumentNullException(nameof(rankingService));
        _weatherAggregator = weatherAggregator ?? throw new ArgumentNullException(nameof(weatherAggregator));
    }

    /// <inheritdoc />
    public async Task<CachedRankings> GetRankingsAsync(CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cached = await _cache.GetRankingsAsync(cancellationToken).ConfigureAwait(false);

        if (cached is not null && !IsStale(cached.GeneratedAt))
        {
            return cached;
        }

        // Cache miss or stale - trigger manual data load
        return await ManualDataLoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WeatherSnapshot> GetWeatherForCoordinatesAsync(
        Coordinates location,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var weatherResponse = await _openMeteoClient.GetBulkForecastAsync(
            [location], days: 7, cancellationToken).ConfigureAwait(false);

        var airQualityResponse = await _openMeteoClient.GetBulkAirQualityAsync(
            [location], days: 7, cancellationToken).ConfigureAwait(false);

        if (!weatherResponse.Data.TryGetValue(location, out var weatherData) ||
            !airQualityResponse.Data.TryGetValue(location, out var airQualityData))
        {
            throw new WeatherDataUnavailableException(
                $"Weather data unavailable for coordinates ({location.Latitude}, {location.Longitude})");
        }

        return _weatherAggregator.GetSnapshotForDate(weatherData, airQualityData, date);
    }

    /// <inheritdoc />
    public async Task<WeatherSnapshot> GetWeatherForDistrictAsync(
        District district,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cached = await _cache.GetDistrictForecastAsync(district.Id, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            var forecast = cached.Forecasts.FirstOrDefault(f => f.Date == date);
            if (forecast is not null)
            {
                return forecast;
            }
        }

        // Cache miss - trigger background refresh (fire-and-forget) so next requests hit cache
        var rankings = await _cache.GetRankingsAsync(cancellationToken).ConfigureAwait(false);
        if (rankings is null || IsStale(rankings.GeneratedAt))
        {
            // Fire-and-forget: refresh cache in background, don't block current request
            _ = Task.Run(async () =>
            {
                try
                {
                    await ManualDataLoadAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow exception - background refresh failure shouldn't affect current request
                }
            }, cancellationToken);
        }

        // Return immediately: fetch single district from API
        return await GetWeatherForCoordinatesAsync(district.Coordinates, date, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsStale(DateTime generatedAt)
    {
        return DateTime.UtcNow - generatedAt > SafeTravelConstants.CacheStalenessThreshold;
    }

    private async Task<CachedRankings> ManualDataLoadAsync(CancellationToken cancellationToken)
    {
        var districts = _districtRepository.GetAll();
        var coordinates = districts.Select(d => d.Coordinates).ToList();

        var weatherTask = _openMeteoClient.GetBulkForecastAsync(coordinates, days: SafeTravelConstants.ForecastDays, cancellationToken);
        var airQualityTask = _openMeteoClient.GetBulkAirQualityAsync(coordinates, days: SafeTravelConstants.ForecastDays, cancellationToken);

        await Task.WhenAll(weatherTask, airQualityTask).ConfigureAwait(false);

        var weatherResponse = await weatherTask.ConfigureAwait(false);
        var airQualityResponse = await airQualityTask.ConfigureAwait(false);

        var districtWeatherData = new Dictionary<District, WeatherSnapshot>();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        foreach (var district in districts)
        {
            if (weatherResponse.Data.TryGetValue(district.Coordinates, out var weather) &&
                airQualityResponse.Data.TryGetValue(district.Coordinates, out var airQuality))
            {
                // Aggregate for rankings
                var snapshot = _weatherAggregator.AggregateToAverage(weather, airQuality);
                districtWeatherData[district] = snapshot;

                // Generate 7-day forecast for district cache
                var dailyForecasts = new List<WeatherSnapshot>();
                for (int i = 0; i < SafeTravelConstants.ForecastDays; i++)
                {
                    try
                    {
                        var date = today.AddDays(i);
                        var dailySnapshot = _weatherAggregator.GetSnapshotForDate(weather, airQuality, date);
                        dailyForecasts.Add(dailySnapshot);
                    }
                    catch (InsufficientDataException)
                    {
                        // Skip days with missing data
                    }
                }

                if (dailyForecasts.Count > 0)
                {
                    var districtForecast = new CachedDistrictForecast(
                        district.Id,
                        dailyForecasts,
                        now);
                    await _cache.SetDistrictForecastAsync(district.Id, districtForecast, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (districtWeatherData.Count == 0)
        {
            throw new WeatherDataUnavailableException("Unable to fetch weather data for any districts.");
        }

        // Compute rankings
        var rankedDistricts = _rankingService.ComputeRankings(districtWeatherData);

        var rankings = new CachedRankings(
            rankedDistricts,
            GeneratedAt: now,
            ExpiresAt: now.Add(SafeTravelConstants.DefaultCacheTtl));

        // Update cache
        await _cache.SetRankingsAsync(rankings, cancellationToken).ConfigureAwait(false);

        return rankings;
    }
}
