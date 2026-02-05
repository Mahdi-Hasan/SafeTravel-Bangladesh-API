using FluentAssertions;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Tests.ValueObjects;

public class DateRangeTests
{
    [Fact]
    public void Create_WithValidRange_ShouldSucceed()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 10);

        // Act
        var range = DateRange.Create(start, end);

        // Assert
        range.Start.Should().Be(start);
        range.End.Should().Be(end);
        range.TotalDays.Should().Be(6);
    }

    [Fact]
    public void Create_WithEndBeforeStart_ShouldThrow()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 10);
        var end = new DateOnly(2026, 2, 5);

        // Act & Assert
        var act = () => DateRange.Create(start, end);
        act.Should().Throw<InvalidDateRangeException>();
    }

    [Fact]
    public void Create_ExceedingMaxDays_ShouldThrow()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 1);
        var end = new DateOnly(2026, 2, 10); // 10 days > 7 max

        // Act & Assert
        var act = () => DateRange.Create(start, end);
        act.Should().Throw<InvalidDateRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void FromToday_WithValidDays_ShouldSucceed(int days)
    {
        // Act
        var range = DateRange.FromToday(days);

        // Assert
        range.TotalDays.Should().Be(days);
        range.Start.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void FromToday_WithInvalidDays_ShouldThrow(int days)
    {
        // Act & Assert
        var act = () => DateRange.FromToday(days);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Contains_DateWithinRange_ShouldReturnTrue()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 10);
        var range = DateRange.Create(start, end);

        // Act & Assert
        range.Contains(new DateOnly(2026, 2, 7)).Should().BeTrue();
        range.Contains(start).Should().BeTrue();
        range.Contains(end).Should().BeTrue();
    }

    [Fact]
    public void Contains_DateOutsideRange_ShouldReturnFalse()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 10);
        var range = DateRange.Create(start, end);

        // Act & Assert
        range.Contains(new DateOnly(2026, 2, 4)).Should().BeFalse();
        range.Contains(new DateOnly(2026, 2, 11)).Should().BeFalse();
    }

    [Fact]
    public void GetDates_ShouldEnumerateAllDates()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 7);
        var range = DateRange.Create(start, end);

        // Act
        var dates = range.GetDates().ToList();

        // Assert
        dates.Should().HaveCount(3);
        dates.Should().ContainInOrder(
            new DateOnly(2026, 2, 5),
            new DateOnly(2026, 2, 6),
            new DateOnly(2026, 2, 7));
    }
}
