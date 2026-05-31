using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using CivicPulse.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly ILocationRepository _repo;
    private readonly IWeatherService _weather;
    private readonly IAirQualityService _airQuality;
    private readonly IOutdoorScoringService _scoring;
    private readonly IDistributedCache _cache;

    public DashboardController(
        ILocationRepository repo,
        IWeatherService weather,
        IAirQualityService airQuality,
        IOutdoorScoringService scoring,
        IDistributedCache cache)
    {
        _repo = repo;
        _weather = weather;
        _airQuality = airQuality;
        _scoring = scoring;
        _cache = cache;
    }

    /// <summary>Full environmental dashboard for a saved location.</summary>
    [HttpGet("{locationId:int}")]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(int locationId, CancellationToken ct)
    {
        var cacheKey = CacheKeys.Dashboard(locationId);
        var cached = await _cache.GetJsonAsync<DashboardResponse>(cacheKey, ct);
        if (cached is not null) return Ok(cached);

        var location = await _repo.GetByIdAsync(locationId, ct);
        if (location is null)
            return NotFound(new { error = $"Location {locationId} not found." });

        var (weather, airQuality, forecast) = await FetchParallelAsync(location.Latitude, location.Longitude, ct);
        var score = _scoring.Calculate(weather, airQuality);
        var recommendations = _scoring.GetRecommendations(score, weather, airQuality);

        var response = new DashboardResponse(
            LocationId: location.Id,
            LocationName: location.Name,
            Country: location.Country,
            Latitude: location.Latitude,
            Longitude: location.Longitude,
            CurrentWeather: weather,
            AirQuality: airQuality,
            Score: score,
            Forecast: forecast,
            Recommendations: recommendations,
            GeneratedAt: DateTime.UtcNow
        );

        await _cache.SetJsonAsync(cacheKey, response, TimeSpan.FromMinutes(15), ct);
        return Ok(response);
    }

    /// <summary>Compare environmental conditions between two saved locations.</summary>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(CompareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Compare([FromQuery] int loc1, [FromQuery] int loc2, CancellationToken ct)
    {
        // Sequential — EF Core DbContext is not thread-safe; cannot run two queries concurrently
        var locationA = await _repo.GetByIdAsync(loc1, ct);
        var locationB = await _repo.GetByIdAsync(loc2, ct);

        if (locationA is null) return NotFound(new { error = $"Location {loc1} not found." });
        if (locationB is null) return NotFound(new { error = $"Location {loc2} not found." });

        var taskA = FetchParallelAsync(locationA.Latitude, locationA.Longitude, ct);
        var taskB = FetchParallelAsync(locationB.Latitude, locationB.Longitude, ct);
        await Task.WhenAll(taskA, taskB);
        var (weatherA, aqA, forecastA) = taskA.Result;
        var (weatherB, aqB, forecastB) = taskB.Result;

        var scoreA = _scoring.Calculate(weatherA, aqA);
        var scoreB = _scoring.Calculate(weatherB, aqB);

        var dashA = new DashboardResponse(locationA.Id, locationA.Name, locationA.Country,
            locationA.Latitude, locationA.Longitude, weatherA, aqA, scoreA, forecastA,
            _scoring.GetRecommendations(scoreA, weatherA, aqA), DateTime.UtcNow);

        var dashB = new DashboardResponse(locationB.Id, locationB.Name, locationB.Country,
            locationB.Latitude, locationB.Longitude, weatherB, aqB, scoreB, forecastB,
            _scoring.GetRecommendations(scoreB, weatherB, aqB), DateTime.UtcNow);

        string winner = scoreA.Total >= scoreB.Total ? locationA.Name : locationB.Name;
        string reason = $"{winner} scores {Math.Max(scoreA.Total, scoreB.Total)} vs {Math.Min(scoreA.Total, scoreB.Total)}.";

        return Ok(new CompareResponse(dashA, dashB, winner, reason));
    }

    /// <summary>7-day AQI trend for a saved location (daily PM2.5-derived AQI).</summary>
    [HttpGet("{locationId:int}/aqtrend")]
    [ProducesResponseType(typeof(IReadOnlyList<AqTrendDay>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAqTrend(int locationId, CancellationToken ct)
    {
        var location = await _repo.GetByIdAsync(locationId, ct);
        if (location is null)
            return NotFound(new { error = $"Location {locationId} not found." });

        var trend = await _airQuality.GetAqTrendAsync(location.Latitude, location.Longitude, ct);
        return Ok(trend);
    }

    private async Task<(WeatherData, AirQualityData, IReadOnlyList<WeatherForecastDay>)> FetchParallelAsync(
        double lat, double lon, CancellationToken ct)
    {
        var weatherTask  = _weather.GetCurrentWeatherAsync(lat, lon, ct);
        var aqTask       = _airQuality.GetCurrentAirQualityAsync(lat, lon, ct);
        var forecastTask = _weather.GetForecastAsync(lat, lon, 7, ct);

        await Task.WhenAll(weatherTask, aqTask, forecastTask);
        return (weatherTask.Result, aqTask.Result, forecastTask.Result);
    }
}

internal static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenBoth<T1, T2>(this (Task<T1>, Task<T2>) tasks)
    {
        await Task.WhenAll(tasks.Item1, tasks.Item2);
        return (tasks.Item1.Result, tasks.Item2.Result);
    }
}
