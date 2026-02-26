namespace SoccerBlast.Shared.Contracts;

/// <summary>League name plus optional badge URL and optional competition id for linking to /competition/{Id}.</summary>
public record LeagueEntryDto(string Name, string? BadgeUrl, int? Id = null);

public class TeamProfileDto
{
    public int TeamId { get; set; }

    // Basic Info
    public int? FormedYear { get; set; }
    public string? Location { get; set; }
    public string? Keywords { get; set; }

    // Stadium
    public string? StadiumName { get; set; }
    public int? StadiumCapacity { get; set; }
    public string? StadiumLocation { get; set; }

    // Leagues
    public string? Leagues { get; set; }
    /// <summary>Same leagues as Leagues but with BadgeUrl when we have a matching Competition. Use for icons.</summary>
    public List<LeagueEntryDto>? LeaguesWithBadges { get; set; }

    // Description
    public string? DescriptionEn { get; set; }

    // Media
    public string? BannerUrl { get; set; }
    /// <summary>v2 strFanart1–4: gallery images for team page.</summary>
    public List<string>? FanartUrls { get; set; }
    public string? JerseyUrl { get; set; }
    public string? BadgeUrl { get; set; }
    public string? LogoUrl { get; set; }

    // Colors
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? TertiaryColor { get; set; }

    // Social Media
    public string? Website { get; set; }
    public string? Facebook { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Youtube { get; set; }

    public DateTime? LastUpdatedUtc { get; set; }
}
