using CivicPulse.Core.Models;

namespace CivicPulse.Core.Interfaces;

public interface IGeocodingService
{
    Task<IReadOnlyList<LocationSearchResult>> SearchAsync(string query, CancellationToken ct = default);
}
