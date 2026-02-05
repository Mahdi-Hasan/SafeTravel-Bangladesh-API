using FluentAssertions;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.Services;

namespace SafeTravel.Domain.Tests.Services;

public class TravelRecommendationPolicyTests
{
    private readonly TravelRecommendationPolicy _policy = new();

    [Fact]
    public void Evaluate_CoolerAndCleaner_ShouldRecommend()
    {
        // Arrange
        var origin = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 45.0);
        var destination = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0);

        // Act
        var result = _policy.Evaluate(origin, destination, "Sylhet");

        // Assert
        result.IsRecommended.Should().BeTrue();
        result.Reason.Should().Contain("cooler");
        result.Reason.Should().Contain("air quality");
    }

    [Fact]
    public void Evaluate_CoolerButDirtier_ShouldNotRecommend()
    {
        // Arrange
        var origin = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 20.0);
        var destination = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 45.0); // Higher PM2.5

        // Act
        var result = _policy.Evaluate(origin, destination, "Sylhet");

        // Assert
        result.IsRecommended.Should().BeFalse();
        result.Reason.Should().Contain("PM2.5");
    }

    [Fact]
    public void Evaluate_WarmerButCleaner_ShouldNotRecommend()
    {
        // Arrange
        var origin = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 45.0);
        var destination = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 20.0); // Higher temp

        // Act
        var result = _policy.Evaluate(origin, destination, "Sylhet");

        // Assert
        result.IsRecommended.Should().BeFalse();
        result.Reason.Should().Contain("warmer");
    }

    [Fact]
    public void Evaluate_WarmerAndDirtier_ShouldNotRecommend()
    {
        // Arrange
        var origin = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0);
        var destination = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 30.0, 45.0); // Both worse

        // Act
        var result = _policy.Evaluate(origin, destination, "Sylhet");

        // Assert
        result.IsRecommended.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_SameConditions_ShouldNotRecommend()
    {
        // Arrange
        var origin = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0);
        var destination = WeatherSnapshot.Create(DateOnly.FromDateTime(DateTime.UtcNow), 25.0, 20.0);

        // Act
        var result = _policy.Evaluate(origin, destination, "Sylhet");

        // Assert
        result.IsRecommended.Should().BeFalse();
    }
}
