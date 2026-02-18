namespace SoccerBlast.Api.Models;

public class CompetitionExternalMap
{
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;

    // e.g. "SportsDB"
    public string Provider { get; set; } = "";

    // e.g. TheSportsDB idLeague
    public string ExternalId { get; set; } = "";

    public DateTime? LastSyncedUtc { get; set; }
}