using System.Net;
using System.Text;
using CivicPulse.Infrastructure.ExternalClients;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace CivicPulse.UnitTests.Services;

public class OpenMeteoClientTests
{
    private const string CurrentWeatherJson = """
        {
          "current": {
            "temperature_2m": 15.5,
            "apparent_temperature": 13.0,
            "wind_speed_10m": 20.0,
            "wind_gusts_10m": 28.0,
            "precipitation": 0.0,
            "precipitation_probability": 10.0,
            "uv_index": 3.0,
            "relative_humidity_2m": 55.0,
            "weather_code": 0
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

    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static OpenMeteoClient BuildClient(Mock<HttpMessageHandler> handler, IDistributedCache? cache = null)
    {
        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.open-meteo.com/") };
        return new OpenMeteoClient(http, cache ?? NewCache(), Mock.Of<ILogger<OpenMeteoClient>>());
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
        var sut = BuildClient(handler, NewCache());

        await sut.GetCurrentWeatherAsync(47.6062, -122.3321);
        await sut.GetCurrentWeatherAsync(47.6062, -122.3321);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
