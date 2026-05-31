# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY CivicPulse.sln .
COPY src/CivicPulse.Core/CivicPulse.Core.csproj src/CivicPulse.Core/
COPY src/CivicPulse.Web/CivicPulse.Web.csproj   src/CivicPulse.Web/

RUN dotnet restore src/CivicPulse.Web/CivicPulse.Web.csproj

COPY src/CivicPulse.Core/ src/CivicPulse.Core/
COPY src/CivicPulse.Web/  src/CivicPulse.Web/

RUN dotnet publish src/CivicPulse.Web/CivicPulse.Web.csproj \
    -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicPulse.Web.dll"]
