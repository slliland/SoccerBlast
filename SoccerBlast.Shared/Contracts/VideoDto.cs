namespace SoccerBlast.Shared.Contracts;

public enum VideoProvider
{
    YouTube = 1,
    External = 2
}

public sealed record VideoDto
{
    public string Id { get; init; } = "";                 // stable ID
    public string Title { get; init; } = "";
    public VideoProvider Provider { get; init; } = VideoProvider.YouTube;

    public string Url { get; init; } = "";                // watch url
    public string? EmbedUrl { get; init; }                // optional
    public string? ThumbnailUrl { get; init; }

    public DateTimeOffset PublishedAtUtc { get; init; }   // sortable
    public int? DurationSeconds { get; init; }            // optional

    public int? CompetitionId { get; init; }
    public string? CompetitionName { get; init; }

    public int? MatchId { get; init; }                    // nullable for curated
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string? Source { get; init; }                  // channel/site
}
