# CivicPulse — Data Sources, APIs & Score Calculation

## External APIs at a Glance

| API | Used for | Key | Rate limit |
|-----|----------|-----|------------|
| **Open-Meteo** | Current weather + 7-day forecast | None (free) | Generous, ~10k/day |
| **OpenAQ v3** | Current air quality + 7-day AQI trend | Required (free) | ~1000 req/day free tier |
| **Nominatim (OSM)** | City name → latitude/longitude | None (free) | 1 request/second |

---

## What happens when you open a Dashboard page

The Blazor page fires two HTTP requests to our own API **at the same time** (in parallel):

```
Browser
  ├── GET /api/dashboard/{locationId}    ← weather, score, forecast, recommendations
  └── GET /api/dashboard/{locationId}/aqtrend  ← 7-day AQI trend
```

The dashboard endpoint itself fires three more requests **in parallel** to the external APIs:

```
/api/dashboard/{id}
  ├── Open-Meteo → current weather
  ├── Open-Meteo → 7-day forecast
  └── OpenAQ    → current air quality
```

All three finish, the scoring algorithm runs on the results, everything is bundled into one response, and the result is cached in Redis for 15 minutes.

---

## 1. Current Weather

**Source:** Open-Meteo  
**File:** `src/CivicPulse.Infrastructure/ExternalClients/OpenMeteoClient.cs`  
**Method:** `GetCurrentWeatherAsync(latitude, longitude)`  
**Cache:** Redis, 30 minutes

### URL called

```
https://api.open-meteo.com/v1/forecast
  ?latitude=14.5995
  &longitude=120.9842
  &current=temperature_2m,apparent_temperature,wind_speed_10m,wind_gusts_10m,
           precipitation,precipitation_probability,uv_index,
           relative_humidity_2m,weather_code
  &timezone=auto
```

### Response (abbreviated)

```json
{
  "current": {
    "temperature_2m": 28.5,
    "apparent_temperature": 32.1,
    "wind_speed_10m": 11.2,
    "wind_gusts_10m": 18.0,
    "precipitation": 0.1,
    "precipitation_probability": 97,
    "uv_index": 2.2,
    "relative_humidity_2m": 78,
    "weather_code": 51
  }
}
```

> **Why `[JsonPropertyName]`?**  
> Open-Meteo uses snake_case (`temperature_2m`, `wind_speed_10m`). C# by default expects camelCase. The `[JsonPropertyName("temperature_2m")]` attribute tells the JSON parser exactly which field name to look for. Without it, all values would deserialize as `0`.

### Weather codes (WMO standard, used by Open-Meteo)

| Code | Condition |
|------|-----------|
| 0 | Clear sky |
| 1, 2, 3 | Mainly clear, partly cloudy, overcast |
| 45, 48 | Fog |
| 51, 53, 55 | Light / moderate / dense drizzle |
| 61, 63, 65 | Slight / moderate / heavy rain |
| 71, 73, 75 | Slight / moderate / heavy snow |
| 80, 81, 82 | Slight / moderate / violent rain showers |
| 95 | Thunderstorm |
| 96, 99 | Thunderstorm with hail |

> These are **different** from OpenWeatherMap codes. The original scoring code used the wrong ranges (300–399 for drizzle, 800 for clear) — this was a bug that caused light drizzle and clear sky to score identically.

### Result model

```csharp
WeatherData(
    TemperatureCelsius, FeelsLikeCelsius,
    WindSpeedKmh, WindGustKmh,
    PrecipitationMm, PrecipitationProbability,
    UvIndex, RelativeHumidity,
    WeatherCode, WeatherDescription,
    ObservedAt
)
```

---

## 2. 7-Day Forecast

**Source:** Open-Meteo (same API, different parameters)  
**File:** `OpenMeteoClient.cs`  
**Method:** `GetForecastAsync(latitude, longitude, days: 7)`  
**Cache:** Redis, 30 minutes

### URL called

```
https://api.open-meteo.com/v1/forecast
  ?latitude=14.5995&longitude=120.9842
  &daily=temperature_2m_max,temperature_2m_min,precipitation_sum,
         precipitation_probability_max,wind_speed_10m_max,
         uv_index_max,weather_code
  &forecast_days=7
  &timezone=auto
```

### Response structure

Open-Meteo returns **arrays** — one value per day across all fields:

```json
{
  "daily": {
    "time":                       ["2026-06-02", "2026-06-03", ...],
    "temperature_2m_max":         [30.1, 29.8, ...],
    "temperature_2m_min":         [24.2, 23.9, ...],
    "precipitation_sum":          [1.2, 0.0, ...],
    "precipitation_probability_max": [80, 30, ...],
    "wind_speed_10m_max":         [15.0, 12.5, ...],
    "uv_index_max":               [3.5, null, ...],
    "weather_code":               [61, 1, ...]
  }
}
```

We zip these arrays together: index `[0]` = today, `[6]` = 7 days out.

