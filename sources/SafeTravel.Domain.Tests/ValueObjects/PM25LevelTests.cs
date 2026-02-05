using Shouldly;
using SafeTravel.Domain.ValueObjects;
using static SafeTravel.Domain.ValueObjects.PM25Level;

namespace SafeTravel.Domain.Tests.ValueObjects;

public class PM25LevelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(12.0)]
    [InlineData(50.5)]
    public void Create_WithValidValue_ShouldSucceed(double value)
    {
        // Act
        var pm25 = PM25Level.Create(value);

        // Assert
        pm25.Value.ShouldBe(value);
    }

    [Fact]
    public void Create_WithNegativeValue_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => PM25Level.Create(-1));
    }

    [Theory]
    [InlineData(0, AirQualityCategory.Good)]
    [InlineData(12.0, AirQualityCategory.Good)]
    [InlineData(12.1, AirQualityCategory.Moderate)]
    [InlineData(35.4, AirQualityCategory.Moderate)]
    [InlineData(35.5, AirQualityCategory.UnhealthyForSensitiveGroups)]
    [InlineData(55.4, AirQualityCategory.UnhealthyForSensitiveGroups)]
    [InlineData(55.5, AirQualityCategory.Unhealthy)]
    [InlineData(150.4, AirQualityCategory.Unhealthy)]
    [InlineData(150.5, AirQualityCategory.VeryUnhealthy)]
    [InlineData(250.5, AirQualityCategory.Hazardous)]
    public void GetAirQualityCategory_ShouldReturnCorrectCategory(double value, AirQualityCategory expected)
    {
        // Arrange
        var pm25 = PM25Level.Create(value);

        // Act & Assert
        pm25.GetAirQualityCategory().ShouldBe(expected);
    }

    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(20, 10, false)]
    [InlineData(10, 10, false)]
    public void IsCleanerThan_ShouldCompareCorrectly(double thisValue, double otherValue, bool expected)
    {
        // Arrange
        var pm1 = PM25Level.Create(thisValue);
        var pm2 = PM25Level.Create(otherValue);

        // Act & Assert
        pm1.IsCleanerThan(pm2).ShouldBe(expected);
    }
}
