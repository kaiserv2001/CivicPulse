using CivicPulse.Core.Entities;

namespace CivicPulse.Core.Interfaces;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Location?> GetByCoordinatesAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<Location> AddAsync(Location location, CancellationToken ct = default);
    Task<IReadOnlyList<FavoriteLocation>> GetFavoritesForUserAsync(string userId, CancellationToken ct = default);
    Task<FavoriteLocation> AddFavoriteAsync(FavoriteLocation favorite, CancellationToken ct = default);
    Task<bool> FavoriteExistsAsync(string userId, int locationId, CancellationToken ct = default);
    Task DeleteFavoriteAsync(int favoriteId, string userId, CancellationToken ct = default);
}
