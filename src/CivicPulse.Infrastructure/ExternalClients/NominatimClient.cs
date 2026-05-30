using System.Net.Http.Json;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.ExternalClients;

// Nominatim (OpenStreetMap): https://nominatim.openstreetmap.org
// Usage policy: 1 request/second max; cache aggressively to stay compliant.
// Nominatim policy: https://operations.osmfoundation.org/policies/nominatim/
public class NominatimClient : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NominatimClient> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly SemaphoreSlim RateLimiter = new(1, 1);

    public NominatimClient(HttpClient http, IMemoryCache cache, ILogger<NominatimClient> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LocationSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var cacheKey = $"geocode_{query.ToLowerInvariant().Trim()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LocationSearchResult>? cached) && cached is not null)
            return cached;

        // Rate-limit: enforce at most 1 req/sec as required by Nominatim policy
        await RateLimiter.WaitAsync(ct);
        try
        {
            var url = $"search?q={Uri.EscapeDataString(query)}&format=json&addressdetails=1&limit=5";
            _logger.LogInformation("Geocoding query: {Query}", query);

            var results = await _http.GetFromJsonAsync<NominatimResult[]>(url, ct) ?? [];

            var locations = results
                .Where(r => double.TryParse(r.Lat, out _) && double.TryParse(r.Lon, out _))
                .Select(r => new LocationSearchResult(
                    DisplayName: r.DisplayName,
                    City: r.Address?.City ?? r.Address?.Town ?? r.Address?.Village ?? query,
                    Country: r.Address?.Country ?? string.Empty,
                    State: r.Address?.State,
                    Latitude: double.Parse(r.Lat),
                    Longitude: double.Parse(r.Lon),
                    PlaceType: r.Type
                ))
                .ToList();

            _cache.Set(cacheKey, (IReadOnlyList<LocationSearchResult>)locations, CacheTtl);
            return locations;
        }
        finally
        {
            // Hold the semaphore for 1 second before releasing to enforce the rate limit
            _ = Task.Delay(1000, ct).ContinueWith(_ => RateLimiter.Release(), CancellationToken.None);
        }
    }

    private sealed record NominatimResult(
        string Lat, string Lon, string DisplayName, string Type, NominatimAddress? Address);
    private sealed record NominatimAddress(
        string? City, string? Town, string? Village, string? State, string? Country);
}
