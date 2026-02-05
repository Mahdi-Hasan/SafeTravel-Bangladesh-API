namespace SafeTravel.Domain.ValueObjects;

public sealed record Temperature
{
    public double Celsius { get; }

    private Temperature(double celsius) => Celsius = celsius;

    public static Temperature FromCelsius(double celsius)
    {
        if (celsius is < -100 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(celsius), celsius, "Temperature must be between -100°C and 100°C.");
        }
        return new Temperature(celsius);
    }

    public double ToFahrenheit() => (Celsius * 9 / 5) + 32;
    public bool IsCoolerThan(Temperature other) => Celsius < other.Celsius;
    public override string ToString() => $"{Celsius:F1}°C";
}
