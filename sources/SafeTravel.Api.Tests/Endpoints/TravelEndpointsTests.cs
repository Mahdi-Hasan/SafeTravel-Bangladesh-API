using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using SafeTravel.Application.DTOs;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using Shouldly;

namespace SafeTravel.Api.Tests.Endpoints;

public class TravelEndpointsTests : IClassFixture<SafeTravelApiFactory>
{
    private readonly SafeTravelApiFactory _factory;
    private readonly HttpClient _client;

    public TravelEndpointsTests(SafeTravelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTravelRecommendation_ShouldReturnBadRequest_WhenInvalidLatitude()
    {
        // Arrange
        var request = new TravelRecommendationRequest(
            Latitude: 100.0, // Invalid: outside [-90, 90]
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/travel/recommendation", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTravelRecommendation_ShouldReturnBadRequest_WhenInvalidLongitude()
    {
        // Arrange
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 200.0, // Invalid: outside [-180, 180]
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/travel/recommendation", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTravelRecommendation_ShouldReturnBadRequest_WhenEmptyDestination()
    {
        // Arrange
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "", // Empty destination
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/travel/recommendation", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTravelRecommendation_ShouldReturnBadRequest_WhenDateTooFarInFuture()
    {
        // Arrange
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))); // Too far: > 7 days

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/travel/recommendation", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
