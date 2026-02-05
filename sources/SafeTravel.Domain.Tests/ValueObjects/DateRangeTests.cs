using Shouldly;
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
        range.Start.ShouldBe(start);
        range.End.ShouldBe(end);
        range.TotalDays.ShouldBe(6);
    }

    [Fact]
    public void Create_WithEndBeforeStart_ShouldThrow()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 10);
        var end = new DateOnly(2026, 2, 5);

        // Act & Assert
        Should.Throw<InvalidDateRangeException>(() => DateRange.Create(start, end));
    }

    [Fact]
    public void Create_ExceedingMaxDays_ShouldThrow()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 1);
        var end = new DateOnly(2026, 2, 10); // 10 days > 7 max

        // Act & Assert
        Should.Throw<InvalidDateRangeException>(() => DateRange.Create(start, end));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void FromToday_WithValidDays_ShouldSucceed(int days)
    {
        // Act
        var range = DateRange.FromToday(days);

        // Assert
        range.TotalDays.ShouldBe(days);
        range.Start.ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void FromToday_WithInvalidDays_ShouldThrow(int days)
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => DateRange.FromToday(days));
    }

    [Fact]
    public void Contains_DateWithinRange_ShouldReturnTrue()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 10);
        var range = DateRange.Create(start, end);

        // Act & Assert
        range.Contains(new DateOnly(2026, 2, 7)).ShouldBeTrue();
        range.Contains(start).ShouldBeTrue();
        range.Contains(end).ShouldBeTrue();
    }

    [Fact]
    public void Contains_DateOutsideRange_ShouldReturnFalse()
    {
        // Arrange
        var start = new DateOnly(2026, 2, 5);
        var end = new DateOnly(2026, 2, 10);
        var range = DateRange.Create(start, end);

        // Act & Assert
        range.Contains(new DateOnly(2026, 2, 4)).ShouldBeFalse();
        range.Contains(new DateOnly(2026, 2, 11)).ShouldBeFalse();
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
        dates.Count.ShouldBe(3);
        dates[0].ShouldBe(new DateOnly(2026, 2, 5));
        dates[1].ShouldBe(new DateOnly(2026, 2, 6));
        dates[2].ShouldBe(new DateOnly(2026, 2, 7));
    }
}
