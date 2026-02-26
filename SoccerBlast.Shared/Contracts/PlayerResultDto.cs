namespace SoccerBlast.Shared.Contracts;

public class PlayerResultDto
{
    public string? EventName { get; set; }
    public string? League { get; set; }
    public string? Season { get; set; }
    public string? DateEvent { get; set; }
    public string? HomeTeam { get; set; }
    public string? AwayTeam { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Result { get; set; }
}
