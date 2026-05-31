using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using CivicPulse.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.ExternalClients;

// Open-Meteo API: https://api.open-meteo.com — no API key required
public class OpenMeteoClient : IWeatherService
{
    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OpenMeteoClient> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<int, string> WeatherDescriptions = new()
    {
        [0] = "Clear sky", [1] = "Mainly clear", [2] = "Partly cloudy", [3] = "Overcast",
        [45] = "Fog", [48] = "Icy fog", [51] = "Light drizzle", [61] = "Slight rain",
        [63] = "Moderate rain", [65] = "Heavy rain", [71] = "Slight snow", [80] = "Rain showers",
        [95] = "Thunderstorm", [99] = "Thunderstorm with hail"
    };

    public OpenMeteoClient(HttpClient http, IDistributedCache cache, ILogger<OpenMeteoClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WeatherData> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.CurrentWeather(latitude, longitude);
        var cached = await _cache.GetJsonAsync<WeatherData>(cacheKey, ct);
        if (cached is not null) return cached;

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

        await _cache.SetJsonAsync(cacheKey, result, CacheTtl, ct);
        return result;
    }

    public async Task<IReadOnlyList<WeatherForecastDay>> GetForecastAsync(
        double latitude, double longitude, int days = 7, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Forecast(latitude, longitude, days);
        var cached = await _cache.GetJsonAsync<List<WeatherForecastDay>>(cacheKey, ct);
        if (cached is not null) return cached;

        var url = $"v1/forecast?latitude={latitude}&longitude={longitude}" +
                  $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max," +
                  $"wind_speed_10m_max,uv_index_max,weather_code&forecast_days={days}&timezone=auto";

        var response = await _http.GetFromJsonAsync<OpenMeteoDailyResponse>(url, ct)
            ?? throw new InvalidOperationException("Open-Meteo returned null forecast response.");

        var result = response.Daily.Time
            .Select((date, i) => new WeatherForecastDay(
                Date: DateTime.Parse(date),
                MaxTemperatureCelsius: response.Daily.Temperature2mMax?[i] ?? 0,
                MinTemperatureCelsius: response.Daily.Temperature2mMin?[i] ?? 0,
                PrecipitationMm: response.Daily.PrecipitationSum?[i] ?? 0,
                PrecipitationProbability: response.Daily.PrecipitationProbabilityMax?[i] ?? 0,
                MaxWindSpeedKmh: response.Daily.WindSpeed10mMax?[i] ?? 0,
                UvIndexMax: response.Daily.UvIndexMax?[i] ?? 0,
                WeatherCode: response.Daily.WeatherCode?[i] ?? 0
            ))
            .ToList();

        await _cache.SetJsonAsync(cacheKey, result, CacheTtl, ct);
        return result;
    }

    private sealed record OpenMeteoCurrentResponse(CurrentBlock Current);
    private sealed record CurrentBlock(
        [property: JsonPropertyName("temperature_2m")]          double Temperature2m,
        [property: JsonPropertyName("apparent_temperature")]    double ApparentTemperature,
        [property: JsonPropertyName("wind_speed_10m")]          double WindSpeed10m,
        [property: JsonPropertyName("wind_gusts_10m")]          double WindGusts10m,
        [property: JsonPropertyName("precipitation")]           double Precipitation,
        [property: JsonPropertyName("precipitation_probability")]double PrecipitationProbability,
        [property: JsonPropertyName("uv_index")]                double UvIndex,
        [property: JsonPropertyName("relative_humidity_2m")]    double RelativeHumidity2m,
        [property: JsonPropertyName("weather_code")]            int WeatherCode);
    private sealed record OpenMeteoDailyResponse(DailyBlock Daily);
    private sealed record DailyBlock(
        [property: JsonPropertyName("time")]                          string[] Time,
        [property: JsonPropertyName("temperature_2m_max")]            double[]? Temperature2mMax,
        [property: JsonPropertyName("temperature_2m_min")]            double[]? Temperature2mMin,
        [property: JsonPropertyName("precipitation_sum")]             double[]? PrecipitationSum,
        [property: JsonPropertyName("precipitation_probability_max")] double[]? PrecipitationProbabilityMax,
        [property: JsonPropertyName("wind_speed_10m_max")]            double[]? WindSpeed10mMax,
        [property: JsonPropertyName("uv_index_max")]                  double[]? UvIndexMax,
        [property: JsonPropertyName("weather_code")]                  int[]? WeatherCode);
}
