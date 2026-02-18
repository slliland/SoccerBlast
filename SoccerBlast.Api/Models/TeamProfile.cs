namespace SoccerBlast.Api.Models;

public class TeamProfile
{
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    // Basic Info
    public int? FormedYear { get; set; }
    public string? Location { get; set; }
    public string? Keywords { get; set; }

    // Stadium
    public string? StadiumName { get; set; }
    public int? StadiumCapacity { get; set; }
    public string? StadiumLocation { get; set; }

    // Leagues (stored as comma-separated)
    public string? Leagues { get; set; }

    // Description
    public string? DescriptionEn { get; set; }

    // Media
    public string? BannerUrl { get; set; }
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