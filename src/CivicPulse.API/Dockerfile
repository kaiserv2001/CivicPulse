# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY CivicPulse.sln .
COPY src/CivicPulse.Core/CivicPulse.Core.csproj             src/CivicPulse.Core/
COPY src/CivicPulse.Infrastructure/CivicPulse.Infrastructure.csproj src/CivicPulse.Infrastructure/
COPY src/CivicPulse.API/CivicPulse.API.csproj               src/CivicPulse.API/
COPY src/CivicPulse.Web/CivicPulse.Web.csproj               src/CivicPulse.Web/
COPY tests/CivicPulse.UnitTests/CivicPulse.UnitTests.csproj tests/CivicPulse.UnitTests/
COPY tests/CivicPulse.IntegrationTests/CivicPulse.IntegrationTests.csproj tests/CivicPulse.IntegrationTests/

RUN dotnet restore

COPY . .
RUN dotnet publish src/CivicPulse.API/CivicPulse.API.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicPulse.API.dll"]
