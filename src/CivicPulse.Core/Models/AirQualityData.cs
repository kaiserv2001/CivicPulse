namespace CivicPulse.Core.Models;

public record AirQualityData(
    double Aqi,
    double Pm25,
    double Pm10,
    double No2,
    double O3,
    double Co,
    string AqiCategory,
    string? DominantPollutant,
    DateTime ObservedAt
);
