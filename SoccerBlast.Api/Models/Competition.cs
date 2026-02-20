namespace SoccerBlast.Api.Models;

public class Competition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; } = "";
    /// <summary>League badge/icon URL from TheSportsDB (strLeagueBadge).</summary>
    public string? BadgeUrl { get; set; }
}
