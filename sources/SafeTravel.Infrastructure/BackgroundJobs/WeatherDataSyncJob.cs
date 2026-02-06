using Hangfire;
using Microsoft.Extensions.Logging;
using SafeTravel.Application.Services;

namespace SafeTravel.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job that syncs weather data cache periodically.
/// Designed to run every 10 minutes via Hangfire.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]
public sealed class WeatherDataSyncJob
{
    private readonly IWeatherDataService _weatherDataService;
    private readonly ILogger<WeatherDataSyncJob> _logger;

    // For idempotency tracking
    private static readonly SemaphoreSlim ExecutionLock = new(1, 1);
    private static DateTime _lastSuccessfulExecution = DateTime.MinValue;
    private static readonly TimeSpan MinExecutionInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Reset static state for testing purposes only.
    /// </summary>
    internal static void ResetForTesting()
    {
        _lastSuccessfulExecution = DateTime.MinValue;
    }

    public WeatherDataSyncJob(
        IWeatherDataService weatherDataService,
        ILogger<WeatherDataSyncJob> logger)
    {
        _weatherDataService = weatherDataService ?? throw new ArgumentNullException(nameof(weatherDataService));
        _logger = logger;
    }

    /// <summary>
    /// Execute the weather data sync job.
    /// This method is idempotent - concurrent executions are prevented.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Try to acquire lock (prevent concurrent execution)
        if (!await ExecutionLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogWarning("Weather sync job skipped - another execution is in progress");
            return;
        }

        try
        {
            // Idempotency check: skip if recently executed
            var timeSinceLastExecution = DateTime.UtcNow - _lastSuccessfulExecution;
            if (timeSinceLastExecution < MinExecutionInterval)
            {
                _logger.LogInformation(
                    "Weather sync job skipped - last execution was {TimeSinceLastExecution} ago",
                    timeSinceLastExecution);
                return;
            }

            _logger.LogInformation("Starting weather data sync job");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Force cache refresh by fetching rankings (triggers ManualDataLoad)
            await _weatherDataService.GetRankingsAsync(cancellationToken);

            stopwatch.Stop();
            _lastSuccessfulExecution = DateTime.UtcNow;

            _logger.LogInformation(
                "Weather data sync completed successfully in {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Weather data sync job was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather data sync job failed");
            throw; // Let Hangfire handle retry
        }
        finally
        {
            ExecutionLock.Release();
        }
    }
}
