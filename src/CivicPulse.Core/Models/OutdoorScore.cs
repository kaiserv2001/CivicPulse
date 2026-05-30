namespace CivicPulse.Core.Models;

public record OutdoorScore(
    int Total,
    int WeatherScore,
    int AirQualityScore,
    int WindScore,
    int UvScore,
    string Grade,
    string Summary
);

public record ActivityRecommendation(
    string Activity,
    bool Suitable,
    string Reason,
    string Icon
);
