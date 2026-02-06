# SafeTravel Bangladesh API

A .NET 10 REST API that recommends optimal travel destinations across Bangladesh's 64 districts based on **temperature** and **air quality (PM2.5)**.

## Features

### Top 10 Districts

Returns the **coolest and cleanest** districts in Bangladesh, ranked by:

- Average temperature at 2 PM over 7 days (ascending)
- Average PM2.5 at 2 PM over 7 days (ascending)

### Travel Recommendation

Compares your **current location** with a **destination district** and recommends travel only if:

- Destination is cooler (lower temperature) **AND**
- Destination has better air quality (lower PM2.5)

### Performance

- **<500ms** response time (p99) via pre-computed Redis cache
- **Background sync** every 10 minutes keeps data fresh
- **Fallback**: If cache is stale (>12 min) and no job running, triggers manual data load

## Data Sources

| Data                 | Source                                                                                                          | Update Frequency      |
| -------------------- | --------------------------------------------------------------------------------------------------------------- | --------------------- |
| District Coordinates | [bd-districts.json](https://raw.githubusercontent.com/strativ-dev/technical-screening-test/main/bd-districts.json) | Static (64 districts) |
| Weather Forecast     | [Open-Meteo Weather API](https://open-meteo.com/)                                                                  | Every 10 min          |
| Air Quality (PM2.5)  | [Open-Meteo Air Quality API](https://open-meteo.com/)                                                              | Every 10 min          |

## Tech Stack

| Component       | Technology                               |
| --------------- | ---------------------------------------- |
| Runtime         | .NET 10, ASP.NET Core Minimal APIs       |
| Architecture    | Clean Architecture, CQRS (LiteBus)       |
| Caching         | Redis (primary), IMemoryCache (fallback) |
| Background Jobs | Hangfire (10-min refresh cycle)          |
| External API    | Open-Meteo (Weather & Air Quality)       |
| Logging         | Serilog + Grafana/Loki                   |
| Testing         | xUnit, NSubstitute, Shouldly, WireMock   |

## API Endpoints

| Method | Endpoint                          | Description                           |
| ------ | --------------------------------- | ------------------------------------- |
| GET    | `/api/v1/districts/top10`       | Top 10 coolest & cleanest districts   |
| POST   | `/api/v1/travel/recommendation` | Travel recommendation for destination |
| GET    | `/health/live`                  | Liveness probe                        |
| GET    | `/health/ready`                 | Readiness probe (Redis + sync status) |
| GET    | `/hangfire`                     | Background jobs dashboard             |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) (for Redis, Grafana, Loki)

### Running Locally

```bash
# Clone the repository
git clone https://github.com/Mahdi-Hasan/SafeTravel-Bangladesh-API
cd SafeTravel-Bangladesh-API

# Run (requires Redis)
docker-compose up -d
dotnet run --project src/SafeTravel.Api
```

**Swagger UI**: `http://localhost:5000/swagger`

## Documentation

- [Technical Design Document](docs/technical_design_document.md)

## License

MIT
