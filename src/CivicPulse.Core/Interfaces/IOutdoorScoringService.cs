using CivicPulse.Core.Models;

namespace CivicPulse.Core.Interfaces;

public interface IOutdoorScoringService
{
    OutdoorScore Calculate(WeatherData weather, AirQualityData airQuality);
    IReadOnlyList<ActivityRecommendation> GetRecommendations(OutdoorScore score, WeatherData weather, AirQualityData airQuality);
}
