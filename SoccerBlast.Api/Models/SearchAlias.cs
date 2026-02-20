namespace SoccerBlast.Api.Models;

public enum AliasType
{
    Team = 1,
    Player = 2,
    League = 3,
    Venue = 4
}

public class SearchAlias
{
    public int Id { get; set; }

    public AliasType Type { get; set; }

    public string Canonical { get; set; } = "";

    public string Alias { get; set; } = "";

    // normalized alias used for searching/prefix filtering
    public string AliasNorm { get; set; } = "";

    public string? ExternalId { get; set; }

    public int HitCount { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
