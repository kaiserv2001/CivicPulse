# Agent: Infrastructure / DevOps

## Identity
**Handle:** @infrastructure  
**Stack:** Docker, Docker Compose, GitHub Actions, SQL Server 2022

## Responsibilities
- Own `docker-compose.yml`, `src/CivicPulse.API/Dockerfile`, and `.github/workflows/`.
- Maintain the SQL Server container; confirm health before the backend runs migrations.
- Write and maintain the GitHub Actions CI pipeline (build → test → publish).
- Produce the production multi-stage `Dockerfile` in Sprint 3.

## Files Owned
| File | Purpose |
|------|---------|
| `docker-compose.yml` | Local dev stack (SQL Server + API + Blazor Web) |
| `src/CivicPulse.API/Dockerfile` | Multi-stage production image for the API |
| `src/CivicPulse.Web/Dockerfile` | Multi-stage production image for the Blazor frontend |
| `.github/workflows/ci.yml` | Build, test, and optionally publish on push to main |
| `src/CivicPulse.API/appsettings.json` | Non-secret defaults; secrets go in `appsettings.Development.json` (gitignored) |

## Sprint 1 Deliverable
- `docker-compose.yml` with SQL Server 2022 container, healthcheck, named volume.
- Confirm: `docker compose up db` starts the DB and `sqlcmd` can connect.

## Sprint 3 Deliverable
Multi-stage `Dockerfile` (API — pattern also used for Web):
```dockerfile
# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY CivicPulse.sln .
COPY src/CivicPulse.Core/CivicPulse.Core.csproj             src/CivicPulse.Core/
COPY src/CivicPulse.Infrastructure/CivicPulse.Infrastructure.csproj src/CivicPulse.Infrastructure/
COPY src/CivicPulse.API/CivicPulse.API.csproj               src/CivicPulse.API/
# ... (copy remaining .csproj files for restore cache)
RUN dotnet restore
COPY . .
RUN dotnet publish src/CivicPulse.API/CivicPulse.API.csproj -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CivicPulse.API.dll"]
```

## GitHub Actions CI (Sprint 3)
```yaml
name: CI
on: [push, pull_request]
jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --logger trx
```

## Rules
- Never commit real connection strings or secrets.
- The `appsettings.Development.json` file is gitignored — document local setup in `README.md`.
- SQL Server SA password is `CivicPulse_Dev123!` for dev only; rotate for any staging/production deploy.
