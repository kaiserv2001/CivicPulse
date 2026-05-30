namespace CivicPulse.Core.Entities;

public class WeatherCache
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public double TemperatureCelsius { get; set; }
    public double WindSpeedKmh { get; set; }
    public double PrecipitationMm { get; set; }
    public int WeatherCode { get; set; }
    public double UvIndex { get; set; }
    public double RelativeHumidity { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public Location Location { get; set; } = null!;
}
