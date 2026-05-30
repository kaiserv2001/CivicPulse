namespace CivicPulse.Core.Models;

public record LocationSearchResult(
    string DisplayName,
    string City,
    string Country,
    string? State,
    double Latitude,
    double Longitude,
    string PlaceType
);
