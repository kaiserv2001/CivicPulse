using System.Net;
using System.Net.Http.Json;
using CivicPulse.Core.Entities;
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

[Collection("Integration")]
public class DashboardControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DashboardControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> BuildFactory(Action<IServiceCollection>? configure = null)
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

                configure?.Invoke(services);
            });
        });
    }

    private static WeatherData StubWeather() => new(
        22, 21, 10, 15, 0, 0, 3, 50, 0, "Clear sky", DateTime.UtcNow);

    private static AirQualityData StubAq() =>
        new(30, 8, 15, 10, 40, 0.5, "Good", "PM2.5", DateTime.UtcNow);

    private static IReadOnlyList<WeatherForecastDay> StubForecast() =>
        Enumerable.Range(0, 7)
            .Select(i => new WeatherForecastDay(DateTime.Today.AddDays(i), 25, 15, 0, 0, 10, 3, 0))
            .ToList();

    [Fact]
    public async Task GetDashboard_ExistingLocation_Returns200WithScore()
    {
        var weatherMock = new Mock<IWeatherService>();
        var aqMock = new Mock<IAirQualityService>();
        weatherMock.Setup(w => w.GetCurrentWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubWeather());
        weatherMock.Setup(w => w.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubForecast());
        aqMock.Setup(a => a.GetCurrentAirQualityAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubAq());

        await using var factory = BuildFactory(services =>
        {
            services.AddScoped<IWeatherService>(_ => weatherMock.Object);
            services.AddScoped<IAirQualityService>(_ => aqMock.Object);
        });

        int locationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loc = new Location { Name = "Seattle", Country = "US", Latitude = 47.6, Longitude = -122.3 };
            db.Locations.Add(loc);
            await db.SaveChangesAsync();
            locationId = loc.Id;
        }

        var response = await factory.CreateClient().GetAsync($"/api/dashboard/{locationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        body!.Score.Total.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task GetDashboard_NonExistentLocation_Returns404()
    {
        await using var factory = BuildFactory();
        var response = await factory.CreateClient().GetAsync("/api/dashboard/999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Compare_BothLocationsExist_Returns200WithWinner()
    {
        var weatherMock = new Mock<IWeatherService>();
        var aqMock = new Mock<IAirQualityService>();
        weatherMock.Setup(w => w.GetCurrentWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubWeather());
        weatherMock.Setup(w => w.GetForecastAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubForecast());
        aqMock.Setup(a => a.GetCurrentAirQualityAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubAq());

        int loc1Id, loc2Id;
        await using var factory = BuildFactory(services =>
        {
            services.AddScoped<IWeatherService>(_ => weatherMock.Object);
            services.AddScoped<IAirQualityService>(_ => aqMock.Object);
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seattle = new Location { Name = "Seattle", Country = "US", Latitude = 47.6, Longitude = -122.3 };
            var portland = new Location { Name = "Portland", Country = "US", Latitude = 45.5, Longitude = -122.6 };
            db.Locations.AddRange(seattle, portland);
            await db.SaveChangesAsync();
            loc1Id = seattle.Id; loc2Id = portland.Id;
        }

        var response = await factory.CreateClient()
            .GetAsync($"/api/dashboard/compare?loc1={loc1Id}&loc2={loc2Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CompareResponse>();
        body!.Winner.Should().NotBeNullOrEmpty();
    }
}
