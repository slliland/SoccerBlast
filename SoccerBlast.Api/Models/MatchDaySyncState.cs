namespace SoccerBlast.Api.Models;

public sealed class MatchDaySyncState
{
    public DateOnly LocalDate { get; set; }
    public DateTime LastSyncedUtc { get; set; }
    public int? LastSyncedCount { get; set; }
    public string? Provider { get; set; }
}

