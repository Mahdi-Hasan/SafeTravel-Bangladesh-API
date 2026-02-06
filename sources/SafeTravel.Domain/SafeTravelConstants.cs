namespace SafeTravel.Domain;

/// <summary>
/// Application-wide constants for the SafeTravel domain.
/// </summary>
public static class SafeTravelConstants
{
    /// <summary>
    /// Cache staleness threshold. Data older than this is considered stale
    /// and will trigger a manual data refresh.
    /// </summary>
    public static readonly TimeSpan CacheStalenessThreshold = TimeSpan.FromMinutes(12);

    /// <summary>
    /// Default cache TTL (time-to-live) for cached data.
    /// </summary>
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(25);

    /// <summary>
    /// Number of days to fetch forecast data for.
    /// </summary>
    public const int ForecastDays = 7;

    /// <summary>
    /// Number of top districts to return in rankings.
    /// </summary>
    public const int TopDistrictsCount = 10;
}
