namespace SoccerBlast.Shared.Contracts;

public class PlayerDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BannerUrl { get; set; }

    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    public int? CurrentTeamId { get; set; }
    public string? CurrentTeamName { get; set; }

    public string? Description { get; set; }
    public string? SeasonStats { get; set; }

    /// <summary>Active, Retired, etc.</summary>
    public string? Status { get; set; }
    public string? Gender { get; set; }
    public string? Side { get; set; }
    public string? College { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }
    public string? PosterUrl { get; set; }
    public string? Agent { get; set; }
    public string? BirthLocation { get; set; }
    public string? Ethnicity { get; set; }
    public string? Wage { get; set; }

    /// <summary>Thumb / cutout / cartoon for media featured area.</summary>
    public string? CutoutUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public string? CartoonUrl { get; set; }
    public string? RenderUrl { get; set; }
    public string? KitUrl { get; set; }
    public List<string>? FanartUrls { get; set; }
}

