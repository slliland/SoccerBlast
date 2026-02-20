namespace SoccerBlast.Shared.Contracts;

public class PlayerDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PhotoUrl { get; set; }

    public int? CurrentTeamId { get; set; }
    public string? CurrentTeamName { get; set; }
}

