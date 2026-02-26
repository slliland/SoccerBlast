namespace SoccerBlast.Shared.Contracts;

public class CompetitionDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    public string? BadgeUrl { get; set; }

    /// <summary>Total number of matches we have stored for this competition.</summary>
    public int MatchCount { get; set; }

    /// <summary>Optional rich profile from TheSportsDB v2 lookup/league.</summary>
    public LeagueProfileDto? Profile { get; set; }

    /// <summary>Optional list of teams from TheSportsDB v2 list/teams/{idLeague}.</summary>
    public List<LeagueTeamDto>? Teams { get; set; }

    /// <summary>Optional list of seasons from TheSportsDB v2 list/seasons/{idLeague}.</summary>
    public List<string>? Seasons { get; set; }

    /// <summary>TheSportsDB league id (e.g. 4328). Use for seasons/table/schedule API when present.</summary>
    public string? ExternalId { get; set; }
}

