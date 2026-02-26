using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

/// <summary>
/// Helper service for managing team like/follow functionality across pages.
/// </summary>
public static class TeamLikeHelper
{
    /// <summary>
    /// Check if a team is liked/followed.
    /// </summary>
    public static bool IsLiked(int teamId, ICollection<int> followedTeamIds)
        => followedTeamIds.Contains(teamId);

    /// <summary>
    /// Get the appropriate like icon path based on whether team is liked.
    /// </summary>
    public static string GetLikeIconPath(int teamId, ICollection<int> followedTeamIds)
        => IsLiked(teamId, followedTeamIds) ? "images/like/like.png" : "images/like/unlike.png";

    /// <summary>
    /// Toggle like status for a team and save preferences with team info.
    /// </summary>
    public static async Task ToggleLikeAsync(
        int teamId,
        string teamName,
        string? teamCrestUrl,
        ICollection<int> followedTeamIds,
        UserPrefsStore prefsStore,
        Func<Task> onStateChanged)
    {
        var prefs = await prefsStore.GetAsync();
        
        if (teamId == 0)
            return;

        if (followedTeamIds.Contains(teamId))
        {
            followedTeamIds.Remove(teamId);
            prefs.FollowedTeamsInfo.Remove(teamId);
        }
        else
        {
            followedTeamIds.Add(teamId);
            prefs.FollowedTeamsInfo[teamId] = new TeamInfo 
            { 
                Id = teamId, 
                Name = teamName, 
                CrestUrl = teamCrestUrl 
            };
        }

        prefs.FollowedTeamIds = followedTeamIds.ToList();
        await prefsStore.SaveAsync(prefs);
        
        await onStateChanged();
    }
}
