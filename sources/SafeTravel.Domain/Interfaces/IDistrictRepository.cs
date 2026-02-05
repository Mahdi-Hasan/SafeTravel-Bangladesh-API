using SafeTravel.Domain.Entities;

namespace SafeTravel.Domain.Interfaces;

public interface IDistrictRepository
{
    District? GetByName(string name);
    IReadOnlyList<District> GetAll();
}
