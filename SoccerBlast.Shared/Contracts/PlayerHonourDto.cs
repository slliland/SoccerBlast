namespace SoccerBlast.Shared.Contracts;

public class PlayerHonourDto
{
    public string? Title { get; set; }
    public string? Competition { get; set; }
    public string? Season { get; set; }
    public string? Honour { get; set; }
    /// <summary>Honour/competition logo from API (e.g. strHonourLogo, strHonourLogos).</summary>
    public string? HonourLogoUrl { get; set; }
    /// <summary>Trophy/badge image from API (e.g. strTrophy, strHonourTrophy).</summary>
    public string? TrophyUrl { get; set; }
}
