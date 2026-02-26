namespace SoccerBlast.Shared.Contracts;

/// <summary>Enriched venue/stadium for team Stadium tab (lookup/venue with fanart, map, etc.).</summary>
public class StadiumDetailDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ThumbUrl { get; set; }
    public List<string> FanartUrls { get; set; } = new();
    public int? FormedYear { get; set; }
    public string? MapCoordinates { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public string? Location { get; set; }
    public string? Country { get; set; }
    public string? Cost { get; set; }
}
