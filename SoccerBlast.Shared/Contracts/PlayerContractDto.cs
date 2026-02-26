namespace SoccerBlast.Shared.Contracts;

public class PlayerContractDto
{
    /// <summary>TheSportsDB idTeam when available; use for link to /team/{TeamId}.</summary>
    public string? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamBadge { get; set; }
    public string? Sport { get; set; }
    public string? YearStart { get; set; }
    public string? YearEnd { get; set; }
    public string? Wage { get; set; }
    public string? Signing { get; set; }
}
