# SafeTravel Bangladesh API

A .NET REST API project  to help users decide the best districts in Bangladesh for travel based on temperature and air quality.

## Features
- **Find Best Districts**: Returns the top 10 coolest and cleanest districts in Bangladesh.
- **Travel Recommendation**: Compares a user's current location with a destination to provide travel advice.
- **High Performance**: Optimized with background data caching to ensure <500ms response times.

## Prerequisites
- .NET 10 SDK (or later)
- Internet connection (for fetching weather data from Open-Meteo)

## Getting Started

### 1. Clone the repository
```bash
git clone <repository-url>
```

### 2. Run the Application
```bash
dotnet run --project SafeTravel.Api
```
*Note: Pending project creation.*

## Architecture
- **Data Source**: Open-Meteo (Weather & Air Quality)
- **Caching**: In-Memory Cache with Background Service (IHostedService) for periodic updates.

## API Documentation
Once running, Swagger UI will be available at:
`http://localhost:5000/swagger` (or configured port)

## License
MIT

## Developer Guide

### Contribution Workflow
1.  **Trunk-Based Development**: We follow a trunk-based development strategy.
    -   Small, frequent commits directly to the `main` branch (or short-lived feature branches that merge quickly).
    -   Ensure the code builds and tests pass before pushing.
2.  **Code Style**: Follow standard C# coding conventions.
3.  **Commit Messages**: Write clear, descriptive commit messages.

### Setup for Development
1.  Clone the repo.
2.  Restore dependencies: `dotnet restore`
3.  Run tests: `dotnet test`

