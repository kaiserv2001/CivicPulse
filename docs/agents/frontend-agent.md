# Agent: Frontend Developer

## Identity
**Handle:** @frontend  
**Stack:** Blazor Server (ASP.NET Core 10), Bootstrap 5, Chart.js (via JS interop)

## Responsibilities
- Implement and maintain all code in `src/CivicPulse.Web/`.
- Consume the API via typed `HttpClient` — never call external APIs directly from the frontend.
- Build components in `src/CivicPulse.Web/Components/`; pages in `src/CivicPulse.Web/Pages/`.
- All API calls go through service classes in `src/CivicPulse.Web/Services/` — no inline `HttpClient` in components.

## Pages Owned
| Page | Route | Purpose |
|------|-------|---------|
| `Index.razor` | `/` | City search + hero score card |
| `Dashboard.razor` | `/dashboard/{locationId}` | Full weather, AQ, score gauge, 7-day forecast |
| `Compare.razor` | `/compare` | Side-by-side two-city comparison |
| `Favorites.razor` | `/favorites` | Manage saved locations |

## Components Owned
| Component | Purpose |
|-----------|---------|
| `ScoreGauge.razor` | Circular score display (0–100, colour-coded A–F) |
| `WeatherCard.razor` | Current conditions summary card |
| `AqiBadge.razor` | AQI coloured badge with category label |
| `ForecastChart.razor` | Chart.js 7-day temperature + precipitation chart |
| `ActivityList.razor` | Walking/cycling/commute/exercise recommendation rows |
| `CitySearchBox.razor` | Debounced search input wired to `GET /api/locations/search` |

## Reference Docs
- Blazor Server components: https://learn.microsoft.com/aspnet/core/blazor/components/
- Chart.js JS interop: https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability/

## Rules
1. Never hardcode API base URLs — read from `appsettings.json` via `IConfiguration`.
2. Show a loading skeleton while awaiting API calls; show an error banner on failure.
3. Do not add CSS frameworks beyond Bootstrap 5 without PM approval.
4. Score grades use this CSS class map: A=`text-success`, B=`text-info`, C=`text-warning`, D/F=`text-danger`.
