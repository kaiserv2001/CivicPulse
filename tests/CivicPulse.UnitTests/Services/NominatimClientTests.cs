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

public class NominatimClientTests
{
    // JSON keys use camelCase to match System.Text.Json's web defaults (PropertyNameCaseInsensitive=true)
    private const string SearchJson = """
        [
          {
            "lat": "47.6062",
            "lon": "-122.3321",
            "displayName": "Seattle, King County, Washington, United States",
            "type": "city",
            "address": {
              "city": "Seattle",
              "state": "Washington",
              "country": "United States"
            }
          }
        ]
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

    private static NominatimClient BuildClient(Mock<HttpMessageHandler> handler, IMemoryCache? cache = null)
    {
        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://nominatim.openstreetmap.org/") };
        http.DefaultRequestHeaders.Add("User-Agent", "CivicPulse/1.0 (test)");
        return new NominatimClient(http, cache ?? new MemoryCache(new MemoryCacheOptions()), Mock.Of<ILogger<NominatimClient>>());
    }

    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsMappedResults()
    {
        var handler = SetupHandler(SearchJson);
        var sut = BuildClient(handler);

        var results = await sut.SearchAsync("Seattle");

        results.Should().HaveCount(1);
        results[0].DisplayName.Should().Be("Seattle, King County, Washington, United States");
        results[0].Latitude.Should().Be(47.6062);
        results[0].Longitude.Should().Be(-122.3321);
        results[0].City.Should().Be("Seattle");
        results[0].Country.Should().Be("United States");
    }

    [Fact]
    public async Task SearchAsync_CalledTwiceWithSameQuery_SecondCallServedFromCache()
    {
        var handler = SetupHandler(SearchJson);
        var sut = BuildClient(handler, new MemoryCache(new MemoryCacheOptions()));

        await sut.SearchAsync("Seattle");
        await sut.SearchAsync("Seattle");

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
