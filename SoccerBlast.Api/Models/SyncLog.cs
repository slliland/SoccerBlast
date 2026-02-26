namespace SoccerBlast.Api.Models;

public class SyncLog
{
    public int Id { get; set; }

    public string SyncType { get; set; } = "";
    public DateOnly LocalDate { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }

    public bool Success { get; set; }
    public int SyncedMatches { get; set; }
    public string? ErrorMessage { get; set; }
}
