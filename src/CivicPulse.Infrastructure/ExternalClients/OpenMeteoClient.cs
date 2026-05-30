using System.Net.Http.Json;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.ExternalClients;

// Open-Meteo API: https://api.open-meteo.com — no API key required
public class OpenMeteoClient : IWeatherService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenMeteoClient> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<int, string> WeatherDescriptions = new()
    {
        [0] = "Clear sky", [1] = "Mainly clear", [2] = "Partly cloudy", [3] = "Overcast",
        [45] = "Fog", [48] = "Icy fog", [51] = "Light drizzle", [61] = "Slight rain",
        [63] = "Moderate rain", [65] = "Heavy rain", [71] = "Slight snow", [80] = "Rain showers",
        [95] = "Thunderstorm", [99] = "Thunderstorm with hail"
    };

    public OpenMeteoClient(HttpClient http, IMemoryCache cache, ILogger<OpenMeteoClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WeatherData> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = $"weather_current_{latitude:F3}_{longitude:F3}";
        if (_cache.TryGetValue(cacheKey, out WeatherData? cached) && cached is not null)
            return cached;

        var url = $"v1/forecast?latitude={latitude}&longitude={longitude}" +
                  "&current=temperature_2m,apparent_temperature,wind_speed_10m,wind_gusts_10m," +
                  "precipitation,precipitation_probability,uv_index,relative_humidity_2m,weather_code" +
                  "&timezone=auto";

        _logger.LogInformation("Fetching current weather for {Lat},{Lon}", latitude, longitude);

        var response = await _http.GetFromJsonAsync<OpenMeteoCurrentResponse>(url, ct)
            ?? throw new InvalidOperationException("Open-Meteo returned null response.");

        var c = response.Current;
        var result = new WeatherData(
            TemperatureCelsius: c.Temperature2m,
            FeelsLikeCelsius: c.ApparentTemperature,
            WindSpeedKmh: c.WindSpeed10m,
            WindGustKmh: c.WindGusts10m,
            PrecipitationMm: c.Precipitation,
            PrecipitationProbability: c.PrecipitationProbability,
            UvIndex: c.UvIndex,
            RelativeHumidity: c.RelativeHumidity2m,
            WeatherCode: c.WeatherCode,
            WeatherDescription: WeatherDescriptions.GetValueOrDefault(c.WeatherCode, "Unknown"),
            ObservedAt: DateTime.UtcNow
        );

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<IReadOnlyList<WeatherForecastDay>> GetForecastAsync(
        double latitude, double longitude, int days = 7, CancellationToken ct = default)
    {
        var cacheKey = $"weather_forecast_{latitude:F3}_{longitude:F3}_{days}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<WeatherForecastDay>? cached) && cached is not null)
            return cached;

        var url = $"v1/forecast?latitude={latitude}&longitude={longitude}" +
                  $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max," +
                  $"wind_speed_10m_max,uv_index_max,weather_code&forecast_days={days}&timezone=auto";

        var response = await _http.GetFromJsonAsync<OpenMeteoDailyResponse>(url, ct)
            ?? throw new InvalidOperationException("Open-Meteo returned null forecast response.");

        var result = response.Daily.Time
            .Select((date, i) => new WeatherForecastDay(
                Date: DateTime.Parse(date),
                MaxTemperatureCelsius: response.Daily.Temperature2mMax[i],
                MinTemperatureCelsius: response.Daily.Temperature2mMin[i],
                PrecipitationMm: response.Daily.PrecipitationSum[i],
                PrecipitationProbability: response.Daily.PrecipitationProbabilityMax[i],
                MaxWindSpeedKmh: response.Daily.WindSpeed10mMax[i],
                UvIndexMax: response.Daily.UvIndexMax[i],
                WeatherCode: response.Daily.WeatherCode[i]
            ))
            .ToList();

        _cache.Set(cacheKey, (IReadOnlyList<WeatherForecastDay>)result, CacheTtl);
        return result;
    }

    private sealed record OpenMeteoCurrentResponse(CurrentBlock Current);
    private sealed record CurrentBlock(
        double Temperature2m, double ApparentTemperature,
        double WindSpeed10m, double WindGusts10m,
        double Precipitation, double PrecipitationProbability,
        double UvIndex, double RelativeHumidity2m, int WeatherCode);
    private sealed record OpenMeteoDailyResponse(DailyBlock Daily);
    private sealed record DailyBlock(
        string[] Time, double[] Temperature2mMax, double[] Temperature2mMin,
        double[] PrecipitationSum, double[] PrecipitationProbabilityMax,
        double[] WindSpeed10mMax, double[] UvIndexMax, int[] WeatherCode);
}
