using CivicPulse.Core.Interfaces;
using CivicPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebPush;

namespace CivicPulse.Infrastructure.BackgroundJobs;

// Runs every 30 minutes: refreshes weather/AQ cache for all favorited locations, then
// sends push notifications to users whose favorited location score dropped below 40.
// Scoped services (DbContext) are resolved via IServiceScopeFactory.
public class WeatherRefreshJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeatherRefreshJob> _logger;
    private readonly VapidDetails _vapid;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromHours(2);

    public WeatherRefreshJob(
        IServiceScopeFactory scopeFactory,
        ILogger<WeatherRefreshJob> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _vapid = new VapidDetails(
            config["Vapid:Subject"]!,
            config["Vapid:PublicKey"]!,
            config["Vapid:PrivateKey"]!);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeatherRefreshJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllFavoritedLocationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WeatherRefreshJob cycle failed; will retry next interval.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshAllFavoritedLocationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
        var aqService = scope.ServiceProvider.GetRequiredService<IAirQualityService>();
        var scoringService = scope.ServiceProvider.GetRequiredService<IOutdoorScoringService>();

        var locations = await db.FavoriteLocations
            .Select(f => f.Location)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation("Refreshing weather for {Count} favorited locations.", locations.Count);

        foreach (var location in locations)
        {
            try
            {
                var weather = await weatherService.GetCurrentWeatherAsync(location.Latitude, location.Longitude, ct);
                var aq = await aqService.GetCurrentAirQualityAsync(location.Latitude, location.Longitude, ct);
                var score = scoringService.Calculate(weather, aq);

                if (score.Total < 40)
                    await SendLowScoreNotificationsAsync(db, location.Id, location.Name, score.Total, ct);
                else
                    await ClearLowScoreFlagsAsync(db, location.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh data for location {Id} ({Name}).", location.Id, location.Name);
            }
        }
    }

    private async Task SendLowScoreNotificationsAsync(
        AppDbContext db, int locationId, string locationName, int score, CancellationToken ct)
    {
        var cooldownThreshold = DateTime.UtcNow - NotificationCooldown;

        var favorites = await db.FavoriteLocations
            .Where(f => f.LocationId == locationId
                     && (f.NotifiedLowScoreAt == null || f.NotifiedLowScoreAt < cooldownThreshold))
            .ToListAsync(ct);

        if (favorites.Count == 0) return;

        var userIds = favorites.Select(f => f.UserId).ToHashSet();
        var subscriptions = await db.PushSubscriptions
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = $"⚠️ Poor conditions in {locationName}",
            body = $"Outdoor score dropped to {score}/100 — may not be safe to go outside.",
            locationId
        });

        var client = new WebPushClient();
        var stale = new List<Core.Entities.PushSubscription>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(pushSub, payload, _vapid, ct);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Browser unsubscribed — clean up
                stale.Add(sub);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push to subscription {Id}.", sub.Id);
            }
        }

        if (stale.Count > 0) db.PushSubscriptions.RemoveRange(stale);

        foreach (var fav in favorites)
            fav.NotifiedLowScoreAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Sent low-score push for {Location} (score {Score}) to {Count} subscription(s).",
            locationName, score, subscriptions.Count - stale.Count);
    }

    private static async Task ClearLowScoreFlagsAsync(AppDbContext db, int locationId, CancellationToken ct)
    {
        var flagged = await db.FavoriteLocations
            .Where(f => f.LocationId == locationId && f.NotifiedLowScoreAt != null)
            .ToListAsync(ct);

        if (flagged.Count == 0) return;

        foreach (var fav in flagged)
            fav.NotifiedLowScoreAt = null;

        await db.SaveChangesAsync(ct);
    }
}
