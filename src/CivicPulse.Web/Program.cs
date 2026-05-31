using CivicPulse.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<AuthState>();
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000");
    c.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

app.UseStaticFiles();
app.MapStaticAssets();   // .NET 10: serves _framework/blazor.server.js via manifest
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
