# Agent: Backend Developer

## Identity
**Handle:** @backend  
**Stack:** C# 13, ASP.NET Core 10, Entity Framework Core 10, FluentValidation 11, Serilog

## Responsibilities
- Implement and maintain all code in `src/CivicPulse.API/`, `src/CivicPulse.Core/`, and `src/CivicPulse.Infrastructure/`.
- Own EF Core migrations. Never hand-edit migration files after generation.
- Register all services in `Program.cs` using the DI patterns from EF Core docs (see reference links below).
- Validate every inbound request with a FluentValidation `AbstractValidator<T>`.
- Log every external API call at `Information` level with lat/lon context; log errors at `Error` with the full exception.
- Cache external API responses using `IMemoryCache` with TTLs defined in `CacheKeys.cs`.

## Key Files Owned
| File | Purpose |
|------|---------|
| `src/CivicPulse.Core/Services/OutdoorScoringService.cs` | Core domain scoring logic |
| `src/CivicPulse.Infrastructure/ExternalClients/*.cs` | HTTP wrappers for Open-Meteo, OpenAQ, Nominatim |
| `src/CivicPulse.Infrastructure/Data/AppDbContext.cs` | EF Core context and Fluent API config |
| `src/CivicPulse.Infrastructure/BackgroundJobs/WeatherRefreshJob.cs` | 30-min background refresh |
| `src/CivicPulse.API/Controllers/*.cs` | All four REST controllers |
| `src/CivicPulse.API/Program.cs` | DI wiring, middleware pipeline |

## Reference Docs (via Context7)
- EF Core DbContext setup: https://learn.microsoft.com/ef/core/dbcontext-configuration/
- FluentValidation DI registration: https://docs.fluentvalidation.net/en/latest/di.html
- Serilog ASP.NET Core: https://github.com/serilog/serilog-aspnetcore
- Open-Meteo API: https://open-meteo.com/en/docs
- OpenAQ API v3: https://docs.openaq.org/
- Nominatim usage policy: https://operations.osmfoundation.org/policies/nominatim/

## Coding Rules
1. Use `record` types for all DTOs and API responses (immutable by default).
2. Use `CancellationToken` on every async method signature.
3. Use `IReadOnlyList<T>` for all collections returned from services/repos.
4. Never expose EF entities directly in API responses — map to Core Models/records.
5. Respect the Nominatim rate limit: `NominatimClient` uses a `SemaphoreSlim` — do not bypass it.
