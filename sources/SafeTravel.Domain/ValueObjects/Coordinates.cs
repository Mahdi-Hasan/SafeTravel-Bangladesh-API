namespace SafeTravel.Domain.ValueObjects;

/// <summary>
/// Represents geographic coordinates with latitude and longitude.
/// Immutable value object with validation.
/// </summary>
public sealed record Coordinates
{
    public double Latitude { get; }
    public double Longitude { get; }

    private Coordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Creates a new Coordinates instance with validation.
    /// </summary>
    /// <param name="latitude">Latitude value between -90 and 90.</param>
    /// <param name="longitude">Longitude value between -180 and 180.</param>
    /// <returns>A valid Coordinates instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When coordinates are out of valid range.</exception>
    public static Coordinates Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be between -180 and 180 degrees.");
        }

        return new Coordinates(latitude, longitude);
    }

    /// <summary>
    /// Checks if coordinates are within Bangladesh's approximate bounds.
    /// </summary>
    /// <returns>True if within Bangladesh region.</returns>
    public bool IsWithinBangladesh()
    {
        // Approximate Bangladesh bounds
        const double minLat = 20.5;
        const double maxLat = 26.6;
        const double minLon = 88.0;
        const double maxLon = 92.7;

        return Latitude >= minLat && Latitude <= maxLat &&
               Longitude >= minLon && Longitude <= maxLon;
    }

    public override string ToString() => $"({Latitude:F4}, {Longitude:F4})";
}
