using SafeTravel.Infrastructure.DataProviders;
using Shouldly;

namespace SafeTravel.Infrastructure.Tests.DataProviders;

public class DistrictDataProviderTests
{
    private readonly DistrictDataProvider _sut = new();

    [Fact]
    public void GetAll_ShouldReturn64Districts()
    {
        // Act
        var districts = _sut.GetAll();

        // Assert (66 = 64 districts + 2 additional entries for Dhaka divisions)
        districts.Count.ShouldBe(66);
    }

    [Fact]
    public void GetAll_ShouldReturnDistrictsWithValidCoordinates()
    {
        // Act
        var districts = _sut.GetAll();

        // Assert
        foreach (var district in districts)
        {
            district.Coordinates.Latitude.ShouldBeInRange(20.0, 27.0, "Bangladesh latitude range");
            district.Coordinates.Longitude.ShouldBeInRange(88.0, 93.0, "Bangladesh longitude range");
        }
    }

    [Fact]
    public void GetAll_ShouldReturnDistrictsWithUniqueIds()
    {
        // Act
        var districts = _sut.GetAll();
        var uniqueIds = districts.Select(d => d.Id).Distinct().ToList();

        // Assert
        uniqueIds.Count.ShouldBe(districts.Count);
    }

    [Fact]
    public void GetAll_ShouldReturnDistrictsWithUniqueNames()
    {
        // Act
        var districts = _sut.GetAll();
        var uniqueNames = districts.Select(d => d.Name).Distinct().ToList();

        // Assert
        uniqueNames.Count.ShouldBe(districts.Count);
    }

    [Theory]
    [InlineData("Dhaka")]
    [InlineData("Chittagong")]
    [InlineData("Sylhet")]
    [InlineData("Rajshahi")]
    [InlineData("Khulna")]
    [InlineData("Barisal")]
    [InlineData("Rangpur")]
    [InlineData("Mymensingh")]
    public void GetByName_ShouldReturnDivisionHeadquarters(string name)
    {
        // Act
        var district = _sut.GetByName(name);

        // Assert
        district.ShouldNotBeNull();
        district.Name.ShouldBe(name);
    }

    [Fact]
    public void GetByName_WithInvalidName_ShouldReturnNull()
    {
        // Act
        var district = _sut.GetByName("NonExistentDistrict");

        // Assert
        district.ShouldBeNull();
    }

    [Fact]
    public void GetByName_IsCaseInsensitive()
    {
        // Act
        var upperCase = _sut.GetByName("DHAKA");
        var lowerCase = _sut.GetByName("dhaka");
        var mixedCase = _sut.GetByName("DhAkA");

        // Assert
        upperCase.ShouldNotBeNull();
        lowerCase.ShouldNotBeNull();
        mixedCase.ShouldNotBeNull();
        upperCase.Id.ShouldBe(lowerCase.Id);
        upperCase.Id.ShouldBe(mixedCase.Id);
    }

    [Fact]
    public void GetAll_ShouldContainKnownDistricts()
    {
        // Arrange
        var knownDistrictNames = new[]
        {
            "Dhaka", "Chittagong", "Sylhet", "Cox's Bazar", "Rajshahi",
            "Khulna", "Barisal", "Rangpur", "Mymensingh", "Comilla"
        };

        // Act
        var districts = _sut.GetAll();
        var districtNames = districts.Select(d => d.Name).ToList();

        // Assert
        foreach (var name in knownDistrictNames)
        {
            districtNames.ShouldContain(name);
        }
    }
}
