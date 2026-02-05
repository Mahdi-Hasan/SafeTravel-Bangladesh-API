using SafeTravel.Domain.Entities;

namespace SafeTravel.Domain.Interfaces;

/// <summary>
/// Repository interface for accessing district data.
/// </summary>
public interface IDistrictRepository
{
    /// <summary>
    /// Gets a district by its name (case-insensitive).
    /// </summary>
    /// <param name="name">District name.</param>
    /// <returns>The district if found, null otherwise.</returns>
    District? GetByName(string name);

    /// <summary>
    /// Gets all 64 Bangladesh districts.
    /// </summary>
    /// <returns>A read-only list of all districts.</returns>
    IReadOnlyList<District> GetAll();
}
