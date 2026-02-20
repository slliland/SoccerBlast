namespace SoccerBlast.Shared.Contracts;

public class LeagueDto
{
    public int CompetitionId { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    /// <summary>League badge/icon URL from API (e.g. TheSportsDB). Use as fallback before local LeagueIconMap.</summary>
    public string? BadgeUrl { get; set; }

    public int MatchCount { get; set; }
    public int LiveCount { get; set; }
}
