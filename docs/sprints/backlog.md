# Backlog (Post-Sprint 3 / Future Work)

Items here are not scheduled. PM promotes them to a sprint when capacity allows.

---

## Potential Sprint 4 Items

| ID | Description | Agent | Effort |
|----|-------------|-------|--------|
| BL-1 | Redis cache option — swap `IMemoryCache` for `IDistributedCache` backed by Redis; add Redis service to `docker-compose.yml` | @backend / @infrastructure | M |
| BL-2 | AQI trend chart — 7-day historical AQ data from OpenAQ stored in `AirQualityCache` table, rendered as line chart | @backend / @frontend | M |
| BL-3 | Push notifications — browser notification when a favorited location's score drops below 40 | @frontend | L |
| BL-4 | Dark mode toggle — CSS variable switch in Blazor layout | @frontend | S |
| BL-5 | Rate-limit middleware — global 60 req/min per IP using ASP.NET Core rate limiting (added in .NET 7) | @backend | S |
| BL-6 | User profile page — update email, change password | @backend / @frontend | M |
| BL-7 | Deployment to Azure Container Apps or Fly.io with a real hosted DB | @infrastructure | L |
| BL-8 | OpenAPI code generation — generate TypeScript client from Swagger spec for potential React frontend | @infrastructure / @frontend | S |

---

## Known Technical Debt

| ID | Description | Filed By |
|----|-------------|----------|
| TD-1 | `FavoritesController.GetUserId()` uses `X-User-Id` header as a placeholder until JWT is implemented in Sprint 3 | @pm |
| TD-2 | `OpenAQClient` falls back to a zero-value `AirQualityData` when no sensors exist nearby — should surface a "no data" state to the UI | @backend |
| TD-3 | `DashboardController.Compare` fetches both cities' data serially in `FetchParallelAsync` — the outer pair of locations is already parallel but could use `Task.WhenAll` more aggressively | @backend |
