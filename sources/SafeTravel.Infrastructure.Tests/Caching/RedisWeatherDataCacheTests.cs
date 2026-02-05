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
    public void Constructor_WithNullConnectionString_UsesInMemoryFallback()
    {
        // Act
        using var cache = new RedisWeatherDataCache(_logger, null);
        cache.GetRankings(); // Should not throw

        // Assert
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("in-memory fallback")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void GetRankings_WhenEmptyCache_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = cache.GetRankings();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void SetAndGetRankings_WithValidData_RoundTripsCorrectly()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        var rankings = CreateTestRankings();

        // Act
        cache.SetRankings(rankings);
        var result = cache.GetRankings();

        // Assert
        result.ShouldNotBeNull();
        result.Rankings.Count.ShouldBe(rankings.Rankings.Count);
        result.GeneratedAt.ShouldBe(rankings.GeneratedAt);
    }

    [Fact]
    public void GetRankings_WhenExpired_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        var expiredRankings = new CachedRankings(
            Rankings: [],
            GeneratedAt: DateTime.UtcNow.AddHours(-1),
            ExpiresAt: DateTime.UtcNow.AddMinutes(-30) // Already expired
        );

        // Act
        cache.SetRankings(expiredRankings);
        var result = cache.GetRankings();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetDistrictForecast_WhenEmpty_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = cache.GetDistrictForecast("dhaka");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void SetAndGetDistrictForecast_WithValidData_RoundTripsCorrectly()
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
        cache.SetDistrictForecast(districtId, forecast);
        var result = cache.GetDistrictForecast(districtId);

        // Assert
        result.ShouldNotBeNull();
        result.DistrictId.ShouldBe(districtId);
        result.Forecasts.Count.ShouldBe(1);
    }

    [Fact]
    public void GetMetadata_WhenNoRankings_ReturnsNull()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);

        // Act
        var result = cache.GetMetadata();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetMetadata_WithRankings_ReturnsMetadata()
    {
        // Arrange
        using var cache = new RedisWeatherDataCache(_logger, null);
        cache.SetRankings(CreateTestRankings());
        cache.SetDistrictForecast("dhaka", CreateTestForecast("dhaka"));
        cache.SetDistrictForecast("chittagong", CreateTestForecast("chittagong"));

        // Act
        var result = cache.GetMetadata();

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
