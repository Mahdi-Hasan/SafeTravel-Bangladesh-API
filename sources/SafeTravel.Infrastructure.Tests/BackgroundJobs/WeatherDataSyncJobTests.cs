using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SafeTravel.Application.Services;
using SafeTravel.Domain.Entities;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Domain.Models;
using SafeTravel.Domain.ValueObjects;
using SafeTravel.Infrastructure.BackgroundJobs;
using Shouldly;

namespace SafeTravel.Infrastructure.Tests.BackgroundJobs;

public class WeatherDataSyncJobTests
{
    private readonly IWeatherDataService _weatherDataService = Substitute.For<IWeatherDataService>();
    private readonly ILogger<WeatherDataSyncJob> _logger = Substitute.For<ILogger<WeatherDataSyncJob>>();

    public WeatherDataSyncJobTests()
    {
        // Reset static state before each test for isolation
        WeatherDataSyncJob.ResetForTesting();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallGetRankingsAsync()
    {
        // Arrange
        var job = CreateJob();
        SetupSuccessfulRankingsResponse();

        // Act
        await job.ExecuteAsync();

        // Assert
        await _weatherDataService.Received(1).GetRankingsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var job = CreateJob();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await job.ExecuteAsync(cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_ShouldRethrowForHangfireRetry()
    {
        // Arrange
        var job = CreateJob();
        var expectedException = new InvalidOperationException("API unavailable");

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(expectedException);

        // Act & Assert
        var thrownException = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await job.ExecuteAsync());

        thrownException.ShouldBeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogStartAndCompletion()
    {
        // Arrange
        var job = CreateJob();
        SetupSuccessfulRankingsResponse();

        // Act
        await job.ExecuteAsync();

        // Assert - verify logging was called (at least for start message)
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFails_ShouldLogError()
    {
        // Arrange
        var job = CreateJob();
        var exception = new InvalidOperationException("Test failure");

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        // Act
        try
        {
            await job.ExecuteAsync();
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - verify error logging was called
        _logger.ReceivedWithAnyArgs().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private WeatherDataSyncJob CreateJob()
    {
        return new WeatherDataSyncJob(_weatherDataService, _logger);
    }

    private void SetupSuccessfulRankingsResponse()
    {
        var district = District.Create("1", "Dhaka", 23.8, 90.4);
        var ranked = new RankedDistrict(
            rank: 1,
            district: district,
            avgTemperature: Temperature.FromCelsius(30.0),
            avgPM25: PM25Level.Create(50.0),
            generatedAt: DateTime.UtcNow);

        var rankings = new CachedRankings(
            [ranked],
            GeneratedAt: DateTime.UtcNow,
            ExpiresAt: DateTime.UtcNow.AddMinutes(20));

        _weatherDataService.GetRankingsAsync(Arg.Any<CancellationToken>())
            .Returns(rankings);
    }
}
