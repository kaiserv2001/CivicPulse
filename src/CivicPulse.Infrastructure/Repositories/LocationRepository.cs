using CivicPulse.Core.Entities;
using CivicPulse.Core.Interfaces;
using CivicPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CivicPulse.Infrastructure.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly AppDbContext _db;

    public LocationRepository(AppDbContext db) => _db = db;

    public Task<Location?> GetByIdAsync(int id, CancellationToken ct) =>
        _db.Locations.FindAsync(new object[] { id }, ct).AsTask();

    public Task<Location?> GetByCoordinatesAsync(double latitude, double longitude, CancellationToken ct) =>
        _db.Locations.FirstOrDefaultAsync(
            l => Math.Abs(l.Latitude - latitude) < 0.001 && Math.Abs(l.Longitude - longitude) < 0.001, ct);

    public async Task<Location> AddAsync(Location location, CancellationToken ct)
    {
        _db.Locations.Add(location);
        await _db.SaveChangesAsync(ct);
        return location;
    }

    public Task<IReadOnlyList<FavoriteLocation>> GetFavoritesForUserAsync(string userId, CancellationToken ct) =>
        _db.FavoriteLocations
           .Where(f => f.UserId == userId)
           .Include(f => f.Location)
           .OrderByDescending(f => f.SavedAt)
           .ToListAsync(ct)
           .ContinueWith(t => (IReadOnlyList<FavoriteLocation>)t.Result, ct);

    public async Task<FavoriteLocation> AddFavoriteAsync(FavoriteLocation favorite, CancellationToken ct)
    {
        _db.FavoriteLocations.Add(favorite);
        await _db.SaveChangesAsync(ct);
        return favorite;
    }

    public Task<bool> FavoriteExistsAsync(string userId, int locationId, CancellationToken ct) =>
        _db.FavoriteLocations.AnyAsync(f => f.UserId == userId && f.LocationId == locationId, ct);

    public async Task DeleteFavoriteAsync(int favoriteId, string userId, CancellationToken ct)
    {
        var favorite = await _db.FavoriteLocations
            .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId, ct);
        if (favorite is not null)
        {
            _db.FavoriteLocations.Remove(favorite);
            await _db.SaveChangesAsync(ct);
        }
    }
}
