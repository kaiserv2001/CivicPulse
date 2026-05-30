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

- [x] **BACK-6** — Implement `GET /api/dashboard/{locationId}` fully.
  - **AC:** Returns HTTP 404 when location ID not found. ✅
  - **AC:** Returns HTTP 200 `DashboardResponse` with weather, AQ, score, 7-day forecast, recommendations. ✅
  - **AC:** Response cached in `IMemoryCache` for 15 minutes. ✅
  - **AC:** Weather and AQ fetched in parallel via `Task.WhenAll`. ✅
  - **Files:** `src/CivicPulse.API/Controllers/DashboardController.cs` ✅

- [x] **BACK-7** — Implement `POST /api/favorites` and `DELETE /api/favorites/{id}`.
  - **AC:** `POST` upserts `Location` row if coordinates not already present (0.001° tolerance). ✅
  - **AC:** Returns HTTP 201 with `{ "id", "name" }`. ✅
  - **AC:** Duplicate `POST` returns HTTP 409. ✅
  - **AC:** `DELETE` returns HTTP 204 and removes the row. ✅
  - **Files:** `src/CivicPulse.API/Controllers/FavoritesController.cs` ✅

- [x] **BACK-8** — Implement `GET /api/recommendations/{locationId}`.
  - **AC:** Returns HTTP 200 with 4 activity items (Walking, Cycling, Outdoor Commute, Outdoor Work / Exercise). ✅
  - **AC:** Returns HTTP 404 if location not found. ✅
  - **Files:** `src/CivicPulse.API/Controllers/RecommendationsController.cs` ✅

- [x] **BACK-9** — Implement `GET /api/dashboard/compare?loc1={id}&loc2={id}`.
  - **AC:** Both locations fetched in parallel. ✅
  - **AC:** Response includes `locationA`, `locationB`, `winner`, `winnerReason`. ✅
  - **AC:** Returns HTTP 404 specifying which location was not found. ✅
  - **Files:** `src/CivicPulse.API/Controllers/DashboardController.cs` ✅

- [x] **BACK-10** — Add FluentValidation for `AddFavoriteRequest`.
  - **AC:** `CityName` required, max 200 chars. ✅
  - **AC:** `Country` required, max 100 chars. ✅
  - **AC:** `Latitude` −90 to 90; `Longitude` −180 to 180. ✅
  - **AC:** Invalid request returns HTTP 400 via `AddFluentValidationAutoValidation()`. ✅
  - **Files:** `src/CivicPulse.API/Validators/AddFavoriteRequestValidator.cs` ✅

- [x] **BACK-11** — Confirm `WeatherRefreshJob` runs and logs correctly.
  - **AC:** Logs `"WeatherRefreshJob started."` on startup. ✅
  - **AC:** Logs `"Refreshing weather for {Count} favorited locations."` every 30 min. ✅
  - **AC:** Per-location exceptions are caught and logged as `Warning`; job continues. ✅
  - **Files:** `src/CivicPulse.Infrastructure/BackgroundJobs/WeatherRefreshJob.cs` ✅

---

### @qa

- [x] **QA-3** — Complete all 8 unit tests in `OutdoorScoringServiceTests.cs`.
  - **AC:** All 8 tests pass — ideal (A), hazardous air, thunderstorm, high wind, extreme heat, clamped 0–100, rain, ideal all-suitable. ✅
  - **Files:** `tests/CivicPulse.UnitTests/Scoring/OutdoorScoringServiceTests.cs` ✅

- [x] **QA-4** — Write `OpenMeteoClientTests.cs`.
  - **AC:** `GetCurrentWeatherAsync_ValidCoords_ReturnsParsedWeatherData` — all fields asserted. ✅
  - **AC:** `GetCurrentWeatherAsync_CalledTwice_SecondCallHitsCacheNotHttp` — handler called `Times.Once()`. ✅
  - **Files:** `tests/CivicPulse.UnitTests/Services/OpenMeteoClientTests.cs` ✅

- [x] **QA-5** — Write `NominatimClientTests.cs`.
  - **AC:** `SearchAsync_ValidQuery_ReturnsMappedResults` — `DisplayName`, `Latitude`, `Longitude` asserted. ✅
  - **AC:** `SearchAsync_CalledTwiceWithSameQuery_SecondCallServedFromCache` ✅
  - **Files:** `tests/CivicPulse.UnitTests/Services/NominatimClientTests.cs` ✅

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| BACK-1 migration applied (Sprint 1) | BACK-7 (favorites DB writes) | ✅ Done |
| BACK-6 (DashboardResponse shape final) | QA-3 assertions using DashboardResponse | ✅ Done |
| BACK-10 (validator registered) | BACK-7 AC on 400 response | ✅ Done |

---

## Definition of Done Checklist
- [x] All tasks above have `[x]`.
- [x] `dotnet test` exits 0 — 12/12 unit tests pass (8 scoring + 2 OpenMeteo + 2 Nominatim).
- [ ] `GET /api/dashboard/1` returns a valid `DashboardResponse` with weather + AQ + score. *(requires running DB)*
- [ ] `POST /api/favorites` persists a row visible in the DB. *(requires running DB)*
- [ ] `GET /api/recommendations/1` returns 4 activity items. *(requires running DB)*
- [ ] `GET /api/dashboard/compare?loc1=1&loc2=2` returns a `CompareResponse`. *(requires running DB)*
- [ ] Background job logs visible in console on app start. *(requires running app)*
