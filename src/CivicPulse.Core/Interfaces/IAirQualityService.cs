using CivicPulse.Core.Models;

namespace CivicPulse.Core.Interfaces;

public interface IAirQualityService
{
    Task<AirQualityData> GetCurrentAirQualityAsync(double latitude, double longitude, CancellationToken ct = default);
}
