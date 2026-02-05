namespace SafeTravel.Domain.ValueObjects;

/// <summary>
/// Represents a temperature value in Celsius.
/// Immutable value object.
/// </summary>
public sealed record Temperature
{
    public double Celsius { get; }

    private Temperature(double celsius)
    {
        Celsius = celsius;
    }

    /// <summary>
    /// Creates a Temperature instance with validation.
    /// </summary>
    /// <param name="celsius">Temperature in Celsius (-100 to 100 range).</param>
    /// <returns>A valid Temperature instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When temperature is outside reasonable range.</exception>
    public static Temperature FromCelsius(double celsius)
    {
        // Reasonable range for Earth temperatures
        if (celsius is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(celsius),
                celsius,
                "Temperature must be between -100°C and 100°C.");
        }

        return new Temperature(celsius);
    }

    /// <summary>
    /// Converts Celsius to Fahrenheit.
    /// </summary>
    public double ToFahrenheit() => (Celsius * 9 / 5) + 32;

    /// <summary>
    /// Compares this temperature to another.
    /// </summary>
    /// <returns>True if this temperature is lower (cooler).</returns>
    public bool IsCoolerThan(Temperature other) => Celsius < other.Celsius;

    public override string ToString() => $"{Celsius:F1}°C";
}
