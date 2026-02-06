using System.Net;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace SafeTravel.Integration.Tests.Scenarios;

/// <summary>
/// Integration tests for resilience scenarios (Redis down, Open-Meteo down).
/// These tests validate the system's graceful degradation behavior.
/// </summary>
public class ResilienceScenariosTests : IntegrationTestBase
{
    public ResilienceScenariosTests(IntegrationTestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OpenMeteoDown_WithCache_StillReturns200()
    {
        // Arrange - Pre-populate cache first (simulating warm cache)
        var cache = Factory.Services.GetRequiredService<IWeatherDataCache>();
        var rankings = TestDataBuilder.CreateCachedRankings(isStale: false);
        cache.SetRankings(rankings);

        // Now make Open-Meteo unavailable
        Factory.WireMockServer.SetupServiceUnavailable();

        // Act - Should still work because we have cached data
        var response = await Client.GetAsync("/api/v1/districts/top-10");

        // Assert - Should return data from cache
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValidRequest_WithFreshCache_ReturnsSuccessfully()
    {
        // Arrange - Pre-populate cache
        var cache = Factory.Services.GetRequiredService<IWeatherDataCache>();
        var rankings = TestDataBuilder.CreateCachedRankings();
        cache.SetRankings(rankings);

        // Act
        var response = await Client.GetAsync("/api/v1/districts/top-10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task ApiReturnsData_WhenWireMockConfigured()
    {
        // Arrange - Configure WireMock first before any cache is populated
        Factory.WireMockServer.SetupSuccessfulForecast();
        Factory.WireMockServer.SetupSuccessfulAirQuality();

        // Ensure we have cache
        var cache = Factory.Services.GetRequiredService<IWeatherDataCache>();
        var rankings = TestDataBuilder.CreateCachedRankings();
        cache.SetRankings(rankings);

        // Act
        var response = await Client.GetAsync("/api/v1/districts/top-10");

        // Assert - Should return 200 with data
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
