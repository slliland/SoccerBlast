namespace SoccerBlast.Api.Models;

public class Match
{
    public int Id { get; set; }

    public string Provider { get; set; } = "SportsDbMatches";
    public int ExternalId { get; set; }

    public DateTimeOffset UtcDate { get; set; }
    public string Status { get; set; } = "";

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;
}
