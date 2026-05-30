using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CivicPulse.API.Controllers;
using CivicPulse.Core.Interfaces;
using CivicPulse.Infrastructure.BackgroundJobs;
using CivicPulse.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace CivicPulse.IntegrationTests.Controllers;

[Collection("Integration")]
public class FavoritesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Must match appsettings.json Jwt section
    private const string JwtKey = "CHANGE_THIS_IN_PRODUCTION_TO_A_32_CHAR_SECRET";
    private const string JwtIssuer = "CivicPulse";
    private const string JwtAudience = "CivicPulseClient";

    public FavoritesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> BuildFactory()
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

                var geocodingMock = new Mock<IGeocodingService>();
                services.AddScoped<IGeocodingService>(_ => geocodingMock.Object);
            });
        });
    }

    private static string GenerateJwt(string userId = "42")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient AuthClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateJwt());
        return client;
    }

    private static AddFavoriteRequest SeattleRequest() =>
        new("Seattle", "United States", "Washington", 47.6062, -122.3321, null);

    [Fact]
    public async Task AddFavorite_NewLocation_Returns201()
    {
        await using var factory = BuildFactory();
        var response = await AuthClient(factory).PostAsJsonAsync("/api/favorites", SeattleRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Seattle");
    }

    [Fact]
    public async Task AddFavorite_Duplicate_Returns409()
    {
        await using var factory = BuildFactory();
        var client = AuthClient(factory);

        await client.PostAsJsonAsync("/api/favorites", SeattleRequest());
        var duplicate = await client.PostAsJsonAsync("/api/favorites", SeattleRequest());

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteFavorite_OwnedFavorite_Returns204AndRowGone()
    {
        await using var factory = BuildFactory();
        var client = AuthClient(factory);

        var addResponse = await client.PostAsJsonAsync("/api/favorites", SeattleRequest());
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<FavoriteCreatedDto>();

        var deleteResponse = await client.DeleteAsync($"/api/favorites/{added!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exists = await db.FavoriteLocations.AnyAsync(f => f.Id == added.Id);
        exists.Should().BeFalse();
    }

    private record FavoriteCreatedDto(int Id, string Name);
}
