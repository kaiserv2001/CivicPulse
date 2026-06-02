using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Http;
using CivicPulse.Core.Models;

namespace CivicPulse.Web.Services;

public class ApiClient(HttpClient http, AuthState auth)
{
    private void ApplyAuth() =>
        http.DefaultRequestHeaders.Authorization = auth.Token is not null
            ? new AuthenticationHeaderValue("Bearer", auth.Token)
            : null;

    public Task<List<LocationSearchResult>?> SearchLocationsAsync(string query, CancellationToken ct = default) =>
        http.GetFromJsonAsync<List<LocationSearchResult>>(
            $"api/locations/search?query={Uri.EscapeDataString(query)}", ct);

    public Task<DashboardResponse?> GetDashboardAsync(int locationId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<DashboardResponse>($"api/dashboard/{locationId}", ct);

    public Task<CompareResponse?> CompareAsync(int loc1, int loc2, CancellationToken ct = default) =>
        http.GetFromJsonAsync<CompareResponse>($"api/dashboard/compare?loc1={loc1}&loc2={loc2}", ct);

    public async Task<(int locationId, string? error)> AddFavoriteAsync(
        LocationSearchResult result, CancellationToken ct = default)
    {
        ApplyAuth();
        var request = new
        {
            cityName = result.City,
            country = result.Country,
            state = result.State,
            latitude = result.Latitude,
            longitude = result.Longitude,
            alias = (string?)null
        };

        var response = await http.PostAsJsonAsync("api/favorites", request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            var body = await response.Content.ReadFromJsonAsync<FavoriteCreated>(ct);
            return (body?.LocationId ?? 0, null);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadFromJsonAsync<FavoriteCreated>(ct);
            return (body?.LocationId ?? 0, null);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (0, "unauthorized");

        return (0, "error");
    }

    public async Task<List<FavoriteItem>?> GetFavoritesAsync(CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.GetAsync("api/favorites", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<FavoriteItem>>(ct);
    }

    public async Task<bool> DeleteFavoriteAsync(int id, CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.DeleteAsync($"api/favorites/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<(bool success, string? token, string? error)> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", new { email, password }, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return (true, body?.Token, null);
        }
        return (false, null, "Invalid credentials.");
    }

    public async Task<(bool success, string? token, string? error)> RegisterAsync(
        string email, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", new { email, password }, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return (true, body?.Token, null);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return (false, null, "Email already registered.");
        return (false, null, "Registration failed.");
    }

    public Task<IReadOnlyList<AqTrendDay>?> GetAqTrendAsync(int locationId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<IReadOnlyList<AqTrendDay>>($"api/dashboard/{locationId}/aqtrend", ct);

    public async Task<ProfileInfo?> GetProfileAsync(CancellationToken ct = default)
    {
        ApplyAuth();
        try { return await http.GetFromJsonAsync<ProfileInfo>("api/profile", ct); }
        catch { return null; }
    }

    public async Task<(bool success, ProfileInfo? profile, string? error)> UpdateEmailAsync(
        string newEmail, CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.PutAsJsonAsync("api/profile/email", new { newEmail }, ct);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<ProfileInfo>(ct);
            return (true, body, null);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return (false, null, "Email already in use.");
        return (false, null, "Failed to update email.");
    }

    public async Task<(bool success, string? error)> UpdatePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.PutAsJsonAsync("api/profile/password",
            new { currentPassword, newPassword }, ct);
        if (response.IsSuccessStatusCode) return (true, null);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            return (false, "Current password is incorrect.");
        return (false, "Failed to update password.");
    }

    public async Task<string?> GetVapidPublicKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await http.GetFromJsonAsync<VapidKeyResponse>("api/push/vapid-public-key", ct);
            return r?.PublicKey;
        }
        catch { return null; }
    }

    public async Task<bool> PushSubscribeAsync(string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.PostAsJsonAsync("api/push/subscribe", new { endpoint, p256dh, auth }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PushUnsubscribeAsync(string endpoint, CancellationToken ct = default)
    {
        ApplyAuth();
        var response = await http.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "api/push/subscribe")
            {
                Content = JsonContent.Create(new { endpoint })
            }, ct);
        return response.IsSuccessStatusCode;
    }

    private record VapidKeyResponse(string PublicKey);

    private record FavoriteCreated(int Id, int LocationId, string Name);
    private record TokenResponse(string Token);
}

public record FavoriteItem(int Id, string? Alias, DateTime SavedAt, SavedLocation Location);
public record SavedLocation(int Id, string Name, string Country, double Latitude, double Longitude);
public record ProfileInfo(string Email, DateTime CreatedAt);
