using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services.Video;

public sealed class VideoAggregator
{
    private readonly IMemoryCache _cache;
    private readonly YouTubeRssClient _yt;
    private readonly IOptions<YouTubeSourceOptions> _ytOptions;

    public VideoAggregator(IMemoryCache cache, YouTubeRssClient yt, IOptions<YouTubeSourceOptions> ytOptions)
    {
        _cache = cache;
        _yt = yt;
        _ytOptions = ytOptions;
    }

    public async Task<IReadOnlyList<VideoDto>> GetRecentAsync(
        int limit,
        int? competitionId,
        string? tag,
        string? q,
        string? source,
        DateTimeOffset? sinceUtc,
        CancellationToken ct = default)
    {
        var all = await GetAllCachedAsync(ct);

        IEnumerable<VideoDto> filtered = all;

        if (competitionId is not null)
            filtered = filtered.Where(v => v.CompetitionId == competitionId);

        if (!string.IsNullOrWhiteSpace(source))
        {
            source = source.Trim();
            filtered = filtered.Where(v =>
                !string.IsNullOrWhiteSpace(v.Source) &&
                v.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            tag = tag.Trim();
            filtered = filtered.Where(v => v.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
        }

        if (sinceUtc is not null)
            filtered = filtered.Where(v => v.PublishedAtUtc >= sinceUtc.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            filtered = filtered.Where(v =>
                v.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (v.Source?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                v.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered
            .OrderByDescending(v => v.PublishedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }


    private async Task<List<VideoDto>> GetAllCachedAsync(CancellationToken ct)
    {
        // Cache key could incorporate config hash later; for MVP keep it simple.
        const string cacheKey = "videos:recent:all";

        if (_cache.TryGetValue(cacheKey, out List<VideoDto>? cached) && cached is not null)
            return cached;

        var opts = _ytOptions.Value;
        var tasks = opts.Channels.Select(c => _yt.FetchChannelAsync(c, ct)).ToList();

        var results = await Task.WhenAll(tasks);
        var merged = results.SelectMany(x => x).ToList();

        // de-dupe by Id
        merged = merged
            .GroupBy(v => v.Id)
            .Select(g => g.OrderByDescending(x => x.PublishedAtUtc).First())
            .OrderByDescending(v => v.PublishedAtUtc)
            .ToList();

        _cache.Set(cacheKey, merged, TimeSpan.FromMinutes(Math.Max(1, opts.CacheMinutes)));

        return merged;
    }
}
