using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;

namespace CivicPulse.Core.Services;

public class OutdoorScoringService : IOutdoorScoringService
{
    public OutdoorScore Calculate(WeatherData weather, AirQualityData airQuality)
    {
        int weatherScore = ScoreWeather(weather);
        int airScore = ScoreAirQuality(airQuality);
        int windScore = ScoreWind(weather);
        int uvScore = ScoreUv(weather);

        int total = (int)Math.Round(weatherScore * 0.35 + airScore * 0.30 + windScore * 0.20 + uvScore * 0.15);

        return new OutdoorScore(
            Total: total,
            WeatherScore: weatherScore,
            AirQualityScore: airScore,
            WindScore: windScore,
            UvScore: uvScore,
            Grade: ToGrade(total),
            Summary: ToSummary(total)
        );
    }

    public IReadOnlyList<ActivityRecommendation> GetRecommendations(
        OutdoorScore score, WeatherData weather, AirQualityData airQuality)
    {
        bool rainRisk = weather.PrecipitationProbability > 40 || weather.PrecipitationMm > 2;
        bool highWind = weather.WindSpeedKmh > 30;
        bool badAir = airQuality.Aqi > 100;
        bool extremeHeat = weather.TemperatureCelsius > 35;
        bool extremeCold = weather.TemperatureCelsius < 2;

        return new List<ActivityRecommendation>
        {
            new("Walking", score.Total >= 50 && !rainRisk && !badAir,
                rainRisk ? "Rain likely" : badAir ? "Poor air quality" : "Conditions suitable",
                "🚶"),
            new("Cycling", score.Total >= 55 && !rainRisk && !highWind && !badAir,
                highWind ? "High wind speeds" : rainRisk ? "Rain likely" : badAir ? "Poor air quality" : "Safe to ride",
                "🚴"),
            new("Outdoor Commute", score.Total >= 45 && !rainRisk,
                rainRisk ? "Bring rain gear" : extremeHeat ? "Heat warning" : "Good for commuting",
                "🚌"),
            new("Outdoor Work / Exercise", score.Total >= 60 && !badAir && !extremeHeat && !extremeCold,
                extremeHeat ? "Heat risk — limit exertion" : extremeCold ? "Too cold for prolonged outdoor work" : badAir ? "AQI too high" : "Good conditions",
                "🏃")
        };
    }

    private static int ScoreWeather(WeatherData w)
    {
        // Start at 100 and deduct
        int score = 100;
        score -= w.WeatherCode switch
        {
            >= 200 and <= 299 => 60, // Thunderstorm
            >= 300 and <= 399 => 30, // Drizzle
            >= 500 and <= 531 => 45, // Rain
            >= 600 and <= 622 => 50, // Snow
            >= 700 and <= 781 => 20, // Fog/mist
            800 => 0,                // Clear
            >= 801 and <= 804 => 10, // Cloudy
            _ => 10
        };

        if (w.TemperatureCelsius > 38 || w.TemperatureCelsius < 0) score -= 25;
        else if (w.TemperatureCelsius > 33 || w.TemperatureCelsius < 5) score -= 15;

        return Math.Clamp(score, 0, 100);
    }

    private static int ScoreAirQuality(AirQualityData a) => a.Aqi switch
    {
        <= 50 => 100,
        <= 100 => 75,
        <= 150 => 50,
        <= 200 => 25,
        _ => 0
    };

    private static int ScoreWind(WeatherData w) => w.WindSpeedKmh switch
    {
        <= 15 => 100,
        <= 25 => 80,
        <= 40 => 55,
        <= 55 => 30,
        _ => 0
    };

    private static int ScoreUv(WeatherData w) => w.UvIndex switch
    {
        <= 2 => 100,
        <= 5 => 85,
        <= 7 => 65,
        <= 10 => 40,
        _ => 20
    };

    private static string ToGrade(int score) => score switch
    {
        >= 85 => "A",
        >= 70 => "B",
        >= 55 => "C",
        >= 40 => "D",
        _ => "F"
    };

    private static string ToSummary(int score) => score switch
    {
        >= 85 => "Excellent conditions — get outside!",
        >= 70 => "Good day for most outdoor activities.",
        >= 55 => "Fair — comfortable with minor precautions.",
        >= 40 => "Poor — limited outdoor activities advised.",
        _ => "Unsafe — stay indoors if possible."
    };
}
