namespace SoccerBlast.Api.Models;

public class PlayerExternalMap
{
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string Provider { get; set; } = "";

    public string ExternalId { get; set; } = "";

    public DateTime? LastSyncedUtc { get; set; }
}

