namespace SoccerBlast.Shared.Contracts;

public sealed class UserPrefs
{
    public string DefaultViewMode { get; set; } = "Cards";
    public bool FollowedTeamsOnly { get; set; } = false;
    public bool PinnedLeaguesFirst { get; set; } = false;
    public bool SmartRefresh { get; set; } = true;

    public List<int> FollowedTeamIds { get; set; } = new();
    public List<int> PinnedCompetitionIds { get; set; } = new();
    
    // Store team info for UI display (crests, names)
    public Dictionary<int, TeamInfo> FollowedTeamsInfo { get; set; } = new();
}

public sealed class TeamInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? CrestUrl { get; set; }
}
