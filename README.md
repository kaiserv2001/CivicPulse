# CivicPulse — Environmental Decision Dashboard

> Helps users decide whether it is a good day to walk, bike, commute, or work outdoors in any city.

## Architecture

```
CivicPulse/
├── src/
│   ├── CivicPulse.API/           # ASP.NET Core 8 Web API + Serilog + Swagger
│   ├── CivicPulse.Core/          # Domain entities, interfaces, models, scoring service
│   ├── CivicPulse.Infrastructure/# EF Core, HTTP clients, repository, background job
│   └── CivicPulse.Web/           # Blazor Server frontend
└── tests/
    ├── CivicPulse.UnitTests/     # xUnit + Moq + FluentAssertions
    └── CivicPulse.IntegrationTests/ # WebApplicationFactory end-to-end
```

**Project team docs:** [`docs/agents/README.md`](docs/agents/README.md)  
**Sprint plans:** [`docs/sprints/`](docs/sprints/)

## External APIs (all free, no key required for basic use)

| API | Purpose | Docs |
|-----|---------|------|
| [Open-Meteo](https://open-meteo.com/en/docs) | Current weather + 7-day forecast | No key needed |
| [OpenAQ v3](https://docs.openaq.org/) | Real-time air quality (PM2.5, PM10, NO2, O3) | No key needed |
| [Nominatim / OSM](https://nominatim.openstreetmap.org/search) | City geocoding | 1 req/sec limit; results cached 24 h |

## REST Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/locations/search?query=` | Search city by name |
| GET | `/api/dashboard/{locationId}` | Full weather + AQ + outdoor score + 7-day forecast |
| GET | `/api/dashboard/compare?loc1=&loc2=` | Side-by-side city comparison |
| GET | `/api/recommendations/{locationId}` | Activity suitability recommendations |
| GET | `/api/favorites` | List saved favorites (JWT required) |
| POST | `/api/favorites` | Save a location (JWT required) |
| DELETE | `/api/favorites/{id}` | Remove a favorite (JWT required) |
| POST | `/api/auth/register` | Create account, returns JWT |
| POST | `/api/auth/login` | Authenticate, returns JWT |

## Outdoor Suitability Score

Scores 0–100 composed of four weighted sub-scores:

| Component | Weight | Basis |
|-----------|--------|-------|
| Weather | 35% | Weather code, temperature range |
| Air Quality | 30% | US EPA AQI breakpoints (PM2.5) |
| Wind | 20% | Wind speed km/h |
| UV Index | 15% | WHO UV Index scale |

Grades: **A** (≥85) · **B** (≥70) · **C** (≥55) · **D** (≥40) · **F** (<40)

## Local Setup

### Prerequisites
- Docker Desktop
- .NET 8 SDK
- (Optional) VS Code or Visual Studio 2022

### 1. Start the database
```bash
docker compose up db
```
Wait until the container is `healthy` (about 30 seconds).

### 2. Apply EF Core migrations
```bash
dotnet ef database update --project src/CivicPulse.Infrastructure --startup-project src/CivicPulse.API
```

### 3. Run the API
```bash
dotnet run --project src/CivicPulse.API
```
Swagger UI: http://localhost:5000/swagger

### 4. Run the Blazor frontend
```bash
dotnet run --project src/CivicPulse.Web
```
App: http://localhost:5001

### 5. Run tests
```bash
dotnet test
```

## Stack
- ASP.NET Core 8 Web API
- Entity Framework Core 8 + SQL Server 2022
- Blazor Server
- Serilog (structured logging, rolling file + console)
- FluentValidation 11
- xUnit + Moq + FluentAssertions
- Docker Compose
- GitHub Actions CI
