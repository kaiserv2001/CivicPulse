# Sprint 3 — Frontend, Auth & Polish

**PM:** @pm  
**Duration:** Week 5–6 (2026-06-22 → 2026-07-05)  
**Theme:** Working Blazor UI, JWT authentication, integration tests, production Docker image, and a portfolio-ready README.  
**Prerequisite:** Sprint 2 DoD fully checked off.

---

## Sprint Goal
By end of Sprint 3, a user can open the browser, search for a city, see the full dashboard, save a favorite, and compare two cities — all through the Blazor UI. JWT protects the favorites endpoints. Integration tests cover all five API endpoints. The app builds and runs via `docker compose up`.

---

## Tasks

### @frontend

- [x] **FRONT-1** — Build `CitySearchBox.razor` component.
  - **AC:** Text input debounces 400 ms before calling `GET /api/locations/search?query={q}`.
  - **AC:** While loading, a spinner replaces the results list.
  - **AC:** On error (non-200 response), shows a red inline alert: "Search failed. Please try again."
  - **AC:** Results display as a dropdown list showing `displayName`. Clicking an item navigates to `/dashboard/{locationId}` (creates the location via `POST /api/favorites` first if not already saved).
  - **Files:** `src/CivicPulse.Web/Components/CitySearchBox.razor`

- [x] **FRONT-2** — Build `Dashboard.razor` page.
  - **AC:** Page at `/dashboard/{locationId}` loads and renders `WeatherCard.razor`, `AqiBadge.razor`, `ScoreGauge.razor`, `ActivityList.razor`, and `ForecastChart.razor`.
  - **AC:** `ScoreGauge.razor` renders the numeric score (0–100) inside a coloured circle: green (A), teal (B), yellow (C), orange (D), red (F).
  - **AC:** `ForecastChart.razor` renders a Chart.js bar+line combo chart via JS interop: bars for daily precipitation (mm), line for max temperature (°C), x-axis = date labels for 7 days.
  - **AC:** Page shows a loading skeleton (grey placeholder cards) while awaiting the API response.
  - **Files:** `src/CivicPulse.Web/Pages/Dashboard.razor`, `src/CivicPulse.Web/Components/*.razor`

- [x] **FRONT-3** — Build `Compare.razor` page.
  - **AC:** Page at `/compare` shows two `CitySearchBox` components side by side, labelled "City A" and "City B".
  - **AC:** Once both cities are selected, fetches `GET /api/dashboard/compare?loc1={id}&loc2={id}` and renders two `WeatherCard` + `ScoreGauge` columns.
  - **AC:** A banner at the top highlights the winning city with the reason string from the API response.
  - **Files:** `src/CivicPulse.Web/Pages/Compare.razor`

- [x] **FRONT-4** — Build `Favorites.razor` page.
  - **AC:** Page at `/favorites` lists all saved locations with their name, country, and "Go to dashboard" link.
  - **AC:** Each row has a "Remove" button that calls `DELETE /api/favorites/{id}` and removes the row from the list without a full page reload.
  - **AC:** Empty state shows: "No favorites yet. Search for a city to add one."
  - **Files:** `src/CivicPulse.Web/Pages/Favorites.razor`

---

### @backend

- [x] **BACK-12** — Add JWT authentication to `POST /api/favorites`, `DELETE /api/favorites/{id}`, and `GET /api/favorites`.
  - **AC:** Requests without `Authorization: Bearer <token>` header return HTTP 401.
  - **AC:** `POST /api/auth/register` accepts `{ "email": string, "password": string }`, creates a user record, returns `{ "token": "<jwt>" }` with 24-hour expiry.
  - **AC:** `POST /api/auth/login` accepts same body, validates credentials, returns the same token shape or HTTP 401 on bad credentials.
  - **AC:** JWT `sub` claim is used as `userId` in `FavoritesController` (replaces the `X-User-Id` header placeholder).
  - **AC:** JWT secret is read from `Jwt:Key` in configuration — not hardcoded.
  - **Files:** New `src/CivicPulse.API/Controllers/AuthController.cs`, new `src/CivicPulse.Core/Entities/AppUser.cs`, new EF migration.

---

### @infrastructure

- [x] **INFRA-3** — Write the production multi-stage `Dockerfile` for `CivicPulse.API`.
  - **AC:** `docker build -f src/CivicPulse.API/Dockerfile -t civicpulse-api .` succeeds from the repo root.
  - **AC:** Final image is based on `mcr.microsoft.com/dotnet/aspnet:8.0` (runtime only, no SDK).
  - **AC:** `docker run -p 5000:8080 civicpulse-api` starts the API and `GET /swagger` is reachable.
  - **Files:** `src/CivicPulse.API/Dockerfile`

- [x] **INFRA-4** — Write GitHub Actions CI pipeline.
  - **AC:** `.github/workflows/ci.yml` triggers on `push` and `pull_request` to `main`.
  - **AC:** Pipeline steps: `checkout` → `setup-dotnet@v4` (8.0.x) → `dotnet restore` → `dotnet build --no-restore -c Release` → `dotnet test --no-build -c Release`.
  - **AC:** Pipeline completes green on a clean push with all tests passing.
  - **Files:** `.github/workflows/ci.yml`

---

### @qa

- [x] **QA-6** — Write `LocationsControllerTests.cs` (integration test using `WebApplicationFactory`).
  - **AC:** `Search_ValidQuery_Returns200WithResults` — calls real controller wired with an in-memory DB and a mocked `IGeocodingService`; asserts HTTP 200 and non-empty array.
  - **AC:** `Search_SingleCharQuery_Returns400` — asserts HTTP 400 with the error message.
  - **Files:** `tests/CivicPulse.IntegrationTests/Controllers/LocationsControllerTests.cs`

- [x] **QA-7** — Write `DashboardControllerTests.cs`.
  - **AC:** `GetDashboard_ExistingLocation_Returns200WithScore` — seeds a `Location` row, mocks `IWeatherService` and `IAirQualityService`, asserts HTTP 200 and `score.total` is between 0 and 100.
  - **AC:** `GetDashboard_NonExistentLocation_Returns404`.
  - **AC:** `Compare_BothLocationsExist_Returns200WithWinner`.
  - **Files:** `tests/CivicPulse.IntegrationTests/Controllers/DashboardControllerTests.cs`

- [x] **QA-8** — Write `FavoritesControllerTests.cs`.
  - **AC:** `AddFavorite_NewLocation_Returns201`.
  - **AC:** `AddFavorite_Duplicate_Returns409`.
  - **AC:** `DeleteFavorite_OwnedFavorite_Returns204AndRowGone`.
  - **Files:** `tests/CivicPulse.IntegrationTests/Controllers/FavoritesControllerTests.cs`

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| BACK-12 (JWT auth wired) | FRONT-4 (favorites auth header) | Pending |
| INFRA-3 (Dockerfile working) | INFRA-4 (CI publish step) | Pending |
| QA-6,7,8 all passing | Sprint 3 DoD | Pending |

---

## Definition of Done Checklist
- [x] All tasks above have `[x]`.
- [x] `dotnet test` exits 0; all unit + integration tests pass.
- [x] Blazor app renders dashboard for Seattle with real data.
- [x] Compare page shows side-by-side comparison with a winner banner.
- [x] Favorites persists across browser refresh (stored in DB, retrieved on page load).
- [x] `POST /api/favorites` without JWT returns 401.
- [x] `docker compose up` starts DB + API; Swagger and Blazor UI are both reachable.
- [x] GitHub Actions CI pipeline goes green on push to main.
- [x] `README.md` describes architecture, setup steps, and API endpoint table.
