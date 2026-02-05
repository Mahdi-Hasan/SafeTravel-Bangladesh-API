namespace SafeTravel.Domain.ValueObjects;

/// <summary>
/// Represents PM2.5 air quality level in μg/m³.
/// Provides category classification based on EPA standards.
/// </summary>
public sealed record PM25Level
{
    /// <summary>
    /// Air quality categories based on PM2.5 concentration.
    /// </summary>
    public enum AirQualityCategory
    {
        Good,
        Moderate,
        UnhealthyForSensitiveGroups,
        Unhealthy,
        VeryUnhealthy,
        Hazardous
    }

    /// <summary>
    /// PM2.5 concentration in μg/m³.
    /// </summary>
    public double Value { get; }

    private PM25Level(double value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a PM25Level instance with validation.
    /// </summary>
    /// <param name="value">PM2.5 value in μg/m³ (must be non-negative).</param>
    /// <returns>A valid PM25Level instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When value is negative.</exception>
    public static PM25Level Create(double value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "PM2.5 level cannot be negative.");
        }

        return new PM25Level(value);
    }

    /// <summary>
    /// Gets the air quality category based on EPA PM2.5 breakpoints.
    /// </summary>
    /// <returns>The air quality category.</returns>
    public AirQualityCategory GetAirQualityCategory()
    {
        return Value switch
        {
            <= 12.0 => AirQualityCategory.Good,
            <= 35.4 => AirQualityCategory.Moderate,
            <= 55.4 => AirQualityCategory.UnhealthyForSensitiveGroups,
            <= 150.4 => AirQualityCategory.Unhealthy,
            <= 250.4 => AirQualityCategory.VeryUnhealthy,
            _ => AirQualityCategory.Hazardous
        };
    }

    /// <summary>
    /// Compares this PM2.5 level to another.
    /// </summary>
    /// <returns>True if this air quality is better (lower PM2.5).</returns>
    public bool IsCleanerThan(PM25Level other) => Value < other.Value;

    public override string ToString() => $"{Value:F1} μg/m³ ({GetAirQualityCategory()})";
}
