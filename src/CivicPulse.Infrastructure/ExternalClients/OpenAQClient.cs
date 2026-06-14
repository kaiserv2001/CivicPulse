using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using CivicPulse.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.ExternalClients;

// OpenAQ API v3: https://api.openaq.org/v3
// Flow: GET /v3/locations (find nearby active stations + sensor IDs)
//    →  GET /v3/locations/{id}/latest (current readings per sensor)
//    →  GET /v3/sensors/{sensorId}/measurements?period_name=day (7-day trend)
public class OpenAQClient : IAirQualityService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OpenAQClient> _logger;
    private static readonly TimeSpan CurrentCacheTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan TrendCacheTtl   = TimeSpan.FromHours(6);

    public OpenAQClient(HttpClient http, IDistributedCache cache, ILogger<OpenAQClient> logger)
    {
        _http   = http;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<AirQualityData> GetCurrentAirQualityAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.AirQuality(latitude, longitude);
        var cached = await _cache.GetJsonAsync<AirQualityData>(cacheKey, ct);
        if (cached is not null) return cached;

        _logger.LogInformation("Fetching air quality for {Lat},{Lon}", latitude, longitude);

        var locResp = await FetchLocationsAsync(latitude, longitude, ct);
        if (locResp is null) return DefaultAirQuality();

        // Pick the most recently active station that has sensors — no hard time cutoff
        // so stations that report daily (common outside Europe/US) still show data.
        var best = locResp.Results
            .Where(l => l.Sensors?.Length > 0)
            .OrderByDescending(l => l.DatetimeLast?.Utc ?? DateTime.MinValue)
            .FirstOrDefault();

        if (best is null)
        {
            _logger.LogWarning("No AQ stations with sensors near {Lat},{Lon}", latitude, longitude);
            return DefaultAirQuality();
        }

        var sensorToParam = best.Sensors!
            .Where(s => s.Parameter?.Name is not null)
            .ToDictionary(s => s.Id, s => s.Parameter!.Name!);

        OpenAQLatestResponse? latestResp;
        try
        {
            latestResp = await _http.GetFromJsonAsync<OpenAQLatestResponse>(
                $"v3/locations/{best.Id}/latest", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAQ latest request failed; returning default.");
            return DefaultAirQuality();
        }

        if (latestResp?.Results is null || latestResp.Results.Length == 0)
            return DefaultAirQuality();

        var vals = new Dictionary<string, double>();
        foreach (var r in latestResp.Results)
            if (r.Value >= 0 && sensorToParam.TryGetValue(r.SensorsId, out var param))
                vals[param] = r.Value;

        if (!vals.ContainsKey("pm25") && !vals.ContainsKey("pm10"))
            return DefaultAirQuality();

        double pm25 = vals.GetValueOrDefault("pm25");
        double pm10 = vals.GetValueOrDefault("pm10");
        double no2  = vals.GetValueOrDefault("no2");
        double o3   = vals.GetValueOrDefault("o3");
        double co   = vals.GetValueOrDefault("co");

        double aqi = CalculateUsAqi(pm25);
        var result = new AirQualityData(aqi, pm25, pm10, no2, o3, co,
            AqiCategory(aqi), DominantPollutant(pm25, pm10, no2, o3), DateTime.UtcNow);

        await _cache.SetJsonAsync(cacheKey, result, CurrentCacheTtl, ct);
        return result;
    }

    public async Task<IReadOnlyList<AqTrendDay>> GetAqTrendAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.AqTrend(latitude, longitude);
        var cached = await _cache.GetJsonAsync<List<AqTrendDay>>(cacheKey, ct);
        if (cached is not null) return cached;

        _logger.LogInformation("Fetching AQ trend for {Lat},{Lon}", latitude, longitude);

        var locResp = await FetchLocationsAsync(latitude, longitude, ct);
        if (locResp is null) return EmptyTrend();

        // Candidate PM2.5 sensors, most-recently-active station first. A station can report
        // current (hourly) data yet have no recent daily aggregate, so we don't commit to the
        // first one — we try each until a sensor actually returns daily data, capped to keep
        // API usage bounded.
        var candidates = locResp.Results
            .Where(l => l.Sensors?.Length > 0)
            .OrderByDescending(l => l.DatetimeLast?.Utc ?? DateTime.MinValue)
            .Select(l => l.Sensors!.FirstOrDefault(s =>
                string.Equals(s.Parameter?.Name, "pm25", StringComparison.OrdinalIgnoreCase)))
            .Where(s => s is not null)
            .Select(s => s!.Id)
            .Distinct()
            .Take(5)
            .ToList();

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No PM2.5 sensor found near {Lat},{Lon} for trend.", latitude, longitude);
            return EmptyTrend();
        }

        // OpenAQ v3 daily aggregates live at /sensors/{id}/days (daily mean), not the raw
        // /measurements endpoint. The range filter is date_from/date_to (date-only);
        // note the /days endpoint silently ignores datetime_from/datetime_to and sort_order,
        // so we must bound the range explicitly to get the *recent* 7 days rather than the
        // oldest 7 in the sensor's history.
        var dateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var dateTo   = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        foreach (var sensorId in candidates)
        {
            var url = $"v3/sensors/{sensorId}/days" +
                      $"?date_from={dateFrom}&date_to={dateTo}&limit=7";

            OpenAQMeasurementsResponse? measResp;
            try
            {
                measResp = await _http.GetFromJsonAsync<OpenAQMeasurementsResponse>(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAQ daily request failed for sensor {Id}; trying next.", sensorId);
                continue;
            }

            if (measResp?.Results is null || measResp.Results.Length == 0)
                continue;

            var trend = measResp.Results
                .Where(r => r.Period?.DatetimeFrom?.Utc is not null && r.Value >= 0)
                .OrderBy(r => r.Period!.DatetimeFrom!.Utc)
                .Select(r =>
                {
                    var date = DateOnly.FromDateTime(r.Period!.DatetimeFrom!.Utc);
                    var aqi  = CalculateUsAqi(r.Value);
                    return new AqTrendDay(date, aqi, AqiCategory(aqi));
                })
                .ToList();

            if (trend.Count == 0) continue;

            await _cache.SetJsonAsync(cacheKey, trend, TrendCacheTtl, ct);
            return trend;
        }

        _logger.LogWarning("No recent daily PM2.5 data near {Lat},{Lon} for trend.", latitude, longitude);
        return EmptyTrend();
    }

    // ── shared helpers ─────────────────────────────────────────────────────

    private async Task<OpenAQLocationsResponse?> FetchLocationsAsync(
        double latitude, double longitude, CancellationToken ct)
    {
        var url = $"v3/locations?coordinates={latitude},{longitude}&radius=25000&limit=10";
        try
        {
            return await _http.GetFromJsonAsync<OpenAQLocationsResponse>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("OpenAQ API key rejected (HTTP {Status}).", ex.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAQ locations request failed.");
            return null;
        }
    }

    private static double CalculateUsAqi(double pm25) => pm25 switch
    {
        <= 12.0  => Linear(0,   50,  0,     12.0,  pm25),
        <= 35.4  => Linear(51,  100, 12.1,  35.4,  pm25),
        <= 55.4  => Linear(101, 150, 35.5,  55.4,  pm25),
        <= 150.4 => Linear(151, 200, 55.5,  150.4, pm25),
        <= 250.4 => Linear(201, 300, 150.5, 250.4, pm25),
        _        => Linear(301, 400, 250.5, 350.4, pm25)
    };

    private static double Linear(double aqiLow, double aqiHigh,
        double concLow, double concHigh, double conc) =>
        Math.Round((aqiHigh - aqiLow) / (concHigh - concLow) * (conc - concLow) + aqiLow);

    private static string AqiCategory(double aqi) => aqi switch
    {
        <= 50  => "Good",
        <= 100 => "Moderate",
        <= 150 => "Unhealthy for Sensitive Groups",
        <= 200 => "Unhealthy",
        <= 300 => "Very Unhealthy",
        _      => "Hazardous"
    };

    private static string? DominantPollutant(double pm25, double pm10, double no2, double o3)
    {
        var max = new[] { ("PM2.5", pm25), ("PM10", pm10), ("NO2", no2), ("O3", o3) }.MaxBy(p => p.Item2);
        return max.Item2 > 0 ? max.Item1 : null;
    }

    private static AirQualityData DefaultAirQuality() =>
        new(0, 0, 0, 0, 0, 0, "Unknown", null, DateTime.UtcNow);

    private static List<AqTrendDay> EmptyTrend() =>
        Enumerable.Range(0, 7)
            .Select(i => new AqTrendDay(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6 + i)), 0, "No data"))
            .ToList();

    // ── response models ────────────────────────────────────────────────────

    private sealed record OpenAQLocationsResponse(OpenAQLocation[] Results);

    private sealed record OpenAQLocation(
        int Id,
        string Name,
        [property: JsonPropertyName("datetimeLast")]  OpenAQDatetime? DatetimeLast,
        [property: JsonPropertyName("sensors")]       OpenAQSensor[]? Sensors);

    private sealed record OpenAQSensor(int Id, OpenAQParameter? Parameter);
    private sealed record OpenAQParameter(string? Name);
    private sealed record OpenAQDatetime([property: JsonPropertyName("utc")] DateTime Utc);

    private sealed record OpenAQLatestResponse(OpenAQLatestResult[] Results);

    private sealed record OpenAQLatestResult(
        double Value,
        [property: JsonPropertyName("sensorsId")] int SensorsId);

    private sealed record OpenAQMeasurementsResponse(OpenAQMeasurement[] Results);

    private sealed record OpenAQMeasurement(
        double Value,
        [property: JsonPropertyName("period")] OpenAQPeriod? Period);

    private sealed record OpenAQPeriod(
        [property: JsonPropertyName("datetimeFrom")] OpenAQDatetime? DatetimeFrom);
}
