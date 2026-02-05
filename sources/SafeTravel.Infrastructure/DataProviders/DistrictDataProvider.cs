using System.Reflection;
using System.Text.Json;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;

namespace SafeTravel.Infrastructure.DataProviders;

public sealed class DistrictDataProvider : IDistrictRepository
{
    private readonly IReadOnlyList<District> _districts;
    private readonly IReadOnlyDictionary<string, District> _districtsByName;

    public DistrictDataProvider()
    {
        var districts = LoadDistrictsFromEmbeddedResource();
        _districts = districts;
        _districtsByName = districts.ToDictionary(
            d => d.Name,
            d => d,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<District> GetAll() => _districts;

    public District? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _districtsByName.TryGetValue(name, out var district) ? district : null;
    }

    private static List<District> LoadDistrictsFromEmbeddedResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SafeTravel.Infrastructure.Data.districts.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var districtDtos = JsonSerializer.Deserialize<List<DistrictDto>>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize districts JSON.");

        return districtDtos
            .Select(dto => District.Create(dto.Id, dto.Name, dto.Latitude, dto.Longitude))
            .ToList();
    }

    private sealed record DistrictDto(string Id, string Name, double Latitude, double Longitude);
}
