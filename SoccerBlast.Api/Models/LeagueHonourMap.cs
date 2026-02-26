namespace SoccerBlast.Api.Models;

/// <summary>Maps a league (TheSportsDB id) to honour(s) that represent its title. Used to serve past champions from HonourWinners.</summary>
public class LeagueHonourMap
{
    public string LeagueId { get; set; } = "";
    public int HonourId { get; set; }

    public Honour Honour { get; set; } = null!;
}
