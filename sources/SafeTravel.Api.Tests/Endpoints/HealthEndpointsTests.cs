using System.Net;
using NSubstitute;
using SafeTravel.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace SafeTravel.Api.Tests.Endpoints;

public class HealthEndpointsTests : IClassFixture<SafeTravelApiFactory>
{
    private readonly SafeTravelApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointsTests(SafeTravelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LivenessCheck_ShouldAlwaysReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessCheck_ShouldReturnOk_WhenCacheIsHealthy()
    {
        // Arrange
        var metadata = new CacheMetadata(
            LastUpdated: DateTime.UtcNow.AddMinutes(-5),
            IsHealthy: true,
            DistrictsCached: 64);

        _factory.MockCache.GetMetadata().Returns(metadata);

        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessCheck_ShouldReturnServiceUnavailable_WhenCacheIsNull()
    {
        // Arrange
        _factory.MockCache.GetMetadata().Returns((CacheMetadata?)null);

        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ReadinessCheck_ShouldReturnServiceUnavailable_WhenDataIsStale()
    {
        // Arrange
        var metadata = new CacheMetadata(
            LastUpdated: DateTime.UtcNow.AddMinutes(-45), // Stale data (>30 min)
            IsHealthy: true,
            DistrictsCached: 64);

        _factory.MockCache.GetMetadata().Returns(metadata);

        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
