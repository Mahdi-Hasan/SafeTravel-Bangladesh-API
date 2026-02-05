using FluentAssertions;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Tests.ValueObjects;

public class CoordinatesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(23.8103, 90.4125)] // Dhaka
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void Create_WithValidCoordinates_ShouldSucceed(double lat, double lon)
    {
        // Act
        var coordinates = Coordinates.Create(lat, lon);

        // Assert
        coordinates.Latitude.Should().Be(lat);
        coordinates.Longitude.Should().Be(lon);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    public void Create_WithInvalidLatitude_ShouldThrow(double lat, double lon)
    {
        // Act & Assert
        var act = () => Coordinates.Create(lat, lon);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("latitude");
    }

    [Theory]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void Create_WithInvalidLongitude_ShouldThrow(double lat, double lon)
    {
        // Act & Assert
        var act = () => Coordinates.Create(lat, lon);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("longitude");
    }

    [Fact]
    public void IsWithinBangladesh_WithDhakaCoordinates_ShouldReturnTrue()
    {
        // Arrange
        var dhaka = Coordinates.Create(23.8103, 90.4125);

        // Act & Assert
        dhaka.IsWithinBangladesh().Should().BeTrue();
    }

    [Fact]
    public void IsWithinBangladesh_WithLondonCoordinates_ShouldReturnFalse()
    {
        // Arrange
        var london = Coordinates.Create(51.5074, -0.1278);

        // Act & Assert
        london.IsWithinBangladesh().Should().BeFalse();
    }

    [Fact]
    public void Equality_TwoCoordinatesWithSameValues_ShouldBeEqual()
    {
        // Arrange
        var coord1 = Coordinates.Create(23.8103, 90.4125);
        var coord2 = Coordinates.Create(23.8103, 90.4125);

        // Assert
        coord1.Should().Be(coord2);
        (coord1 == coord2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var coords = Coordinates.Create(23.8103, 90.4125);

        // Act & Assert
        coords.ToString().Should().Be("(23.8103, 90.4125)");
    }
}
