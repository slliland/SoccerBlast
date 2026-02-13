namespace SoccerBlast.Shared.Contracts;

public class LeagueDto
{
    public int CompetitionId { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }

    public int MatchCount { get; set; }
    public int LiveCount { get; set; }
}
