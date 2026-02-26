namespace SoccerBlast.Shared.Contracts;

public class PlayerMilestoneDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Season { get; set; }
    public string? Stat { get; set; }
    /// <summary>Team from API (strTeam).</summary>
    public string? Team { get; set; }
    /// <summary>Milestone logo from API (strMilestoneLogo).</summary>
    public string? MilestoneLogoUrl { get; set; }
    /// <summary>Date of milestone from API (dateMilestone).</summary>
    public string? DateMilestone { get; set; }
    /// <summary>Fallback badge/logo (strBadge, strThumb, strLogo).</summary>
    public string? BadgeUrl { get; set; }
}
