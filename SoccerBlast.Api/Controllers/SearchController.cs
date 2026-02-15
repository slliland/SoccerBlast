using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Shared.Contracts;
using SoccerBlast.Api.Data;
namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] string q, [FromQuery] int limit = 12)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return new List<SearchResultDto>();

        // Give each type a slice so news isn't pushed out by teams/matches
        var per = Math.Max(2, limit / 4);

        var teams = await _db.Teams
            .Where(t => t.Name.Contains(q))
            .OrderBy(t => t.Name)
            .Take(per)
            .Select(t => new SearchResultDto
            {
                Type = SearchResultType.Team,
                Title = t.Name,
                Subtitle = "Team",
                Url = $"/team/{t.Id}",
                Id = t.Id
            })
            .ToListAsync();

        var comps = await _db.Competitions
            .Where(c => c.Name.Contains(q))
            .OrderBy(c => c.Name)
            .Take(per)
            .Select(c => new SearchResultDto
            {
                Type = SearchResultType.Competition,
                Title = c.Name,
                Subtitle = "League",
                Url = $"/competition/{c.Id}",
                Id = c.Id
            })
            .ToListAsync();

        var matches = await _db.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.HomeTeam.Name.Contains(q) || m.AwayTeam.Name.Contains(q))
            .OrderByDescending(m => m.UtcDate)
            .Take(per)
            .Select(m => new SearchResultDto
            {
                Type = SearchResultType.Match,
                Title = m.HomeTeam.Name + " vs " + m.AwayTeam.Name,
                Subtitle = m.Competition.Name,
                Url = $"/match/{m.Id}",
                Id = m.Id,
                When = new DateTimeOffset(DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc))
            })
            .ToListAsync();

        var news = await _db.NewsItems
            .Where(n => n.Title.Contains(q))
            .OrderByDescending(n => n.PublishedAtUtc)
            .Take(per)
            .Select(n => new SearchResultDto
            {
                Type = SearchResultType.News,
                Title = n.Title,
                Subtitle = n.Source,
                Url = n.Url,
                When = n.PublishedAtUtc == null
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(n.PublishedAtUtc.Value, DateTimeKind.Utc))
            })
            .ToListAsync();

        // Merge, then you can optionally sort (e.g., news by recency)
        var merged = teams
            .Concat(comps)
            .Concat(matches)
            .Concat(news)
            .Take(limit)
            .ToList();

        return merged;
    }
}
