using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SafeTravel.Domain.ValueObjects;
using SafeTravel.Infrastructure.ExternalApis;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SafeTravel.Infrastructure.Tests.ExternalApis;

/// <summary>
/// Integration tests for OpenMeteoClient using WireMock to simulate API responses.
/// Uses a DelegatingHandler to redirect requests from the real API to WireMock.
/// </summary>
public class OpenMeteoClientIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly OpenMeteoClient _sut;
    private readonly HttpClient _httpClient;

    public OpenMeteoClientIntegrationTests()
    {
        _server = WireMockServer.Start();

        // Create a handler that redirects all requests to WireMock server
        var redirectHandler = new RedirectToWireMockHandler(_server.Urls[0]);
        _httpClient = new HttpClient(redirectHandler);

        _sut = new OpenMeteoClient(_httpClient, NullLogger<OpenMeteoClient>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    #region GetBulkForecastAsync Tests

    [Fact]
    public async Task GetBulkForecastAsync_WithSingleLocation_ReturnsParsedData()
    {
        // Arrange
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": ["2026-02-05T00:00", "2026-02-05T01:00", "2026-02-05T02:00"],
                "temperature_2m": [25.5, 24.8, 24.2]
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldContainKey(location);
        result.Data[location].Count.ShouldBe(3);
        result.Data[location][0].TemperatureCelsius.ShouldBe(25.5);
        result.Data[location][1].TemperatureCelsius.ShouldBe(24.8);
        result.Data[location][2].TemperatureCelsius.ShouldBe(24.2);
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithMultipleLocations_ReturnsParsedData()
    {
        // Arrange - response for multiple locations
        var responseJson = """
        [
            {
                "latitude": 23.8,
                "longitude": 90.4,
                "hourly": {
                    "time": ["2026-02-05T00:00"],
                    "temperature_2m": [25.0]
                }
            },
            {
                "latitude": 22.3,
                "longitude": 91.8,
                "hourly": {
                    "time": ["2026-02-05T00:00"],
                    "temperature_2m": [27.0]
                }
            }
        ]
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var locations = new[]
        {
            Coordinates.Create(23.8, 90.4),
            Coordinates.Create(22.3, 91.8)
        };

        // Act
        var result = await _sut.GetBulkForecastAsync(locations, 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.Count.ShouldBe(2);
        result.Data[locations[0]][0].TemperatureCelsius.ShouldBe(25.0);
        result.Data[locations[1]][0].TemperatureCelsius.ShouldBe(27.0);
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithNullTemperatureValues_SkipsNullEntries()
    {
        // Arrange - response with some null temperature values
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": ["2026-02-05T00:00", "2026-02-05T01:00", "2026-02-05T02:00"],
                "temperature_2m": [25.5, null, 24.2]
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None);

        // Assert
        result.Data[location].Count.ShouldBe(2); // Null entry skipped
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithEmptyLocations_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _sut.GetBulkForecastAsync([], 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithServerError_ThrowsHttpRequestException()
    {
        // Arrange
        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        var location = Coordinates.Create(23.8, 90.4);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None));
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithBadRequest_ThrowsHttpRequestException()
    {
        // Arrange
        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBody("Bad Request"));

        var location = Coordinates.Create(23.8, 90.4);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None));
    }

    #endregion

    #region GetBulkAirQualityAsync Tests

    [Fact]
    public async Task GetBulkAirQualityAsync_WithSingleLocation_ReturnsParsedData()
    {
        // Arrange
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": ["2026-02-05T00:00", "2026-02-05T01:00"],
                "pm2_5": [35.5, 40.2]
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/air-quality")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkAirQualityAsync([location], 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldContainKey(location);
        result.Data[location].Count.ShouldBe(2);
        result.Data[location][0].PM25.ShouldBe(35.5);
        result.Data[location][1].PM25.ShouldBe(40.2);
    }

    [Fact]
    public async Task GetBulkAirQualityAsync_WithMultipleLocations_ReturnsParsedData()
    {
        // Arrange
        var responseJson = """
        [
            {
                "latitude": 23.8,
                "longitude": 90.4,
                "hourly": {
                    "time": ["2026-02-05T00:00"],
                    "pm2_5": [35.0]
                }
            },
            {
                "latitude": 22.3,
                "longitude": 91.8,
                "hourly": {
                    "time": ["2026-02-05T00:00"],
                    "pm2_5": [45.0]
                }
            }
        ]
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/air-quality")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var locations = new[]
        {
            Coordinates.Create(23.8, 90.4),
            Coordinates.Create(22.3, 91.8)
        };

        // Act
        var result = await _sut.GetBulkAirQualityAsync(locations, 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.Count.ShouldBe(2);
        result.Data[locations[0]][0].PM25.ShouldBe(35.0);
        result.Data[locations[1]][0].PM25.ShouldBe(45.0);
    }

    [Fact]
    public async Task GetBulkAirQualityAsync_WithNullPM25Values_SkipsNullEntries()
    {
        // Arrange
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": ["2026-02-05T00:00", "2026-02-05T01:00", "2026-02-05T02:00"],
                "pm2_5": [35.5, null, 40.2]
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/air-quality")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkAirQualityAsync([location], 7, CancellationToken.None);

        // Assert
        result.Data[location].Count.ShouldBe(2); // Null entry skipped
    }

    [Fact]
    public async Task GetBulkAirQualityAsync_WithEmptyLocations_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _sut.GetBulkAirQualityAsync([], 7, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBulkAirQualityAsync_WithServerError_ThrowsHttpRequestException()
    {
        // Arrange
        _server.Given(Request.Create()
                .WithPath("/v1/air-quality")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody("Service Unavailable"));

        var location = Coordinates.Create(23.8, 90.4);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await _sut.GetBulkAirQualityAsync([location], 7, CancellationToken.None));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetBulkForecastAsync_WithEmptyHourlyData_ReturnsEmptyList()
    {
        // Arrange
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": [],
                "temperature_2m": []
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None);

        // Assert
        result.Data[location].ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBulkForecastAsync_WithMismatchedArrayLengths_UsesMinimumLength()
    {
        // Arrange - more times than temperatures
        var responseJson = """
        {
            "latitude": 23.8,
            "longitude": 90.4,
            "hourly": {
                "time": ["2026-02-05T00:00", "2026-02-05T01:00", "2026-02-05T02:00"],
                "temperature_2m": [25.5]
            }
        }
        """;

        _server.Given(Request.Create()
                .WithPath("/v1/forecast")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(responseJson));

        var location = Coordinates.Create(23.8, 90.4);

        // Act
        var result = await _sut.GetBulkForecastAsync([location], 7, CancellationToken.None);

        // Assert
        result.Data[location].Count.ShouldBe(1);
    }

    #endregion

    /// <summary>
    /// Handler that redirects requests from the real Open-Meteo API to WireMock server.
    /// </summary>
    private sealed class RedirectToWireMockHandler : DelegatingHandler
    {
        private readonly string _wireMockBaseUrl;

        public RedirectToWireMockHandler(string wireMockBaseUrl)
        {
            _wireMockBaseUrl = wireMockBaseUrl.TrimEnd('/');
            InnerHandler = new HttpClientHandler();
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Redirect the request to WireMock
            var originalUri = request.RequestUri!;
            var newUri = new Uri($"{_wireMockBaseUrl}{originalUri.PathAndQuery}");
            request.RequestUri = newUri;

            return base.SendAsync(request, cancellationToken);
        }
    }
}
