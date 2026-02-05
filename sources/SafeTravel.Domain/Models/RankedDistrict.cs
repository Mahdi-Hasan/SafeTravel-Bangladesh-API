using SafeTravel.Domain.Entities;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Domain.Models;

/// <summary>
/// Represents a district with its calculated ranking based on weather averages.
/// Used for the Top 10 districts response.
/// </summary>
public sealed record RankedDistrict
{
    public int Rank { get; }
    public District District { get; }
    public Temperature AvgTemperature { get; }
    public PM25Level AvgPM25 { get; }
    public DateTime GeneratedAt { get; }

    public RankedDistrict(
        int rank,
        District district,
        Temperature avgTemperature,
        PM25Level avgPM25,
        DateTime generatedAt)
    {
        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be at least 1.");

        Rank = rank;
        District = district ?? throw new ArgumentNullException(nameof(district));
        AvgTemperature = avgTemperature ?? throw new ArgumentNullException(nameof(avgTemperature));
        AvgPM25 = avgPM25 ?? throw new ArgumentNullException(nameof(avgPM25));
        GeneratedAt = generatedAt;
    }

    /// <summary>
    /// Creates a RankedDistrict from raw values.
    /// </summary>
    public static RankedDistrict Create(
        int rank,
        District district,
        double avgTempCelsius,
        double avgPM25Value)
    {
        return new RankedDistrict(
            rank,
            district,
            Temperature.FromCelsius(avgTempCelsius),
            PM25Level.Create(avgPM25Value),
            DateTime.UtcNow);
    }

    public override string ToString() =>
        $"#{Rank}: {District.Name} - {AvgTemperature}, {AvgPM25}";
}
