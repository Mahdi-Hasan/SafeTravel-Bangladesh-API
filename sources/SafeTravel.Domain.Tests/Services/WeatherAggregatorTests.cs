using FluentAssertions;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Services;

namespace SafeTravel.Domain.Tests.Services;

public class WeatherAggregatorTests
{
    private readonly WeatherAggregator _aggregator = new();

    [Fact]
    public void AggregateToAverage_WithValidData_ShouldComputeCorrectly()
    {
        // Arrange
        var weatherData = CreateHourlyWeatherData(7, 25.0);
        var airQualityData = CreateHourlyAirQualityData(7, 15.0);

        // Act
        var result = _aggregator.AggregateToAverage(weatherData, airQualityData);

        // Assert
        result.Temperature.Celsius.Should().Be(25.0);
        result.PM25.Value.Should().Be(15.0);
    }

    [Fact]
    public void AggregateToAverage_WithEmptyData_ShouldThrow()
    {
        // Arrange
        var weatherData = new List<HourlyWeatherData>();
        var airQualityData = new List<HourlyAirQualityData>();

        // Act & Assert
        var act = () => _aggregator.AggregateToAverage(weatherData, airQualityData);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void GetSnapshotForDate_WithValidData_ShouldReturnCorrectSnapshot()
    {
        // Arrange
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var targetDateTime = targetDate.ToDateTime(new TimeOnly(14, 0));

        var weatherData = new List<HourlyWeatherData>
        {
            new(targetDateTime, 28.5)
        };

        var airQualityData = new List<HourlyAirQualityData>
        {
            new(targetDateTime, 22.3)
        };

        // Act
        var result = _aggregator.GetSnapshotForDate(weatherData, airQualityData, targetDate);

        // Assert
        result.Date.Should().Be(targetDate);
        result.Temperature.Celsius.Should().Be(28.5);
        result.PM25.Value.Should().Be(22.3);
    }

    [Fact]
    public void GetSnapshotForDate_NoDataForDate_ShouldThrow()
    {
        // Arrange
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var differentDate = targetDate.AddDays(5);

        var weatherData = new List<HourlyWeatherData>
        {
            new(differentDate.ToDateTime(new TimeOnly(14, 0)), 28.5)
        };

        var airQualityData = new List<HourlyAirQualityData>
        {
            new(differentDate.ToDateTime(new TimeOnly(14, 0)), 22.3)
        };

        // Act & Assert
        var act = () => _aggregator.GetSnapshotForDate(weatherData, airQualityData, targetDate);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void ExtractValuesAtHour_ShouldOnlyExtract2PMValues()
    {
        // Arrange - Create data for multiple hours
        var baseDate = DateTime.UtcNow.Date;
        var weatherData = new List<HourlyWeatherData>
        {
            new(baseDate.AddHours(0), 20.0),  // 00:00
            new(baseDate.AddHours(14), 25.0), // 14:00 ✓
            new(baseDate.AddHours(18), 22.0), // 18:00
            new(baseDate.AddDays(1).AddHours(14), 26.0) // Next day 14:00 ✓
        };

        // Act
        var result = _aggregator.ExtractValuesAtHour(weatherData, 14);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(25.0);
        result.Should().Contain(26.0);
    }

    private static List<HourlyWeatherData> CreateHourlyWeatherData(int days, double temp)
    {
        var data = new List<HourlyWeatherData>();
        var startDate = DateTime.UtcNow.Date;

        for (int d = 0; d < days; d++)
        {
            data.Add(new HourlyWeatherData(startDate.AddDays(d).AddHours(14), temp));
        }

        return data;
    }

    private static List<HourlyAirQualityData> CreateHourlyAirQualityData(int days, double pm25)
    {
        var data = new List<HourlyAirQualityData>();
        var startDate = DateTime.UtcNow.Date;

        for (int d = 0; d < days; d++)
        {
            data.Add(new HourlyAirQualityData(startDate.AddDays(d).AddHours(14), pm25));
        }

        return data;
    }
}
