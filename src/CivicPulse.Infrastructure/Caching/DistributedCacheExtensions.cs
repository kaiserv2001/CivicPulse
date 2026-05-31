using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace CivicPulse.Infrastructure.Caching;

public static class DistributedCacheExtensions
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public static async Task<T?> GetJsonAsync<T>(
        this IDistributedCache cache, string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, Opts);
    }

    public static Task SetJsonAsync<T>(
        this IDistributedCache cache, string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Opts);
        return cache.SetAsync(key, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
    }
}
