namespace CivicPulse.Infrastructure.Caching;

public static class CacheKeys
{
    public static string CurrentWeather(double lat, double lon) => $"weather_current_{lat:F3}_{lon:F3}";
    public static string Forecast(double lat, double lon, int days) => $"weather_forecast_{lat:F3}_{lon:F3}_{days}";
    public static string AirQuality(double lat, double lon) => $"aq_{lat:F3}_{lon:F3}";
    public static string Geocode(string query) => $"geocode_{query.ToLowerInvariant().Trim()}";
    public static string Dashboard(int locationId) => $"dashboard_{locationId}";
}
