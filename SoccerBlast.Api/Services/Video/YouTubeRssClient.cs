using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Options;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services.Video;

public sealed class YouTubeRssClient
{
    private readonly IHttpClientFactory _httpFactory;

    public YouTubeRssClient(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<List<VideoDto>> FetchChannelAsync(
        YouTubeChannelSource src,
        CancellationToken ct = default)
    {
        // YouTube RSS feed pattern:
        // https://www.youtube.com/feeds/videos.xml?channel_id=CHANNEL_ID
       var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(src.ChannelId)}";
       var http = _httpFactory.CreateClient("youtube-rss");

        HttpResponseMessage resp;
        try
        {
            resp = await http.GetAsync(feedUrl, ct);
        }
        catch
        {
            // network problems -> skip this source
            return new List<VideoDto>();
        }

        // IMPORTANT: don’t throw; skip bad sources
        if (!resp.IsSuccessStatusCode)
            return new List<VideoDto>();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);

        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore
        });

        var feed = SyndicationFeed.Load(reader);
        if (feed == null) return new List<VideoDto>();


        var list = new List<VideoDto>();

        foreach (var item in feed.Items)
        {
            // item.Id is often like: "yt:video:VIDEO_ID"
            var videoId = TryExtractYouTubeVideoId(item);
            if (string.IsNullOrWhiteSpace(videoId))
                continue;

            var published = item.PublishDate != default
                ? item.PublishDate
                : (item.LastUpdatedTime != default ? item.LastUpdatedTime : DateTimeOffset.UtcNow);

            var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
            var embedUrl = $"https://www.youtube.com/embed/{videoId}";

            // Thumbnail is typically available via media extensions, but we can derive a reliable one:
            // https://i.ytimg.com/vi/VIDEO_ID/hqdefault.jpg
            var thumbUrl = $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";

            list.Add(new VideoDto
            {
                Id = $"yt:{videoId}",
                Title = item.Title?.Text ?? "(untitled)",
                Provider = VideoProvider.YouTube,
                Url = watchUrl,
                EmbedUrl = embedUrl,
                ThumbnailUrl = thumbUrl,
                PublishedAtUtc = published.ToUniversalTime(),
                DurationSeconds = null, // RSS doesn’t include duration reliably
                CompetitionId = src.CompetitionId,
                CompetitionName = src.CompetitionName,
                MatchId = null,
                Tags = src.Tags ?? Array.Empty<string>(),
                Source = src.Name
            });
        }

        return list;
    }

    private static string? TryExtractYouTubeVideoId(SyndicationItem item)
    {
        // 1) Prefer item.Id "yt:video:XXXX"
        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            var id = item.Id;
            var idx = id.LastIndexOf(':');
            if (idx >= 0 && idx + 1 < id.Length)
                return id[(idx + 1)..];
        }

        // 2) Fallback: look at links
        var link = item.Links?.FirstOrDefault()?.Uri?.ToString();
        if (!string.IsNullOrWhiteSpace(link))
        {
            // common: https://www.youtube.com/watch?v=XXXX
            var uri = new Uri(link);
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var v = q["v"];
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        return null;
    }
}
