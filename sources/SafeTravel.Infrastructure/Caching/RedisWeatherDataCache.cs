using System.Text.Json;
using Microsoft.Extensions.Logging;
using SafeTravel.Domain;
using SafeTravel.Domain.Interfaces;
using StackExchange.Redis;
using static SafeTravel.Infrastructure.InfrastructureConstants;

namespace SafeTravel.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of IWeatherDataCache with in-memory fallback.
/// </summary>
public sealed class RedisWeatherDataCache : IWeatherDataCache, IDisposable
{
    private readonly ILogger<RedisWeatherDataCache> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDatabase? _database;

    // In-memory fallback when Redis is unavailable
    private readonly InMemoryCache _fallback = new();
    private bool _useInMemoryFallback;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisWeatherDataCache(ILogger<RedisWeatherDataCache> logger, string? connectionString = null)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Redis connection string not provided, using in-memory fallback");
            _useInMemoryFallback = true;
            return;
        }

        try
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _database = _redis.GetDatabase();
            _logger.LogInformation("Connected to Redis successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis, falling back to in-memory cache");
            _useInMemoryFallback = true;
        }
    }

    public async Task<CachedRankings?> GetRankingsAsync(CancellationToken cancellationToken = default)
    {
        if (_useInMemoryFallback)
        {
            var result = _fallback.GetRankings();
            LogCacheAccess("GetRankings", Redis.RankingsKey, result != null);
            return result;
        }

        try
        {
            var data = await _database!.StringGetAsync(Redis.RankingsKey).ConfigureAwait(false);
            if (data.IsNullOrEmpty)
            {
                LogCacheAccess("GetRankings", Redis.RankingsKey, false);
                return null;
            }

            LogCacheAccess("GetRankings", Redis.RankingsKey, true);
            return JsonSerializer.Deserialize<CachedRankings>(data.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error getting rankings, falling back to memory");
            SwitchToFallback();
            return _fallback.GetRankings();
        }
    }

    public async Task SetRankingsAsync(CachedRankings rankings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(rankings, JsonOptions);

        if (_useInMemoryFallback)
        {
            _fallback.SetRankings(rankings);
            _logger.LogDebug("Cached rankings in memory");
            return;
        }

        try
        {
            await _database!.StringSetAsync(
                Redis.RankingsKey,
                json,
                SafeTravelConstants.DefaultCacheTtl).ConfigureAwait(false);
            _logger.LogDebug("Cached rankings in Redis with TTL {Ttl}", SafeTravelConstants.DefaultCacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error setting rankings, falling back to memory");
            SwitchToFallback();
            _fallback.SetRankings(rankings);
        }
    }

    public async Task<CachedDistrictForecast?> GetDistrictForecastAsync(string districtId, CancellationToken cancellationToken = default)
    {
        var key = string.Format(Redis.DistrictForecastKeyPattern, districtId);

        if (_useInMemoryFallback)
        {
            var result = _fallback.GetDistrictForecast(districtId);
            LogCacheAccess("GetDistrictForecast", key, result != null);
            return result;
        }

        try
        {
            var data = await _database!.StringGetAsync(key).ConfigureAwait(false);
            if (data.IsNullOrEmpty)
            {
                LogCacheAccess("GetDistrictForecast", key, false);
                return null;
            }

            LogCacheAccess("GetDistrictForecast", key, true);
            return JsonSerializer.Deserialize<CachedDistrictForecast>(data.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error getting district forecast for {DistrictId}", districtId);
            SwitchToFallback();
            return _fallback.GetDistrictForecast(districtId);
        }
    }

    public async Task SetDistrictForecastAsync(string districtId, CachedDistrictForecast forecast, CancellationToken cancellationToken = default)
    {
        var key = string.Format(Redis.DistrictForecastKeyPattern, districtId);
        var json = JsonSerializer.Serialize(forecast, JsonOptions);

        if (_useInMemoryFallback)
        {
            _fallback.SetDistrictForecast(districtId, forecast);
            return;
        }

        try
        {
            await _database!.StringSetAsync(key, json, SafeTravelConstants.DefaultCacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error setting district forecast for {DistrictId}", districtId);
            SwitchToFallback();
            _fallback.SetDistrictForecast(districtId, forecast);
        }
    }

    public async Task<CacheMetadata?> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_useInMemoryFallback)
        {
            return _fallback.GetMetadata();
        }

        try
        {
            var data = await _database!.StringGetAsync(Redis.MetadataKey).ConfigureAwait(false);
            return data.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<CacheMetadata>(data.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis error getting metadata");
            return _fallback.GetMetadata();
        }
    }

    private void SwitchToFallback()
    {
        if (!_useInMemoryFallback)
        {
            _useInMemoryFallback = true;
            _logger.LogWarning("Switched to in-memory cache fallback due to Redis errors");
        }
    }

    private void LogCacheAccess(string operation, string key, bool hit)
    {
        if (hit)
            _logger.LogDebug("Cache HIT: {Operation} for key {Key}", operation, key);
        else
            _logger.LogDebug("Cache MISS: {Operation} for key {Key}", operation, key);
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }

    /// <summary>
    /// Simple in-memory cache for fallback scenarios.
    /// </summary>
    private sealed class InMemoryCache
    {
        private CachedRankings? _rankings;
        private readonly Dictionary<string, CachedDistrictForecast> _districtForecasts = new();
        private readonly object _lock = new();

        public CachedRankings? GetRankings()
        {
            lock (_lock)
            {
                if (_rankings == null) return null;
                if (DateTime.UtcNow > _rankings.ExpiresAt) return null;
                return _rankings;
            }
        }

        public void SetRankings(CachedRankings rankings)
        {
            lock (_lock)
            {
                _rankings = rankings;
            }
        }

        public CachedDistrictForecast? GetDistrictForecast(string districtId)
        {
            lock (_lock)
            {
                return _districtForecasts.GetValueOrDefault(districtId);
            }
        }

        public void SetDistrictForecast(string districtId, CachedDistrictForecast forecast)
        {
            lock (_lock)
            {
                _districtForecasts[districtId] = forecast;
            }
        }

        public CacheMetadata? GetMetadata()
        {
            lock (_lock)
            {
                if (_rankings == null) return null;
                return new CacheMetadata(
                    LastUpdated: _rankings.GeneratedAt,
                    IsHealthy: true,
                    DistrictsCached: _districtForecasts.Count);
            }
        }
    }
}
