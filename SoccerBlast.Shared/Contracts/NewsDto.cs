namespace SoccerBlast.Shared.Contracts;

public class NewsDto
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public string? ThumbnailUrl { get; set; }
}
