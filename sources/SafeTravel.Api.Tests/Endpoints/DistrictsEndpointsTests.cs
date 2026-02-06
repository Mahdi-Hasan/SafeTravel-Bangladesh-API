using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using SafeTravel.Application.DTOs;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using Shouldly;
using Xunit;

namespace SafeTravel.Api.Tests.Endpoints;

public class DistrictsEndpointsTests : IClassFixture<SafeTravelApiFactory>
{
    private readonly SafeTravelApiFactory _factory;
    private readonly HttpClient _client;

    public DistrictsEndpointsTests(SafeTravelApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTop10Districts_ShouldReturnOk_WhenDataIsAvailable()
    {
        // Arrange
        var dhaka = District.Create("01", "Dhaka", 23.8103, 90.4125);
        var chattogram = District.Create("02", "Chattogram", 22.3569, 91.7832);
        var sylhet = District.Create("03", "Sylhet", 24.8949, 91.8687);

        var mockRankings = new CachedRankings(
            Rankings: new List<RankedDistrict>
            {
                RankedDistrict.Create(1, dhaka, 25.0, 30.0),
                RankedDistrict.Create(2, chattogram, 24.5, 29.5),
                RankedDistrict.Create(3, sylhet, 24.0, 29.0)
            },
            GeneratedAt: DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt: DateTime.UtcNow.AddMinutes(15));

        _factory.MockCache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns(mockRankings);

        // Act
        var response = await _client.GetAsync("/api/v1/districts/top-10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Top10DistrictsResponse>();
        result.ShouldNotBeNull();
        result.Districts.ShouldNotBeEmpty();
        result.Districts.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetTop10Districts_ShouldIncludeMetadata()
    {
        // Arrange
        var generatedAt = DateTime.UtcNow.AddMinutes(-5);
        var mockRankings = new CachedRankings(
            Rankings: new List<RankedDistrict>(),
            GeneratedAt: generatedAt,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15));

        _factory.MockCache.GetRankingsAsync(Arg.Any<CancellationToken>()).Returns(mockRankings);

        // Act
        var response = await _client.GetAsync("/api/v1/districts/top-10");
        var result = await response.Content.ReadFromJsonAsync<Top10DistrictsResponse>();

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.ShouldNotBeNull();
        result.Metadata.GeneratedAt.ShouldBe(generatedAt);
    }
}
