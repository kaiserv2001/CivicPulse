using CivicPulse.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CivicPulse.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<FavoriteLocation> FavoriteLocations => Set<FavoriteLocation>();
    public DbSet<WeatherCache> WeatherCaches => Set<WeatherCache>();
    public DbSet<AirQualityCache> AirQualityCaches => Set<AirQualityCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Location>(b =>
        {
            b.HasKey(l => l.Id);
            b.HasIndex(l => new { l.Latitude, l.Longitude }).IsUnique();
            b.Property(l => l.Name).HasMaxLength(200).IsRequired();
            b.Property(l => l.Country).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<FavoriteLocation>(b =>
        {
            b.HasKey(f => f.Id);
            b.HasIndex(f => new { f.UserId, f.LocationId }).IsUnique();
            b.HasOne(f => f.Location)
             .WithMany(l => l.FavoriteLocations)
             .HasForeignKey(f => f.LocationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeatherCache>(b =>
        {
            b.HasKey(w => w.Id);
            b.HasIndex(w => new { w.LocationId, w.FetchedAt });
            b.HasOne(w => w.Location)
             .WithMany(l => l.WeatherCaches)
             .HasForeignKey(w => w.LocationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AirQualityCache>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.LocationId, a.FetchedAt });
        });
    }
}
