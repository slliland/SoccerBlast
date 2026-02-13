namespace SoccerBlast.Api.Models;

public class SyncLog
{
    public int Id { get; set; }

    // "TODAY" or "DATE"
    public string SyncType { get; set; } = "";

    // The date user requested (America/New_York local date)
    public DateTime LocalDate { get; set; }

    // When the sync was executed (UTC)
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }

    public bool Success { get; set; }

    // How many matches were saved (after filtering)
    public int SyncedMatches { get; set; }

    // Store errors (if any)
    public string? ErrorMessage { get; set; }
}
