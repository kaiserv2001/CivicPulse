# Backlog

Items here are not scheduled. PM promotes them to a sprint when capacity allows.

---

## Completed

| ID | Description | Resolved In |
|----|-------------|-------------|
| BL-4 | Dark mode toggle | Sprint 4 |
| BL-5 | Rate-limit middleware (60 req/min/IP, 429) | Sprint 4 |
| BL-6 | User profile page (update email, change password) | Sprint 4 |
| TD-1 | `X-User-Id` header placeholder replaced by JWT `sub` claim | Sprint 3 |
| TD-2 | `OpenAQClient` "no data" state surfaced in `AqiBadge` | Sprint 4 |
| TD-3 | `DashboardController.Compare` full parallel fetch via `Task.WhenAll` | Sprint 4 |

---

## Open

| ID | Description | Agent | Effort |
|----|-------------|-------|--------|
| BL-1 | Redis cache — swap `IMemoryCache` for `IDistributedCache` backed by Redis; add `redis` service to `docker-compose.yml` | @backend / @infrastructure | M |
| BL-2 | AQI trend chart — fetch 7-day historical AQ data from OpenAQ, return from a new `/aqtrend` endpoint, render as a Chart.js line chart on the dashboard | @backend / @frontend | M |
| BL-3 | Browser push notifications — Web Push API notification when a favorited location's score drops below 40 (requires service worker) | @frontend | L |
| BL-7 | Deploy to Azure Container Apps or Fly.io with a real hosted DB | @infrastructure | L |
| BL-8 | OpenAPI TypeScript client codegen — generate a typed TS client from the Swagger spec using `@hey-api/openapi-ts`; document the workflow in README | @infrastructure | S |
