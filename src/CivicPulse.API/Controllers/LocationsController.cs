using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LocationsController : ControllerBase
{
    private readonly IGeocodingService _geocoding;
    private readonly ILocationRepository _repo;

    public LocationsController(IGeocodingService geocoding, ILocationRepository repo)
    {
        _geocoding = geocoding;
        _repo = repo;
    }

    /// <summary>Search for cities by name using Nominatim / OpenStreetMap.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<LocationSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return BadRequest(new { error = "Query must be at least 2 characters." });

        var results = await _geocoding.SearchAsync(query, ct);
        return Ok(results);
    }
}
