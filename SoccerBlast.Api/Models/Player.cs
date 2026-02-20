namespace SoccerBlast.Api.Models;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public string? PhotoUrl { get; set; }

    public int? CurrentTeamId { get; set; }
    public Team? CurrentTeam { get; set; }
}

