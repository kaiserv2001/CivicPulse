# CivicPulse — Code Structure & Architecture

## Overview

CivicPulse is split into four projects inside one solution (`CivicPulse.sln`). Each project has a clear responsibility — they don't all know about each other, they only know what they need to. This separation is called **layered architecture**.

```
CivicPulse/
├── src/
│   ├── CivicPulse.Core/           # Business rules — no frameworks, no databases
│   ├── CivicPulse.Infrastructure/ # Data access, external APIs, background jobs
│   ├── CivicPulse.API/            # HTTP endpoints (the backend server)
│   └── CivicPulse.Web/            # Blazor UI (the frontend)
├── tests/
│   ├── CivicPulse.UnitTests/      # Fast isolated tests (no database, no network)
│   └── CivicPulse.IntegrationTests/ # Full request/response tests against a real API
└── docs/
```

---

## Project: CivicPulse.Core

**Rule: this project has zero dependencies on any framework or external library.** It only uses the .NET standard library. This means you could swap out the database, the web framework, or the cache without touching any of this code.

```
Core/
├── Entities/          # Database table shapes (C# classes that map to DB rows)
│   ├── AppUser.cs           — registered user (id, email, password hash)
│   ├── FavoriteLocation.cs  — a user's saved city
│   ├── Location.cs          — a city (name, country, lat/lon)
│   ├── WeatherCache.cs      — cached weather row in the DB
│   ├── AirQualityCache.cs   — cached air quality row in the DB
│   └── PushSubscription.cs  — a browser's push notification subscription
│
├── Models/            # Data shapes that flow through the app (not stored in DB)
│   ├── WeatherData.cs         — current weather reading
│   ├── WeatherForecastDay.cs  — one day in the 7-day forecast
│   ├── AirQualityData.cs      — current air quality reading
│   ├── AqTrendDay.cs          — one day in the 7-day AQI trend
│   ├── OutdoorScore.cs        — calculated score + grade + summary
│   ├── ActivityRecommendation.cs — is walking/cycling etc. suitable today?
│   ├── DashboardResponse.cs   — everything bundled for one location
│   └── LocationSearchResult.cs — a result from the geocoding search
│
├── Interfaces/        # Contracts — "I promise this service can do X"
│   ├── IWeatherService.cs       — fetch current weather + forecast
│   ├── IAirQualityService.cs    — fetch AQ + AQI trend
│   ├── IGeocodingService.cs     — turn a city name into lat/lon
│   ├── ILocationRepository.cs  — read/write locations in the database
│   └── IOutdoorScoringService.cs — calculate outdoor score
│
└── Services/
    └── OutdoorScoringService.cs — the scoring algorithm (pure math, no I/O)
```

### What is an Interface?

An interface is a promise. `IWeatherService` says: "whoever implements me must provide a `GetCurrentWeatherAsync` method." The API project uses `IWeatherService` without knowing whether the data comes from Open-Meteo, a mock, or anywhere else. This is what makes the unit tests work — tests swap in a fake implementation instead of calling the real internet.

### What is a Record?

```csharp
public record WeatherData(double TemperatureCelsius, double WindSpeedKmh, ...);
```

A `record` is a lightweight C# type designed for holding data. Two key properties: it is **immutable** (you can't change its fields after creation) and two records with the same values are considered equal. Models in CivicPulse use records because weather data and scores are just values — they aren't objects with behaviour.

---

## Project: CivicPulse.Infrastructure

This project does the "heavy lifting" — talking to databases, calling external APIs, and running background tasks. It implements the interfaces defined in Core.

```
Infrastructure/
├── Data/
│   └── AppDbContext.cs      — EF Core "table of contents" for the database.
│                              Declares which entities map to which tables,
│                              and sets up indexes and constraints.
│
├── Migrations/              — Auto-generated SQL scripts. Each file is a
│                              snapshot of one change to the DB schema.
│                              Running `MigrateAsync()` at startup applies
│                              any that haven't run yet.
│
├── Repositories/
│   └── LocationRepository.cs — All SQL queries for the Locations table.
│                               Searches by name, looks up by ID.
│
├── ExternalClients/         — HTTP clients that call third-party APIs
│   ├── OpenMeteoClient.cs   — Implements IWeatherService. Calls Open-Meteo
│   │                          for current weather and 7-day forecast.
│   ├── OpenAQClient.cs      — Implements IAirQualityService. Calls OpenAQ
│   │                          for current readings and 7-day AQI trend.
│   └── NominatimClient.cs   — Implements IGeocodingService. Turns a city
│                              name into latitude/longitude.
│
├── Caching/
│   ├── CacheKeys.cs         — Central list of cache key strings so they
│   │                          are consistent everywhere, e.g.
│   │                          "weather:14.59:120.98"
│   └── DistributedCacheExtensions.cs — Helper methods to serialize/
│                                        deserialize objects to/from Redis
│                                        as JSON.
│
└── BackgroundJobs/
    └── WeatherRefreshJob.cs — A background service that runs every 30 min.
                               Refreshes weather/AQ data for all favorited
                               locations and sends push notifications if any
                               location's score drops below 40.
```

