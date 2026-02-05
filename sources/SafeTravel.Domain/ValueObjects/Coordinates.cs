namespace SafeTravel.Domain.ValueObjects;

public sealed record Coordinates
{
    public double Latitude { get; }
    public double Longitude { get; }

    private Coordinates(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Coordinates Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");
        }

        return new Coordinates(latitude, longitude);
    }

    public bool IsWithinBangladesh()
    {
        const double minLat = 20.5, maxLat = 26.6;
        const double minLon = 88.0, maxLon = 92.7;

        return Latitude >= minLat && Latitude <= maxLat &&
               Longitude >= minLon && Longitude <= maxLon;
    }

    public override string ToString() => $"({Latitude:F4}, {Longitude:F4})";
}