> **Why `double?[]?`?**  
> `uv_index_max` can contain `null` elements on overcast days. A plain `double[]` cannot hold null — it would crash. `double?[]?` means "an array (which might itself be null) of nullable doubles." The `?? 0` fallback substitutes 0 when a null element is encountered.

### Result model

```csharp
WeatherForecastDay(
    Date, MaxTemperatureCelsius, MinTemperatureCelsius,
    PrecipitationMm, PrecipitationProbability,
    MaxWindSpeedKmh, UvIndexMax, WeatherCode
)
```

---

## 3. Current Air Quality

**Source:** OpenAQ v3  
**File:** `src/CivicPulse.Infrastructure/ExternalClients/OpenAQClient.cs`  
**Method:** `GetCurrentAirQualityAsync(latitude, longitude)`  
**Cache:** Redis, 60 minutes

OpenAQ aggregates readings from real physical monitoring stations worldwide. Coverage is good in Europe, North America, East Asia, and some Southeast Asian cities. Coverage is sparse in rural areas and much of Africa/South America.

### Step 1 — find nearby stations

```
GET https://api.openaq.org/v3/locations
  ?coordinates=14.5995,120.9842
  &radius=25000
  &limit=10
```

Returns up to 10 monitoring stations within 25 km. Each station includes:
- Its sensor list (PM2.5, PM10, NO2, O3, CO sensors)
- `datetimeLast` — when data was last received

We pick the station that reported most recently and has at least one sensor. There is no longer a strict 48-hour cutoff — some stations report once per day or less, and cutting them off caused cities to show "No sensor data" even when data existed.

### Step 2 — get the latest readings

```
GET https://api.openaq.org/v3/locations/{stationId}/latest
```

Returns the most recent reading for each sensor at that station:

```json
{
  "results": [
    { "sensorsId": 12345, "value": 12.3 },
    { "sensorsId": 12346, "value": 28.1 }
  ]
}
```

We cross-reference `sensorsId` with the sensor list from Step 1 to know which value belongs to which pollutant (PM2.5, PM10, etc.).

### Step 3 — calculate US AQI from PM2.5

The AQI number is calculated using the **US EPA piecewise linear formula**. PM2.5 (fine particulate matter, the most dangerous common pollutant) is the primary input:

| PM2.5 (µg/m³) | AQI range |
|----------------|-----------|
| 0 – 12.0 | 0 – 50 (Good) |
| 12.1 – 35.4 | 51 – 100 (Moderate) |
| 35.5 – 55.4 | 101 – 150 (Unhealthy for sensitive groups) |
| 55.5 – 150.4 | 151 – 200 (Unhealthy) |
| 150.5 – 250.4 | 201 – 300 (Very Unhealthy) |
| > 250.4 | 301+ (Hazardous) |

The formula interpolates linearly within each band:

```csharp
AQI = ((AQI_high - AQI_low) / (conc_high - conc_low)) * (pm25 - conc_low) + AQI_low
```

### Result model

```csharp
AirQualityData(
    Aqi, Pm25, Pm10, No2, O3, Co,
    AqiCategory, DominantPollutant, ObservedAt
)
```

If no station is found near the location, all values are 0 and `AqiCategory = "Unknown"`, which displays as "No sensor data available" in the UI.

---

## 4. 7-Day AQI Trend

**Source:** OpenAQ v3  
**File:** `OpenAQClient.cs`  
**Method:** `GetAqTrendAsync(latitude, longitude)`  
**Cache:** Redis, 60 minutes  
**Endpoint:** `GET /api/dashboard/{locationId}/aqtrend`

This is a **separate API call** from the main dashboard, fired in parallel by the Blazor page. It uses the same station lookup logic, then goes one step further.

### Step 1 — find the station (same as current AQ)

Same `GET /v3/locations` call. This time we specifically look for a station that has a **PM2.5 sensor** (not just any sensor).

### Step 2 — fetch 7 days of daily aggregated readings

```
GET https://api.openaq.org/v3/sensors/{pm25SensorId}/measurements
  ?period_name=day
  &date_from=2026-05-26T00:00:00Z
  &date_to=2026-06-02T00:00:00Z
  &limit=7
```

`period_name=day` tells OpenAQ to aggregate all individual readings for a day into one average value. The response looks like:

```json
{
  "results": [
    {
      "value": 14.5,
      "period": {
        "datetimeFrom": { "utc": "2026-05-26T00:00:00+00:00" }
      }
    },
    ...
  ]
}
```

### Step 3 — convert each day's PM2.5 to AQI

The same EPA formula from current AQ is applied to each daily average, producing 7 `AqTrendDay` objects:

```csharp
AqTrendDay(Date: DateOnly, Aqi: double, Category: string)
```

If the city has no PM2.5 sensor in OpenAQ, the trend returns 7 "No data" entries and the chart section is hidden in the UI.

---

## 5. Outdoor Score

