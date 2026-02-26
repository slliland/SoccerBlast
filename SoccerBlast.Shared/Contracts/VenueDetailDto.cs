namespace SoccerBlast.Shared.Contracts;

/// <summary>Used by VenuesController (by-id / by-external). Rich data from v2 lookup/venue.</summary>
public class VenueDetailDto
{
    public int Id { get; set; }
    /// <summary>External id (e.g. TheSportsDB venue id) when loaded by external id.</summary>
    public string? ExternalId { get; set; }
    public string Name { get; set; } = "";
    public string? AlternateName { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public int? Capacity { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public string? LogoUrl { get; set; }
    public int? FormedYear { get; set; }
    public string? MapCoordinates { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Cost { get; set; }
    public string? Website { get; set; }
    public string? Timezone { get; set; }
    public List<string> FanartUrls { get; set; } = new();
}

/// <summary>Single event (upcoming or past) at a venue from v2 schedule/next/venue or schedule/previous/venue.</summary>
public class VenueEventDto
{
    public string? IdEvent { get; set; }
    public string? LeagueId { get; set; }
    public string? LeagueName { get; set; }
    public string? HomeTeamId { get; set; }
    public string? HomeTeamName { get; set; }
    public string? AwayTeamId { get; set; }
    public string? AwayTeamName { get; set; }
    public DateTime? UtcDate { get; set; }
    public string? DateEvent { get; set; }
    public string? StrTime { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Status { get; set; }
    public string? HomeTeamBadge { get; set; }
    public string? AwayTeamBadge { get; set; }
}