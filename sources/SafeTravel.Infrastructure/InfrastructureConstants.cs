namespace SafeTravel.Infrastructure;

/// <summary>
/// Infrastructure-specific constants for external APIs and services.
/// </summary>
public static class InfrastructureConstants
{
    public static class OpenMeteoApi
    {
        /// <summary>
        /// Base URL for Open-Meteo weather forecast API.
        /// </summary>
        public const string WeatherBaseUrl = "https://api.open-meteo.com/v1/forecast";

        /// <summary>
        /// Base URL for Open-Meteo air quality API.
        /// </summary>
        public const string AirQualityBaseUrl = "https://air-quality-api.open-meteo.com/v1/air-quality";
    }

    public static class Resilience
    {
        /// <summary>
        /// Number of retry attempts for transient failures.
        /// </summary>
        public const int RetryCount = 3;

        /// <summary>
        /// Number of consecutive failures before circuit breaker opens.
        /// </summary>
        public const int CircuitBreakerThreshold = 5;

        /// <summary>
        /// Duration the circuit breaker stays open.
        /// </summary>
        public static readonly TimeSpan CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Timeout for individual HTTP requests.
        /// </summary>
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Total timeout for HTTP client operations.
        /// </summary>
        public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(30);
    }

    public static class Redis
    {
        /// <summary>
        /// Redis key prefix for all SafeTravel cache entries.
        /// </summary>
        public const string KeyPrefix = "safetravel";

        /// <summary>
        /// Redis key for rankings cache.
        /// </summary>
        public const string RankingsKey = "safetravel:rankings";

        /// <summary>
        /// Redis key pattern for district forecasts.
        /// </summary>
        public const string DistrictForecastKeyPattern = "safetravel:districts:{0}";

        /// <summary>
        /// Redis key for cache metadata.
        /// </summary>
        public const string MetadataKey = "safetravel:metadata";
    }
}
