using Microsoft.Extensions.Logging;
using SafeTravel.Application.Services;

namespace SafeTravel.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job that refreshes weather data cache periodically.
/// Designed to run every 10 minutes via Hangfire.
/// </summary>
public sealed class WeatherDataRefreshJob
{
    private readonly IWeatherDataService _weatherDataService;
    private readonly ILogger<WeatherDataRefreshJob> _logger;

    // For idempotency tracking
    private static readonly SemaphoreSlim ExecutionLock = new(1, 1);
    private static DateTime _lastSuccessfulExecution = DateTime.MinValue;
    private static readonly TimeSpan MinExecutionInterval = TimeSpan.FromMinutes(5);

    public WeatherDataRefreshJob(
        IWeatherDataService weatherDataService,
        ILogger<WeatherDataRefreshJob> logger)
    {
        _weatherDataService = weatherDataService ?? throw new ArgumentNullException(nameof(weatherDataService));
        _logger = logger;
    }

    /// <summary>
    /// Execute the weather data refresh job.
    /// This method is idempotent - concurrent executions are prevented.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Try to acquire lock (prevent concurrent execution)
        if (!await ExecutionLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogWarning("Weather refresh job skipped - another execution is in progress");
            return;
        }

        try
        {
            // Idempotency check: skip if recently executed
            var timeSinceLastExecution = DateTime.UtcNow - _lastSuccessfulExecution;
            if (timeSinceLastExecution < MinExecutionInterval)
            {
                _logger.LogInformation(
                    "Weather refresh job skipped - last execution was {TimeSinceLastExecution} ago",
                    timeSinceLastExecution);
                return;
            }

            _logger.LogInformation("Starting weather data refresh job");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Force cache refresh by fetching rankings (triggers ManualDataLoad)
            await _weatherDataService.GetRankingsAsync(cancellationToken);

            stopwatch.Stop();
            _lastSuccessfulExecution = DateTime.UtcNow;

            _logger.LogInformation(
                "Weather data refresh completed successfully in {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Weather data refresh job was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather data refresh job failed");
            throw; // Let Hangfire handle retry
        }
        finally
        {
            ExecutionLock.Release();
        }
    }
}