### What is EF Core?

Entity Framework Core (EF Core) is an **ORM** — Object-Relational Mapper. Instead of writing SQL by hand, you write C# and EF translates it. For example:

```csharp
// This C# line:
db.Locations.Where(l => l.Name.Contains("Manila")).ToListAsync()

// Becomes this SQL:
SELECT * FROM Locations WHERE Name LIKE '%Manila%'
```

`AppDbContext` is the central class. It has one `DbSet<T>` property per table, and `OnModelCreating` is where we define constraints (unique indexes, foreign keys, max lengths).

### What is a Migration?

A migration is a versioned script that describes one change to the database schema. When you add a new table or column, you run `dotnet ef migrations add <name>` and EF generates the script. On app startup, `MigrateAsync()` runs any unapplied scripts in order. This means the database always matches the code.

### What is a Background Service?

`WeatherRefreshJob` inherits from `BackgroundService`. ASP.NET Core starts it when the API starts and it runs a loop forever (every 30 minutes) in the background, independent of any HTTP request. It uses `IServiceScopeFactory` to create a fresh database connection each cycle because `DbContext` is not thread-safe — you cannot share one instance across background work.

---

## Project: CivicPulse.API

This is the backend HTTP server. It receives requests from the Blazor frontend (or anyone with Swagger) and returns JSON responses.

```
API/
├── Controllers/
│   ├── AuthController.cs        — POST /api/auth/register, /api/auth/login
│   │                              Creates accounts and returns JWT tokens.
│   ├── DashboardController.cs   — GET /api/dashboard/{id}
│   │                              GET /api/dashboard/compare
│   │                              GET /api/dashboard/{id}/aqtrend
│   ├── FavoritesController.cs   — GET/POST/DELETE /api/favorites
│   ├── LocationsController.cs   — GET /api/locations/search
│   ├── ProfileController.cs     — GET/PUT /api/profile
│   ├── PushController.cs        — GET /api/push/vapid-public-key
│   │                              POST/DELETE /api/push/subscribe
│   └── RecommendationsController.cs
│
├── Extensions/                  — Helper methods to keep Program.cs tidy
├── Middleware/                  — Custom request processing (e.g. error handling)
├── Validators/                  — FluentValidation rules for request bodies
├── Program.cs                   — App startup: wires up all services, middleware,
│                                  database, JWT, Redis, rate limiting, CORS
└── appsettings.json             — Configuration: connection strings, JWT secret,
                                   VAPID keys (empty — real values in .env)
```

### What is a Controller?

A controller is a C# class that maps HTTP requests to C# methods. The `[HttpGet("{locationId:int}")]` attribute means "when a GET request arrives at `/api/dashboard/42`, call this method and pass `42` as `locationId`."

### What is JWT?

JWT (JSON Web Token) is how authentication works. When you log in, the API signs a token with a secret key and returns it. Your browser stores it and sends it with every future request (`Authorization: Bearer <token>`). The API checks the signature to confirm the token is genuine — no database lookup needed. Think of it like a concert wristband: the bouncer can verify it's real by looking at it, without calling the box office.

### What is Dependency Injection (DI)?

Notice that `DashboardController` takes `IWeatherService`, `IAirQualityService` etc. in its constructor — it doesn't create them itself. ASP.NET Core has a built-in container that creates the right concrete class and passes it in. This is registered in `Program.cs`:

```csharp
builder.Services.AddHttpClient<IWeatherService, OpenMeteoClient>(...);
```

This means: "whenever something needs an `IWeatherService`, give it an `OpenMeteoClient`." The controller never needs to know which class it actually gets.

### What is Middleware?

Middleware is a chain of steps every HTTP request passes through before reaching a controller, and every response passes through on the way back out. The order in `Program.cs` matters:

```
Request →
  Serilog logging
  → HTTPS redirect
  → CORS (allow cross-origin requests from the Blazor app)
  → Rate limiter (max 60 req/min per IP)
  → Authentication (read and validate the JWT)
  → Authorization (check the user is allowed to call this endpoint)
  → Controller
← Response
```

---

## Project: CivicPulse.Web

The Blazor Server frontend. Blazor lets you write interactive web UIs in C# instead of JavaScript. In "Server" mode, all C# code runs on the server — the browser only receives HTML and thin JavaScript for DOM updates over a SignalR connection (a persistent websocket).

