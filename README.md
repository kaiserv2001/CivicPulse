# CivicPulse — Environmental Decision Dashboard

> Helps users decide whether it is a good day to walk, bike, commute, or work outdoors in any city.

## Live Demo

**🌐 [civicpulse-7ij3.onrender.com](https://civicpulse-7ij3.onrender.com)**

Hosted free on Render (single app serving the Blazor UI + API). A couple of things to expect:

- **Cold start:** the free instance sleeps after ~15 min of inactivity, so the first request may take ~30–50s to wake. Loads after that are fast.
- **Ephemeral data:** it runs in in-memory mode — registered accounts and saved favorites are temporary and get wiped whenever the instance restarts or wakes from idle, which also signs you out. Use a throwaway email/password.

## Architecture

```
CivicPulse/
├── src/
│   ├── CivicPulse.API/              # ASP.NET Core 10 Web API + Blazor Server UI + Serilog + Swagger
│   ├── CivicPulse.Core/             # Domain entities, interfaces, models, scoring service
│   └── CivicPulse.Infrastructure/   # EF Core, HTTP clients, Redis cache, background job
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

### 1. Run the app (API + Blazor UI, in-memory database + in-memory cache)

The Web API and the Blazor Server UI are served from a single app.

```bash
dotnet run --project src/CivicPulse.API
```

The `"USE_INMEMORY": "1"` key in `appsettings.json` bypasses SQL Server and Redis entirely.  
The app binds to http://localhost:8080 by default (see `Properties/launchSettings.json`), and the
UI calls its own API via the `ApiBaseUrl` setting — if you change the port, set `ApiBaseUrl` to match.

- App / Blazor UI: http://localhost:8080
- Swagger UI: http://localhost:8080/swagger

### 2. Run the tests

```bash
dotnet test
```

20 tests · 12 unit · 8 integration — all passing.

---

### With Docker (SQL Server + Redis)

```bash
docker compose up --build
```

- App / Blazor UI: http://localhost:5000
- API + Swagger: http://localhost:5000/swagger
- Redis: `localhost:6379`

EF migrations run automatically on startup. Cache entries survive container restarts via the named `redisdata` volume.

## TypeScript Client

A typed TypeScript client can be generated from the live Swagger spec. Requires Node.js ≥ 18 and the app running at `localhost:8080`.

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
