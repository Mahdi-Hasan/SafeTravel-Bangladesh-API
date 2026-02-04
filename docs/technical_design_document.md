# Technical Design Document: SafeTravel Bangladesh API

**Version:** 1.0
**Date:** February 4, 2026
**Author:** Mehedi Hasan
**Status:** Draft

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Non-Functional Requirements](#2-non-functional-requirements)
3. [High-Level Architecture](#3-high-level-architecture)
4. [Technology Stack](#4-technology-stack)
5. [Clean Architecture Layer Responsibilities](#5-clean-architecture-layer-responsibilities)
6. [Background Job Design (Hangfire)](#6-background-job-design-hangfire)
7. [Caching Strategy](#7-caching-strategy)
8. [API Design](#8-api-design)
9. [Domain Logic](#9-domain-logic)
10. [HTTP &amp; External API Best Practices](#10-http--external-api-best-practices)
11. [Failure Scenarios &amp; Recovery](#11-failure-scenarios--recovery)
12. [Observability &amp; Monitoring](#12-observability--monitoring)
13. [Security &amp; Rate Limiting](#13-security--rate-limiting)
14. [Trade-offs &amp; Design Justification](#14-trade-offs--design-justification)
15. [Infrastructure &amp; Deployment](#15-infrastructure--deployment)
16. [Acceptance Criteria](#16-acceptance-criteria)
17. [Future Improvements](#17-future-improvements)

- [Appendix A: District Data Source](#appendix-a-district-data-source)
- [Appendix B: Open-Meteo API Reference](#appendix-b-open-meteo-api-reference)
- [Appendix C: Glossary](#appendix-c-glossary)

---

## 1. System Overview

### 1.1 Purpose

SafeTravel Bangladesh API is a production-grade .NET 10 REST API that recommends optimal travel destinations across Bangladesh's 64 districts based on **temperature** and **air quality (PM2.5)** metrics.

### 1.2 Core Capabilities

| Feature                         | Description                                                                                                                 |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **Top 10 Districts**      | Returns the 10 coolest and cleanest districts ranked by 7-day average temperature at 2 PM, with PM2.5 as tie-breaker        |
| **Travel Recommendation** | Compares user's current location with destination district on a specific date, returning "Recommended" or "Not Recommended" |

### 1.3 Key Design Principles

- **Cache-first with fallback** — API reads from Redis first; if unavailable/empty, falls back to Open-Meteo directly
- **Background-first data fetching** — Scheduled jobs pre-fetch and cache data for optimal performance
- **Graceful degradation** — Serves cached data when available, fresh data when cache misses
- **Crash resilience** — Jobs survive restarts and deployments
- **Startup district dictionary** — All 64 district coordinates loaded into in-memory dictionary at startup for O(1) lookup

---

## 2. Non-Functional Requirements

### 2.1 Performance

| Metric            | Target         | Strategy                                 |
| ----------------- | -------------- | ---------------------------------------- |
| API Response Time | ≤ 500ms (p99) | Pre-computed cache, no runtime API calls |
| Cache Hit Rate    | > 99%          | Background refresh every 10 minutes      |
| Throughput        | 1000 RPS       | Stateless API, Redis-backed cache        |

### 2.2 Reliability

| Metric           | Target        | Strategy                                             |
| ---------------- | ------------- | ---------------------------------------------------- |
| API Availability | 99.9%         | Cache-first with stale fallback                      |
| Data Freshness   | ≤ 12 minutes | 10-minute refresh cycle + 2 min buffer for job delay |
| Job Success Rate | > 99.5%       | Retry policies, partial failure handling             |

### 2.3 Scalability

- **Horizontal scaling**: Stateless API instances behind load balancer
- **Shared state**: Redis as single source of truth for cache and job coordination
- **Background jobs**: Single Hangfire server with distributed lock prevents duplicates

### 2.4 Data Correctness

- Atomic cache updates prevent partial reads
- Version-stamped cache entries for consistency
- Validation on all external API responses

---

## 3. High-Level Architecture

```mermaid
flowchart TB
    CLIENTS["<b>CLIENTS</b>"] --> CONTROLLERS["<b>Controllers</b><br/>/api/v1/districts/top10<br/>/api/v1/travel/recommendation"]
    CONTROLLERS --> APPLICATION["<b>Application Layer</b><br/>Top10DistrictsQuery<br/>TravelRecommendationQuery"]
    APPLICATION --> DOMAIN["<b>Domain Layer</b><br/>DistrictRankingService<br/>TravelRecommendationPolicy"]
    DOMAIN --> REDIS["<b>REDIS CACHE</b>"]
    DOMAIN --> HANGFIRE["<b>HANGFIRE + REDIS</b><br/>WeatherDataSyncJob<br/>Recurring: every 10 min<br/>Retry: 3 attempts"]
    REDIS --> INFRA["<b>Infrastructure Layer</b><br/>OpenMeteoWeatherClient<br/>OpenMeteoAirQualityClient<br/>HttpClientFactory pooling"]
    HANGFIRE --> INFRA
    INFRA --> EXTERNAL["<b>External APIs</b><br/>Open-Meteo Weather API<br/>Open-Meteo Air Quality API"]
```

### Data Flow

1. **Background Job (every 10 minutes)**:

   - Hangfire triggers `WeatherDataSyncJob`
   - Job fetches weather + air quality for all 64 districts via bulk requests
   - Computes averages, rankings, and per-day forecasts
   - Atomically updates Redis cache
2. **API Request Flow**:

   - Controller receives request → dispatches Litebus query
   - Query handler attempts Redis cache first
   - **If cache hit**: Returns pre-computed response (fast path, <50ms)
   - **If cache miss/Redis down**: Falls back to Open-Meteo API directly (slow path, ~2-3s)

---

## 4. Technology Stack

| Component                   | Technology                 | Justification                                              |
| --------------------------- | -------------------------- | ---------------------------------------------------------- |
| **Runtime**           | .NET 10                    | Latest version, performance improvements, native AOT ready |
| **Framework**         | ASP.NET Core Minimal APIs  | Low overhead, high performance                             |
| **Architecture**      | Clean Architecture         | Separation of concerns, testability                        |
| **CQRS**              | LiteBus                    | Lightweight, decouples handlers, minimal overhead          |
| **Background Jobs**   | Hangfire + Redis           | Persistent, survives restarts, built-in retry              |
| **Cache**             | Redis                      | Distributed, atomic operations, high throughput            |
| **HTTP Client**       | IHttpClientFactory + Polly | Connection pooling, resilience policies                    |
| **Serialization**     | System.Text.Json           | Native, high performance                                   |
| **Validation**        | FluentValidation           | Declarative, testable                                      |
| **Logging**           | Serilog + Grafana/Loki     | Structured logging, centralized log aggregation            |
| **API Documentation** | Swagger/OpenAPI            | Interactive docs, client SDK generation                    |
| **Containerization**  | Docker + docker-compose    | Local Redis, consistent environments                       |

### 4.1 Environment Variables

| Variable                       | Description                                             | Default Value                                                                                     |
| ------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `CACHE_STALENESS_MINUTES`    | Cache freshness threshold in minutes (integer; 1–1440) | `30`                                                                                            |
| `DISTRICT_DATA_URL`          | URL to district coordinates JSON                        | `https://raw.githubusercontent.com/strativ-dev/technical-screening-test/main/bd-districts.json` |
| `OPENMETEO_WEATHER_BASE_URL` | Open-Meteo Weather API base URL                         | `https://api.open-meteo.com/v1/`                                                                |
| `OPENMETEO_AIR_QUALITY_URL`  | Open-Meteo Air Quality API base URL                     | `https://air-quality-api.open-meteo.com/v1/`                                                    |
| `REDIS_CONNECTION_STRING`    | Redis connection string                                 | `localhost:6379`                                                                                |
| `HANGFIRE_REDIS_PREFIX`      | Hangfire Redis key prefix                               | `safetravel:hangfire`                                                                           |
| `WEATHER_SYNC_CRON`          | Hangfire sync job cron expression                       | `*/10 * * * *` (every 10 minutes)                                                               |

---

## 5. Clean Architecture Layer Responsibilities

```
SafeTravel.Bangladesh/
├── src/
│   ├── SafeTravel.Domain/              # Layer 1: Enterprise Business Rules
│   ├── SafeTravel.Application/         # Layer 2: Application Business Rules  
│   ├── SafeTravel.Infrastructure/      # Layer 3: Interface Adapters
│   └── SafeTravel.Api/                 # Layer 4: Frameworks & Drivers
└── tests/
    ├── SafeTravel.Domain.Tests/
    ├── SafeTravel.Application.Tests/
    └── SafeTravel.Integration.Tests/
```

### 5.1 Domain Layer (`SafeTravel.Domain`)

**Zero external dependencies.** Contains:

- **Entities**: `District`, `WeatherForecast`, `AirQualityReading`
- **Value Objects**: `Coordinates`, `Temperature`, `PM25Level`, `DateRange`
- **Domain Services**: `DistrictRankingService`, `TravelRecommendationPolicy`
- **Interfaces**: `IDistrictRepository`, `IWeatherDataCache`
- **Exceptions**: `ForecastUnavailableException`, `InvalidDateRangeException`

**Key Domain Models (Pseudo):**

```
District { Id, Name, Location(Lat, Long) }
WeatherSnapshot { Temperature, PM25, Date }
RankedDistrict { Rank, DistrictId, DistrictName, AvgTemp, AvgPM25 }
RecommendationResult { IsRecommended, Reason }
```

### 5.2 Application Layer (`SafeTravel.Application`)

Contains **use cases** implemented as Litebus queries.

- **Queries**: `GetTop10DistrictsQuery`, `GetTravelRecommendationQuery`
- **Query Handlers**: Business orchestration, cache access with API fallback
- **DTOs**: Response models, request validation
- **Interfaces**: `IWeatherDataCache`, `IOpenMeteoClient`

**Query Handler Logic (Pseudo):**

```
GetTop10DistrictsHandler:
    rankings = TryGetFromRedisCache()
  
    IF rankings IS NULL OR Redis unavailable THEN
        weatherData = FetchFromOpenMeteoAPI(all 64 districts)
        rankings = ComputeRankings(weatherData)
        TrySaveToCache(rankings)  // Best effort
    END IF
  
    RETURN Top10(rankings)
```

### 5.3 Infrastructure Layer (`SafeTravel.Infrastructure`)

Implements all external dependencies:

- **External API Clients**: `OpenMeteoWeatherClient`, `OpenMeteoAirQualityClient`
- **Cache Implementation**: `RedisWeatherDataCache`
- **Background Jobs**: `WeatherDataSyncJob`
- **Data Providers**: `DistrictDataProvider` (loads JSON into dictionary at startup)

### 5.4 API Layer (`SafeTravel.Api`)

Thin layer with:

- **Minimal API Endpoints**: Route definitions only
- **Middleware**: Exception handling, request logging
- **Configuration**: DI registration, Hangfire dashboard

### 5.5 Startup District Dictionary

At application startup, load all 64 districts into an in-memory dictionary for O(1) coordinate lookup:

**Startup Logic (Pseudo):**

```
ON Application Start:
    jsonContent = ReadFile("bd-districts.json")
    districts = Deserialize<List<District>>(jsonContent)
  
    // Create dictionary keyed by district name (case-insensitive)
    DistrictDictionary = districts.ToDictionary(
        key: d => d.Name.ToLowerInvariant(),
        value: d => d
    )
  
    // Register as Singleton in DI container
    services.AddSingleton(DistrictDictionary)
```

**Usage:**

```
GetDistrictByName(name):
    RETURN DistrictDictionary[name.ToLowerInvariant()] OR throw NotFoundException
```

---

## 6. Background Job Design (Hangfire)

### 6.1 Why Hangfire Over IHostedService

| Aspect                         | IHostedService             | Hangfire                                      |
| ------------------------------ | -------------------------- | --------------------------------------------- |
| **Crash Recovery**       | Job state lost on restart  | Jobs persisted in Redis, resume after restart |
| **Deployment Safety**    | Running jobs terminated    | Jobs continue on other instances or retry     |
| **Retry Logic**          | Manual implementation      | Built-in exponential backoff                  |
| **Monitoring**           | Custom dashboards needed   | Built-in web dashboard                        |
| **Duplicate Prevention** | Manual distributed locking | `DisableConcurrentExecution` attribute      |
| **Job History**          | Not available              | Full execution history with errors            |

> [!IMPORTANT]
> **IHostedService** is unsuitable for production because a deployment or pod restart will terminate in-flight jobs without recovery. Hangfire persists job state to Redis, enabling automatic resume.

### 6.2 Job Scheduling

**Job Design (Pseudo):**

```
WeatherDataSyncJob:
    [DisableConcurrentExecution: 300s timeout]
    [AutomaticRetry: 3 attempts with delays 30s, 60s, 120s]
  
    ExecuteAsync():
        districts = GetAllDistrictsFromDictionary()
  
        // Bulk fetch for all 64 districts (single API call each)
        weatherData = OpenMeteoClient.GetBulkForecast(districts, 7 days)
        airQualityData = OpenMeteoClient.GetBulkAirQuality(districts, 7 days)
  
        // Process and compute rankings
        processedData = ProcessWeatherData(districts, weatherData, airQualityData)
  
        // Atomic cache update
        RedisCache.SetAllDistrictData(processedData)
        RedisCache.SetRankings(ComputeRankings(processedData))
  
        Log("Weather sync completed for {districts.Count} districts")
```

**Job Registration (Pseudo):**

```
ON Application Start:
    Hangfire.UseRedisStorage(connectionString, prefix: "safetravel:hangfire")
    Hangfire.AddServer(workerCount: 2, queues: ["weather-sync", "default"])
  
    RecurringJob.AddOrUpdate(
        jobId: "weather-data-sync",
        job: WeatherDataSyncJob.ExecuteAsync,
        cron: "*/10 * * * *",  // Every 10 minutes
        timezone: "Asia/Dhaka"
    )
```

### 6.3 Idempotency

Jobs are **idempotent by design**:

- Each execution completely replaces cached data
- No incremental updates that could cause inconsistency
- Cache keys are fixed, not time-based
- `DisableConcurrentExecution` prevents parallel execution

### 6.4 Retry Strategy

| Attempt | Delay | Cumulative Time                    |
| ------- | ----- | ---------------------------------- |
| 1       | 30s   | 30s                                |
| 2       | 60s   | 1m 30s                             |
| 3       | 120s  | 3m 30s                             |
| Failed  | -     | Job marked failed, alert triggered |

### 6.5 Failure Handling

**Error Handling Strategy (Pseudo):**

```
TRY:
    SyncWeatherData()
CATCH HttpRequestException (429 TooManyRequests):
    Log.Warning("Rate limited, will retry")
    THROW  // Let Hangfire retry with backoff
CATCH HttpRequestException (Network failure):
    Log.Warning("API unreachable, will retry")
    THROW  // Let Hangfire retry
CATCH JsonException:
    Log.Error("Invalid API response - fail immediately")
    THROW BackgroundJobFailedException  // Don't retry corrupt data
```

---

## 7. Caching Strategy

### 7.1 Cache Architecture

```
Redis Instance
├── safetravel:rankings                    # Pre-computed top 64 rankings
├── safetravel:districts:{districtId}      # Per-district 7-day forecast
├── safetravel:metadata                    # Last sync timestamp, version
└── safetravel:hangfire:*                  # Hangfire job storage
```

### 7.2 Cache Keys & TTL

| Key Pattern                   | Content                              | TTL       | Rationale                                              |
| ----------------------------- | ------------------------------------ | --------- | ------------------------------------------------------ |
| `safetravel:rankings`       | Sorted list of all 64 districts      | 20 min    | ~1.7x staleness threshold (12 min) for fallback buffer |
| `safetravel:districts:{id}` | 7-day forecast for single district   | 20 min    | Matches rankings TTL                                   |
| `safetravel:metadata`       | `{ lastSync, version, isHealthy }` | No expiry | Always available for health checks                     |

### 7.3 Cache Data Structures (Pseudo)

```
CachedRankings {
    Districts: List<RankedDistrict>
    GeneratedAt: DateTime
    Version: int
    IsStale: bool  // True if (Now - GeneratedAt) > 12 minutes
}

RankedDistrict {
    Rank, DistrictId, DistrictName, AvgTemp, AvgPM25
}

CachedDistrictForecast {
    DistrictId: string
    Forecasts: List<DailyForecast>  // 7 days
    GeneratedAt: DateTime
}

DailyForecast { Date, TempAt2PM, PM25At2PM }
```

### 7.4 Cache-Aside with Manual Data Loader (Pseudo)

The staleness threshold is **12 minutes** (10 min job interval + 2 min buffer). If cache data is older than 12 minutes AND no background job is currently running, the API will **manually trigger the data loader** synchronously.

```
GetRankingsAsync():
    stalenessThreshold = 12 minutes  // 10 min job + 2 min buffer
  
    // Step 1: Try Redis cache with freshness check
    TRY:
        cacheEntry = Redis.Get("safetravel:rankings")
  
        IF cacheEntry IS NOT NULL THEN
            dataAge = Now - cacheEntry.GeneratedAt
  
            IF dataAge <= stalenessThreshold THEN
                // Data is fresh - return immediately
                RETURN cacheEntry
            ELSE
                // Data is stale - check if background job is running
                IF Hangfire.IsJobRunning("weather-data-sync") THEN
                    // Job is running, return stale data (will be fresh soon)
                    Log.Info("Cache stale but job running, returning stale data")
                    RETURN cacheEntry
                ELSE
                    // No job running - manually trigger data loader
                    Log.Info("Cache stale ({dataAge} min), manually loading fresh data")
                    GOTO ManualDataLoad
                END IF
            END IF
    CATCH RedisConnectionException:
        Log.Warning("Redis unavailable")

    ManualDataLoad:
    // Step 2: Manual Data Loader (synchronous fetch)
    Log.Info("Manually fetching fresh data from Open-Meteo")
    districts = GetAllDistrictsFromDictionary()
    weatherData = OpenMeteoClient.GetBulkForecast(districts)
    airQualityData = OpenMeteoClient.GetBulkAirQuality(districts)

    rankings = ComputeRankings(weatherData, airQualityData)

    // Update cache synchronously
    TRY:
        Redis.Set("safetravel:rankings", rankings, TTL: 20 min)
    CATCH:
        Log.Warning("Failed to update cache")

    RETURN rankings
```

### 7.5 Cache Behavior Summary

| Scenario                                      | Behavior                                                          |
| --------------------------------------------- | ----------------------------------------------------------------- |
| Redis available + data < 12 min               | Return from Redis (fast path ~50ms)                               |
| Redis available + data > 12 min + job running | Return stale data (job will refresh soon)                         |
| Redis available + data > 12 min + no job      | **Manual data loader** → fetch fresh, update cache, return |
| Redis unavailable                             | **Manual data loader** → fetch from Open-Meteo (~2-3s)     |
| Cache empty on startup                        | **Manual data loader** → fetch from Open-Meteo             |

### 7.6 Cache-Aside Pattern Sequence Diagram

The following diagram illustrates the cache-aside pattern with freshness checking and manual data loader trigger:

```mermaid
sequenceDiagram
    participant Client
    participant API as SafeTravel API
    participant Redis as Redis Cache
    participant OpenMeteo as Open-Meteo API
    participant Hangfire as Background Job

    Note over Client,Hangfire: Happy Path - Cache Hit (Fresh < 12 min)
    Client->>API: GET /api/v1/districts/top10
    API->>Redis: GET safetravel:rankings
    Redis-->>API: CachedRankings (age < 12 min)
    API-->>Client: 200 OK (Top 10 Districts)

    Note over Client,Hangfire: Cache Miss - Manual Data Loader
    Client->>API: GET /api/v1/districts/top10
    API->>Redis: GET safetravel:rankings
    Redis-->>API: NULL (cache miss)
    API->>OpenMeteo: GET /forecast (Manual Data Loader)
    OpenMeteo-->>API: Weather + AQ Data
    API->>API: ComputeRankings()
    API->>Redis: SET safetravel:rankings
    API-->>Client: 200 OK (Top 10 Districts)

    Note over Client,Hangfire: Stale Cache (> 12 min) - Manual Trigger
    Client->>API: GET /api/v1/districts/top10
    API->>Redis: GET safetravel:rankings
    Redis-->>API: CachedRankings (age > 12 min)
    API->>Hangfire: Check if job running?
    Hangfire-->>API: No job running
    API->>OpenMeteo: GET /forecast (Manual Data Loader)
    OpenMeteo-->>API: Fresh Data
    API->>Redis: SET safetravel:rankings
    API-->>Client: 200 OK (Fresh Data)

    Note over Client,Hangfire: Background Job - Scheduled Refresh (Every 10 min)
    Hangfire->>OpenMeteo: GET /forecast (all 64 districts)
    OpenMeteo-->>Hangfire: Weather + AQ Data
    Hangfire->>Hangfire: ComputeRankings()
    Hangfire->>Redis: SET safetravel:rankings (atomic)

    Note over Client,Hangfire: Redis Down - Direct Fallback
    Client->>API: GET /api/v1/districts/top10
    API-xRedis: Connection Failed
    API->>OpenMeteo: GET /forecast (Manual Data Loader)
    OpenMeteo-->>API: Weather + AQ Data
    API-->>Client: 200 OK (Top 10 Districts)
```

---

## 8. API Design

### 8.1 Endpoints

| Method | Endpoint                          | Description                                |
| ------ | --------------------------------- | ------------------------------------------ |
| GET    | `/api/v1/districts/top10`       | Get top 10 coolest & cleanest districts    |
| POST   | `/api/v1/travel/recommendation` | Get travel recommendation for destination  |
| GET    | `/health/live`                  | Liveness probe (app is running)            |
| GET    | `/health/ready`                 | Readiness probe (Redis + last sync status) |
| GET    | `/hangfire`                     | Hangfire dashboard (secured)               |

### 8.2 Request/Response Models

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

### 8.3 API Optimizations

#### 8.3.1 Response Compression

The API layer implements **Brotli** and **Gzip** compression to reduce bandwidth and improve response times for clients.

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

#### 8.3.2 E-Tag Caching

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

**Implementation Strategy:**

- ETags are based on `metadata.generatedAt` from cached data
- Since cache updates happen atomically, all responses for the same cache version have identical ETags
- 304 responses save bandwidth and parsing time on client side

**Configuration:**

| Endpoint                          | ETag Strategy               | Cache-Control          |
| :-------------------------------- | :-------------------------- | :--------------------- |
| `/api/v1/districts/top10`       | Strong ETag (version-based) | `public, max-age=60` |
| `/api/v1/travel/recommendation` | Strong ETag (version-based) | `public, max-age=60` |

## 9. Domain Logic

### 9.1 Ranking Logic (Pseudo)

```
ComputeRankings(districtData):
    // Sort by coolest first, then cleanest air as tie-breaker
    sorted = districtData
        .OrderBy(AvgTempAt2PM)      // Primary: Lowest temp
        .ThenBy(AvgPM25At2PM)       // Secondary: Lowest PM2.5
  
    FOR EACH district IN sorted WITH index:
        YIELD RankedDistrict {
            Rank: index + 1,
            DistrictId, DistrictName,
            AvgTemp: Round(temp, 1),
            AvgPM25: Round(pm25, 1)
        }
```

### 9.2 Temperature & PM2.5 Averaging (Pseudo)

```
AggregateWeatherData(district, weatherData, airQualityData, startDate, days=7):
    targetDates = [startDate, startDate+1, ..., startDate+6]  // 7 days
  
    temperatures = []
    pm25Values = []
  
    FOR EACH date IN targetDates:
        temp = GetValueAt2PM(weatherData, date)    // 14:00 local time
        pm25 = GetValueAt2PM(airQualityData, date)
  
        IF temp IS NOT NULL THEN temperatures.Add(temp)
        IF pm25 IS NOT NULL THEN pm25Values.Add(pm25)
  
    // Require at least 70% data coverage
    IF temperatures.Count < days * 0.7 THEN
        THROW InsufficientDataException
  
    RETURN {
        District: district,
        AvgTempAt2PM: Average(temperatures),
        AvgPM25At2PM: Average(pm25Values) OR 0 if empty,
        DataPoints: temperatures.Count
    }
```

### 9.3 Recommendation Policy (Pseudo)

```
EvaluateRecommendation(origin, destination):
    isCooler = destination.Temp < origin.Temp
    isCleaner = destination.PM25 < origin.PM25
  
    isRecommended = isCooler AND isCleaner  // BOTH must be true
  
    IF isRecommended THEN
        tempDiff = origin.Temp - destination.Temp
        pm25Improvement = (1 - destination.PM25 / origin.PM25) * 100
        reason = "{destination} is {tempDiff}°C cooler and has {pm25Improvement}% better air quality"
    ELSE IF NOT isCooler AND NOT isCleaner THEN
        reason = "{destination} is both warmer and has worse air quality"
    ELSE IF NOT isCooler THEN
        reason = "Better air quality but warmer - both conditions must be better"
    ELSE
        reason = "Cooler but worse air quality - both conditions must be better"
  
    RETURN { IsRecommended: isRecommended, Reason: reason }
```

---

## 10. HTTP & External API Best Practices

### 10.1 HttpClient Configuration

Use `IHttpClientFactory` with Polly resilience policies:

**Configuration (Pseudo):**

```
HttpClient for OpenMeteo:
    BaseAddress: "https://api.open-meteo.com/v1/"
    Timeout: 30 seconds
  
    Policies:
        - Retry: 3 attempts with exponential backoff (2^attempt seconds + jitter)
        - CircuitBreaker: Opens after 5 failures, stays open for 30s
        - Timeout: 10s per attempt
```

### 10.2 Bulk API Requests

Open-Meteo supports fetching multiple locations in a single request using comma-separated coordinates:

**Bulk Request (Pseudo):**

```
GetBulkForecast(districts, forecastDays):
    latitudes = Join(districts.Select(d => d.Lat), ",")
    longitudes = Join(districts.Select(d => d.Long), ",")
  
    url = "forecast?latitude={latitudes}&longitude={longitudes}
           &hourly=temperature_2m&forecast_days={forecastDays}&timezone=Asia/Dhaka"
  
    response = HttpClient.GetAsync(url)
    RETURN Deserialize<BulkWeatherResponse>(response)
```

### 10.3 Timeout Configuration

| Layer              | Timeout | Purpose                 |
| ------------------ | ------- | ----------------------- |
| HttpClient.Timeout | 30s     | Overall request timeout |
| Polly Timeout      | 10s     | Per-attempt timeout     |
| Hangfire Job       | 300s    | Total job execution     |
| Redis Operations   | 5s      | Cache read/write        |

---

## 11. Failure Scenarios & Recovery

### 11.1 Failure Matrix

| Scenario                         | Detection            | Mitigation                    | Recovery                             |
| -------------------------------- | -------------------- | ----------------------------- | ------------------------------------ |
| **Open-Meteo API down**    | HTTP 5xx, timeout    | Hangfire retries (3 attempts) | Serve stale cache or fail gracefully |
| **Open-Meteo rate limit**  | HTTP 429             | Exponential backoff           | Auto-retry after delay               |
| **Redis unavailable**      | Connection exception | Fallback to Open-Meteo API    | Auto-reconnect                       |
| **Cache miss (empty)**     | Null cache read      | Fallback to Open-Meteo API    | Background job populates cache       |
| **App restart during job** | Hangfire detects     | Job re-queued                 | Automatic resume                     |
| **Corrupt API response**   | JSON parse error     | Fail job, alert               | Manual investigation                 |

### 11.2 Fallback Strategy (Pseudo)

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

## 12. Observability & Monitoring

### 12.1 Logging Infrastructure (Serilog + Grafana + Loki)

The API implements a comprehensive logging strategy using **Serilog** as the structured logging framework with **Grafana + Loki** for log aggregation, storage, and visualization.

#### 12.1.1 Why Serilog + Grafana + Loki

| Decision                    | Choice  | Justification                                                                                  |
| --------------------------- | ------- | ---------------------------------------------------------------------------------------------- |
| **Logging Framework** | Serilog | Industry-standard structured logging for .NET, rich sink ecosystem, enrichment support         |
| **Log Aggregation**   | Loki    | Prometheus-inspired, log labels for efficient querying, cost-effective (no full-text indexing) |
| **Visualization**     | Grafana | Unified dashboards for logs + metrics, powerful query language (LogQL), alerting support       |

#### 12.1.2 Serilog Configuration (Pseudo)

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

#### 12.1.3 Loki Sink Configuration

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

#### 12.1.4 Structured Log Events

All log events follow a consistent structure with contextual properties:

**Key Log Events:**

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

**Log Event Examples (Pseudo):**

```
// Request logging
Log.Information(
    "API request {Method} {Path} completed with {StatusCode} in {DurationMs}ms",
    method, path, statusCode, durationMs
)

// Cache operation logging
Log.Debug(
    "Cache {Operation} for key {CacheKey}, Age: {AgeSeconds}s",
    operation, cacheKey, ageSeconds
)

// Background job logging
Log.Information(
    "Weather sync job {JobId} completed: {DistrictCount} districts in {DurationSeconds}s",
    jobId, districtCount, durationSeconds
)

// Error logging with structured exception
Log.Error(
    exception,
    "External API call to {Url} failed: {ErrorMessage}",
    url, exception.Message
)
```

#### 12.1.5 Request Correlation

Every HTTP request is automatically enriched with a correlation ID for distributed tracing:

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

#### 12.1.6 Grafana Dashboard

A pre-configured Grafana dashboard provides operational visibility:

**Key Panels:**

| Panel                     | Query (LogQL)                                                                                                            | Purpose               |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------ | --------------------- |
| **Request Rate**    | `rate({app="safetravel-api"} \|~ "Request completed" [5m])`                                                             | Traffic monitoring    |
| **Error Rate**      | `rate({app="safetravel-api", level="error"} [5m])`                                                                     | Error detection       |
| **Cache Hit Ratio** | `count_over_time({app="safetravel-api"} \|~ "CacheHit" [5m]) / count_over_time({app="safetravel-api"} \|~ "Cache" [5m])` | Cache effectiveness   |
| **Job Failures**    | `{app="safetravel-api"} \|~ "WeatherSyncFailed"`                                                                        | Background job health |
| **P99 Latency**     | Derived from `DurationMs` property                                                                                     | Performance SLA       |

#### 12.1.7 Alerting Rules (Grafana)

| Alert                   | Condition                             | Severity | Action          |
| ----------------------- | ------------------------------------- | -------- | --------------- |
| High Error Rate         | `error_rate > 5%` over 5 min        | Critical | PagerDuty/Slack |
| Cache Miss Spike        | `cache_miss_rate > 20%` over 10 min | Warning  | Slack           |
| Weather Sync Failure    | 3 consecutive job failures            | Critical | PagerDuty       |
| Data Staleness          | No successful sync in 30 min          | Warning  | Slack           |
| API Latency Degradation | `p99 > 500ms` over 5 min            | Warning  | Slack           |

### 12.2 Key Metrics to Monitor

| Metric                            | Type      | Alert Threshold |
| --------------------------------- | --------- | --------------- |
| `api_request_duration_seconds`  | Histogram | p99 > 500ms     |
| `cache_hit_ratio`               | Gauge     | < 0.95          |
| `hangfire_job_duration_seconds` | Histogram | > 60s           |
| `hangfire_job_failures_total`   | Counter   | > 3/hour        |
| `data_staleness_seconds`        | Gauge     | > 900 (15 min)  |

### 12.3 Health Check Logic (Pseudo)

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

## 13. Security & Rate Limiting

### 13.1 Security Measures

| Layer                        | Measure           | Implementation                    |
| ---------------------------- | ----------------- | --------------------------------- |
| **Transport**          | HTTPS only        | Kestrel HTTPS redirect middleware |
| **Input Validation**   | Schema validation | FluentValidation on all requests  |
| **Coordinates**        | Bounds checking   | Lat: -90 to 90, Long: -180 to 180 |
| **District Names**     | Whitelist         | Only accept known district names  |
| **Hangfire Dashboard** | Authentication    | `IDashboardAuthorizationFilter` |
| **Redis**              | Password auth     | `redis://user:password@host`    |
| **Headers**            | Security headers  | HSTS, X-Content-Type-Options      |

### 13.2 Rate Limiting

**Configuration (Pseudo):**

```
RateLimit:
    PartitionBy: Client IP Address
    Limit: 100 requests per minute
    QueueLimit: 10 pending requests
    RejectionStatus: 429 Too Many Requests
```

### 13.3 Input Validation Rules

| Field                   | Validation                        | Error Message                                |
| ----------------------- | --------------------------------- | -------------------------------------------- |
| `latitude`            | Between -90 and 90                | "Latitude must be between -90 and 90"        |
| `longitude`           | Between -180 and 180              | "Longitude must be between -180 and 180"     |
| `destinationDistrict` | Must exist in district dictionary | "District not found"                         |
| `travelDate`          | Within next 7 days                | "Travel date must be within the next 7 days" |

---

## 14. Trade-offs & Design Justification

### 14.1 Hangfire vs IHostedService

| Decision           | Hangfire            | Trade-off                                      |
| ------------------ | ------------------- | ---------------------------------------------- |
| **Chosen**   | Hangfire with Redis | Higher operational complexity (Redis required) |
| **Rejected** | IHostedService      | Simpler but jobs lost on restart               |

**Justification**: The 500ms API latency requirement mandates pre-computed cached data. A missed sync job directly impacts data freshness. Hangfire's persistent job queue ensures syncs complete even across deployments, which is critical for a travel recommendation API where stale temperature data could lead to poor recommendations.

### 14.2 Redis vs In-Memory Cache

| Decision                | Redis                  | Trade-off                              |
| ----------------------- | ---------------------- | -------------------------------------- |
| **Chosen**        | Redis as primary cache | Requires infrastructure, added latency |
| **Complementary** | In-memory L1 (5s TTL)  | Limited by memory, not shared          |

**Justification**: Horizontal scaling requires shared cache state. Multiple API instances must see the same pre-computed rankings. Redis provides atomic operations for cache updates, preventing partial reads during sync.

### 14.3 Bulk API Requests vs Per-District Calls

| Decision           | Bulk Requests                   | Trade-off                              |
| ------------------ | ------------------------------- | -------------------------------------- |
| **Chosen**   | Single request for 64 districts | Larger payload, all-or-nothing failure |
| **Rejected** | 64 parallel requests            | Higher latency, rate limit risk        |

**Justification**: Open-Meteo supports bulk coordinate queries. A single request for 64 districts completes in ~2-3 seconds vs ~30+ seconds for sequential calls. The risk of partial failure is mitigated by retry logic and stale cache fallback.

### 14.4 CQRS Without Separate Read/Write Models

| Decision                | Simplified CQRS                         | Trade-off       |
| ----------------------- | --------------------------------------- | --------------- |
| **Chosen**        | Queries only, no commands               | Not "pure" CQRS |
| **Justification** | This is a read-only API; no user writes | -               |

**Justification**: The API consumes external data and serves computed results. There are no user-initiated write operations, making full CQRS with commands unnecessary and over-engineered.

### 14.5 HTTP Verb for Travel Recommendation

| Decision           | `POST /api/v1/travel/recommendation` | Trade-off                                  |
| :----------------- | :------------------------------------- | :----------------------------------------- |
| **Chosen**   | **POST**                         | "Read" operation using write verb semantic |
| **Rejected** | GET                                    | Query parameters would be complex/lengthy  |

**Justification**: While technically a read query, **POST** is used because the request requires a complex body (`currentLocation`, `destinationDistrict`, `travelDate`). Sending complex JSON objects in GET query parameters is messy and has length limitations. This aligns with standard practices for complex search/calculation endpoints.

---

## 15. Infrastructure & Deployment

### 15.1 Container Architecture

The application is containerized for consistent deployment across environments:

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Compose Stack                      │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐  │
│  │ SafeTravel.Api  │  │     Redis       │  │   Grafana   │  │
│  │   (Kestrel)     │──│  (Cache + HF)   │  │   + Loki    │  │
│  │   Port: 5000    │  │   Port: 6379    │  │ Port: 3000  │  │
│  └─────────────────┘  └─────────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Container Specifications:**

| Container          | Image                  | Purpose                  | Resource Limits    |
| ------------------ | ---------------------- | ------------------------ | ------------------ |
| `safetravel-api` | Custom (.NET 10)       | API application          | 512MB RAM, 0.5 CPU |
| `redis`          | `redis:7-alpine`     | Cache + Hangfire storage | 256MB RAM          |
| `loki`           | `grafana/loki:2.9`   | Log aggregation          | 512MB RAM          |
| `grafana`        | `grafana/grafana:10` | Dashboards & alerts      | 256MB RAM          |

### 15.2 Docker Compose (Local Development)

**docker-compose.yml (Pseudo):**

```yaml
services:
  api:
    build: ./src/SafeTravel.Api
    ports: ["5000:5000"]
    environment:
      - REDIS_CONNECTION_STRING=redis:6379
      - LOKI_URL=http://loki:3100
    depends_on: [redis, loki]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
    volumes: [redis-data:/data]

  loki:
    image: grafana/loki:2.9
    ports: ["3100:3100"]
  
  grafana:
    image: grafana/grafana:10
    ports: ["3000:3000"]
    volumes: [grafana-data:/var/lib/grafana]
```

### 15.3 Cloud Deployment Considerations

| Aspect            | Recommendation                          | Notes                             |
| ----------------- | --------------------------------------- | --------------------------------- |
| **Compute** | Azure Container Apps / AWS ECS          | Serverless container hosting      |
| **Redis**   | Azure Cache for Redis / AWS ElastiCache | Managed Redis with HA             |
| **Logs**    | Grafana Cloud or self-hosted Loki       | Managed option reduces ops burden |
| **Secrets** | Azure Key Vault / AWS Secrets Manager   | Never store in env vars in prod   |
| **CI/CD**   | GitHub Actions        | Docker build → push → deploy    |

### 15.4 API Documentation (Swagger/OpenAPI)

The API exposes interactive documentation via **Swagger UI** and **OpenAPI 3.0** spec:

**Configuration (Pseudo):**

```
Swagger:
    Title: "SafeTravel Bangladesh API"
    Version: "v1"
    Description: "Travel recommendations based on weather and air quality"
  
    Endpoints:
        SwaggerUI: /swagger
        OpenAPI JSON: /swagger/v1/swagger.json
  
    Features:
        - Request/response examples
        - Schema validation
        - Try-it-out functionality
        - Authentication headers (if secured)
```

**Access:**

| Environment | Swagger URL                       |
| ----------- | --------------------------------- |
| Local       | `http://localhost:5000/swagger` |
| Production  | Disabled or protected behind auth |

> [!NOTE]
> Swagger UI is enabled in Development environment only. In Production, only the OpenAPI JSON spec is available for client SDK generation.

### 15.5 CI/CD Pipeline

The application uses an automated CI/CD pipeline for quality assurance and safe deployments.

#### 15.5.1 CI Pipeline (All Branches)

Runs on every push and pull request to ensure code quality:

| Stage                  | Actions                                        | Success Criteria              | Duration |
| ---------------------- | ---------------------------------------------- | ----------------------------- | -------- |
| **Code Quality**       | dotnet format, EditorConfig                    | No formatting violations      | ~30s     |
| **Build**              | dotnet build --configuration Release           | Zero build errors             | ~1-2 min |
| **Unit Tests**         | Domain + Application layer tests               | 100% pass, >85% coverage      | ~1-2 min |
| **Integration Tests**  | API tests with TestContainers (Redis)          | 100% pass                     | ~2-3 min |
| **Security Scan**      | Dependency vulnerability check                 | No high/critical CVEs         | ~30s     |
| **Docker Build**       | Multi-stage build with .NET 10                 | Image builds successfully     | ~2-3 min |
| **Container Scan**     | Trivy / Snyk security scan                     | No critical vulnerabilities   | ~1 min   |

**Total Duration**: ~8-12 minutes

#### 15.5.2 CD Pipeline (Main Branch Only)

Automated deployment to staging environment:

**Deployment Flow:**
1. **Build & Tag Container**
   - Build Docker image with commit SHA tag
   - Push to container registry

2. **Staging Deployment** (Automatic)
   - Deploy to staging environment
   - Run smoke tests (health checks, key API endpoints)
   - Verify cache connectivity and background jobs
   - Monitor for errors

**Rollback on Failure:**
- Auto-rollback if smoke tests fail or error rate >5%
- Previous version retained for manual rollback if needed

#### 15.5.3 Environments

| Environment      | Deployment Trigger      | Purpose                       |
| ---------------- | ----------------------- | ----------------------------- |
| **Development**  | Manual (docker-compose) | Local development & debugging |
| **Staging**      | Auto (on main push)     | Testing & demonstration       |

**Key Environment Variables:**
- `REDIS_CONNECTION_STRING`: Redis instance endpoint
- `CACHE_STALENESS_MINUTES`: Staleness threshold (default: 12)
- `WEATHER_SYNC_CRON`: Background job schedule (default: `*/10 * * * *`)
- `LOKI_URL`: Logging aggregation endpoint

#### 15.5.4 Testing Strategy

**Test Coverage:**
- **Unit Tests**: ~200 tests covering domain logic, ranking algorithm, business policies
- **Integration Tests**: ~30 E2E tests with TestContainers for Redis
- **Smoke Tests**: Post-deployment validation (health checks, /top10, /recommendation)
- **Load Tests**: 1000 RPS sustained for 5 minutes using wrk/k6

**Coverage Enforcement**: PRs blocked if coverage drops below 85%

---


---

## 16. Acceptance Criteria

This section maps technical design decisions back to the original requirements.

### 16.1 Performance Requirements

| Requirement       | Target         | Design Solution                                | Acceptance Test                           |
| ----------------- | -------------- | ---------------------------------------------- | ----------------------------------------- |
| API Response Time | ≤ 500ms (p99) | Pre-computed Redis cache, no runtime API calls | `wrk` load test: p99 < 500ms at 100 RPS |
| Cache Hit Rate    | > 99%          | Background refresh every 10 min, 20 min TTL    | Monitor `cache_hit_ratio` metric > 0.99 |
| Throughput        | 1000 RPS       | Stateless API, Redis-backed cache              | `wrk` load test: sustained 1000 RPS     |

### 16.2 Functional Requirements

| Requirement           | Source  | Implementation                                               | Verification                                        |
| --------------------- | ------- | ------------------------------------------------------------ | --------------------------------------------------- |
| Top 10 Districts      | Req 2.4 | `GET /api/v1/districts/top10` returns ranked list          | Unit test: verify sorting by temp ASC, PM2.5 ASC    |
| Travel Recommendation | Req 2.5 | `POST /api/v1/travel/recommendation` with strict AND logic | Unit test: all 4 combination scenarios              |
| 7-Day Forecast        | Req 2.2 | Today + next 6 days at 14:00 local time                      | Integration test: verify date range in API response |
| District Lookup       | Req 2.6 | Case-insensitive dictionary lookup                           | Unit test: "Dhaka", "DHAKA", "dhaka" all resolve    |
| Date Validation       | Req 2.7 | 400 error if travelDate > 7 days                             | Unit test: day 8 returns 400                        |

### 16.3 Reliability Requirements

| Requirement          | Target   | Design Solution                  | Verification                                      |
| -------------------- | -------- | -------------------------------- | ------------------------------------------------- |
| API Availability     | 99.9%    | Cache-first with stale fallback  | Integration test: API responds when Redis down    |
| Job Crash Recovery   | 100%     | Hangfire Redis persistence       | Manual test: restart app mid-job, verify resume   |
| External API Failure | Graceful | Retry policies + cached fallback | Integration test: mock 503, verify stale response |

## 17. Future Improvements

| Improvement                         | Benefit                  | Effort |
| ----------------------------------- | ------------------------ | ------ |
| **Multi-region Redis**        | Global latency reduction | High   |
| **Historical data API**       | Trend analysis           | Medium |
| **Real-time weather updates** | Sub-minute freshness     | High   |

---

## Appendix A: District Data Source

Static JSON file loaded at startup into dictionary:

```json
[
  { "id": "dhaka", "name": "Dhaka", "lat": 23.8103, "long": 90.4125 },
  { "id": "sylhet", "name": "Sylhet", "lat": 24.8949, "long": 91.8687 }
  // ... 62 more districts
]
```

Source: https://raw.githubusercontent.com/strativ-dev/technical-screening-test/main/bd-districts.json

---

## Appendix B: Open-Meteo API Reference

**Weather Forecast:**

```
GET https://api.open-meteo.com/v1/forecast
    ?latitude=23.8103,24.8949,...
    &longitude=90.4125,91.8687,...
    &hourly=temperature_2m
    &forecast_days=7
    &timezone=Asia/Dhaka
```

**Air Quality:**

```
GET https://air-quality-api.open-meteo.com/v1/air-quality
    ?latitude=23.8103,24.8949,...
    &longitude=90.4125,91.8687,...
    &hourly=pm2_5
    &forecast_days=7
    &timezone=Asia/Dhaka
```

---

## Appendix C: Glossary

| Term                          | Definition                                                                                                                    |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **CQRS**                | Command Query Responsibility Segregation - a pattern that separates read and write operations into different models           |
| **LiteBus**             | A lightweight in-process message bus for .NET, used here for dispatching queries to handlers                                  |
| **PM2.5**               | Particulate Matter 2.5 - fine inhalable particles with diameters ≤2.5 micrometers, a key air quality metric                  |
| **Cache-Aside**         | A caching strategy where the application checks the cache first, and on miss, fetches from the source and populates the cache |
| **TTL**                 | Time To Live - duration after which cached data expires automatically                                                         |
| **Staleness Threshold** | The maximum acceptable age of cached data before it's considered stale and requires refresh (12 minutes in this system)       |
| **Idempotent**          | An operation that produces the same result regardless of how many times it's executed                                         |
| **Circuit Breaker**     | A resilience pattern that stops requests to a failing service to prevent cascade failures                                     |
| **Polly**               | A .NET resilience library providing retry, circuit breaker, timeout, and fallback policies                                    |
| **Hangfire**            | A .NET library for background job processing with persistence and automatic retries                                           |
| **Redis**               | An in-memory data structure store used as a cache and message broker                                                          |
| **Loki**                | A log aggregation system designed for cloud-native environments, inspired by Prometheus                                       |
| **LogQL**               | Loki's query language for filtering and analyzing log data                                                                    |
| **ETag**                | Entity Tag - an HTTP header for cache validation, enabling conditional requests                                               |
| **Brotli**              | A compression algorithm that achieves better compression ratios than gzip                                                     |
| **Open-Meteo**          | A free weather API providing forecasts and historical weather data                                                            |
| **Minimal APIs**        | ASP.NET Core's lightweight approach to building HTTP APIs with minimal ceremony                                               |
| **Clean Architecture**  | An architectural pattern emphasizing separation of concerns and dependency inversion                                          |
| **p99**                 | 99th percentile - the value below which 99% of observations fall (used for latency metrics)                                   |
| **RPS**                 | Requests Per Second - a throughput measurement                                                                                |
