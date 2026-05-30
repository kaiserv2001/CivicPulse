namespace CivicPulse.Core.Entities;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FavoriteLocation> FavoriteLocations { get; set; } = new List<FavoriteLocation>();
    public ICollection<WeatherCache> WeatherCaches { get; set; } = new List<WeatherCache>();
}
