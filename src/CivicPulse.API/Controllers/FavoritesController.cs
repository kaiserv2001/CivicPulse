using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CivicPulse.Core.Entities;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly ILocationRepository _repo;
    private readonly IGeocodingService _geocoding;

    public FavoritesController(ILocationRepository repo, IGeocodingService geocoding)
    {
        _repo = repo;
        _geocoding = geocoding;
    }

    /// <summary>List all favorites for the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavorites(CancellationToken ct)
    {
        var userId = GetUserId();
        var favorites = await _repo.GetFavoritesForUserAsync(userId, ct);
        return Ok(favorites.Select(f => new
        {
            f.Id,
            f.Alias,
            f.SavedAt,
            Location = new { f.Location.Id, f.Location.Name, f.Location.Country, f.Location.Latitude, f.Location.Longitude }
        }));
    }

    /// <summary>Save a searched location as a favorite. Upserts the Location row if needed.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request, CancellationToken ct)
    {
        var userId = GetUserId();

        var location = await _repo.GetByCoordinatesAsync(request.Latitude, request.Longitude, ct);
        if (location is null)
        {
            location = await _repo.AddAsync(new Location
            {
                Name = request.CityName,
                Country = request.Country,
                State = request.State,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            }, ct);
        }

        if (await _repo.FavoriteExistsAsync(userId, location.Id, ct))
            return Conflict(new { error = "Already in favorites.", LocationId = location.Id });

        var favorite = await _repo.AddFavoriteAsync(new FavoriteLocation
        {
            UserId = userId,
            LocationId = location.Id,
            Alias = request.Alias
        }, ct);

        return CreatedAtAction(nameof(GetFavorites), new { id = favorite.Id },
            new { favorite.Id, LocationId = location.Id, location.Name });
    }

    /// <summary>Remove a location from the authenticated user's favorites.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteFavorite(int id, CancellationToken ct)
    {
        await _repo.DeleteFavoriteAsync(id, GetUserId(), ct);
        return NoContent();
    }

    // ASP.NET Core 9 uses JsonWebTokenHandler which does not remap "sub" to NameIdentifier
    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("JWT sub claim missing.");
}

public record AddFavoriteRequest(
    string CityName,
    string Country,
    string? State,
    double Latitude,
    double Longitude,
    string? Alias
);
