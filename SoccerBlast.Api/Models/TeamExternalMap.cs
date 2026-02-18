namespace SoccerBlast.Api.Models;

public class TeamExternalMap
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    // e.g. "SportsDB"
    public string Provider { get; set; } = "";

    // e.g. TheSportsDB idTeam
    public string ExternalId { get; set; } = "";

    public DateTime? LastSyncedUtc { get; set; }
}
