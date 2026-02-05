using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using SafeTravel.Domain.Interfaces;
using SafeTravel.Infrastructure.Caching;
using SafeTravel.Infrastructure.DataProviders;
using SafeTravel.Infrastructure.ExternalApis;
using static SafeTravel.Infrastructure.InfrastructureConstants;

namespace SafeTravel.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? redisConnectionString = null)
    {
        // District Repository
        services.AddSingleton<IDistrictRepository, DistrictDataProvider>();

        // Weather Data Cache (Redis with in-memory fallback)
        services.AddSingleton<IWeatherDataCache>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisWeatherDataCache>>();
            return new RedisWeatherDataCache(logger, redisConnectionString);
        });

        // OpenMeteo Client with Polly resilience
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>(client =>
            {
                client.Timeout = Resilience.TotalTimeout;
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        // Retry 3 times with exponential backoff (1s, 2s, 4s)
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: Resilience.RetryCount,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Logging handled by Polly.Extensions.Http
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        // Open circuit after 5 consecutive failures, stay open for 30 seconds
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: Resilience.CircuitBreakerThreshold,
                durationOfBreak: Resilience.CircuitBreakerBreakDuration);
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        // Timeout per-request: 10 seconds
        return Policy.TimeoutAsync<HttpResponseMessage>(Resilience.RequestTimeout);
    }
}
