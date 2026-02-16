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
    /// Toggle like status for a team and save preferences.
    /// </summary>
    public static async Task ToggleLikeAsync(
        int teamId,
        ICollection<int> followedTeamIds,
        UserPrefsStore prefsStore,
        Func<Task> onStateChanged)
    {
        if (followedTeamIds.Contains(teamId))
            followedTeamIds.Remove(teamId);
        else
            followedTeamIds.Add(teamId);

        var prefs = await prefsStore.GetAsync();
        prefs.FollowedTeamIds = followedTeamIds.ToList();
        await prefsStore.SaveAsync(prefs);
        
        await onStateChanged();
    }
}
