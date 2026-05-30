using CivicPulse.Core.Interfaces;
using CivicPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CivicPulse.Infrastructure.BackgroundJobs;

// IHostedService background job that refreshes weather/AQ cache for all favorited locations.
// Runs every 30 minutes. Scoped services (DbContext) are resolved via IServiceScopeFactory.
public class WeatherRefreshJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeatherRefreshJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    public WeatherRefreshJob(IServiceScopeFactory scopeFactory, ILogger<WeatherRefreshJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeatherRefreshJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAllFavoritedLocationsAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RefreshAllFavoritedLocationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
        var aqService = scope.ServiceProvider.GetRequiredService<IAirQualityService>();

        var locations = await db.FavoriteLocations
            .Select(f => f.Location)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation("Refreshing weather for {Count} favorited locations.", locations.Count);

        foreach (var location in locations)
        {
            try
            {
                await weatherService.GetCurrentWeatherAsync(location.Latitude, location.Longitude, ct);
                await aqService.GetCurrentAirQualityAsync(location.Latitude, location.Longitude, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh data for location {Id} ({Name}).", location.Id, location.Name);
            }
        }
    }
}
