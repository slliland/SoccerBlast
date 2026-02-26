namespace SoccerBlast.Shared.Contracts;

/// <summary>League analysis payload derived from LeagueSeasonStandings (champions, titles, trends).</summary>
public class LeagueAnalysisDto
{
    public List<ChampionTimelineItemDto> ChampionTimeline { get; set; } = new();
    public List<TitlesByClubDto> TitlesByClub { get; set; } = new();
    public List<TopNByClubDto> Top4ByClub { get; set; } = new();
    public List<PointsByClubSeasonDto> PointsByClubBySeason { get; set; } = new();
    public List<LeagueGoalsBySeasonDto> LeagueGoalsBySeason { get; set; } = new();
    public List<string> Seasons { get; set; } = new();
}

public class ChampionTimelineItemDto
{
    public string Season { get; set; } = "";
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
}

public class TitlesByClubDto
{
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
    public int Titles { get; set; }
}

public class TopNByClubDto
{
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
    public int Top4Count { get; set; }
}

public class PointsByClubSeasonDto
{
    public string TeamId { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string? TeamBadgeUrl { get; set; }
    public List<SeasonPointsDto> SeasonPoints { get; set; } = new();
}

public class SeasonPointsDto
{
    public string Season { get; set; } = "";
    public int Points { get; set; }
}

public class LeagueGoalsBySeasonDto
{
    public string Season { get; set; } = "";
    public int TotalGoals { get; set; }
    public int TeamCount { get; set; }
}
