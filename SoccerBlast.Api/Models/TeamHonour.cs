namespace SoccerBlast.Api.Models;

/// <summary>Link between a team (SportsDB id) and an honour. Stored in main DB.</summary>
public class TeamHonour
{
    public string TeamId { get; set; } = ""; // TheSportsDB team id
    public int HonourId { get; set; }

    public Honour Honour { get; set; } = null!;
}
