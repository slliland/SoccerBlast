namespace SoccerBlast.Api.Models;

/// <summary>One winner row: team X won honour Y in year Z. Stored in main DB for "winner years" list.</summary>
public class HonourWinner
{
    public int HonourId { get; set; }
    public string YearLabel { get; set; } = "";
    public string TeamId { get; set; } = "";
    public string? TeamName { get; set; }
    public string? TeamBadgeUrl { get; set; }

    public Honour Honour { get; set; } = null!;
}
