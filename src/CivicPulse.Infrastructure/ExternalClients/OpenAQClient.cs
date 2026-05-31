using System.Net.Http.Json;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using CivicPulse.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.ExternalClients;

// OpenAQ API v3: https://api.openaq.org/v3 — free, requires API key
public class OpenAQClient : IAirQualityService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OpenAQClient> _logger;
    private static readonly TimeSpan CurrentCacheTtl = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan TrendCacheTtl = TimeSpan.FromMinutes(60);

    public OpenAQClient(HttpClient http, IDistributedCache cache, ILogger<OpenAQClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AirQualityData> GetCurrentAirQualityAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.AirQuality(latitude, longitude);
        var cached = await _cache.GetJsonAsync<AirQualityData>(cacheKey, ct);
        if (cached is not null) return cached;

        _logger.LogInformation("Fetching air quality for {Lat},{Lon}", latitude, longitude);

        OpenAQResponse? response;
        var url = $"v3/measurements?coordinates={latitude},{longitude}&radius=25000&limit=100&order_by=datetime&sort=desc";
        try
        {
            response = await _http.GetFromJsonAsync<OpenAQResponse>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("OpenAQ API requires an API key (HTTP {Status}); returning default.", ex.StatusCode);
            return DefaultAirQuality();
        }

        if (response?.Results is null || response.Results.Length == 0)
        {
            _logger.LogWarning("No AQ data found near {Lat},{Lon}; returning default", latitude, longitude);
            return DefaultAirQuality();
        }

        double pm25 = GetLatestValue(response.Results, "pm25");
        double pm10 = GetLatestValue(response.Results, "pm10");
        double no2  = GetLatestValue(response.Results, "no2");
        double o3   = GetLatestValue(response.Results, "o3");
        double co   = GetLatestValue(response.Results, "co");

        double aqi = CalculateUsAqi(pm25);
        var result = new AirQualityData(aqi, pm25, pm10, no2, o3, co, AqiCategory(aqi),
            DominantPollutant(pm25, pm10, no2, o3), DateTime.UtcNow);

        await _cache.SetJsonAsync(cacheKey, result, CurrentCacheTtl, ct);
        return result;
    }

    public async Task<IReadOnlyList<AqTrendDay>> GetAqTrendAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.AqTrend(latitude, longitude);
        var cached = await _cache.GetJsonAsync<List<AqTrendDay>>(cacheKey, ct);
        if (cached is not null) return cached;

        var dateFrom = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var url = $"v3/measurements?coordinates={latitude},{longitude}&radius=25000" +
                  $"&date_from={Uri.EscapeDataString(dateFrom)}&limit=1000&order_by=datetime&sort=asc";

        OpenAQResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<OpenAQResponse>(url, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("OpenAQ API key required for trend data; returning empty trend.");
            return EmptyTrend();
        }

        if (response?.Results is null || response.Results.Length == 0)
            return EmptyTrend();

        // Group PM2.5 readings by calendar day, compute daily mean AQI
        var byDay = response.Results
            .Where(r => r.Parameter == "pm25" && r.Value >= 0)
            .GroupBy(r => DateOnly.FromDateTime(r.Date.Utc))
            .ToDictionary(g => g.Key, g => g.Average(r => r.Value));

        var result = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6 + i));
                if (!byDay.TryGetValue(day, out var meanPm25))
                    return new AqTrendDay(day, 0, "No data");
                var aqi = CalculateUsAqi(meanPm25);
                return new AqTrendDay(day, Math.Round(aqi, 0), AqiCategory(aqi));
            })
            .ToList();

        await _cache.SetJsonAsync(cacheKey, result, TrendCacheTtl, ct);
        return result;
    }

    private static double GetLatestValue(OpenAQMeasurement[] results, string parameter) =>
        results.Where(r => r.Parameter == parameter)
               .OrderByDescending(r => r.Date.Utc)
               .FirstOrDefault()?.Value ?? 0;

    private static double CalculateUsAqi(double pm25) => pm25 switch
    {
        <= 12.0  => Linear(0, 50, 0, 12.0, pm25),
        <= 35.4  => Linear(51, 100, 12.1, 35.4, pm25),
        <= 55.4  => Linear(101, 150, 35.5, 55.4, pm25),
        <= 150.4 => Linear(151, 200, 55.5, 150.4, pm25),
        <= 250.4 => Linear(201, 300, 150.5, 250.4, pm25),
        _        => Linear(301, 400, 250.5, 350.4, pm25)
    };

    private static double Linear(double aqiLow, double aqiHigh, double concLow, double concHigh, double conc) =>
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

    private sealed record OpenAQResponse(OpenAQMeasurement[] Results);
    private sealed record OpenAQMeasurement(string Parameter, double Value, OpenAQDate Date);
    private sealed record OpenAQDate(DateTime Utc, string Local);
}
