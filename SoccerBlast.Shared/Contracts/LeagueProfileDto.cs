namespace SoccerBlast.Shared.Contracts;

public class LeagueProfileDto
{
    public string? Sport { get; set; }
    public string? Gender { get; set; }
    public int? FormedYear { get; set; }
    public string? Country { get; set; }

    public DateTime? FirstEventDateUtc { get; set; }
    public string? CurrentSeason { get; set; }
    public int? CurrentRound { get; set; }

    public string? DescriptionEn { get; set; }

    public string? BannerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrophyUrl { get; set; }
    public List<string>? FanartUrls { get; set; }

    public string? TvRights { get; set; }

    public string? Website { get; set; }
    public string? Facebook { get; set; }
    public string? Twitter { get; set; }
    public string? Instagram { get; set; }
    public string? Youtube { get; set; }
}

