using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RecommendationsController : ControllerBase
{
    private readonly ILocationRepository _repo;
    private readonly IWeatherService _weather;
    private readonly IAirQualityService _airQuality;
    private readonly IOutdoorScoringService _scoring;

    public RecommendationsController(
        ILocationRepository repo,
        IWeatherService weather,
        IAirQualityService airQuality,
        IOutdoorScoringService scoring)
    {
        _repo = repo;
        _weather = weather;
        _airQuality = airQuality;
        _scoring = scoring;
    }

    /// <summary>Returns activity suitability recommendations for a saved location.</summary>
    [HttpGet("{locationId:int}")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRecommendations(int locationId, CancellationToken ct)
    {
        var location = await _repo.GetByIdAsync(locationId, ct);
        if (location is null)
            return NotFound(new { error = $"Location {locationId} not found." });

        var weatherTask = _weather.GetCurrentWeatherAsync(location.Latitude, location.Longitude, ct);
        var aqTask = _airQuality.GetCurrentAirQualityAsync(location.Latitude, location.Longitude, ct);
        await Task.WhenAll(weatherTask, aqTask);

        var score = _scoring.Calculate(weatherTask.Result, aqTask.Result);
        var activities = _scoring.GetRecommendations(score, weatherTask.Result, aqTask.Result);

        return Ok(new RecommendationResponse(
            LocationId: locationId,
            LocationName: location.Name,
            Score: score,
            Activities: activities,
            GeneratedAt: DateTime.UtcNow
        ));
    }
}

public record RecommendationResponse(
    int LocationId,
    string LocationName,
    OutdoorScore Score,
    IReadOnlyList<ActivityRecommendation> Activities,
    DateTime GeneratedAt
);
