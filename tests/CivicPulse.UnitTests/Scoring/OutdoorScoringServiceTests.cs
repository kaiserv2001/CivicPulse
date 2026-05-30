using CivicPulse.Core.Models;
using CivicPulse.Core.Services;
using FluentAssertions;
using Xunit;

namespace CivicPulse.UnitTests.Scoring;

public class OutdoorScoringServiceTests
{
    private readonly OutdoorScoringService _sut = new();

    private static WeatherData IdealWeather() => new(
        TemperatureCelsius: 22, FeelsLikeCelsius: 22, WindSpeedKmh: 10, WindGustKmh: 15,
        PrecipitationMm: 0, PrecipitationProbability: 0, UvIndex: 3,
        RelativeHumidity: 50, WeatherCode: 800, WeatherDescription: "Clear sky", ObservedAt: DateTime.UtcNow);

    private static AirQualityData GoodAir() =>
        new(Aqi: 30, Pm25: 8, Pm10: 15, No2: 10, O3: 40, Co: 0.5, AqiCategory: "Good",
            DominantPollutant: "PM2.5", ObservedAt: DateTime.UtcNow);

    private static AirQualityData HazardousAir() =>
        new(Aqi: 350, Pm25: 180, Pm10: 250, No2: 80, O3: 120, Co: 5, AqiCategory: "Hazardous",
            DominantPollutant: "PM2.5", ObservedAt: DateTime.UtcNow);

    [Fact]
    public void Calculate_IdealConditions_ReturnsGradeA()
    {
        var score = _sut.Calculate(IdealWeather(), GoodAir());

        score.Total.Should().BeGreaterThanOrEqualTo(85);
        score.Grade.Should().Be("A");
    }

    [Fact]
    public void Calculate_HazardousAir_ReturnsLowTotal()
    {
        var score = _sut.Calculate(IdealWeather(), HazardousAir());

        score.Total.Should().BeLessThan(75);
        score.AirQualityScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_Thunderstorm_PenalizesWeatherScore()
    {
        var storm = IdealWeather() with { WeatherCode = 200 };
        var score = _sut.Calculate(storm, GoodAir());

        score.WeatherScore.Should().BeLessThan(50);
    }

    [Fact]
    public void Calculate_HighWind_PenalizesWindScore()
    {
        var windy = IdealWeather() with { WindSpeedKmh = 60 };
        var score = _sut.Calculate(windy, GoodAir());

        score.WindScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_ExtremeHeat_PenalizesWeatherScore()
    {
        var hot = IdealWeather() with { TemperatureCelsius = 40 };
        var score = _sut.Calculate(hot, GoodAir());

        score.WeatherScore.Should().BeLessThan(80);
    }

    [Fact]
    public void Calculate_TotalIsClamped_Between0And100()
    {
        var worstCase = (IdealWeather() with { WeatherCode = 200, WindSpeedKmh = 80 });
        var score = _sut.Calculate(worstCase, HazardousAir());

        score.Total.Should().BeInRange(0, 100);
    }

    [Fact]
    public void GetRecommendations_RainLikely_WalkingNotSuitable()
    {
        var rainy = IdealWeather() with { PrecipitationProbability = 80, PrecipitationMm = 5 };
        var score = _sut.Calculate(rainy, GoodAir());
        var recs = _sut.GetRecommendations(score, rainy, GoodAir());

        recs.First(r => r.Activity == "Walking").Suitable.Should().BeFalse();
        recs.First(r => r.Activity == "Cycling").Suitable.Should().BeFalse();
    }

    [Fact]
    public void GetRecommendations_IdealConditions_AllActivitiesSuitable()
    {
        var score = _sut.Calculate(IdealWeather(), GoodAir());
        var recs = _sut.GetRecommendations(score, IdealWeather(), GoodAir());

        recs.Should().AllSatisfy(r => r.Suitable.Should().BeTrue());
    }
}
