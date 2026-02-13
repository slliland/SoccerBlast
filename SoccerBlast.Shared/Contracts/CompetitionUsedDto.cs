namespace SoccerBlast.Shared.Contracts;

public class CompetitionUsedDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    public int MatchCount { get; set; }
}
