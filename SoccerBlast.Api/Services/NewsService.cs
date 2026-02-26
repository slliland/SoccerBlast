using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SoccerBlast.Shared.Contracts;
using System.Text.RegularExpressions;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace SoccerBlast.Api.Services;

/// <summary>Precomputed team fields for cheap contains-check in news tagging (avoids repeated ToLower/Replace per article).</summary>
internal sealed record TeamMatchKey(int Id, string NameLower, string CoreLower);

public class NewsService
{
    private static readonly SemaphoreSlim _newsRefreshLock = new(1, 1);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _db;
    private readonly ILogger<NewsService> _logger;

    private static string CoreLowerFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var lower = name.Trim().ToLowerInvariant();
        return lower
            .Replace(" fc", "")
            .Replace(" afc", "")
            .Replace(" united", "")
            .Replace(" city", "")
            .Replace(" town", "")
            .Trim();
    }

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

    public NewsService(HttpClient http, IMemoryCache cache, AppDbContext db, ILogger<NewsService> logger)
    {
        _http = http;
        _cache = cache;
        _db = db;
        _logger = logger;
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

        // Cache the aggregated result for a short time; only one refresh (fetch + upsert) at a time
        return await _cache.GetOrCreateAsync($"news:recent:{limit}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            await _newsRefreshLock.WaitAsync();
            try
            {
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
            }
            finally
            {
                _newsRefreshLock.Release();
            }
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
            PublishedAt = n.PublishedAtUtc,
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

                    var pub = NormalizePublishedUtc(TryParseDate(
                        item.Elements().FirstOrDefault(x => x.Name.LocalName == "pubDate")?.Value
                        ?? item.Elements().FirstOrDefault(x => x.Name.LocalName == "date")?.Value
                    ));

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

                    var pub = NormalizePublishedUtc(TryParseDate(
                        entry.Elements().FirstOrDefault(x => x.Name.LocalName == "updated")?.Value
                        ?? entry.Elements().FirstOrDefault(x => x.Name.LocalName == "published")?.Value
                    ));

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

    private static DateTime AsUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt
        };
    }

    private static DateTimeOffset? TryParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, out var dto)) return dto;
        return null;
    }

    /// <summary>Normalize to UTC for PostgreSQL timestamptz; RSS/Atom often return Unspecified or Local.</summary>
    private static DateTimeOffset? NormalizePublishedUtc(DateTimeOffset? dto)
    {
        if (!dto.HasValue) return null;
        return new DateTimeOffset(AsUtc(dto.Value.UtcDateTime), TimeSpan.Zero);
    }

    private async Task UpsertNewsAsync(List<NewsDto> items)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("UpsertNewsAsync started, items={Count}", items.Count);

        if (items.Count == 0)
        {
            _logger.LogDebug("UpsertNewsAsync: no items, skipping");
            return;
        }

        string Norm(string url) => url.Trim();
        string Hash(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Norm(url)));
            return Convert.ToHexString(bytes);
        }

        // Precompute hashes once
        var itemWithHash = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .Select(x => new { Dto = x, Hash = Hash(x.Url) })
            .ToList();

        if (itemWithHash.Count == 0)
        {
            _logger.LogWarning("UpsertNewsAsync: no items with valid Url after hash, skipping");
            return;
        }

        _logger.LogDebug("UpsertNewsAsync: itemWithHash={Count}, distinct hashes={Distinct}",
            itemWithHash.Count, itemWithHash.Select(x => x.Hash).Distinct().Count());

        // 1) Load existing rows in chunks (avoid huge ANY/IN payload)
        var existing = new Dictionary<string, NewsItem>(StringComparer.Ordinal);
        const int lookupChunkSize = 100;
        int lookupChunks = 0;

        foreach (var hashChunk in itemWithHash.Select(x => x.Hash).Distinct().Chunk(lookupChunkSize))
        {
            var chunk = hashChunk.ToList();
            var rows = await _db.NewsItems
                .Where(x => chunk.Contains(x.UrlHash))
                .ToListAsync();
            foreach (var row in rows)
                existing[row.UrlHash] = row;
            lookupChunks++;
        }

        _logger.LogDebug("UpsertNewsAsync: loaded existing rows={Existing}, lookupChunks={Chunks}", existing.Count, lookupChunks);

        // 2) Upsert in smaller batches (avoid giant SaveChanges batch on Supabase); track inserted for tagging
        const int saveChunkSize = 25;
        int inserted = 0, updated = 0, saveRounds = 0;
        var insertedDtos = new List<NewsDto>();

        try
        {
            for (int i = 0; i < itemWithHash.Count; i += saveChunkSize)
            {
                var chunk = itemWithHash.Skip(i).Take(saveChunkSize).ToList();

                foreach (var x in chunk)
                {
                    var dto = x.Dto;
                    var h = x.Hash;

                    if (existing.TryGetValue(h, out var row))
                    {
                        row.Title = dto.Title ?? "";
                        row.Source = dto.Source ?? "";
                        row.Url = dto.Url ?? "";
                        row.ThumbnailUrl = dto.ThumbnailUrl;
                        row.Content = dto.Content;
                        row.PublishedAtUtc = dto.PublishedAt.HasValue ? new DateTimeOffset(AsUtc(dto.PublishedAt.Value.UtcDateTime), TimeSpan.Zero) : null;
                        updated++;
                    }
                    else
                    {
                        var newRow = new NewsItem
                        {
                            Title = dto.Title ?? "",
                            Source = dto.Source ?? "",
                            Url = dto.Url ?? "",
                            ThumbnailUrl = dto.ThumbnailUrl,
                            Content = dto.Content,
                            PublishedAtUtc = dto.PublishedAt.HasValue ? new DateTimeOffset(AsUtc(dto.PublishedAt.Value.UtcDateTime), TimeSpan.Zero) : null,
                            UrlHash = h
                        };

                        _db.NewsItems.Add(newRow);
                        existing[h] = newRow;
                        inserted++;
                        insertedDtos.Add(dto);
                    }
                }

                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
                saveRounds++;
            }

            _logger.LogInformation("UpsertNewsAsync completed: inserted={Inserted}, updated={Updated}, saveRounds={Rounds}, elapsedMs={Elapsed}",
                inserted, updated, saveRounds, sw.ElapsedMilliseconds);

            // Only run team-tagging for newly inserted articles (much cheaper when nothing new)
            if (insertedDtos.Count > 0)
                await UpsertNewsTeamsAsync(insertedDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertNewsAsync failed: items={Count}, inserted={Inserted}, updated={Updated}, saveRounds={Rounds}, elapsedMs={Elapsed}",
                items.Count, inserted, updated, saveRounds, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task UpsertNewsTeamsAsync(List<NewsDto> items)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("UpsertNewsTeamsAsync started, items={Count}", items.Count);

        // Load teams once (cached 24h – teams rarely change; precomputed NameLower/CoreLower for cheap contains)
        _logger.LogInformation("UpsertNewsTeamsAsync: loading teams (cache or DB)...");
        var teams = await _cache.GetOrCreateAsync("news:teams", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            _logger.LogInformation("UpsertNewsTeamsAsync: cache miss, querying Teams from DB...");
            var rows = await _db.Teams
                .AsNoTracking()
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();
            var list = rows
                .Select(t => new TeamMatchKey(t.Id, t.Name.Trim().ToLowerInvariant(), CoreLowerFromName(t.Name)))
                .ToList();
            _logger.LogInformation("UpsertNewsTeamsAsync: Teams query returned {Count} rows (precomputed matcher)", list.Count);
            return list;
        });

        if (teams == null || teams.Count == 0)
        {
            _logger.LogWarning("UpsertNewsTeamsAsync finished: no teams in DB or cache, skipping");
            return;
        }
        _logger.LogInformation("UpsertNewsTeamsAsync: teams loaded={Count}, elapsedMs={Elapsed}", teams.Count, sw.ElapsedMilliseconds);

        // Find the NewsItem rows you just upserted (by UrlHash)
        string Norm(string url) => url.Trim();
        string Hash(string url)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Norm(url)));
            return Convert.ToHexString(bytes);
        }

        var hashes = items.Select(x => Hash(x.Url)).Distinct().ToList();

        var newsRows = new List<NewsItem>();
        const int hashChunkSize = 100;

        foreach (var hashChunk in hashes.Chunk(hashChunkSize))
        {
            var chunk = hashChunk.ToList();
            var part = await _db.NewsItems
                .Where(n => chunk.Contains(n.UrlHash))
                .ToListAsync();

            newsRows.AddRange(part);
        }

        _logger.LogInformation("UpsertNewsTeamsAsync: newsRows found={Count} for hashes={HashCount}, elapsedMs={Elapsed}", newsRows.Count, hashes.Count, sw.ElapsedMilliseconds);

        // Build desired mappings (precomputed NameLower/CoreLower – no per-article ToLower/Replace)
        var desired = new List<NewsItemTeam>();

        foreach (var n in newsRows)
        {
            if (string.IsNullOrWhiteSpace(n.Title)) continue;

            var searchText = $"{n.Title} {n.Source}".ToLowerInvariant();

            foreach (var t in teams)
            {
                if (searchText.Contains(t.NameLower))
                {
                    desired.Add(new NewsItemTeam { NewsItemId = n.Id, TeamId = t.Id });
                    continue;
                }
                if (t.CoreLower.Length >= 4 && searchText.Contains(t.CoreLower))
                    desired.Add(new NewsItemTeam { NewsItemId = n.Id, TeamId = t.Id });
            }
        }

        if (desired.Count == 0)
        {
            _logger.LogInformation("UpsertNewsTeamsAsync finished: no desired team-news pairs (newsRows={NewsRows}), elapsedMs={Elapsed}", newsRows.Count, sw.ElapsedMilliseconds);
            return;
        }

        // Insert only new pairs (don’t spam duplicates)
        var newsIds = desired.Select(x => x.NewsItemId).Distinct().ToList();

        var existingPairs = new List<(int NewsItemId, int TeamId)>();
        const int newsIdChunkSize = 200;

        foreach (var idChunk in newsIds.Chunk(newsIdChunkSize))
        {
            var chunk = idChunk.ToList();

            var part = await _db.Set<NewsItemTeam>()
                .Where(x => chunk.Contains(x.NewsItemId))
                .Select(x => new { x.NewsItemId, x.TeamId })
                .ToListAsync();

            existingPairs.AddRange(part.Select(p => (p.NewsItemId, p.TeamId)));
        }

        var exists = existingPairs.ToHashSet();

        var toAdd = desired
            .Where(x => !exists.Contains((x.NewsItemId, x.TeamId)))
            .ToList();

        if (toAdd.Count == 0)
        {
            _logger.LogInformation("UpsertNewsTeamsAsync finished: no new pairs to add (desired={Desired}, existingPairs={Existing}), elapsedMs={Elapsed}", desired.Count, existingPairs.Count, sw.ElapsedMilliseconds);
            return;
        }

        const int insertChunkSize = 100;
        int insertRounds = 0;
        try
        {
            for (int i = 0; i < toAdd.Count; i += insertChunkSize)
            {
                var chunk = toAdd.Skip(i).Take(insertChunkSize).ToList();
                _db.Set<NewsItemTeam>().AddRange(chunk);
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
                insertRounds++;
            }
            _logger.LogInformation("UpsertNewsTeamsAsync completed: desired={Desired}, existingPairs={Existing}, toAdd={ToAdd}, insertRounds={Rounds}, elapsedMs={Elapsed}",
                desired.Count, existingPairs.Count, toAdd.Count, insertRounds, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertNewsTeamsAsync failed: toAdd={ToAdd}, insertRounds={Rounds}, elapsedMs={Elapsed}",
                toAdd.Count, insertRounds, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
