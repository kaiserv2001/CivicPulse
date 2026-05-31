# Sprint 5 — AQI Trend Chart, Redis Cache & TypeScript Codegen

**PM:** @pm  
**Duration:** Week 9–10 (2026-06-14 → 2026-06-27)  
**Theme:** Richer data visualisation (AQI history chart), production-grade caching (Redis), and developer tooling (TypeScript client codegen).  
**Prerequisite:** Sprint 4 DoD fully checked off.

---

## Sprint Goal
By end of Sprint 5: the dashboard shows a 7-day AQI trend line chart alongside the existing forecast chart; all API caching runs through Redis so cache survives app restarts and is shared across replicas; and a typed TypeScript API client can be regenerated with a single command from the Swagger spec.

---

## Tasks

### @backend

- [ ] **BACK-16** — AQI trend endpoint (BL-2).
  - **AC:** `GET /api/dashboard/{locationId}/aqtrend` returns an array of `{ date: string, aqi: number, category: string }` for the past 7 days, one entry per day, newest last.
  - **AC:** Data is fetched from OpenAQ `/v3/measurements` with a 7-day `date_from` filter; each day's AQI is the mean of all PM2.5 readings in that calendar day.
  - **AC:** If a day has no readings, the entry uses `aqi: 0, category: "No data"`.
  - **AC:** Response is cached in `IMemoryCache` for 60 minutes (Redis in BACK-17 will replace this).
  - **Files:** `src/CivicPulse.Core/Interfaces/IAirQualityService.cs` (add method), `src/CivicPulse.Core/Models/AqTrendDay.cs` (new), `src/CivicPulse.Infrastructure/ExternalClients/OpenAQClient.cs` (implement), `src/CivicPulse.API/Controllers/DashboardController.cs` (new action)

- [ ] **BACK-17** — Redis distributed cache (BL-1).
  - **AC:** `redis` service added to `docker-compose.yml` (image `redis:7-alpine`, port 6379, named volume `redisdata`).
  - **AC:** `Microsoft.Extensions.Caching.StackExchangeRedis` added to `CivicPulse.Infrastructure.csproj`.
  - **AC:** `Program.cs` registers `AddStackExchangeRedisCache` when `USE_INMEMORY != "1"`; falls back to `AddDistributedMemoryCache` for local/test runs.
  - **AC:** `OpenMeteoClient` and `OpenAQClient` swap their `IMemoryCache` fields for `IDistributedCache`; serialise cached objects as JSON (`System.Text.Json`).
  - **AC:** `DashboardController` cache also swapped to `IDistributedCache`.
  - **AC:** `dotnet test` still passes (integration tests use the in-memory fallback automatically).
  - **Files:** `docker-compose.yml`, `src/CivicPulse.Infrastructure/CivicPulse.Infrastructure.csproj`, `src/CivicPulse.API/Program.cs`, `src/CivicPulse.Infrastructure/ExternalClients/OpenMeteoClient.cs`, `src/CivicPulse.Infrastructure/ExternalClients/OpenAQClient.cs`, `src/CivicPulse.API/Controllers/DashboardController.cs`

---

### @frontend

- [ ] **FRONT-8** — AQI trend chart component (BL-2).
  - **AC:** New `AqiTrendChart.razor` component accepts `IReadOnlyList<AqTrendDay>` as a parameter.
  - **AC:** Renders a Chart.js line chart via JS interop: x-axis = date labels (e.g. "Mon 09"), y-axis = AQI value, line colour shifts from green (≤50) to orange (≤150) to red (>150) using a segment colour array.
  - **AC:** `Dashboard.razor` calls `GET /api/dashboard/{id}/aqtrend` after the main dashboard load and renders `AqiTrendChart` below the forecast chart.
  - **AC:** While loading, shows the same skeleton placeholder style used by other cards.
  - **AC:** If all 7 days are "No data" (AQI = 0), shows a muted "No historical AQ data available." notice instead of an empty chart.
  - **Files:** `src/CivicPulse.Web/Components/AqiTrendChart.razor`, `src/CivicPulse.Web/Pages/Dashboard.razor`, `src/CivicPulse.Web/Services/ApiClient.cs`, `src/CivicPulse.Web/wwwroot/js/chartInterop.js`

---

### @infrastructure

- [ ] **INFRA-5** — TypeScript client codegen (BL-8).
  - **AC:** `scripts/gen-ts-client.sh` shell script fetches `http://localhost:5000/swagger/v1/swagger.json` and runs `npx @hey-api/openapi-ts` to output a typed client into `clients/typescript/`.
  - **AC:** `clients/typescript/` is added to `.gitignore` (generated artefact, not committed).
  - **AC:** `README.md` gains a "TypeScript Client" section explaining how to run the script and use the generated client.
  - **Files:** `scripts/gen-ts-client.sh`, `.gitignore`, `README.md`

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| BACK-16 (aqtrend endpoint) | FRONT-8 (chart component) | Pending |
| BACK-17 (Redis) | BACK-16 cache layer | Can be done in parallel; BACK-16 starts with `IMemoryCache` |

---

## Definition of Done Checklist
- [ ] All tasks above have `[x]`.
- [ ] `dotnet test` exits 0 (all tests pass).
- [ ] `docker compose up --build` starts DB + Redis + API + Web with no errors.
- [ ] Dashboard page shows AQI trend chart for a real city with 7 date labels.
- [ ] Cache entries survive an API container restart (verified via Redis CLI `KEYS *`).
- [ ] `scripts/gen-ts-client.sh` runs cleanly and outputs files to `clients/typescript/`.
- [ ] README documents the TypeScript client generation step.
