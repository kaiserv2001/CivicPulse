# CivicPulse — Environmental Decision Dashboard

> Helps users decide whether it is a good day to walk, bike, commute, or work outdoors in any city.

## Architecture

```
CivicPulse/
├── src/
│   ├── CivicPulse.API/              # ASP.NET Core 10 Web API + Serilog + Swagger
│   ├── CivicPulse.Core/             # Domain entities, interfaces, models, scoring service
│   ├── CivicPulse.Infrastructure/   # EF Core, HTTP clients, Redis cache, background job
│   └── CivicPulse.Web/              # Blazor Server frontend
└── tests/
    ├── CivicPulse.UnitTests/        # xUnit + Moq + FluentAssertions
    └── CivicPulse.IntegrationTests/ # WebApplicationFactory end-to-end
```

**Project team docs:** [`docs/agents/README.md`](docs/agents/README.md)  
**Sprint plans:** [`docs/sprints/`](docs/sprints/)

## External APIs

| API | Purpose | Key required |
|-----|---------|--------------|
| [Open-Meteo](https://open-meteo.com/en/docs) | Current weather + 7-day forecast | No |
| [OpenAQ v3](https://docs.openaq.org/) | Real-time + historical air quality (PM2.5, PM10, NO₂, O₃) | Yes (free tier) |
| [Nominatim / OSM](https://nominatim.openstreetmap.org/search) | City geocoding (1 req/s, cached 24 h) | No |

> **Note:** If no OpenAQ key is configured the dashboard loads with AQI defaulting to "Unknown" — all other features remain fully functional.

## REST Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/locations/search?query=` | — | Search city by name |
| GET | `/api/dashboard/{locationId}` | — | Weather + AQ + score + 7-day forecast |
| GET | `/api/dashboard/{locationId}/aqtrend` | — | 7-day daily AQI history |
| GET | `/api/dashboard/compare?loc1=&loc2=` | — | Side-by-side city comparison |
| GET | `/api/favorites` | JWT | List saved favorites |
| POST | `/api/favorites` | JWT | Save a location |
| DELETE | `/api/favorites/{id}` | JWT | Remove a favorite |
| POST | `/api/auth/register` | — | Create account → returns JWT |
| POST | `/api/auth/login` | — | Authenticate → returns JWT |
| GET | `/api/profile` | JWT | Get email and account creation date |
| PUT | `/api/profile/email` | JWT | Update email |
| PUT | `/api/profile/password` | JWT | Change password |

Swagger UI available at `/swagger` when running locally.

## Outdoor Suitability Score

Scores 0–100 composed of four weighted sub-scores:

| Component | Weight | Basis |
|-----------|--------|-------|
| Weather | 35% | Weather code, temperature range |
| Air Quality | 30% | US EPA AQI breakpoints (PM2.5) |
| Wind | 20% | Wind speed km/h |
| UV Index | 15% | WHO UV Index scale |

Grades: **A** (≥85) · **B** (≥70) · **C** (≥55) · **D** (≥40) · **F** (<40)

## Local Setup (no Docker required)

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 1. Run the API (in-memory database + in-memory cache)

```bash
dotnet run --project src/CivicPulse.API --urls http://localhost:5000
```

The `"USE_INMEMORY": "1"` key in `appsettings.json` bypasses SQL Server and Redis entirely.  
Swagger UI: http://localhost:5000/swagger

### 2. Run the Blazor frontend

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/CivicPulse.Web \
  --urls http://localhost:5001
```

App: http://localhost:5001

### 3. Run the tests

```bash
dotnet test
```

20 tests · 12 unit · 8 integration — all passing.

---

### With Docker (SQL Server + Redis)

```bash
docker compose up --build
```

- API + Swagger: http://localhost:5000/swagger
- Blazor UI: http://localhost:5001
- Redis: `localhost:6379`

EF migrations run automatically on startup. Cache entries survive container restarts via the named `redisdata` volume.

## TypeScript Client

A typed TypeScript client can be generated from the live Swagger spec. Requires Node.js ≥ 18 and the API running at `localhost:5000`.

```bash
bash scripts/gen-ts-client.sh
```

This runs `openapi-typescript` and writes type definitions to `clients/typescript/api.d.ts`. The `clients/typescript/` directory is git-ignored (generated artefact).

## Stack

- ASP.NET Core 10 Web API
- Entity Framework Core 10 + SQL Server 2022 (or in-memory for dev)
- Redis 7 (`IDistributedCache`) for shared, persistent caching
- Blazor Server with Chart.js interop
- JWT authentication (HS256, 24-hour expiry)
- Rate limiting: 60 req/min per IP (ASP.NET Core built-in)
- Serilog (structured logging, rolling file + console)
- FluentValidation 11
- xUnit + Moq + FluentAssertions
- GitHub Actions CI
