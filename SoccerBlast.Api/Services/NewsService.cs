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

                    // Try multiple places for images
                    var thumb =
                        GetMediaThumbnail(item)
                        ?? GetEnclosureImage(item)
                        ?? GetFirstImageFromHtml(item.Elements().FirstOrDefault(x => x.Name.LocalName == "description")?.Value)
                        ?? GetFirstImageFromHtml(item.Elements().FirstOrDefault(x => x.Name.LocalName == "encoded")?.Value);

                    if (string.IsNullOrWhiteSpace(thumb) && !string.IsNullOrWhiteSpace(link))
                        thumb = Favicon(link);

                    return new NewsDto
                    {
                        Source = source,
                        Title = title,
                        Url = link,
                        PublishedAt = pub,
                        ThumbnailUrl = thumb
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

                    var thumb =
                        GetMediaThumbnail(entry)
                        ?? GetFirstImageFromHtml(entry.Elements().FirstOrDefault(x => x.Name.LocalName == "content")?.Value);

                    if (string.IsNullOrWhiteSpace(thumb) && !string.IsNullOrWhiteSpace(link))
                        thumb = Favicon(link);

                    return new NewsDto
                    {
                        Source = source,
                        Title = title,
                        Url = link,
                        PublishedAt = pub,
                        ThumbnailUrl = thumb
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

        // very simple <img src="..."> match
        var m = Regex.Match(html, "<img[^>]+src=[\"'](?<src>[^\"']+)[\"']", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var src = m.Groups["src"].Value;
        if (string.IsNullOrWhiteSpace(src)) return null;

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
            var h = Hash(dto.Url);
            if (existing.TryGetValue(h, out var row))
            {
                // update
                row.Title = dto.Title ?? "";
                row.Source = dto.Source ?? "";
                row.Url = dto.Url ?? "";
                row.ThumbnailUrl = dto.ThumbnailUrl;
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
                    PublishedAtUtc = dto.PublishedAt?.UtcDateTime,
                    UrlHash = h
                });
            }
        }

        await _db.SaveChangesAsync();
    }
}
