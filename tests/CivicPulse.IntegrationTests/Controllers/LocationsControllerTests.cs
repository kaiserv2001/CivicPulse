using System.Net;
using System.Net.Http.Json;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Models;
using CivicPulse.Infrastructure.BackgroundJobs;
using CivicPulse.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CivicPulse.IntegrationTests.Controllers;

// All integration test classes share one collection so factories are created sequentially,
// avoiding concurrent races on Serilog's static Log.Logger.
[Collection("Integration")]
public class LocationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LocationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> BuildFactory(Mock<IGeocodingService>? geocodingMock = null)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the SqlServer options configuration — EF Core 9 registers
                // IDbContextOptionsConfiguration<T> via AddSingleton (not TryAdd), so if we
                // just add InMemory on top the options end up with both providers and throw.
                var optionConfigs = services
                    .Where(d => d.ServiceType.IsGenericType
                             && d.ServiceType.GetGenericTypeDefinition().Name
                                    .StartsWith("IDbContextOptionsConfiguration")
                             && d.ServiceType.GenericTypeArguments.Length == 1
                             && d.ServiceType.GenericTypeArguments[0] == typeof(AppDbContext))
                    .ToList();
                foreach (var d in optionConfigs) services.Remove(d);

                var dbOpts = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbOpts is not null) services.Remove(dbOpts);

                // Remove the background job so it doesn't access the DB on startup
                var job = services.SingleOrDefault(d => d.ImplementationType == typeof(WeatherRefreshJob));
                if (job is not null) services.Remove(job);

                var dbName = $"TestDb_{Guid.NewGuid()}";
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

                if (geocodingMock is not null)
                    services.AddScoped<IGeocodingService>(_ => geocodingMock.Object);
            });
        });
    }

    [Fact]
    public async Task Search_ValidQuery_Returns200WithResults()
    {
        var expected = new List<LocationSearchResult>
        {
            new("Seattle, WA, USA", "Seattle", "United States", "Washington", 47.6062, -122.3321, "city")
        };
        var geocodingMock = new Mock<IGeocodingService>();
        geocodingMock
            .Setup(g => g.SearchAsync("seattle", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        await using var factory = BuildFactory(geocodingMock);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/locations/search?query=seattle");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<LocationSearchResult>>();
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Search_SingleCharQuery_Returns400()
    {
        await using var factory = BuildFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/locations/search?query=x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("2 characters");
    }
}
