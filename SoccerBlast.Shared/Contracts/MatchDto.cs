namespace SoccerBlast.Shared.Contracts;

public class MatchDto
{
    public int Id { get; set; }
    public DateTimeOffset UtcDate { get; set; }

    public int CompetitionId { get; set; }
    public string CompetitionName { get; set; } = "";
    /// <summary>League badge URL from API (e.g. TheSportsDB). Used for league icons when building leagues from matches.</summary>
    public string? CompetitionBadgeUrl { get; set; }

    public int HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = "";
    public string? HomeTeamCrestUrl { get; set; }

    public int AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = "";
    public string? AwayTeamCrestUrl { get; set; }

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string Status { get; set; } = "";

    /// <summary>TheSportsDB idEvent when available; used for link to /match/{ExternalId}.</summary>
    public string? ExternalId { get; set; }
}
