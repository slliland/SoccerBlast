namespace SoccerBlast.Api.Models;

public class Match
{
    public int Id { get; set; }

    public DateTime UtcDate { get; set; }
    public string Status { get; set; } = "";

    // optional / can be missing
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    // foreign key column
    public int CompetitionId { get; set; }
    // navigation object EF can load
    public Competition Competition { get; set; } = null!;

    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;
}
