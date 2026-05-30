namespace CivicPulse.Core.Entities;

public class FavoriteLocation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string? Alias { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    public Location Location { get; set; } = null!;
}
