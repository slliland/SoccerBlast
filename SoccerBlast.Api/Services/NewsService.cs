using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using SoccerBlast.Shared.Contracts;
using System.Text.RegularExpressions;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace SoccerBlast.Api.Services;

public class NewsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;

    // Pick a few reliable football feeds (you can add/remove anytime)
    // Tip: keep it small (3–6 feeds) to avoid slowness.
    private static readonly (string Source, string Url)[] Feeds =
    [
        ("BBC Sport - Football", "https://feeds.bbci.co.uk/sport/football/rss.xml"),
        ("Sky Sports - Football", "https://www.skysports.com/rss/12040"),
        ("ESPN - Soccer", "https://www.espn.com/espn/rss/soccer/news"),
        ("The Guardian - Football", "https://www.theguardian.com/football/rss"),
        ("UEFA - News", "https://www.uefa.com/rssfeed/news/")
    ];

    public NewsService(HttpClient http, IMemoryCache cache, AppDbContext db)
    {
        _http = http;
        _cache = cache;
        _db = db;
    }

    public async Task ClearCacheAndRefreshAsync()
    {
        // Clear common cache keys
        for (int i = 1; i <= 50; i++)
        {
            _cache.Remove($"news:recent:{i}");
        }
        
        // Force a fresh fetch
        await GetRecentAsync(120);
    }

    public async Task<List<NewsDto>> GetRecentAsync(int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        // Cache the aggregated result for a short time
        return await _cache.GetOrCreateAsync($"news:recent:{limit}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var tasks = Feeds.Select(f => FetchFeedAsync(f.Source, f.Url)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Flatten + dedupe by Url + sort newest first
            var all = results.SelectMany(x => x)
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .GroupBy(x => x.Url)
                .Select(g => g.First())
                .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(limit)
                .ToList();
            await UpsertNewsAsync(all);
            return all;
        }) ?? new List<NewsDto>();
    }

    public async Task<List<NewsDto>> GetRecommendedAsync(List<int> teamIds, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        if (teamIds.Count == 0) return await GetRecentAsync(limit);

        var rows = await _db.NewsItems
            .AsNoTracking()
            .Where(n => n.PublishedAtUtc != null)
            .Join(_db.Set<NewsItemTeam>().Where(x => teamIds.Contains(x.TeamId)),
                n => n.Id,
                nt => nt.NewsItemId,
                (n, nt) => n)
            .Distinct()
            .OrderByDescending(n => n.PublishedAtUtc)
            .Take(limit)
            .ToListAsync();

        return rows.Select(n => new NewsDto
        {
            Title = n.Title,
            Url = n.Url,
            Source = n.Source,
            PublishedAt = n.PublishedAtUtc.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(n.PublishedAtUtc.Value, DateTimeKind.Utc))
                : null,
            ThumbnailUrl = n.ThumbnailUrl,
            Content = n.Content
        }).ToList();
    }

    private async Task<List<NewsDto>> FetchFeedAsync(string source, string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SoccerBlast/1.0");

            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var xml = await resp.Content.ReadAsStringAsync();
            return ParseRssOrAtom(source, xml);
        }
        catch
        {
            // If one feed fails, don’t break the whole page
            return new List<NewsDto>();
        }
    }

    private static List<NewsDto> ParseRssOrAtom(string source, string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root == null) return new List<NewsDto>();

        // RSS
        var channel = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "channel");
        if (channel != null)
        {
            return channel.Elements().Where(e => e.Name.LocalName == "item")
                .Select(item =>
                {
                    var title = item.Elements().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim() ?? "";
                    var link = item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Value?.Trim() ?? "";

                    var pub = TryParseDate(
                        item.Elements().FirstOrDefault(x => x.Name.LocalName == "pubDate")?.Value
                        ?? item.Elements().FirstOrDefault(x => x.Name.LocalName == "date")?.Value
                    );

                    // Try to get content (description or encoded content)
                    var content =
                        item.Elements().FirstOrDefault(x => x.Name.LocalName == "encoded")?.Value?.Trim()
                        ?? item.Elements().FirstOrDefault(x => x.Name.LocalName == "description")?.Value?.Trim()
                        ?? "";

                    // Try multiple places for images
                    var thumb =
                        GetMediaThumbnail(item)
                        ?? GetEnclosureImage(item)
                        ?? GetFirstImageFromHtml(content);

                    if (string.IsNullOrWhiteSpace(thumb) && !string.IsNullOrWhiteSpace(link))
                        thumb = Favicon(link);

                    return new NewsDto
                    {
                        Source = source,
                        Title = title,
                        Url = link,
                        PublishedAt = pub,
                        ThumbnailUrl = thumb,
                        Content = content
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Url))
                .ToList();
        }

        // Atom
        var entries = root.Elements().Where(e => e.Name.LocalName == "entry").ToList();
        if (entries.Count > 0)
        {
            return entries.Select(entry =>
                {
                    var title = entry.Elements().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim() ?? "";
                    var link = entry.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value?.Trim() ?? "";

                    var pub = TryParseDate(
                        entry.Elements().FirstOrDefault(x => x.Name.LocalName == "updated")?.Value
                        ?? entry.Elements().FirstOrDefault(x => x.Name.LocalName == "published")?.Value
                    );

                    // Try to get content (content or summary)
                    var content =
                        entry.Elements().FirstOrDefault(x => x.Name.LocalName == "content")?.Value?.Trim()
                        ?? entry.Elements().FirstOrDefault(x => x.Name.LocalName == "summary")?.Value?.Trim()
                        ?? "";

                    var thumb =
                        GetMediaThumbnail(entry)
                        ?? GetFirstImageFromHtml(content);

                    if (string.IsNullOrWhiteSpace(thumb) && !string.IsNullOrWhiteSpace(link))
                        thumb = Favicon(link);

                    return new NewsDto
                    {
                        Source = source,
                        Title = title,
                        Url = link,
                        PublishedAt = pub,
                        ThumbnailUrl = thumb,
                        Content = content
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Url))
                .ToList();
        }

        return new List<NewsDto>();
    }

    private static string? GetMediaThumbnail(XElement itemOrEntry)
    {
        // media:thumbnail / media:content are namespaced, so match by LocalName.
        // Prefer larger images by checking multiple media elements
        var mediaElements = itemOrEntry.Descendants()
            .Where(x => x.Name.LocalName == "content" || x.Name.LocalName == "thumbnail")
            .ToList();

        // Look for the largest image by checking width attribute
        var bestMedia = mediaElements
            .Select(x => new
            {
                Url = x.Attribute("url")?.Value,
                Width = int.TryParse(x.Attribute("width")?.Value, out var w) ? w : 0,
                Medium = x.Attribute("medium")?.Value
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .OrderByDescending(x => x.Width)
            .FirstOrDefault();

        if (bestMedia?.Url != null && bestMedia.Width > 200)
            return bestMedia.Url;

        // Fallback to first available
        var thumb = itemOrEntry.Descendants().FirstOrDefault(x => x.Name.LocalName == "thumbnail")?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(thumb)) return thumb;

        var content = itemOrEntry.Descendants().FirstOrDefault(x => x.Name.LocalName == "content")?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(content)) return content;

        return null;
    }

    private static string? GetEnclosureImage(XElement item)
    {
        var encl = item.Elements().FirstOrDefault(x => x.Name.LocalName == "enclosure");
        if (encl == null) return null;

        var type = encl.Attribute("type")?.Value ?? "";
        var url = encl.Attribute("url")?.Value;

        if (!string.IsNullOrWhiteSpace(url) && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return url;

        return null;
    }

    private static string? GetFirstImageFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Find all <img> tags and try to pick the largest/best one
        var matches = Regex.Matches(html, "<img[^>]+src=[\"'](?<src>[^\"']+)[\"'](?:[^>]+width=[\"'](?<width>\\d+)[\"'])?", RegexOptions.IgnoreCase);
        
        if (matches.Count == 0) return null;

        var images = matches.Cast<System.Text.RegularExpressions.Match>()
            .Select(m => new
            {
                Src = m.Groups["src"].Value,
                Width = int.TryParse(m.Groups["width"].Value, out var w) ? w : 0
            })
            .Where(img => !string.IsNullOrWhiteSpace(img.Src))
            .Where(img => !img.Src.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .Where(img => !img.Src.Contains("icon", StringComparison.OrdinalIgnoreCase) || img.Width > 100)
            .OrderByDescending(img => img.Width)
            .FirstOrDefault();

        if (images == null) return null;

        var src = images.Src;

        // handle protocol-relative URLs: //cdn...
        if (src.StartsWith("//")) src = "https:" + src;

        return src;
    }

    private static string? Favicon(string articleUrl)
    {
        try
        {
            var u = new Uri(articleUrl);
            // quick consistent fallback thumbnail
            return $"{u.Scheme}://{u.Host}/favicon.ico";
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, out var dto)) return dto;
        return null;
    }

    private async Task UpsertNewsAsync(List<NewsDto> items)
    {
        if (items.Count == 0) return;

        // Normalize + hash urls
        string Norm(string url) => url.Trim();
        string Hash(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Norm(url)));
            return Convert.ToHexString(bytes); // e.g. "A1B2..."
        }

        var hashes = items.Select(n => Hash(n.Url)).Distinct().ToList();

        var existing = await _db.NewsItems
            .Where(x => hashes.Contains(x.UrlHash))
            .ToDictionaryAsync(x => x.UrlHash);

        foreach (var dto in items)
        {
            if (string.IsNullOrWhiteSpace(dto.Url)) continue;
            var h = Hash(dto.Url);
            if (existing.TryGetValue(h, out var row))
            {
                // update
                row.Title = dto.Title ?? "";
                row.Source = dto.Source ?? "";
                row.Url = dto.Url ?? "";
                row.ThumbnailUrl = dto.ThumbnailUrl;
                row.Content = dto.Content;
                row.PublishedAtUtc = dto.PublishedAt?.UtcDateTime;
            }
            else
            {
                _db.NewsItems.Add(new NewsItem
                {
                    Title = dto.Title ?? "",
                    Source = dto.Source ?? "",
                    Url = dto.Url ?? "",
                    ThumbnailUrl = dto.ThumbnailUrl,
                    Content = dto.Content,
                    PublishedAtUtc = dto.PublishedAt?.UtcDateTime,
                    UrlHash = h
                });
            }
        }

        await _db.SaveChangesAsync();
        await UpsertNewsTeamsAsync(items);
    }

    private async Task UpsertNewsTeamsAsync(List<NewsDto> items)
    {
        // Load teams once
        var teams = await _db.Teams
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        if (teams.Count == 0) return;

        // Find the NewsItem rows you just upserted (by UrlHash)
        string Norm(string url) => url.Trim();
        string Hash(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Norm(url)));
            return Convert.ToHexString(bytes);
        }

        var hashes = items.Select(x => Hash(x.Url)).Distinct().ToList();

        var newsRows = await _db.NewsItems
            .Where(n => hashes.Contains(n.UrlHash))
            .ToListAsync();

        // Build desired mappings
        var desired = new List<NewsItemTeam>();

        foreach (var n in newsRows)
        {
            if (string.IsNullOrWhiteSpace(n.Title)) continue;

            // Check both title and full text for team mentions
            var searchText = $"{n.Title} {n.Source}".ToLower();

            foreach (var t in teams)
            {
                var teamName = t.Name.ToLower();
                
                // Check for exact match or common variations
                if (searchText.Contains(teamName, StringComparison.OrdinalIgnoreCase))
                {
                    desired.Add(new NewsItemTeam { NewsItemId = n.Id, TeamId = t.Id });
                    continue;
                }

                // Also check without common suffixes (e.g., "Arsenal" matches "Arsenal FC")
                var coreName = teamName
                    .Replace(" fc", "")
                    .Replace(" afc", "")
                    .Replace(" united", "")
                    .Replace(" city", "")
                    .Replace(" town", "")
                    .Trim();

                if (coreName.Length >= 4 && searchText.Contains(coreName))
                {
                    desired.Add(new NewsItemTeam { NewsItemId = n.Id, TeamId = t.Id });
                }
            }
        }

        if (desired.Count == 0) return;

        // Insert only new pairs (don’t spam duplicates)
        var newsIds = desired.Select(x => x.NewsItemId).Distinct().ToList();

        var existingPairs = await _db.Set<NewsItemTeam>()
            .Where(x => newsIds.Contains(x.NewsItemId))
            .Select(x => new { x.NewsItemId, x.TeamId })
            .ToListAsync();

        var exists = existingPairs.ToHashSet();

        var toAdd = desired
            .Where(x => !exists.Contains(new { x.NewsItemId, x.TeamId }))
            .ToList();

        if (toAdd.Count == 0) return;

        _db.Set<NewsItemTeam>().AddRange(toAdd);
        await _db.SaveChangesAsync();
    }
}