**File:** `src/CivicPulse.Core/Services/OutdoorScoringService.cs`  
**Method:** `Calculate(WeatherData, AirQualityData)`  
**No API calls — pure arithmetic on already-fetched data.**

### Formula

```
Total = (WeatherScore × 35%) + (AirScore × 30%) + (WindScore × 20%) + (UvScore × 15%)
```

Weights reflect how much each factor typically affects whether it is safe and comfortable to be outdoors.

### Weather score (starts at 100, deduct points)

```
Weather code penalty:
  0 (clear)               → −0
  1, 2 (mainly clear)     → −5
  3 (overcast)            → −10
  45/48 (fog)             → −20
  51 (light drizzle)      → −20
  53/55 (mod/dense drizzle) → −30 / −35
  61/63/65 (rain)         → −35 / −45 / −55
  80/81/82 (showers)      → −30 / −45 / −60
  95 (thunderstorm)       → −65
  96/99 (storm + hail)    → −75

Temperature penalty:
  > 38°C or < 0°C         → −25
  > 33°C or < 5°C         → −15

Precipitation probability penalty:
  ≥ 80%  → −15
  ≥ 60%  → −8
  ≥ 40%  → −3
```

> Without the precipitation probability penalty, a city showing "Clear sky" with an 80% rain forecast would score 100 on weather. The penalty means an imminent storm already affects the score before the first raindrop falls.

### Air quality score

| AQI | Score |
|-----|-------|
| ≤ 50 (Good) | 100 |
| ≤ 100 (Moderate) | 75 |
| ≤ 150 (Unhealthy for sensitive groups) | 50 |
| ≤ 200 (Unhealthy) | 25 |
| > 200 | 0 |

### Wind score

| Wind speed | Score |
|------------|-------|
| ≤ 15 km/h | 100 |
| ≤ 25 km/h | 80 |
| ≤ 40 km/h | 55 |
| ≤ 55 km/h | 30 |
| > 55 km/h | 0 |

### UV score

| UV Index | Score |
|----------|-------|
| ≤ 2 (Low) | 100 |
| ≤ 5 (Moderate) | 85 |
| ≤ 7 (High) | 65 |
| ≤ 10 (Very High) | 40 |
| > 10 (Extreme) | 20 |

### Grade

| Total | Grade |
|-------|-------|
| ≥ 85 | A — Excellent |
| ≥ 70 | B — Good |
| ≥ 55 | C — Fair |
| ≥ 40 | D — Poor |
| < 40 | F — Unsafe |

---

## 6. Activity Suitability

**File:** `OutdoorScoringService.cs`  
**Method:** `GetRecommendations(score, weather, airQuality)`  
**No API calls — derived from already-calculated data.**

Four boolean flags are evaluated first:

```csharp
bool rainRisk  = PrecipitationProbability > 40 || PrecipitationMm > 2;
bool highWind  = WindSpeedKmh > 30;
bool badAir    = Aqi > 100;
bool heatRisk  = TemperatureCelsius > 35;
bool coldRisk  = TemperatureCelsius < 2;
```

Then each activity applies its own requirements:

| Activity | Minimum score | Blocking conditions |
|----------|--------------|---------------------|
| 🚶 Walking | 50 | Rain risk, bad air |
| 🚴 Cycling | 55 | Rain risk, high wind, bad air |
| 🚌 Outdoor Commute | 45 | Rain risk |
| 🏃 Outdoor Work/Exercise | 60 | Bad air, heat risk, cold risk |

An activity is `Suitable = true` only if the score meets the minimum **and** none of its blocking conditions are met.

---

## Caching strategy

Every external API call checks Redis before hitting the network, and writes the result back after a successful call. This means:

- The same city requested 5 times in 30 minutes results in **1 Open-Meteo call**, not 5
- The full dashboard response is cached for 15 minutes (so fast page loads don't re-compute the score)
- The `WeatherRefreshJob` background task pre-warms the cache for favorited locations every 30 minutes

Cache keys are defined centrally in `CacheKeys.cs`:

```csharp
weather:{lat}:{lon}      → 30 min
airquality:{lat}:{lon}   → 60 min
aqtrend:{lat}:{lon}      → 60 min
dashboard:{locationId}   → 15 min
geocode:{query}          → 24 hours (city names don't change)
```

To clear all cached data (useful during debugging): `docker exec civicpulse-redis-1 redis-cli FLUSHALL`

---

## City search (Geocoding)

**Source:** Nominatim / OpenStreetMap  
**File:** `NominatimClient.cs`  
**Endpoint:** `GET /api/locations/search?query=Manila`  
**Cache:** Redis, 24 hours

```
GET https://nominatim.openstreetmap.org/search
  ?q=Manila&format=json&limit=5&addressdetails=1
```

Returns latitude, longitude, display name, and country. The result is saved to the `Locations` table in the database so future dashboard requests can look up the coordinates by ID instead of re-geocoding. Nominatim requires a `User-Agent` header with a contact email per their usage policy.
