namespace CivicPulse.Core.Entities;

public class AirQualityCache
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public double Aqi { get; set; }
    public double Pm25 { get; set; }
    public double Pm10 { get; set; }
    public double No2 { get; set; }
    public double O3 { get; set; }
    public string? DominantPollutant { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public Location Location { get; set; } = null!;
}
