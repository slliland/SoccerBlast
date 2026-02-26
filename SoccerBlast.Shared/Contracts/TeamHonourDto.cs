namespace SoccerBlast.Shared.Contracts;

public class TeamHonourDto
{
    public int IdHonour { get; set; }
    public string Slug { get; set; } = "";
    public string? Title { get; set; }
    public string? TrophyImageUrl { get; set; }
    public string HonourUrl { get; set; } = "";
    public string? TypeGuess { get; set; }
    public int Wins { get; set; }
    public List<string>? WinnerYears { get; set; }
}
