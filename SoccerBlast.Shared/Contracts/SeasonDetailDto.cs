namespace SoccerBlast.Shared.Contracts;

/// <summary>Per-season info from TheSportsDB v2 list/seasons (strSeason, optional strBadge, strPoster, strDescriptionEN).</summary>
public class SeasonDetailDto
{
    public string StrSeason { get; set; } = "";
    public string? StrBadge { get; set; }
    public string? StrPoster { get; set; }
    public string? StrDescriptionEN { get; set; }
}
