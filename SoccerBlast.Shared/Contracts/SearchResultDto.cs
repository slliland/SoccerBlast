namespace SoccerBlast.Shared.Contracts;

public enum SearchResultType
{
    Match,
    Team,
    Competition,
    News,
    Player,
    Venue
}

public sealed class SearchResultDto
{
    public SearchResultType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }

    // For navigation
    public string? Url { get; set; }      // external link (news) or /team/sportsdb/{id}
    public int? Id { get; set; }          // optional entity id (local team id)
    /// <summary>When set, this team result is from TheSportsDB v2; use Url to open by external id.</summary>
    public string? SportsDbTeamId { get; set; }
    public DateTimeOffset? When { get; set; } // match kickoff / news publish
}
