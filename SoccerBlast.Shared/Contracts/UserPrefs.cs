namespace SoccerBlast.Shared.Contracts;

public sealed class UserPrefs
{
    public string DefaultViewMode { get; set; } = "Cards";
    public bool FollowedTeamsOnly { get; set; } = false;
    public bool PinnedLeaguesFirst { get; set; } = false;
    public bool SmartRefresh { get; set; } = true;

    public List<int> FollowedTeamIds { get; set; } = new();
    public List<int> PinnedCompetitionIds { get; set; } = new();
}
