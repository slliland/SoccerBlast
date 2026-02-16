namespace SoccerBlast.Web.Services;

/// <summary>
/// Shared helper methods for displaying match information across pages.
/// </summary>
public static class MatchDisplayHelper
{
    /// <summary>
    /// Format a score value, showing "-" for null scores.
    /// </summary>
    public static string FormatScore(int? score) 
        => score.HasValue ? score.Value.ToString() : "-";

    /// <summary>
    /// Check if a match status indicates a live/ongoing match.
    /// </summary>
    public static bool IsLiveStatus(string? status) 
        => status is "IN_PLAY" or "PAUSED" or "LIVE";

    /// <summary>
    /// Format a relative time string (e.g., "5 min ago", "2 hours ago").
    /// </summary>
    public static string FormatRelativeTime(DateTimeOffset? timestamp)
    {
        if (timestamp == null) return "recent";
        var delta = DateTimeOffset.UtcNow - timestamp.Value.ToUniversalTime();

        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalHours < 1) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalDays < 1) return $"{(int)delta.TotalHours} hours ago";
        return $"{(int)delta.TotalDays} days ago";
    }
}
