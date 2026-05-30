using System.Net;
using System.Text;
using CivicPulse.Infrastructure.ExternalClients;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace CivicPulse.UnitTests.Services;

public class OpenMeteoClientTests
{
    // JSON keys use camelCase to match System.Text.Json's web defaults (PropertyNameCaseInsensitive=true)
    private const string CurrentWeatherJson = """
        {
          "current": {
            "temperature2m": 15.5,
            "apparentTemperature": 13.0,
            "windSpeed10m": 20.0,
            "windGusts10m": 28.0,
            "precipitation": 0.0,
            "precipitationProbability": 10.0,
            "uvIndex": 3.0,
            "relativeHumidity2m": 55.0,
            "weatherCode": 0
          }
        }
        """;

    private static Mock<HttpMessageHandler> SetupHandler(string json)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return mock;
    }

    private static OpenMeteoClient BuildClient(Mock<HttpMessageHandler> handler, IMemoryCache? cache = null)
    {
        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.open-meteo.com/") };
        return new OpenMeteoClient(http, cache ?? new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<OpenMeteoClient>>());
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ValidCoords_ReturnsParsedWeatherData()
    {
        var handler = SetupHandler(CurrentWeatherJson);
        var sut = BuildClient(handler);

        var result = await sut.GetCurrentWeatherAsync(47.6062, -122.3321);

        result.TemperatureCelsius.Should().Be(15.5);
        result.FeelsLikeCelsius.Should().Be(13.0);
        result.WindSpeedKmh.Should().Be(20.0);
        result.WindGustKmh.Should().Be(28.0);
        result.PrecipitationMm.Should().Be(0.0);
        result.PrecipitationProbability.Should().Be(10.0);
        result.UvIndex.Should().Be(3.0);
        result.RelativeHumidity.Should().Be(55.0);
        result.WeatherCode.Should().Be(0);
        result.WeatherDescription.Should().Be("Clear sky");
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_CalledTwice_SecondCallHitsCacheNotHttp()
    {
        var handler = SetupHandler(CurrentWeatherJson);
        var sut = BuildClient(handler, new MemoryCache(new MemoryCacheOptions()));

        await sut.GetCurrentWeatherAsync(47.6062, -122.3321);
        await sut.GetCurrentWeatherAsync(47.6062, -122.3321);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
