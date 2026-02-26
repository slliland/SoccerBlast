namespace SoccerBlast.Shared.Contracts;

public class PlayerFormerTeamDto
{
    /// <summary>TheSportsDB idTeam when available; use for link to /team/{TeamId}.</summary>
    public string? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamBadge { get; set; }
    public string? Sport { get; set; }
    public string? YearStart { get; set; }
    public string? YearEnd { get; set; }
    public string? Joined { get; set; }
    public string? Departed { get; set; }
    /// <summary>e.g. Youth, Permanent, Loan. From API or inferred.</summary>
    public string? ContractType { get; set; }
}
