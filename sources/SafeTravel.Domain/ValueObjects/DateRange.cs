using SafeTravel.Domain.Exceptions;

namespace SafeTravel.Domain.ValueObjects;

/// <summary>
/// Represents a date range for weather forecasting.
/// Ensures range is valid and within the 7-day forecast window.
/// </summary>
public sealed record DateRange
{
    public const int MaxForecastDays = 7;

    public DateOnly Start { get; }
    public DateOnly End { get; }

    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a date range with validation.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <returns>A valid DateRange instance.</returns>
    /// <exception cref="InvalidDateRangeException">When range is invalid.</exception>
    public static DateRange Create(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw InvalidDateRangeException.EndBeforeStart();
        }

        var daysDifference = end.DayNumber - start.DayNumber;
        if (daysDifference >= MaxForecastDays)
        {
            throw InvalidDateRangeException.ExceedsMaximumDays(MaxForecastDays);
        }

        return new DateRange(start, end);
    }

    /// <summary>
    /// Creates a date range starting from today for the specified number of days.
    /// </summary>
    /// <param name="days">Number of days (1-7).</param>
    /// <returns>A date range starting from today.</returns>
    public static DateRange FromToday(int days = MaxForecastDays)
    {
        if (days is < 1 or > MaxForecastDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                days,
                $"Days must be between 1 and {MaxForecastDays}.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new DateRange(today, today.AddDays(days - 1));
    }

    /// <summary>
    /// Validates a travel date is within the forecast window (today + 6 days).
    /// </summary>
    /// <param name="travelDate">The travel date to validate.</param>
    /// <exception cref="InvalidDateRangeException">When date is outside valid window.</exception>
    public static void ValidateTravelDate(DateOnly travelDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (travelDate < today)
        {
            throw InvalidDateRangeException.DateInPast();
        }

        var maxDate = today.AddDays(MaxForecastDays - 1);
        if (travelDate > maxDate)
        {
            throw InvalidDateRangeException.OutsideForecastWindow(MaxForecastDays);
        }
    }

    /// <summary>
    /// Gets the number of days in this range (inclusive).
    /// </summary>
    public int TotalDays => End.DayNumber - Start.DayNumber + 1;

    /// <summary>
    /// Checks if a date falls within this range.
    /// </summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>
    /// Enumerates all dates in the range.
    /// </summary>
    public IEnumerable<DateOnly> GetDates()
    {
        for (var date = Start; date <= End; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    public override string ToString() => $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd} ({TotalDays} days)";
}
