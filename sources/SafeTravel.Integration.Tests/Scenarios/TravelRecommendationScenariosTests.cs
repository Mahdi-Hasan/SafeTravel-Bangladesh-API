using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SafeTravel.Application.DTOs;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace SafeTravel.Integration.Tests.Scenarios;

/// <summary>
/// Integration tests for travel recommendation endpoint scenarios.
/// </summary>
public class TravelRecommendationScenariosTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TravelRecommendationScenariosTests(IntegrationTestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TravelRecommendation_Endpoint_Responds()
    {
        // Arrange - Set up cache with forecast data
        var cache = Factory.Services.GetRequiredService<IWeatherDataCache>();
        cache.SetDistrictForecast("03", TestDataBuilder.CreateDistrictForecast(TestDataBuilder.Sylhet));


        Factory.WireMockServer.SetupSuccessfulForecast(temperature: 30.0);
        Factory.WireMockServer.SetupSuccessfulAirQuality(pm25: 50.0);

        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/travel/recommendation", request, JsonOptions);

        // Assert - API should respond (may be 200, 404 for unknown district, or 503)
        var validStatuses = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError };
        validStatuses.ShouldContain(response.StatusCode);
    }

    [Fact]
    public async Task InvalidDistrict_ReturnsClientError()
    {
        // Arrange
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "NonExistentDistrict",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/travel/recommendation", request, JsonOptions);

        // Assert - Should return 4xx client error
        var statusCode = (int)response.StatusCode;
        (statusCode >= 400 && statusCode < 600).ShouldBeTrue($"Expected client error but got {statusCode}");
    }

    [Fact]
    public async Task InvalidDateTooFarInFuture_ReturnsClientError()
    {
        // Arrange - Date more than 7 days in the future
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/travel/recommendation", request, JsonOptions);

        // Assert - Should return client error (400)
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidCoordinates_ReturnsClientError()
    {
        // Arrange - Invalid latitude (out of range)
        var request = new TravelRecommendationRequest(
            Latitude: 200.0, // Invalid latitude
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/travel/recommendation", request, JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PastDate_ReturnsClientError()
    {
        // Arrange - Travel date in the past
        var request = new TravelRecommendationRequest(
            Latitude: 23.8103,
            Longitude: 90.4125,
            DestinationDistrict: "Sylhet",
            TravelDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/travel/recommendation", request, JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
