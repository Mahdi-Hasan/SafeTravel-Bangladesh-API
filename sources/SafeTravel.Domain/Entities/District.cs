using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Entities;

/// <summary>
/// Represents a Bangladesh district with its geographic coordinates.
/// Immutable entity.
/// </summary>
public sealed class District
{
    public string Id { get; }
    public string Name { get; }
    public Coordinates Coordinates { get; }

    public District(string id, string name, Coordinates coordinates)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
    }

    /// <summary>
    /// Creates a District from raw coordinate values.
    /// </summary>
    public static District Create(string id, string name, double latitude, double longitude)
    {
        return new District(id, name, Coordinates.Create(latitude, longitude));
    }

    public override bool Equals(object? obj)
    {
        if (obj is not District other) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"{Name} {Coordinates}";
}
