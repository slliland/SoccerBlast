namespace SoccerBlast.Api.Models;

public class NewsItem
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";

    public DateTime? PublishedAtUtc { get; set; }

    public string? ThumbnailUrl { get; set; }
    public string UrlHash { get; set; } = "";
}
