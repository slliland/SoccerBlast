namespace SoccerBlast.Api.Models;

public class VenueExternalMap
{
    public int VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public string Provider { get; set; } = "";

    public string ExternalId { get; set; } = "";

    public DateTime? LastSyncedUtc { get; set; }
}

