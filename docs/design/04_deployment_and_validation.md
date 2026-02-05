# Deployment & Validation

**Part of:** [SafeTravel Bangladesh API Technical Design](../technical_design_document.md)

**Version:** 1.0
**Date:** February 4, 2026

---

## Table of Contents

1. [Trade-offs & Design Justification](#1-trade-offs--design-justification)
2. [Infrastructure & Deployment](#2-infrastructure--deployment)
3. [Acceptance Criteria](#3-acceptance-criteria)
4. [Future Improvements](#4-future-improvements)
5. [Appendix A: District Data Source](#appendix-a-district-data-source)
6. [Appendix B: Open-Meteo API Reference](#appendix-b-open-meteo-api-reference)
7. [Appendix C: Glossary](#appendix-c-glossary)

---

## 1. Trade-offs & Design Justification

### 1.1 Hangfire vs IHostedService

| Decision           | Hangfire            | Trade-off                                      |
| ------------------ | ------------------- | ---------------------------------------------- |
| **Chosen**   | Hangfire with Redis | Higher operational complexity (Redis required) |
| **Rejected** | IHostedService      | Simpler but jobs lost on restart               |

**Justification**: The 500ms API latency requirement mandates pre-computed cached data. A missed sync job directly impacts data freshness. Hangfire's persistent job queue ensures syncs complete even across deployments, which is critical for a travel recommendation API where stale temperature data could lead to poor recommendations.

### 1.2 Redis vs In-Memory Cache

| Decision                | Redis                  | Trade-off                              |
| ----------------------- | ---------------------- | -------------------------------------- |
| **Chosen**        | Redis as primary cache | Requires infrastructure, added latency |
| **Complementary** | In-memory L1 (5s TTL)  | Limited by memory, not shared          |

**Justification**: Horizontal scaling requires shared cache state. Multiple API instances must see the same pre-computed rankings. Redis provides atomic operations for cache updates, preventing partial reads during sync.

### 1.3 Bulk API Requests vs Per-District Calls

| Decision           | Bulk Requests                   | Trade-off                              |
| ------------------ | ------------------------------- | -------------------------------------- |
| **Chosen**   | Single request for 64 districts | Larger payload, all-or-nothing failure |
| **Rejected** | 64 parallel requests            | Higher latency, rate limit risk        |

**Justification**: Open-Meteo supports bulk coordinate queries. A single request for 64 districts completes in ~2-3 seconds vs ~30+ seconds for sequential calls. The risk of partial failure is mitigated by retry logic and stale cache fallback.

### 1.4 CQRS Without Separate Read/Write Models

| Decision                | Simplified CQRS                         | Trade-off       |
| ----------------------- | --------------------------------------- | --------------- |
| **Chosen**        | Queries only, no commands               | Not "pure" CQRS |
| **Justification** | This is a read-only API; no user writes | -               |

**Justification**: The API consumes external data and serves computed results. There are no user-initiated write operations, making full CQRS with commands unnecessary and over-engineered.

### 1.5 HTTP Verb for Travel Recommendation

| Decision           | `POST /api/v1/travel/recommendation` | Trade-off                                  |
| :----------------- | :------------------------------------- | :----------------------------------------- |
| **Chosen**   | **POST**                         | "Read" operation using write verb semantic |
| **Rejected** | GET                                    | Query parameters would be complex/lengthy  |

**Justification**: While technically a read query, **POST** is used because the request requires a complex body (`currentLocation`, `destinationDistrict`, `travelDate`). Sending complex JSON objects in GET query parameters is messy and has length limitations. This aligns with standard practices for complex search/calculation endpoints.

---

## 2. Infrastructure & Deployment

### 2.1 Container Architecture

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

### 2.2 Docker Compose (Local Development)

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

### 2.3 Cloud Deployment Considerations

| Aspect            | Recommendation                          | Notes                             |
| ----------------- | --------------------------------------- | --------------------------------- |
| **Compute** | Azure Container Apps / AWS ECS          | Serverless container hosting      |
| **Redis**   | Azure Cache for Redis / AWS ElastiCache | Managed Redis with HA             |
| **Logs**    | Grafana Cloud or self-hosted Loki       | Managed option reduces ops burden |
| **Secrets** | Azure Key Vault / AWS Secrets Manager   | Never store in env vars in prod   |
| **CI/CD**   | GitHub Actions        | Docker build → push → deploy    |

### 2.4 API Documentation (Swagger/OpenAPI)

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

### 2.5 CI/CD Pipeline

#### 2.5.1 CI Pipeline (All Branches)

Runs on every push and pull request:

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

#### 2.5.2 CD Pipeline (Main Branch Only)

Automated deployment to staging:

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

#### 2.5.3 Environments

| Environment      | Deployment Trigger      | Purpose                       |
| ---------------- | ----------------------- | ----------------------------- |
| **Development**  | Manual (docker-compose) | Local development & debugging |
| **Staging**      | Auto (on main push)     | Testing & demonstration       |

**Key Environment Variables:**
- `REDIS_CONNECTION_STRING`: Redis instance endpoint
- `CACHE_STALENESS_MINUTES`: Staleness threshold (default: 12)
- `WEATHER_SYNC_CRON`: Background job schedule (default: `*/10 * * * *`)
- `LOKI_URL`: Logging aggregation endpoint

#### 2.5.4 Testing Strategy

**Test Coverage:**
- **Unit Tests**: ~200 tests covering domain logic, ranking algorithm, business policies
- **Integration Tests**: ~30 E2E tests with TestContainers for Redis
- **Smoke Tests**: Post-deployment validation (health checks, /top10, /recommendation)
- **Load Tests**: 1000 RPS sustained for 5 minutes using wrk/k6

**Coverage Enforcement**: PRs blocked if coverage drops below 85%

---

## 3. Acceptance Criteria

### 3.1 Performance Requirements

| Requirement       | Target         | Design Solution                                | Acceptance Test                           |
| ----------------- | -------------- | ---------------------------------------------- | ----------------------------------------- |
| API Response Time | ≤ 500ms (p99) | Pre-computed Redis cache, no runtime API calls | `wrk` load test: p99 < 500ms at 100 RPS |
| Cache Hit Rate    | > 99%          | Background refresh every 10 min, 20 min TTL    | Monitor `cache_hit_ratio` metric > 0.99 |
| Throughput        | 1000 RPS       | Stateless API, Redis-backed cache              | `wrk` load test: sustained 1000 RPS     |

### 3.2 Functional Requirements

| Requirement           | Source  | Implementation                                               | Verification                                        |
| --------------------- | ------- | ------------------------------------------------------------ | --------------------------------------------------- |
| Top 10 Districts      | Req 2.4 | `GET /api/v1/districts/top10` returns ranked list          | Unit test: verify sorting by temp ASC, PM2.5 ASC    |
| Travel Recommendation | Req 2.5 | `POST /api/v1/travel/recommendation` with strict AND logic | Unit test: all 4 combination scenarios              |
| 7-Day Forecast        | Req 2.2 | Today + next 6 days at 14:00 local time                      | Integration test: verify date range in API response |
| District Lookup       | Req 2.6 | Case-insensitive dictionary lookup                           | Unit test: "Dhaka", "DHAKA", "dhaka" all resolve    |
| Date Validation       | Req 2.7 | 400 error if travelDate > 7 days                             | Unit test: day 8 returns 400                        |

### 3.3 Reliability Requirements

| Requirement          | Target   | Design Solution                  | Verification                                      |
| -------------------- | -------- | -------------------------------- | ------------------------------------------------- |
| API Availability     | 99.9%    | Cache-first with stale fallback  | Integration test: API responds when Redis down    |
| Job Crash Recovery   | 100%     | Hangfire Redis persistence       | Manual test: restart app mid-job, verify resume   |
| External API Failure | Graceful | Retry policies + cached fallback | Integration test: mock 503, verify stale response |

---

## 4. Future Improvements

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

---

**Previous:** [API & Operations](./03_api_and_operations.md)
