using Shouldly;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using SafeTravel.Application.DTOs;
using SafeTravel.Application.Handlers;
using SafeTravel.Application.Queries;
using SafeTravel.Application.Services;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Exceptions;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.Services;
using SafeTravel.Domain.ValueObjects;

namespace SafeTravel.Application.Tests.Handlers;

public class GetTravelRecommendationHandlerTests
{
    private readonly IValidator<TravelRecommendationRequest> _validator = Substitute.For<IValidator<TravelRecommendationRequest>>();
    private readonly IDistrictRepository _districtRepository = Substitute.For<IDistrictRepository>();
    private readonly IWeatherDataService _weatherDataService = Substitute.For<IWeatherDataService>();
    private readonly TravelRecommendationPolicy _recommendationPolicy = new();
    private readonly GetTravelRecommendationHandler _handler;

    public GetTravelRecommendationHandlerTests()
    {
        _handler = new GetTravelRecommendationHandler(
            _validator,
            _districtRepository,
            _weatherDataService,
            _recommendationPolicy);

        // Default: validation passes
        _validator.ValidateAsync(Arg.Any<TravelRecommendationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnRecommendation()
    {
        // Arrange
        var request = CreateValidRequest();
        var query = new GetTravelRecommendationQuery(request);

        var destination = District.Create("1", "Sylhet", 24.9, 91.8);
        _districtRepository.GetByName("Sylhet").Returns(destination);

        var originWeather = WeatherSnapshot.Create(request.TravelDate, 32.0, 80.0);
        var destWeather = WeatherSnapshot.Create(request.TravelDate, 25.0, 40.0);

        _weatherDataService.GetWeatherForCoordinatesAsync(
            Arg.Any<Coordinates>(), request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(originWeather);

        _weatherDataService.GetWeatherForDistrictAsync(
            destination, request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(destWeather);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsRecommended.ShouldBeTrue();
        result.DestinationDistrict.ShouldBe("Sylhet");
        result.TravelDate.ShouldBe(request.TravelDate);
    }

    [Fact]
    public async Task HandleAsync_DestinationNotCoolerOrCleaner_ShouldNotRecommend()
    {
        // Arrange
        var request = CreateValidRequest();
        var query = new GetTravelRecommendationQuery(request);

        var destination = District.Create("1", "Dhaka", 23.8, 90.4);
        _districtRepository.GetByName("Sylhet").Returns(destination);

        var originWeather = WeatherSnapshot.Create(request.TravelDate, 25.0, 30.0);
        var destWeather = WeatherSnapshot.Create(request.TravelDate, 28.0, 50.0); // Warmer and dirtier

        _weatherDataService.GetWeatherForCoordinatesAsync(
            Arg.Any<Coordinates>(), request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(originWeather);

        _weatherDataService.GetWeatherForDistrictAsync(
            Arg.Any<District>(), request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(destWeather);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsRecommended.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_DistrictNotFound_ShouldThrow()
    {
        // Arrange
        var request = CreateValidRequest();
        _districtRepository.GetByName("NonExistent").Returns((District?)null);

        // Act & Assert
        await Should.ThrowAsync<DistrictNotFoundException>(async () =>
            await _handler.HandleAsync(
                new GetTravelRecommendationQuery(request with { DestinationDistrict = "NonExistent" }),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ValidationFails_ShouldThrow()
    {
        // Arrange
        var request = CreateValidRequest();
        var query = new GetTravelRecommendationQuery(request);

        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Latitude", "Invalid latitude")
        });

        _validator.ValidateAsync(Arg.Any<TravelRecommendationRequest>(), Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act & Assert
        await Should.ThrowAsync<InvalidDateRangeException>(async () =>
            await _handler.HandleAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ShouldIncludeWeatherComparison()
    {
        // Arrange
        var request = CreateValidRequest();
        var query = new GetTravelRecommendationQuery(request);

        var destination = District.Create("1", "Sylhet", 24.9, 91.8);
        _districtRepository.GetByName("Sylhet").Returns(destination);

        var originWeather = WeatherSnapshot.Create(request.TravelDate, 32.0, 80.0);
        var destWeather = WeatherSnapshot.Create(request.TravelDate, 25.0, 40.0);

        _weatherDataService.GetWeatherForCoordinatesAsync(
            Arg.Any<Coordinates>(), request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(originWeather);

        _weatherDataService.GetWeatherForDistrictAsync(
            destination, request.TravelDate, Arg.Any<CancellationToken>())
            .Returns(destWeather);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.Comparison.ShouldNotBeNull();
        result.Comparison.OriginTemperature.ShouldBe(32.0);
        result.Comparison.OriginPM25.ShouldBe(80.0);
        result.Comparison.DestinationTemperature.ShouldBe(25.0);
        result.Comparison.DestinationPM25.ShouldBe(40.0);
    }

    private static TravelRecommendationRequest CreateValidRequest()
    {
        return new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
    }
}
