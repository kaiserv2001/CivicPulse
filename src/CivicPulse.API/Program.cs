using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Services;
using CivicPulse.Infrastructure.BackgroundJobs;
using CivicPulse.Infrastructure.Data;
using CivicPulse.Infrastructure.ExternalClients;
using CivicPulse.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/civicpulse-.log", rollingInterval: RollingInterval.Day));

    // EF Core — ref: https://learn.microsoft.com/ef/core/dbcontext-configuration/
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Caching
    builder.Services.AddMemoryCache();

    // Core services
    builder.Services.AddScoped<ILocationRepository, LocationRepository>();
    builder.Services.AddScoped<IOutdoorScoringService, OutdoorScoringService>();

    // External API HTTP clients
    builder.Services.AddHttpClient<IWeatherService, OpenMeteoClient>(c =>
    {
        c.BaseAddress = new Uri("https://api.open-meteo.com/");
        c.Timeout = TimeSpan.FromSeconds(10);
    });

    builder.Services.AddHttpClient<IAirQualityService, OpenAQClient>(c =>
    {
        c.BaseAddress = new Uri("https://api.openaq.org/");
        c.Timeout = TimeSpan.FromSeconds(15);
    });

    // Nominatim requires a descriptive User-Agent per their usage policy
    builder.Services.AddHttpClient<IGeocodingService, NominatimClient>(c =>
    {
        c.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
        c.DefaultRequestHeaders.Add("User-Agent", "CivicPulse/1.0 (portfolio project; contact@example.com)");
        c.Timeout = TimeSpan.FromSeconds(10);
    });

    // Background jobs
    builder.Services.AddHostedService<WeatherRefreshJob>();

    // FluentValidation — ref: https://docs.fluentvalidation.net/en/latest/di.html
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "CivicPulse API", Version = "v1" });
        c.EnableAnnotations();
    });

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()));

    var app = builder.Build();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.GetLevel = (ctx, _, ex) => ex != null || ctx.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
