using Shouldly;
using NSubstitute;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Handlers;
using SafeTravel.Application.Queries;
using SafeTravel.Application.Services;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Tests.Handlers;

public class GetTop10DistrictsHandlerTests
{
    private readonly IWeatherDataService _weatherDataService = Substitute.For<IWeatherDataService>();
    private readonly GetTop10DistrictsHandler _handler;

    public GetTop10DistrictsHandlerTests()
    {
        _handler = new GetTop10DistrictsHandler(_weatherDataService);
    }

    [Fact]
    public async Task HandleAsync_WithCachedRankings_ShouldReturnTop10()
    {
        // Arrange
        var rankings = CreateTestRankings(15);
        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Districts.Count.ShouldBe(10);
        result.Districts[0].Rank.ShouldBe(1);
        result.Districts[9].Rank.ShouldBe(10);
    }

    [Fact]
    public async Task HandleAsync_WithFewerThan10Districts_ShouldReturnAll()
    {
        // Arrange
        var rankings = CreateTestRankings(5);
        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Districts.Count.ShouldBe(5);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapDtoFieldsCorrectly()
    {
        // Arrange
        var district = District.Create("1", "Sylhet", 24.9, 91.8);
        var rankedDistrict = new RankedDistrict(
            rank: 1,
            district: district,
            avgTemperature: Temperature.FromCelsius(25.5),
            avgPM25: PM25Level.Create(15.0),
            generatedAt: DateTime.UtcNow);

        var rankings = new CachedRankings(
            [rankedDistrict],
            GeneratedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddMinutes(20));

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        var dto = result.Districts[0];
        dto.Rank.ShouldBe(1);
        dto.DistrictName.ShouldBe("Sylhet");
        dto.AvgTemperature.ShouldBe(25.5);
        dto.AvgPM25.ShouldBe(15.0);
        dto.AirQualityCategory.ShouldBe("Moderate");
    }

    [Fact]
    public async Task HandleAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var generatedAt = DateTime.UtcNow;
        var rankings = new CachedRankings(
            CreateRankedDistricts(3),
            GeneratedAt: generatedAt,
            ExpiresAt: generatedAt.AddMinutes(20));

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Metadata.ShouldNotBeNull();
        result.Metadata.GeneratedAt.ShouldBe(generatedAt);
        result.Metadata.ForecastPeriod.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_StaleData_ShouldIndicateInMetadata()
    {
        // Arrange
        var staleTime = DateTime.UtcNow.AddMinutes(-15); // 15 min old > 12 min threshold
        var rankings = new CachedRankings(
            CreateRankedDistricts(3),
            GeneratedAt: staleTime,
            ExpiresAt: staleTime.AddMinutes(20));

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Metadata.IsStale.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_FreshData_ShouldNotBeStale()
    {
        // Arrange
        var freshTime = DateTime.UtcNow.AddMinutes(-5); // 5 min old < 12 min threshold
        var rankings = new CachedRankings(
            CreateRankedDistricts(3),
            GeneratedAt: freshTime,
            ExpiresAt: freshTime.AddMinutes(20));

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);

        var query = new GetTop10DistrictsQuery();

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Metadata.IsStale.ShouldBeFalse();
    }

    private static CachedRankings CreateTestRankings(int count)
    {
        return new CachedRankings(
            CreateRankedDistricts(count),
            GeneratedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddMinutes(20));
    }

    private static IReadOnlyList<RankedDistrict> CreateRankedDistricts(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new RankedDistrict(
                rank: i,
                district: District.Create(i.ToString(), $"District {i}", 23.0 + i * 0.1, 90.0),
                avgTemperature: Temperature.FromCelsius(20.0 + i),
                avgPM25: PM25Level.Create(10.0 + i),
                generatedAt: DateTime.UtcNow))
            .ToList();
    }
}
