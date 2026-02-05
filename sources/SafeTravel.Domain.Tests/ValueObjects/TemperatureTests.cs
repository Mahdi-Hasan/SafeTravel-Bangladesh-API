using FluentAssertions;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Tests.ValueObjects;

public class TemperatureTests
{
    [Theory]
    [InlineData(25.0)]
    [InlineData(0)]
    [InlineData(-40)]
    [InlineData(50)]
    public void FromCelsius_WithValidTemperature_ShouldSucceed(double celsius)
    {
        // Act
        var temp = Temperature.FromCelsius(celsius);

        // Assert
        temp.Celsius.Should().Be(celsius);
    }

    [Theory]
    [InlineData(-101)]
    [InlineData(101)]
    public void FromCelsius_WithInvalidTemperature_ShouldThrow(double celsius)
    {
        // Act & Assert
        var act = () => Temperature.FromCelsius(celsius);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToFahrenheit_ShouldConvertCorrectly()
    {
        // Arrange
        var temp = Temperature.FromCelsius(25);

        // Act
        var fahrenheit = temp.ToFahrenheit();

        // Assert
        fahrenheit.Should().Be(77); // 25°C = 77°F
    }

    [Theory]
    [InlineData(20, 25, true)]
    [InlineData(25, 20, false)]
    [InlineData(25, 25, false)]
    public void IsCoolerThan_ShouldCompareCorrectly(double thisTemp, double otherTemp, bool expected)
    {
        // Arrange
        var temp1 = Temperature.FromCelsius(thisTemp);
        var temp2 = Temperature.FromCelsius(otherTemp);

        // Act & Assert
        temp1.IsCoolerThan(temp2).Should().Be(expected);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var temp = Temperature.FromCelsius(25.5);

        // Act & Assert
        temp.ToString().Should().Be("25.5°C");
    }
}
