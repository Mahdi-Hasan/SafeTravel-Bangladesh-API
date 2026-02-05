namespace SafeTravel.Domain.Exceptions;

/// <summary>
/// Thrown when a requested district cannot be found.
/// </summary>
public sealed class DistrictNotFoundException : SafeTravelDomainException
{
    public string DistrictName { get; }

    public DistrictNotFoundException(string districtName)
        : base($"District '{districtName}' was not found.")
    {
        DistrictName = districtName;
    }
}
