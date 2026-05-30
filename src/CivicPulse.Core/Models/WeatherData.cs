namespace CivicPulse.Core.Models;

public record WeatherData(
    double TemperatureCelsius,
    double FeelsLikeCelsius,
    double WindSpeedKmh,
    double WindGustKmh,
    double PrecipitationMm,
    double PrecipitationProbability,
    double UvIndex,
    double RelativeHumidity,
    int WeatherCode,
    string WeatherDescription,
    DateTime ObservedAt
);

public record WeatherForecastDay(
    DateTime Date,
    double MaxTemperatureCelsius,
    double MinTemperatureCelsius,
    double PrecipitationMm,
    double PrecipitationProbability,
    double MaxWindSpeedKmh,
    double UvIndexMax,
    int WeatherCode
);
