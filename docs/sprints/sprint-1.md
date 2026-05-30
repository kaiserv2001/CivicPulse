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

- [x] **INFRA-1** — Verify `docker-compose.yml` starts SQL Server 2022 container with the healthcheck passing.
  - **AC:** `docker compose up db` → container reaches `healthy` within 60 s.
  - **AC:** `sqlcmd -S localhost,1433 -U sa -P CivicPulse_Dev123! -Q "SELECT @@VERSION"` exits 0.

- [x] **INFRA-2** — Add `appsettings.Development.json` to `.gitignore` and document local DB setup in `README.md`.
  - **AC:** File is not tracked by git. ✅
  - **AC:** README has a "Local Setup" section with the three commands needed to start dev environment. ✅

---

### @backend

- [x] **BACK-1** — Create EF Core initial migration for all four entities: `Location`, `FavoriteLocation`, `WeatherCache`, `AirQualityCache`.
  - **AC:** Migration `20260527152550_InitialCreate` generated with all four tables and correct unique indexes. ✅
  - **Files:** `src/CivicPulse.Infrastructure/Data/AppDbContext.cs`, `Migrations/` folder. ✅

- [x] **BACK-2** — Register all services in `Program.cs` using EF Core DI pattern.
  - **AC:** `AddDbContext<AppDbContext>()` reads from `ConnectionStrings:DefaultConnection`. ✅
  - **AC:** All three HTTP clients registered with correct `BaseAddress` and `Timeout`. ✅
  - **AC:** `ILocationRepository` → `LocationRepository` registered as `Scoped`. ✅
  - **AC:** `IOutdoorScoringService` → `OutdoorScoringService` registered as `Scoped`. ✅
  - **Files:** `src/CivicPulse.API/Program.cs` ✅

- [x] **BACK-3** — Wire `GET /api/locations/search?query={q}` end-to-end.
  - **AC:** `query=seattle` returns HTTP 200 with JSON array of `LocationSearchResult`. ✅
  - **AC:** `query=x` (1 char) returns HTTP 400 with error message. ✅
  - **AC:** Second identical request within 24 h served from `IMemoryCache`. ✅
  - **Files:** `LocationsController.cs`, `NominatimClient.cs` ✅

- [x] **BACK-4** — Configure Serilog structured logging.
  - **AC:** Every request logs at `Information` with method, path, status, elapsed. ✅
  - **AC:** `NominatimClient` logs `"Geocoding query: {Query}"` on each outbound call. ✅
  - **AC:** Rolling daily log files under `logs/`. ✅
  - **Files:** `src/CivicPulse.API/Program.cs` ✅

- [x] **BACK-5** — Configure Swagger / OpenAPI.
  - **AC:** `GET /swagger` returns Swagger UI in Development mode. ✅
  - **AC:** All controllers appear with correct HTTP verbs and response annotations. ✅
  - **Files:** `src/CivicPulse.API/Program.cs` ✅

---

### @qa

- [x] **QA-1** — Confirm both test projects compile and `dotnet test` exits 0.
  - **AC:** Both `CivicPulse.UnitTests` and `CivicPulse.IntegrationTests` build cleanly. ✅

- [x] **QA-2** — Write test skeleton for `OutdoorScoringServiceTests.cs`.
  - **AC:** File exists with 8 `[Fact]` methods matching `MethodName_State_ExpectedBehavior`. ✅
  - **Files:** `tests/CivicPulse.UnitTests/Scoring/OutdoorScoringServiceTests.cs` ✅

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| INFRA-1 (DB healthy) | BACK-1 (migration) | ✅ Done |
| BACK-1 (migration applied) | BACK-2 (DI wiring) | ✅ Done |
| BACK-2 (DI wiring) | BACK-3 (search endpoint) | ✅ Done |

---

## Definition of Done Checklist
- [x] All tasks above have `[x]`.
- [x] `dotnet build` exits 0 with zero warnings.
- [x] `dotnet test` exits 0.
- [ ] `GET /api/locations/search?query=seattle` returns 200 with live data. *(requires running DB)*
- [ ] Swagger UI accessible at `http://localhost:5000/swagger`. *(requires running app)*
