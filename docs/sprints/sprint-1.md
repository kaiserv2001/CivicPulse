# Sprint 1 — Foundation

**PM:** @pm  
**Duration:** Week 1–2 (2026-05-25 → 2026-06-07)  
**Theme:** Get the skeleton running end-to-end: DB up, migrations applied, one real API call returns live data.

---

## Sprint Goal
By end of Sprint 1, a developer can run `docker compose up`, call `GET /api/locations/search?query=seattle`, and receive a live geocoded JSON response from Nominatim. All entities are persisted via EF Core and the solution compiles with zero warnings.

---

## Tasks

### @infrastructure

- [ ] **INFRA-1** — Verify `docker-compose.yml` starts SQL Server 2022 container with the healthcheck passing.
  - **AC:** `docker compose up db` → container reaches `healthy` within 60 s.
  - **AC:** `sqlcmd -S localhost,1433 -U sa -P CivicPulse_Dev123! -Q "SELECT @@VERSION"` exits 0.

- [ ] **INFRA-2** — Add `appsettings.Development.json` to `.gitignore` and document local DB setup in `README.md`.
  - **AC:** File is not tracked by git.
  - **AC:** README has a "Local Setup" section with the three commands needed to start dev environment.

---

### @backend

- [ ] **BACK-1** — Create EF Core initial migration for all four entities: `Location`, `FavoriteLocation`, `WeatherCache`, `AirQualityCache`.
  - **AC:** `dotnet ef migrations add InitialCreate` generates a migration with all four tables and the correct unique indexes as configured in `AppDbContext.OnModelCreating`.
  - **AC:** `dotnet ef database update` applies cleanly against the SQL Server container from INFRA-1.
  - **Files:** `src/CivicPulse.Infrastructure/Data/AppDbContext.cs`, new `Migrations/` folder.

- [ ] **BACK-2** — Register all services in `Program.cs` using EF Core DI pattern.
  - **AC:** `builder.Services.AddDbContext<AppDbContext>()` reads connection string from `ConnectionStrings:DefaultConnection`.
  - **AC:** All three HTTP clients (`OpenMeteoClient`, `OpenAQClient`, `NominatimClient`) are registered with correct `BaseAddress` and `Timeout`.
  - **AC:** `ILocationRepository` → `LocationRepository` registered as `Scoped`.
  - **AC:** `IOutdoorScoringService` → `OutdoorScoringService` registered as `Scoped`.
  - **Ref:** EF Core DI — https://learn.microsoft.com/ef/core/dbcontext-configuration/
  - **Files:** `src/CivicPulse.API/Program.cs`

- [ ] **BACK-3** — Wire `GET /api/locations/search?query={q}` end-to-end.
  - **AC:** Request with `query=seattle` returns HTTP 200 with a JSON array of at least 1 `LocationSearchResult`.
  - **AC:** Request with `query=x` (1 char) returns HTTP 400 with `{ "error": "Query must be at least 2 characters." }`.
  - **AC:** Second identical request within 24 h is served from `IMemoryCache` (no outbound HTTP call).
  - **Files:** `src/CivicPulse.API/Controllers/LocationsController.cs`, `src/CivicPulse.Infrastructure/ExternalClients/NominatimClient.cs`

- [ ] **BACK-4** — Configure Serilog structured logging.
  - **AC:** Every request logs a single line at `Information` with `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`.
  - **AC:** `NominatimClient` logs `"Geocoding query: {Query}"` at `Information` on each outbound call.
  - **AC:** Log output goes to console and to `logs/civicpulse-YYYYMMDD.log` (rolling daily).
  - **Ref:** Serilog.AspNetCore — https://github.com/serilog/serilog-aspnetcore
  - **Files:** `src/CivicPulse.API/Program.cs`

- [ ] **BACK-5** — Configure Swagger / OpenAPI.
  - **AC:** `GET /swagger` returns the Swagger UI in Development mode.
  - **AC:** All four controllers appear with correct HTTP verbs, routes, and response type annotations.
  - **Files:** `src/CivicPulse.API/Program.cs`

---

### @qa

- [ ] **QA-1** — Confirm both test projects compile and `dotnet test` exits 0 (even with zero tests).
  - **AC:** `dotnet test tests/CivicPulse.UnitTests/` exits 0.
  - **AC:** `dotnet test tests/CivicPulse.IntegrationTests/` exits 0.

- [ ] **QA-2** — Write test skeleton for `OutdoorScoringServiceTests.cs` (class and method stubs only; no assertions yet).
  - **AC:** File exists at `tests/CivicPulse.UnitTests/Scoring/OutdoorScoringServiceTests.cs` with at least 6 `[Fact]` method stubs matching the naming convention `MethodName_State_ExpectedBehavior`.
  - **Files:** `tests/CivicPulse.UnitTests/Scoring/OutdoorScoringServiceTests.cs`

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| INFRA-1 (DB healthy) | BACK-1 (migration) | Pending |
| BACK-1 (migration applied) | BACK-2 (DI wiring) | Pending |
| BACK-2 (DI wiring) | BACK-3 (search endpoint) | Pending |

---

## Definition of Done Checklist
- [ ] All tasks above have `[x]`.
- [ ] `dotnet build` exits 0 with zero warnings.
- [ ] `dotnet test` exits 0.
- [ ] `GET /api/locations/search?query=seattle` returns 200 with live data.
- [ ] Swagger UI accessible at `http://localhost:5000/swagger`.
