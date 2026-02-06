using Hangfire;
using Hangfire.InMemory;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.DependencyInjection;
using SafeTravel.Infrastructure.BackgroundJobs;
using StackExchange.Redis;

namespace SafeTravel.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring Hangfire with Redis storage.
/// </summary>
public static class HangfireServiceRegistration
{
    /// <summary>
    /// Add Hangfire services with optional Redis storage.
    /// Falls back to in-memory storage if Redis is unavailable.
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        string? redisConnectionString = null)
    {
        // Register the sync job
        services.AddScoped<WeatherDataSyncJob>();

        // Configure Hangfire storage
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                services.AddHangfire(config => config
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseRedisStorage(redis, new RedisStorageOptions
                    {
                        Prefix = "hangfire:safetravel:",
                        InvisibilityTimeout = TimeSpan.FromMinutes(30),
                        ExpiryCheckInterval = TimeSpan.FromMinutes(5)
                    }));
            }
            catch
            {
                // Fall back to in-memory if Redis connection fails
                ConfigureInMemoryStorage(services);
            }
        }
        else
        {
            // No Redis connection string - use in-memory
            ConfigureInMemoryStorage(services);
        }

        return services;
    }

    private static void ConfigureInMemoryStorage(IServiceCollection services)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
    }

    /// <summary>
    /// Schedule recurring background jobs.
    /// Call this after the application has started.
    /// </summary>
    public static void ConfigureRecurringJobs()
    {
        // Weather data sync every 10 minutes
        RecurringJob.AddOrUpdate<WeatherDataSyncJob>(
            "weather-data-sync",
            job => job.ExecuteAsync(CancellationToken.None),
            "*/10 * * * *", // Every 10 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }
}
