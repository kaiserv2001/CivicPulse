using CivicPulse.Core.Models;

namespace CivicPulse.Core.Interfaces;

public interface IWeatherService
{
    Task<WeatherData> GetCurrentWeatherAsync(double latitude, double longitude, CancellationToken ct = default);
    Task<IReadOnlyList<WeatherForecastDay>> GetForecastAsync(double latitude, double longitude, int days = 7, CancellationToken ct = default);
}
