using System.Text;
using System.Threading.RateLimiting;
using CivicPulse.API;
using CivicPulse.API.Services;
using CivicPulse.Core.Entities;
using CivicPulse.Core.Interfaces;
using CivicPulse.Core.Services;
using Microsoft.AspNetCore.Identity;
using CivicPulse.Infrastructure.BackgroundJobs;
using CivicPulse.Infrastructure.Data;
using CivicPulse.Infrastructure.ExternalClients;
using CivicPulse.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

    // EF Core — use InMemory when USE_INMEMORY env var is set (no SQL Server required)
    var useInMemory = builder.Configuration["USE_INMEMORY"] == "1"
                   || Environment.GetEnvironmentVariable("USE_INMEMORY") == "1";
    if (useInMemory)
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("CivicPulse_Dev"));
    else
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Distributed cache — Redis in Docker/production, in-memory for local dev and tests
    if (useInMemory)
        builder.Services.AddDistributedMemoryCache();
    else
        builder.Services.AddStackExchangeRedisCache(opts =>
            opts.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

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
        var aqKey = builder.Configuration["OpenAQ:ApiKey"];
        if (!string.IsNullOrEmpty(aqKey))
            c.DefaultRequestHeaders.Add("X-API-Key", aqKey);
    });

    // Nominatim requires a descriptive User-Agent per their usage policy
    builder.Services.AddHttpClient<IGeocodingService, NominatimClient>(c =>
    {
        c.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
        c.DefaultRequestHeaders.Add("User-Agent", "CivicPulse/1.0 (raynorbuhian9@gmail.com)");
        c.Timeout = TimeSpan.FromSeconds(10);
    });

    // Background jobs
    builder.Services.AddHostedService<WeatherRefreshJob>();

    // FluentValidation — ref: https://docs.fluentvalidation.net/en/latest/di.html
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // JWT authentication
    var jwtKey = builder.Configuration["Jwt:Key"]!;
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddControllers();

    // Blazor Server UI (merged from the former CivicPulse.Web project)
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddScoped<AuthState>();
    builder.Services.AddHttpClient<ApiClient>(c =>
    {
        // The UI calls this same app's API endpoints over HTTP. ApiBaseUrl points
        // back at the host (configured per environment); defaults to the local port.
        c.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:8080");
        c.Timeout = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "CivicPulse API", Version = "v1" });
        c.EnableAnnotations();
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header
        });
        c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
        });
    });

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()));

    // Rate limiting — 60 requests per minute per IP, returns 429 on excess
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (ctx, token) =>
        {
            ctx.HttpContext.Response.Headers.RetryAfter = "60";
            await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded. Retry in 60 seconds.", token);
        };
    });

    var app = builder.Build();

    // Apply EF migrations — retry because SQL Server may not have fully attached volume
    // databases even after the healthcheck passes.
    if (!useInMemory)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                Log.Warning(ex, "Migration attempt {Attempt}/10 failed. Retrying in {Delay}s...",
                    attempt, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }

    // Seed a permanent demo account so recruiters can "Sign in as demo" without
    // registering their own email. Idempotent — safe on every boot, and re-creates
    // the account after an in-memory reset (sleep/restart wipes the in-memory DB).
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Users.AnyAsync(u => u.Email == DemoAccount.Email))
        {
            var demo = new AppUser { Email = DemoAccount.Email };
            demo.PasswordHash = new PasswordHasher<AppUser>().HashPassword(demo, DemoAccount.Password);
            db.Users.Add(demo);
            await db.SaveChangesAsync();
            Log.Information("Seeded demo account {Email}", DemoAccount.Email);
        }
    }

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
    app.UseStaticFiles();
    app.MapStaticAssets();   // .NET 10: serves _framework/blazor.server.js via manifest
    app.UseRouting();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

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
