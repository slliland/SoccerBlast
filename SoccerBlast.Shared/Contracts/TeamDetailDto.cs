namespace SoccerBlast.Shared.Contracts;

public class TeamDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? CrestUrl { get; set; }

    public string? SportsDbTeamId { get; set; }

    public TeamProfileDto? Profile { get; set; }
}
