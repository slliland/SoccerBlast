namespace SoccerBlast.Shared.Contracts;

public class TeamPlayerDto
{
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public string? ThumbUrl { get; set; }
}
