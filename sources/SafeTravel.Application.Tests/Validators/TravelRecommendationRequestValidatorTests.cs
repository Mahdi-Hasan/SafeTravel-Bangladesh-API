using Shouldly;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Validators;

namespace SafeTravel.Application.Tests.Validators;

public class TravelRecommendationRequestValidatorTests
{
    private readonly TravelRecommendationRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_ShouldPass()
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(-100)]
    [InlineData(100)]
    public void Validate_InvalidLatitude_ShouldFail(double latitude)
    {
        var request = new TravelRecommendationRequest(
            Latitude: latitude,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Latitude");
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(-200)]
    [InlineData(200)]
    public void Validate_InvalidLongitude_ShouldFail(double longitude)
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: longitude,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Longitude");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyDestination_ShouldFail(string? destination)
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: destination!,
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DestinationDistrict");
    }

    [Fact]
    public void Validate_PastDate_ShouldFail()
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TravelDate");
    }

    [Fact]
    public void Validate_DateBeyond7Days_ShouldFail()
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TravelDate");
    }

    [Fact]
    public void Validate_TodayDate_ShouldPass()
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Day6FromToday_ShouldPass()
    {
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Validate_BoundaryCoordinates_ShouldPass(double lat, double lon)
    {
        var request = new TravelRecommendationRequest(
            Latitude: lat,
            Longitude: lon,
            DestinationDistrict: "Dhaka",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
