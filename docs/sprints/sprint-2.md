# Sprint 2 — Business Logic & API

**PM:** @pm  
**Duration:** Week 3–4 (2026-06-08 → 2026-06-21)  
**Theme:** All five REST endpoints working, the `OutdoorScoringService` fully tested, background job running, caching layer solid.  
**Prerequisite:** Sprint 1 DoD fully checked off.

---

## Sprint Goal
By end of Sprint 2, a developer can call all five endpoints against a running local instance and get correct responses. The scoring service has ≥ 8 unit tests passing. The `WeatherRefreshJob` runs every 30 minutes in the background without errors.

---

## Tasks

### @backend

- [ ] **BACK-6** — Implement `GET /api/dashboard/{locationId}` fully.
  - **AC:** Returns HTTP 404 with `{ "error": "Location X not found." }` when the location ID does not exist in the DB.
  - **AC:** Returns HTTP 200 `DashboardResponse` JSON including: `currentWeather`, `airQuality`, `score` (with `total`, `grade`, `summary`), `forecast` (7 days), and `recommendations` (4 activities).
  - **AC:** Response is stored in `IMemoryCache` with key `dashboard_{locationId}` for 15 minutes; second identical call does not trigger new outbound HTTP requests.
  - **AC:** Weather and air quality are fetched in parallel (`Task.WhenAll`), not sequentially.
  - **Ref:** Open-Meteo forecast params — https://open-meteo.com/en/docs — `daily` variables: `temperature_2m_max`, `temperature_2m_min`, `precipitation_sum`, `precipitation_probability_max`, `wind_speed_10m_max`, `uv_index_max`, `weather_code`.
  - **Files:** `src/CivicPulse.API/Controllers/DashboardController.cs`

- [ ] **BACK-7** — Implement `POST /api/favorites` and `DELETE /api/favorites/{id}`.
  - **AC:** `POST` with a valid `AddFavoriteRequest` body upserts the `Location` row if coordinates don't already exist (within 0.001° tolerance), then inserts `FavoriteLocation`.
  - **AC:** `POST` returns HTTP 201 with `{ "id": <int>, "name": "<string>" }`.
  - **AC:** Duplicate `POST` for same `userId` + `locationId` returns HTTP 409 `{ "error": "Already in favorites." }`.
  - **AC:** `DELETE /api/favorites/{id}` returns HTTP 204 and removes the row. Non-owner delete is silently ignored (no 403 until JWT is added in Sprint 3).
  - **Files:** `src/CivicPulse.API/Controllers/FavoritesController.cs`

- [ ] **BACK-8** — Implement `GET /api/recommendations/{locationId}`.
  - **AC:** Returns HTTP 200 `RecommendationResponse` with `score` and `activities` array of 4 items (Walking, Cycling, Outdoor Commute, Outdoor Work / Exercise), each with `suitable: bool` and `reason: string`.
  - **AC:** Returns HTTP 404 if location not found.
  - **Files:** `src/CivicPulse.API/Controllers/RecommendationsController.cs`

- [ ] **BACK-9** — Implement `GET /api/dashboard/compare?loc1={id}&loc2={id}`.
  - **AC:** Both locations fetched in parallel.
  - **AC:** Response includes `locationA`, `locationB` (full `DashboardResponse` each), `winner` (name of location with higher `score.total`), and `winnerReason` string.
  - **AC:** Returns HTTP 404 if either location ID is not found, specifying which one.
  - **Files:** `src/CivicPulse.API/Controllers/DashboardController.cs`

- [ ] **BACK-10** — Add FluentValidation for `AddFavoriteRequest`.
  - **AC:** `CityName` — required, max 200 chars.
  - **AC:** `Country` — required, max 100 chars.
  - **AC:** `Latitude` — must be between −90 and 90.
  - **AC:** `Longitude` — must be between −180 and 180.
  - **AC:** Invalid request returns HTTP 400 with FluentValidation error messages.
  - **Ref:** FluentValidation DI auto-registration — https://docs.fluentvalidation.net/en/latest/di.html
  - **Files:** New `src/CivicPulse.API/Validators/AddFavoriteRequestValidator.cs`

- [ ] **BACK-11** — Confirm `WeatherRefreshJob` runs and logs correctly.
  - **AC:** On app startup, the job logs `"WeatherRefreshJob started."` at `Information`.
  - **AC:** After 30 minutes, the job logs `"Refreshing weather for {Count} favorited locations."`.
  - **AC:** If a location refresh throws, the job logs a `Warning` and continues to the next location (no crash).
  - **Files:** `src/CivicPulse.Infrastructure/BackgroundJobs/WeatherRefreshJob.cs`

---

### @qa

- [ ] **QA-3** — Complete all 8 unit tests in `OutdoorScoringServiceTests.cs`.
  - **AC:** All 8 tests in the file pass with `dotnet test`.
  - **AC:** Tests cover: ideal conditions (grade A), hazardous air (low total), thunderstorm (weather penalty), high wind (wind score = 0), extreme heat (weather penalty), total clamped to 0–100, rain → walking not suitable, ideal → all activities suitable.
  - **Files:** `tests/CivicPulse.UnitTests/Scoring/OutdoorScoringServiceTests.cs`

- [ ] **QA-4** — Write `OpenMeteoClientTests.cs` — unit test the HTTP client using a mock `HttpMessageHandler`.
  - **AC:** `GetCurrentWeatherAsync_ValidCoords_ReturnsParsedWeatherData` — mock returns a valid JSON fixture; asserts all fields map correctly.
  - **AC:** `GetCurrentWeatherAsync_CalledTwice_SecondCallHitsCacheNotHttp` — verifies the mock `HttpMessageHandler` is only invoked once on two identical calls within the cache TTL.
  - **Files:** `tests/CivicPulse.UnitTests/Services/OpenMeteoClientTests.cs`

- [ ] **QA-5** — Write `NominatimClientTests.cs`.
  - **AC:** `SearchAsync_ValidQuery_ReturnsMappedResults` — mock returns a Nominatim JSON fixture; asserts `DisplayName`, `Latitude`, `Longitude` are correctly parsed.
  - **AC:** `SearchAsync_CalledTwiceWithSameQuery_SecondCallServedFromCache`.
  - **Files:** `tests/CivicPulse.UnitTests/Services/NominatimClientTests.cs`

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| BACK-1 migration applied (Sprint 1) | BACK-7 (favorites DB writes) | Sprint 1 |
| BACK-6 (DashboardResponse shape final) | QA-3 assertions using DashboardResponse | Pending |
| BACK-10 (validator registered) | BACK-7 AC on 400 response | Pending |

---

## Definition of Done Checklist
- [ ] All tasks above have `[x]`.
- [ ] `dotnet test` exits 0; all 8 scoring tests + 4 client tests pass.
- [ ] `GET /api/dashboard/1` returns a valid `DashboardResponse` with weather + AQ + score.
- [ ] `POST /api/favorites` persists a row visible in the DB.
- [ ] `GET /api/recommendations/1` returns 4 activity items.
- [ ] `GET /api/dashboard/compare?loc1=1&loc2=2` returns a `CompareResponse`.
- [ ] Background job logs visible in console on app start.
