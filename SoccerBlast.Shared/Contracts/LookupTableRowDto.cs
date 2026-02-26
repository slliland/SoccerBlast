namespace SoccerBlast.Shared.Contracts;

/// <summary>One row from league standings (v1 lookuptable.php).</summary>
public class LookupTableRowDto
{
    public int Rank { get; set; }
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
    public int Played { get; set; }
    public int Win { get; set; }
    public int Draw { get; set; }
    public int Loss { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
    public string? Form { get; set; }
}
