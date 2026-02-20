namespace SoccerBlast.Api.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? CrestUrl { get; set; }

    /// <summary>SportsDB team id (e.g. "133651"). For soccer teams from match sync this is Id.ToString().</summary>
    public string? SportsDbId { get; set; }
}
