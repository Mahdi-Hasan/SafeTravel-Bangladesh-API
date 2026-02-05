using Shouldly;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.Services;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Tests.Services;

public class DistrictRankingServiceTests
{
    private readonly DistrictRankingService _service = new();

    [Fact]
    public void ComputeRankings_EmptyDictionary_ShouldReturnEmptyList()
    {
        // Arrange
        var data = new Dictionary<District, WeatherSnapshot>();

        // Act
        var result = _service.ComputeRankings(data);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeRankings_ShouldSortByCoolestFirst()
    {
        // Arrange
        var district1 = District.Create("1", "Dhaka", 23.8, 90.4);
        var district2 = District.Create("2", "Sylhet", 24.9, 91.8);
        var district3 = District.Create("3", "Rangpur", 25.7, 89.2);

        var data = new Dictionary<District, WeatherSnapshot>
        {
            [district1] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 25.0), // Hottest
            [district2] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0), // Coolest
            [district3] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 28.0, 22.0), // Middle
        };

        // Act
        var result = _service.ComputeRankings(data);

        // Assert
        result.Count.ShouldBe(3);
        result[0].District.Name.ShouldBe("Sylhet"); // Rank 1 (25°C)
        result[1].District.Name.ShouldBe("Rangpur"); // Rank 2 (28°C)
        result[2].District.Name.ShouldBe("Dhaka"); // Rank 3 (30°C)
    }

    [Fact]
    public void ComputeRankings_SameTempShouldUsePM25AsTieBreaker()
    {
        // Arrange
        var district1 = District.Create("1", "District A", 23.8, 90.4);
        var district2 = District.Create("2", "District B", 24.9, 91.8);

        var data = new Dictionary<District, WeatherSnapshot>
        {
            [district1] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 30.0), // Higher PM2.5
            [district2] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 10.0), // Lower PM2.5
        };

        // Act
        var result = _service.ComputeRankings(data);

        // Assert
        result[0].District.Name.ShouldBe("District B"); // Better air quality wins
        result[1].District.Name.ShouldBe("District A");
    }

    [Fact]
    public void ComputeRankings_ShouldAssignCorrectRanks()
    {
        // Arrange
        var district1 = District.Create("1", "A", 23.8, 90.4);
        var district2 = District.Create("2", "B", 24.9, 91.8);
        var district3 = District.Create("3", "C", 25.7, 89.2);

        var data = new Dictionary<District, WeatherSnapshot>
        {
            [district1] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 25.0),
            [district2] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0),
            [district3] = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 28.0, 22.0),
        };

        // Act
        var result = _service.ComputeRankings(data);

        // Assert
        result[0].Rank.ShouldBe(1);
        result[1].Rank.ShouldBe(2);
        result[2].Rank.ShouldBe(3);
    }

    [Fact]
    public void GetTopDistricts_ShouldReturnOnlyTopN()
    {
        // Arrange
        var districts = Enumerable.Range(1, 20)
            .Select(i => District.Create(i.ToString(), $"District {i}", 23.0 + i * 0.1, 90.0))
            .ToDictionary(
                d => d,
                d => WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 20.0 + double.Parse(d.Id), 15.0));

        // Act
        var result = _service.GetTopDistricts(districts, 10);

        // Assert
        result.Count.ShouldBe(10);
        result.All(r => r.Rank <= 10).ShouldBeTrue();
    }
}
