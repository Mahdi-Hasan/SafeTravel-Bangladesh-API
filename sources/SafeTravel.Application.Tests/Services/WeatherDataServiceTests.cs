using Shouldly;
using NSubstitute;
using SafeTravel.Application.Services;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.Services;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Tests.Services;

public class WeatherDataServiceTests
{
    private readonly IWeatherDataCache _cache = Substitute.For<IWeatherDataCache>();
    private readonly IOpenMeteoClient _openMeteoClient = Substitute.For<IOpenMeteoClient>();
    private readonly IDistrictRepository _districtRepository = Substitute.For<IDistrictRepository>();
    private readonly DistrictRankingService _rankingService = new();
    private readonly WeatherAggregator _weatherAggregator = new();
    private readonly WeatherDataService _service;

    public WeatherDataServiceTests()
    {
        _service = new WeatherDataService(
            _cache,
            _openMeteoClient,
            _districtRepository,
            _rankingService,
            _weatherAggregator);
    }

    [Fact]
    public async Task GetRankingsAsync_FreshCache_ShouldReturnCachedData()
    {
        // Arrange
        var freshRankings = CreateCachedRankings(DateTime.UtcNow.AddMinutes(-5)); // 5 min old
        _cache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns(freshRankings);

        // Act
        var result = await _service.GetRankingsAsync();

        // Assert
        result.ShouldBeSameAs(freshRankings);
        await _openMeteoClient.DidNotReceive()
            .GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRankingsAsync_StaleCache_ShouldTriggerManualLoad()
    {
        // Arrange
        var staleRankings = CreateCachedRankings(DateTime.UtcNow.AddMinutes(-15)); // 15 min old > 12 min threshold
        _cache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns(staleRankings);

        var districts = new List<District>
        {
            District.Create("1", "Dhaka", 23.8, 90.4)
        };
        _districtRepository.GetAll().Returns(districts);

        SetupApiResponses(districts);

        // Act
        var result = await _service.GetRankingsAsync();

        // Assert
        await _openMeteoClient.Received(1)
            .GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRankingsAsync_CacheMiss_ShouldTriggerManualLoad()
    {
        // Arrange
        _cache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns((CachedRankings?)null);

        var districts = new List<District>
        {
            District.Create("1", "Dhaka", 23.8, 90.4)
        };
        _districtRepository.GetAll().Returns(districts);

        SetupApiResponses(districts);

        // Act
        var result = await _service.GetRankingsAsync();

        // Assert
        await _openMeteoClient.Received(1)
            .GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).SetRankingsAsync(Arg.Any<CachedRankings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRankingsAsync_ManualLoad_ShouldUpdateCache()
    {
        // Arrange
        _cache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns((CachedRankings?)null);

        var districts = new List<District>
        {
            District.Create("1", "Dhaka", 23.8, 90.4)
        };
        _districtRepository.GetAll().Returns(districts);

        SetupApiResponses(districts);

        // Act
        await _service.GetRankingsAsync();

        // Assert
        await _cache.Received(1).SetRankingsAsync(Arg.Is<CachedRankings>(r =>
            r.Rankings.Count == 1 &&
            r.GeneratedAt <= DateTime.UtcNow), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRankingsAsync_NoDistrictsWithData_ShouldThrow()
    {
        // Arrange
        _cache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns((CachedRankings?)null);
        _districtRepository.GetAll().Returns(new List<District>());

        var emptyWeatherResponse = new BulkWeatherResponse(
            new Dictionary<Coordinates, IReadOnlyList<HourlyWeatherData>>());
        var emptyAirQualityResponse = new BulkAirQualityResponse(
            new Dictionary<Coordinates, IReadOnlyList<HourlyAirQualityData>>());

        _openMeteoClient.GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyWeatherResponse);
        _openMeteoClient.GetBulkAirQualityAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(emptyAirQualityResponse);

        // Act & Assert
        await Should.ThrowAsync<WeatherDataUnavailableException>(async () =>
            await _service.GetRankingsAsync());
    }

    [Fact]
    public async Task GetWeatherForDistrictAsync_WithCachedForecast_ShouldReturnCached()
    {
        // Arrange
        var district = District.Create("1", "Dhaka", 23.8, 90.4);
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var cachedSnapshot = WeatherSnapshot.Create(targetDate, 30.0, 50.0);

        var cachedForecast = new CachedDistrictForecast(
            district.Id,
            [cachedSnapshot],
            DateTime.UtcNow);

        _cache.GetDistrictForecastAsync(district.Id, Arg.Any<CancellationToken>()).Returns(cachedForecast);

        // Act
        var result = await _service.GetWeatherForDistrictAsync(district, targetDate);

        // Assert
        result.ShouldBeSameAs(cachedSnapshot);
        await _openMeteoClient.DidNotReceive()
            .GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static CachedRankings CreateCachedRankings(DateTime generatedAt)
    {
        var district = District.Create("1", "Dhaka", 23.8, 90.4);
        var ranked = new RankedDistrict(
            rank: 1,
            district: district,
            avgTemperature: Temperature.FromCelsius(30.0),
            avgPM25: PM25Level.Create(50.0),
            generatedAt: generatedAt);

        return new CachedRankings(
            [ranked],
            GeneratedAt: generatedAt,
            ExpiresAt: generatedAt.AddMinutes(20));
    }

    private void SetupApiResponses(IReadOnlyList<District> districts)
    {
        var weatherData = districts.ToDictionary(
            d => d.Coordinates,
            d => (IReadOnlyList<HourlyWeatherData>)Enumerable.Range(0, 168)
                .Select(h => new HourlyWeatherData(DateTime.UtcNow.AddHours(h), 28.0))
                .ToList());

        var airQualityData = districts.ToDictionary(
            d => d.Coordinates,
            d => (IReadOnlyList<HourlyAirQualityData>)Enumerable.Range(0, 168)
                .Select(h => new HourlyAirQualityData(DateTime.UtcNow.AddHours(h), 45.0))
                .ToList());

        _openMeteoClient.GetBulkForecastAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new BulkWeatherResponse(weatherData));

        _openMeteoClient.GetBulkAirQualityAsync(Arg.Any<IEnumerable<Coordinates>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new BulkAirQualityResponse(airQualityData));
    }
}