```
Web/
├── Pages/               — Full pages, each mapped to a URL route
│   ├── Index.razor      — Home / city search (@page "/")
│   ├── Dashboard.razor  — Location dashboard (@page "/dashboard/{LocationId:int}")
│   ├── Compare.razor    — Side-by-side compare (@page "/compare")
│   ├── Favorites.razor  — Saved locations (@page "/favorites")
│   ├── Login.razor      — Login + register (@page "/login")
│   ├── Profile.razor    — Account settings (@page "/profile")
│   ├── _Host.cshtml     — The HTML shell page. Loads Bootstrap, Chart.js,
│   │                      and our JS files. Blazor injects itself here.
│   └── _ViewImports.cshtml
│
├── Components/          — Reusable UI blocks used inside pages
│   ├── ScoreGauge.razor       — The circular score display
│   ├── WeatherCard.razor      — Temperature, wind, humidity card
│   ├── AqiBadge.razor         — Air quality card with pollutant breakdown
│   ├── ActivityList.razor     — Walking/cycling suitability list
│   ├── ForecastChart.razor    — 7-day weather chart (uses Chart.js)
│   ├── AqiTrendChart.razor    — 7-day AQI trend chart (uses Chart.js)
│   ├── CitySearchBox.razor    — Autocomplete city search input
│   ├── ScoreBar.razor         — Horizontal score bar (used in Compare)
│   └── PushSubscribeButton.razor — Enable/disable push notifications
│
├── Services/
│   ├── ApiClient.cs     — All HTTP calls to our own API (/api/...).
│   │                      One method per API endpoint.
│   └── AuthState.cs     — Holds the logged-in user's JWT token and email
│                          in memory. Components inject this to check if
│                          the user is logged in.
│
└── wwwroot/             — Static files served directly to the browser
    ├── css/app.css      — Custom styles
    └── js/
        ├── chartInterop.js  — JS functions called by Blazor to render
        │                      Chart.js graphs (Blazor can't touch the
        │                      DOM directly for canvas elements)
        └── pushInterop.js   — JS functions for Web Push: register service
                               worker, subscribe/unsubscribe, get permission
    └── sw.js            — Service worker (runs in browser background).
                           Receives push messages and shows notifications.
```

### What is Blazor Server?

In a normal React or Vue app, the JavaScript runs entirely in the browser. In Blazor Server, the C# code runs on the server and communicates with the browser over a websocket connection. When you click a button, the event travels to the server, the C# handler runs, and only the changed HTML is sent back. The benefit: you write one language (C#) for both UI and logic.

### What is a Razor Component?

A `.razor` file mixes HTML markup with C# code in a `@code { }` block. For example:

```razor
<h1>Score: @score.Total</h1>   ← HTML with a C# variable injected

@code {
    [Parameter] public OutdoorScore score { get; set; }  ← input from parent
}
```

`[Parameter]` means the parent component passes a value in, like a function argument.

### What is JS Interop?

Blazor can call JavaScript functions using `IJSRuntime`. We use this for two things Chart.js (drawing canvases — Blazor can't do this directly) and the Web Push browser API (subscribing to push notifications). Example:

```csharp
await JS.InvokeVoidAsync("chartInterop.createAqiTrendChart", canvasId, labels, values);
```

This calls the `chartInterop.createAqiTrendChart` function defined in `chartInterop.js` and passes it the arguments.

---

## Tests

### UnitTests
Fast tests that run in milliseconds with no network or database. They test one function in isolation. For example, `OutdoorScoringServiceTests` creates a `WeatherData` object by hand and checks that `Calculate()` returns the expected grade. External dependencies like `IWeatherService` are replaced with **mocks** (fake implementations using the Moq library).

### IntegrationTests
These spin up the full API using `WebApplicationFactory` — the real controllers, real DI container, real scoring logic — but with an in-memory database instead of SQL Server, and mocked HTTP clients instead of real Open-Meteo/OpenAQ calls. They test that an HTTP request to `/api/dashboard/1` returns a `200 OK` with the right shape, end-to-end through the real middleware stack.

---

## How the projects reference each other

```
API  ──depends on──▶  Infrastructure  ──depends on──▶  Core
Web  ──depends on──▶  Core
```

- `Core` depends on nothing (just .NET itself)
- `Infrastructure` knows about `Core` (it implements Core's interfaces)
- `API` knows about both (it wires them together in `Program.cs`)
- `Web` only knows about `Core` (it uses the model classes for deserializing API responses)

`Web` does **not** depend on `Infrastructure` or `API`. It only talks to the API over HTTP, like any external client would.
