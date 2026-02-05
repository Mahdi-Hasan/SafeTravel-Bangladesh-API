# API Design & Operations

**Part of:** [SafeTravel Bangladesh API Technical Design](../technical_design_document.md)

**Version:** 1.0
**Date:** February 4, 2026

---

## Table of Contents

1. [API Design](#1-api-design)
2. [Failure Scenarios & Recovery](#2-failure-scenarios--recovery)
3. [Observability & Monitoring](#3-observability--monitoring)
4. [Security & Rate Limiting](#4-security--rate-limiting)

---

## 1. API Design

### 1.1 Endpoints

| Method | Endpoint                          | Description                                |
| ------ | --------------------------------- | ------------------------------------------ |
| GET    | `/api/v1/districts/top10`       | Get top 10 coolest & cleanest districts    |
| POST   | `/api/v1/travel/recommendation` | Get travel recommendation for destination  |
| GET    | `/health/live`                  | Liveness probe (app is running)            |
| GET    | `/health/ready`                 | Readiness probe (Redis + last sync status) |
| GET    | `/hangfire`                     | Hangfire dashboard (secured)               |

### 1.2 Request/Response Models

#### GET /api/v1/districts/top10

**Response (200 OK):**

```json
{
  "data": [
    {
      "rank": 1,
      "districtId": "sylhet",
      "districtName": "Sylhet",
      "averageTemperature": 24.5,
      "averageTemperatureUnit": "°C",
      "averagePM25": 12.3,
      "pm25Unit": "μg/m³",
      "airQualityCategory": "Good"
    }
  ],
  "metadata": {
    "generatedAt": "2026-02-04T10:00:00Z",
    "forecastPeriod": {
      "start": "2026-02-04",
      "end": "2026-02-10"
    },
    "isStale": false
  }
}
```

**Response Headers:**

```
X-Data-Generated-At: 2026-02-04T10:00:00Z
X-Data-Is-Stale: false
Cache-Control: public, max-age=60
```

#### POST /api/v1/travel/recommendation

**Request:**

```json
{
  "currentLocation": {
    "latitude": 23.8103,
    "longitude": 90.4125
  },
  "destinationDistrict": "Sylhet",
  "travelDate": "2026-02-06"
}
```

**Request Field Specifications:**

| Field                         | Type   | Format                    | Required | Description                            |
| ----------------------------- | ------ | ------------------------- | -------- | -------------------------------------- |
| `currentLocation.latitude`  | number | Decimal                   | Yes      | User's current latitude (-90 to 90)    |
| `currentLocation.longitude` | number | Decimal                   | Yes      | User's current longitude (-180 to 180) |
| `destinationDistrict`       | string | Text                      | Yes      | District name (case-insensitive)       |
| `travelDate`                | string | `yyyy-MM-dd` (ISO 8601) | Yes      | Travel date in Asia/Dhaka timezone     |

**Response (200 OK - Recommended):**

```json
{
  "recommendation": "Recommended",
  "reason": "Sylhet is 4.2°C cooler (26.3°C vs 30.5°C) and has 35% better air quality (PM2.5: 15.2 vs 23.4 μg/m³) compared to your current location on February 6.",
  "comparison": {
    "currentLocation": {
      "temperature": 30.5,
      "pm25": 23.4
    },
    "destination": {
      "districtName": "Sylhet",
      "temperature": 26.3,
      "pm25": 15.2
    },
    "travelDate": "2026-02-06"
  },
  "metadata": {
    "generatedAt": "2026-02-04T10:00:00Z",
    "isStale": false
  }
}
```

**Response (200 OK - Not Recommended):**

```json
{
  "recommendation": "Not Recommended",
  "reason": "While Sylhet is 2.1°C cooler, the air quality is worse (PM2.5: 28.5 vs 23.4 μg/m³). For a recommended destination, both temperature and air quality must be better.",
  "comparison": { ... }
}
```

**Response (400 Bad Request):**

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "errors": {
    "travelDate": ["Travel date must be within the next 7 days."],
    "destinationDistrict": ["District 'InvalidName' not found."]
  }
}
```

**Response (503 Service Unavailable):**

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.4",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Weather data is temporarily unavailable. External API down and no cached data exists.",
  "retryAfter": 60
}
```

**Response Headers:**

```
Retry-After: 60
```

### 1.3 API Optimizations

#### 1.3.1 Response Compression

The API layer implements **Brotli** and **Gzip** compression to reduce bandwidth and improve response times.

**Configuration (Pseudo):**

```
ResponseCompression:
    Providers: [Brotli, Gzip]
    MimeTypes:
        - application/json
        - text/plain
    Level: Optimal (balance between speed and size)
  
    Brotli:
        Quality: 4 (fast compression)
        Window: 22
  
    Gzip:
        Level: 5 (default)
```

**Middleware Registration:**

```
app.UseResponseCompression()  // Before UseRouting()
app.UseRouting()
app.MapEndpoints()
```

**Response Headers:**

```
Content-Encoding: br  // or gzip based on Accept-Encoding
Vary: Accept-Encoding
```

**Benefits:**

- Reduces JSON payload size by ~60-70%
- Top 10 districts response: ~2KB → ~600 bytes
- Travel recommendation response: ~1.5KB → ~450 bytes

#### 1.3.2 E-Tag Caching

Implements **strong ETags** based on cache version to enable client-side conditional requests.

**ETag Generation (Pseudo):**

```
GenerateETag(data):
    cacheVersion = data.Metadata.GeneratedAt.ToUnixTimestamp()
    content = Hash(cacheVersion + ":" + data.ToString())
    RETURN Quote(content)  // "abc123def456"
```

**Request/Response Flow:**

```
// Initial Request
GET /api/v1/districts/top10
→ Response: 200 OK
   ETag: "v1-20260204-100000"
   Cache-Control: public, max-age=60

// Subsequent Request (Client sends ETag)
GET /api/v1/districts/top10
If-None-Match: "v1-20260204-100000"

→ IF ETag matches THEN
      Response: 304 Not Modified (no body)
  ELSE
      Response: 200 OK (full response with new ETag)
```

**Configuration:**

| Endpoint                          | ETag Strategy               | Cache-Control          |
| :-------------------------------- | :-------------------------- | :--------------------- |
| `/api/v1/districts/top10`       | Strong ETag (version-based) | `public, max-age=60` |
| `/api/v1/travel/recommendation` | Strong ETag (version-based) | `public, max-age=60` |

---

## 2. Failure Scenarios & Recovery

### 2.1 Failure Matrix

| Scenario                         | Detection            | Mitigation                    | Recovery                             |
| -------------------------------- | -------------------- | ----------------------------- | ------------------------------------ |
| **Open-Meteo API down**    | HTTP 5xx, timeout    | Hangfire retries (3 attempts) | Serve stale cache or fail gracefully |
| **Open-Meteo rate limit**  | HTTP 429             | Exponential backoff           | Auto-retry after delay               |
| **Redis unavailable**      | Connection exception | Fallback to Open-Meteo API    | Auto-reconnect                       |
| **Cache miss (empty)**     | Null cache read      | Fallback to Open-Meteo API    | Background job populates cache       |
| **App restart during job** | Hangfire detects     | Job re-queued                 | Automatic resume                     |
| **Corrupt API response**   | JSON parse error     | Fail job, alert               | Manual investigation                 |

### 2.2 Fallback Strategy (Pseudo)

```
GetData():
    stalenessThreshold = 12 minutes  // 10 min job + 2 min buffer
  
    // Step 1: Check Redis Cache
    TRY:
        cacheEntry = Redis.Get(key)
  
        IF cacheEntry IS NOT NULL THEN
            dataAge = Now - cacheEntry.GeneratedAt
  
            IF dataAge <= stalenessThreshold THEN
                // Fresh data - return immediately
                RETURN cacheEntry
            ELSE
                // Data is stale - check if background job is running
                IF Hangfire.IsJobRunning("weather-data-sync") THEN
                    Log.Info("Cache stale but job running, returning stale")
                    RETURN cacheEntry
                ELSE
                    // No job running - manually trigger data loader
                    GOTO ManualDataLoad
                END IF
            END IF
    CATCH RedisException:
        Log.Warning("Redis unavailable")
  
    ManualDataLoad:
    // Step 2: Manual Data Loader - Direct API Call
    freshData = OpenMeteoClient.FetchFreshData()
  
    // Best effort: try to populate cache
    TRY:
        Redis.Set(key, freshData, TTL: 20min)
    CATCH:
        Log.Warning("Failed to update cache")
  
    RETURN freshData
```

---

## 3. Observability & Monitoring

### 3.1 Logging Infrastructure (Serilog + Grafana + Loki)

#### 3.1.1 Why Serilog + Grafana + Loki

| Decision                    | Choice  | Justification                                                                                  |
| --------------------------- | ------- | ---------------------------------------------------------------------------------------------- |
| **Logging Framework** | Serilog | Industry-standard structured logging for .NET, rich sink ecosystem, enrichment support         |
| **Log Aggregation**   | Loki    | Prometheus-inspired, log labels for efficient querying, cost-effective (no full-text indexing) |
| **Visualization**     | Grafana | Unified dashboards for logs + metrics, powerful query language (LogQL), alerting support       |

#### 3.1.2 Serilog Configuration (Pseudo)

```
Logging:
    MinimumLevel: Information
    Overrides:
        Microsoft: Warning
        Microsoft.AspNetCore: Warning
        Hangfire: Information
        System.Net.Http.HttpClient: Warning
  
    Enrichers:
        - FromLogContext      // Request-scoped properties
        - WithMachineName     // Hostname for multi-instance debugging
        - WithEnvironmentName // dev/staging/prod
        - WithProperty("Application", "SafeTravel.Api")
  
    Sinks:
        - Console (Compact JSON format for local dev)
        - GrafanaLoki (production)
```

#### 3.1.3 Loki Sink Configuration

**Configuration (Pseudo):**

```
Loki:
    Uri: "http://loki:3100"  // Or env var LOKI_URL
    Labels:
        - app: "safetravel-api"
        - environment: (from config)
        - host: (machine name)
    BatchPostingLimit: 100
    QueueLimit: 1000
    Period: 2 seconds  // Batch flush interval
    UseInternalTimestamp: true
```

**Environment Variables:**

| Variable               | Description            | Default Value             |
| ---------------------- | ---------------------- | ------------------------- |
| `LOKI_URL`           | Loki push API endpoint | `http://localhost:3100` |
| `LOG_LEVEL`          | Minimum log level      | `Information`           |
| `LOG_ENABLE_CONSOLE` | Enable console sink    | `true`                  |
| `LOG_ENABLE_LOKI`    | Enable Loki sink       | `true`                  |

#### 3.1.4 Structured Log Events

| Event                        | Level       | Properties                                        | Purpose                     |
| ---------------------------- | ----------- | ------------------------------------------------- | --------------------------- |
| `ApiRequestStarted`        | Information | `RequestId`, `Path`, `Method`               | Request tracing             |
| `ApiRequestCompleted`      | Information | `RequestId`, `StatusCode`, `DurationMs`     | Performance monitoring      |
| `CacheHit`                 | Debug       | `CacheKey`, `AgeSeconds`                      | Cache effectiveness         |
| `CacheMiss`                | Information | `CacheKey`, `FallbackUsed`                    | Cache miss tracking         |
| `CacheStale`               | Warning     | `CacheKey`, `AgeMinutes`, `Threshold`       | Staleness detection         |
| `WeatherSyncStarted`       | Information | `JobId`, `DistrictCount`                      | Job execution tracking      |
| `WeatherSyncCompleted`     | Information | `JobId`, `DurationSeconds`, `DistrictCount` | Job success monitoring      |
| `WeatherSyncFailed`        | Error       | `JobId`, `ErrorMessage`, `Attempt`          | Job failure alerting        |
| `ExternalApiCallStarted`   | Debug       | `Url`, `Method`                               | External dependency tracing |
| `ExternalApiCallCompleted` | Information | `Url`, `StatusCode`, `DurationMs`           | External API monitoring     |
| `ExternalApiCallFailed`    | Warning     | `Url`, `ErrorType`, `WillRetry`             | Retry and failure tracking  |
| `RedisConnectionFailed`    | Warning     | `Exception`                                     | Cache availability          |

#### 3.1.5 Request Correlation

**Correlation Strategy (Pseudo):**

```
RequestLoggingMiddleware:
    ON Request:
        correlationId = Request.Headers["X-Correlation-Id"] ?? NewGuid()
  
        LogContext.Push("CorrelationId", correlationId)
        LogContext.Push("RequestId", NewGuid())
        LogContext.Push("ClientIp", Request.RemoteIpAddress)
  
        Response.Headers.Add("X-Correlation-Id", correlationId)
  
        Log.Information("Request started: {Method} {Path}", method, path)
  
        AWAIT Next()
  
        Log.Information("Request completed: {StatusCode} in {DurationMs}ms", statusCode, duration)
```

#### 3.1.6 Grafana Dashboard

**Key Panels:**

| Panel                     | Query (LogQL)                                                                                                            | Purpose               |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------ | --------------------- |
| **Request Rate**    | `rate({app="safetravel-api"} \|~ "Request completed" [5m])`                                                             | Traffic monitoring    |
| **Error Rate**      | `rate({app="safetravel-api", level="error"} [5m])`                                                                     | Error detection       |
| **Cache Hit Ratio** | `count_over_time({app="safetravel-api"} \|~ "CacheHit" [5m]) / count_over_time({app="safetravel-api"} \|~ "Cache" [5m])` | Cache effectiveness   |
| **Job Failures**    | `{app="safetravel-api"} \|~ "WeatherSyncFailed"`                                                                        | Background job health |
| **P99 Latency**     | Derived from `DurationMs` property                                                                                     | Performance SLA       |

#### 3.1.7 Alerting Rules (Grafana)

| Alert                   | Condition                             | Severity | Action          |
| ----------------------- | ------------------------------------- | -------- | --------------- |
| High Error Rate         | `error_rate > 5%` over 5 min        | Critical | PagerDuty/Slack |
| Cache Miss Spike        | `cache_miss_rate > 20%` over 10 min | Warning  | Slack           |
| Weather Sync Failure    | 3 consecutive job failures            | Critical | PagerDuty       |
| Data Staleness          | No successful sync in 30 min          | Warning  | Slack           |
| API Latency Degradation | `p99 > 500ms` over 5 min            | Warning  | Slack           |

### 3.2 Key Metrics to Monitor

| Metric                            | Type      | Alert Threshold |
| --------------------------------- | --------- | --------------- |
| `api_request_duration_seconds`  | Histogram | p99 > 500ms     |
| `cache_hit_ratio`               | Gauge     | < 0.95          |
| `hangfire_job_duration_seconds` | Histogram | > 60s           |
| `hangfire_job_failures_total`   | Counter   | > 3/hour        |
| `data_staleness_seconds`        | Gauge     | > 900 (15 min)  |

### 3.3 Health Check Logic (Pseudo)

```
HealthCheck():
    metadata = Redis.GetMetadata()
  
    IF metadata IS NULL THEN RETURN Unhealthy("No weather data")
  
    staleness = Now - metadata.LastSync
  
    IF staleness > 30 minutes THEN RETURN Unhealthy("Data too old")
    IF staleness > 15 minutes THEN RETURN Degraded("Data is stale")
  
    RETURN Healthy("OK")
```

---

## 4. Security & Rate Limiting

### 4.1 Security Measures

| Layer                        | Measure           | Implementation                    |
| ---------------------------- | ----------------- | --------------------------------- |
| **Transport**          | HTTPS only        | Kestrel HTTPS redirect middleware |
| **Input Validation**   | Schema validation | FluentValidation on all requests  |
| **Coordinates**        | Bounds checking   | Lat: -90 to 90, Long: -180 to 180 |
| **District Names**     | Whitelist         | Only accept known district names  |
| **Hangfire Dashboard** | Authentication    | `IDashboardAuthorizationFilter` |
| **Redis**              | Password auth     | `redis://user:password@host`    |
| **Headers**            | Security headers  | HSTS, X-Content-Type-Options      |

### 4.2 Rate Limiting

**Configuration (Pseudo):**

```
RateLimit:
    PartitionBy: Client IP Address
    Limit: 100 requests per minute
    QueueLimit: 10 pending requests
    RejectionStatus: 429 Too Many Requests
```

### 4.3 Input Validation Rules

| Field                   | Validation                        | Error Message                                |
| ----------------------- | --------------------------------- | -------------------------------------------- |
| `latitude`            | Between -90 and 90                | "Latitude must be between -90 and 90"        |
| `longitude`           | Between -180 and 180              | "Longitude must be between -180 and 180"     |
| `destinationDistrict` | Must exist in district dictionary | "District not found"                         |
| `travelDate`          | Within next 7 days                | "Travel date must be within the next 7 days" |

---

**Previous:** [Data & Caching](./02_data_and_caching.md) | **Next:** [Deployment & Validation](./04_deployment_and_validation.md)
