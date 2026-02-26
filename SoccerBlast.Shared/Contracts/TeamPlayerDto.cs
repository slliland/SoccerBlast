namespace SoccerBlast.Shared.Contracts;

public class TeamPlayerDto
{
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public string? JerseyNumber { get; set; }

    public string? ThumbUrl { get; set; }
    public string? CutoutUrl { get; set; }
    public string? RenderUrl { get; set; }
    public string? CartoonUrl { get; set; }

    public int? Age { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }
    public string? Wage { get; set; }
    public string? DateBorn { get; set; }
    public string? StrSigning { get; set; }

    public string? SportsDbPlayerId { get; set; }
}
