using Microsoft.Extensions.Logging;
using NSubstitute;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;
using SafeTravel.Infrastructure.Caching;
using Shouldly;

namespace SafeTravel.Infrastructure.Tests.Caching;

public class RedisWeatherDataCacheTests
{
    private readonly ILogger<RedisWeatherDataCache> _logger;

    public RedisWeatherDataCacheTests()
    {
        _logger = Substitute.For<ILogger<RedisWeatherDataCache>>();
    }

    [Fact]
    public async Task Constructor_WithNullConnectionString_UsesInMemoryFallback()
    {
        // Act
        using var cache = new RedisWeatherDataCache(_logger, null);
        await cache.GetRankingsAsync(); // Should not throw

        // Assert
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("in-memory fallback")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task GetRankingsAsync_WhenEmptyCache_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = await cache.GetRankingsAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAndGetRankingsAsync_WithValidData_RoundTripsCorrectly()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        var rankings = CreateTestRankings();

        // Act
        await cache.SetRankingsAsync(rankings);
        var result = await cache.GetRankingsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Rankings.Count.ShouldBe(rankings.Rankings.Count);
        result.GeneratedAt.ShouldBe(rankings.GeneratedAt);
    }

    [Fact]
    public async Task GetRankingsAsync_WhenExpired_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        var expiredRankings = new CachedRankings(
            Rankings: [],
            GeneratedAt: DateTime.UtcNow.AddHours(-1),
            ExpiresAt: DateTime.UtcNow.AddMinutes(-30) // Already expired
        );

        // Act
        await cache.SetRankingsAsync(expiredRankings);
        var result = await cache.GetRankingsAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetDistrictForecastAsync_WhenEmpty_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = await cache.GetDistrictForecastAsync("dhaka");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAndGetDistrictForecastAsync_WithValidData_RoundTripsCorrectly()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        var districtId = "dhaka";
        var forecast = new CachedDistrictForecast(
            DistrictId: districtId,
            Forecasts:
            [
                new WeatherSnapshot(
                    DateOnly.FromDateTime(DateTime.Today),
                    Temperature.FromCelsius(25.0),
                    PM25Level.Create(35.0),
                    DateTime.UtcNow)
            ],
            GeneratedAt: DateTime.UtcNow
        );

        // Act
        await cache.SetDistrictForecastAsync(districtId, forecast);
        var result = await cache.GetDistrictForecastAsync(districtId);

        // Assert
        result.ShouldNotBeNull();
        result.DistrictId.ShouldBe(districtId);
        result.Forecasts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetMetadataAsync_WhenNoRankings_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = await cache.GetMetadataAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetMetadataAsync_WithRankings_ReturnsMetadata()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);
        await cache.SetRankingsAsync(CreateTestRankings());
        await cache.SetDistrictForecastAsync("dhaka", CreateTestForecast("dhaka"));
        await cache.SetDistrictForecastAsync("chittagong", CreateTestForecast("chittagong"));

        // Act
        var result = await cache.GetMetadataAsync();

        // Assert
        result.ShouldNotBeNull();
        result.IsHealthy.ShouldBeTrue();
        result.DistrictsCached.ShouldBe(2);
    }

    private static CachedRankings CreateTestRankings()
    {
        var now = DateTime.UtcNow;
        return new CachedRankings(
            Rankings:
            [
                new RankedDistrict(
                    rank: 1,
                    district: new SafeTravel.Domain.Entities.District(
                        "dhaka", "Dhaka", Coordinates.Create(23.8, 90.4)),
                    avgTemperature: Temperature.FromCelsius(25.0),
                    avgPM25: PM25Level.Create(35.0),
                    generatedAt: now)
            ],
            GeneratedAt: now,
            ExpiresAt: now.AddMinutes(20)
        );
    }

    private static CachedDistrictForecast CreateTestForecast(string districtId)
    {
        return new CachedDistrictForecast(
            DistrictId: districtId,
            Forecasts:
            [
                new WeatherSnapshot(
                    DateOnly.FromDateTime(DateTime.Today),
                    Temperature.FromCelsius(25.0),
                    PM25Level.Create(35.0),
                    DateTime.UtcNow)
            ],
            GeneratedAt: DateTime.UtcNow
        );
    }
}
