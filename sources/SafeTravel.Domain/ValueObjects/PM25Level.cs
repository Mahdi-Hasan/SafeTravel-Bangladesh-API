namespace SafeTravel.Domain.ValueObjects;

public sealed record PM25Level
{
    public enum AirQualityCategory
    {
        Good,
        Moderate,
        UnhealthyForSensitiveGroups,
        Unhealthy,
        VeryUnhealthy,
        Hazardous
    }

    public double Value { get; }

    private PM25Level(double value) => Value = value;

    public static PM25Level Create(double value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "PM2.5 level cannot be negative.");
        }
        return new PM25Level(value);
    }

    public AirQualityCategory GetAirQualityCategory() => Value switch
    {
        <= 12.0 => AirQualityCategory.Good,
        <= 35.4 => AirQualityCategory.Moderate,
        <= 55.4 => AirQualityCategory.UnhealthyForSensitiveGroups,
        <= 150.4 => AirQualityCategory.Unhealthy,
        <= 250.4 => AirQualityCategory.VeryUnhealthy,
        _ => AirQualityCategory.Hazardous
    };

    public bool IsCleanerThan(PM25Level other) => Value < other.Value;
    public override string ToString() => $"{Value:F1} μg/m³ ({GetAirQualityCategory()})";
}
