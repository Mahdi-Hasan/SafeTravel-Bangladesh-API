using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SafeTravel.Integration.Tests.Infrastructure;

/// <summary>
/// Helper methods for configuring WireMock responses for Open-Meteo API.
/// </summary>
public static class WireMockOpenMeteoHelper
{
    /// <summary>
    /// Configures WireMock to return successful weather forecast data.
    /// </summary>
    public static void SetupSuccessfulForecast(this WireMockServer server, double temperature = 25.0)
    {
        var responseJson = BuildForecastJson(temperature);

        server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));
    }

    /// <summary>
    /// Configures WireMock to return successful air quality data.
    /// </summary>
    public static void SetupSuccessfulAirQuality(this WireMockServer server, double pm25 = 30.0)
    {
        var responseJson = BuildAirQualityJson(pm25);

        server.Given(Request.Create()
                .WithPath("/v1/air-quality")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));
    }

    /// <summary>
    /// Configures WireMock to return 503 Service Unavailable for all endpoints.
    /// </summary>
    public static void SetupServiceUnavailable(this WireMockServer server)
    {
        server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody("Service Unavailable"));
    }

    /// <summary>
    /// Configures WireMock to simulate a timeout.
    /// </summary>
    public static void SetupTimeout(this WireMockServer server)
    {
        server.Given(Request.Create().UsingGet())
            .RespondWith(Response.Create()
                .WithDelay(TimeSpan.FromSeconds(60)));
    }

    /// <summary>
    /// Clears all mappings from the WireMock server.
    /// </summary>
    public static void ResetMappings(this WireMockServer server)
    {
        server.Reset();
    }

    private static string BuildForecastJson(double temperature)
    {
        return """
        [
            {"latitude": 23.81, "longitude": 90.41, "hourly": {"time": ["2026-02-06T14:00"], "temperature_2m": [25.0]}},
            {"latitude": 22.36, "longitude": 91.78, "hourly": {"time": ["2026-02-06T14:00"], "temperature_2m": [26.0]}},
            {"latitude": 24.89, "longitude": 91.87, "hourly": {"time": ["2026-02-06T14:00"], "temperature_2m": [27.0]}}
        ]
        """;
    }

    private static string BuildAirQualityJson(double pm25)
    {
        return """
        [
            {"latitude": 23.81, "longitude": 90.41, "hourly": {"time": ["2026-02-06T14:00"], "pm2_5": [30.0]}},
            {"latitude": 22.36, "longitude": 91.78, "hourly": {"time": ["2026-02-06T14:00"], "pm2_5": [35.0]}},
            {"latitude": 24.89, "longitude": 91.87, "hourly": {"time": ["2026-02-06T14:00"], "pm2_5": [40.0]}}
        ]
        """;
    }
}

/// <summary>
/// Provides test data for integration tests.
/// </summary>
public static class TestDataBuilder
{
    public static District Dhaka => District.Create("01", "Dhaka", 23.8103, 90.4125);
    public static District Chattogram => District.Create("02", "Chattogram", 22.3569, 91.7832);
    public static District Sylhet => District.Create("03", "Sylhet", 24.8949, 91.8687);

    public static IReadOnlyList<District> AllDistricts => [Dhaka, Chattogram, Sylhet];

    public static CachedRankings CreateCachedRankings(
        DateTime? generatedAt = null,
        DateTime? expiresAt = null,
        bool isStale = false)
    {
        var now = DateTime.UtcNow;
        var generated = generatedAt ?? (isStale ? now.AddMinutes(-15) : now.AddMinutes(-5));
        var expires = expiresAt ?? now.AddMinutes(15);

        return new CachedRankings(
            Rankings: [
                RankedDistrict.Create(1, Sylhet, 24.0, 25.0),
                RankedDistrict.Create(2, Chattogram, 25.0, 30.0),
                RankedDistrict.Create(3, Dhaka, 26.0, 35.0)
            ],
            GeneratedAt: generated,
            ExpiresAt: expires);
    }

    public static CachedDistrictForecast CreateDistrictForecast(District district)
    {
        var snapshots = new List<WeatherSnapshot>();
        for (var i = 0; i < 7; i++)
        {
            snapshots.Add(WeatherSnapshot.Create(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i)),
                25.0 + i,
                30.0 + i));
        }

        return new CachedDistrictForecast(
            DistrictId: district.Id,
            Forecasts: snapshots,
            GeneratedAt: DateTime.UtcNow);
    }
}
