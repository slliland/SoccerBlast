namespace SoccerBlast.Api.Models;

/// <summary>Honour/trophy from ScrapeTeamHonours ETL (id_honour). Stored in main DB; script writes to cache, import syncs cache → DB.</summary>
public class Honour
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string? Title { get; set; }
    public string? TrophyImageUrl { get; set; }
    public string HonourUrl { get; set; } = "";
    public string? TypeGuess { get; set; }

    public ICollection<TeamHonour> TeamHonours { get; set; } = new List<TeamHonour>();
    public ICollection<HonourWinner> HonourWinners { get; set; } = new List<HonourWinner>();
}
