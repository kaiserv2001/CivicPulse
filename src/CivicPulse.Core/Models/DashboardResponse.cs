namespace CivicPulse.Core.Models;

public record DashboardResponse(
    int LocationId,
    string LocationName,
    string Country,
    double Latitude,
    double Longitude,
    WeatherData CurrentWeather,
    AirQualityData AirQuality,
    OutdoorScore Score,
    IReadOnlyList<WeatherForecastDay> Forecast,
    IReadOnlyList<ActivityRecommendation> Recommendations,
    DateTime GeneratedAt
);

public record CompareResponse(
    DashboardResponse LocationA,
    DashboardResponse LocationB,
    string Winner,
    string WinnerReason
);
