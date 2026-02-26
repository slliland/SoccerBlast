namespace SoccerBlast.Api.Models;

/// <summary>One row in a league standings table. Populated by ScrapeLeagueTables script; API serves from this when available.</summary>
public class LeagueSeasonStanding
{
    public int Id { get; set; }
    public string LeagueId { get; set; } = "";
    public string Season { get; set; } = "";
    public int Rank { get; set; }
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
    public string? Form { get; set; }
    public int Played { get; set; }
    public int Win { get; set; }
    public int Draw { get; set; }
    public int Loss { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
    public string? StrDescription { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime ScrapedAtUtc { get; set; }
}
