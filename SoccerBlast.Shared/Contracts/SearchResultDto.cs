namespace SoccerBlast.Shared.Contracts;

public enum SearchResultType
{
    Match,
    Team,
    Competition,
    News
}

public sealed class SearchResultDto
{
    public SearchResultType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }

    // For navigation
    public string? Url { get; set; }      // external link (news)
    public int? Id { get; set; }          // optional entity id
    public DateTimeOffset? When { get; set; } // match kickoff / news publish
}
