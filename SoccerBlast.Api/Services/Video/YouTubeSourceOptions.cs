namespace SoccerBlast.Api.Services.Video;

public sealed class YouTubeSourceOptions
{
    public List<YouTubeChannelSource> Channels { get; set; } = new();
    public int CacheMinutes { get; set; } = 10;
}

public sealed class YouTubeChannelSource
{
    public string Name { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Optional mapping fields if you want:
    public int? CompetitionId { get; set; }
    public string? CompetitionName { get; set; }
}
