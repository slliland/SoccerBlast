using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Shared.Contracts;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;

    public SearchController(AppDbContext db, TheSportsDbClient sportsDb)
    {
        _db = db;
        _sportsDb = sportsDb;
    }

    [HttpGet]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] string q, [FromQuery] int limit = 12, CancellationToken ct = default)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return new List<SearchResultDto>();

        var per = Math.Max(2, limit / 4);

        // Team search: v2 only, return ALL matching teams (main, women's, rugby, etc.) so user picks the right one
        var teamResults = new List<SearchResultDto>();
        try
        {
            var v2Teams = await _sportsDb.SearchTeamsAsync(q, ct);

            // Keep only soccer teams (men's, women's, youth, etc. – all use sport = "Soccer" in v2)
            var soccerTeams = v2Teams
                .Where(t => string.IsNullOrWhiteSpace(t.StrSport) ||
                            string.Equals(t.StrSport, "Soccer", StringComparison.OrdinalIgnoreCase))
                .Take(30);

            foreach (var t in soccerTeams)
            {
                if (string.IsNullOrEmpty(t.IdTeam)) continue;
                var subtitle = !string.IsNullOrWhiteSpace(t.StrSport)
                    ? t.StrSport
                    : (!string.IsNullOrWhiteSpace(t.StrLeague) ? t.StrLeague : "Team");
                // PATCH: TSDB Inter Milan wrong gender
                var gender = t.StrGender;
                if (t.IdTeam == "133681") gender = "Male";

                if (!string.IsNullOrWhiteSpace(gender) && !subtitle.Contains(gender, StringComparison.OrdinalIgnoreCase))
                    subtitle = $"{gender} {subtitle}";
                teamResults.Add(new SearchResultDto
                {
                    Type = SearchResultType.Team,
                    Title = t.StrTeam ?? "Unknown",
                    Subtitle = subtitle,
                    Url = $"/team/{t.IdTeam}",
                    Id = null,
                    SportsDbTeamId = t.IdTeam
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] Team search failed: {ex.Message}");
        }

        // League search: v2 API first (so empty DB still returns results)
        var leagueResults = new List<SearchResultDto>();
        try
        {
            var v2Leagues = await _sportsDb.SearchLeaguesAsync(q, ct);
            foreach (var L in v2Leagues.Take(per))
            {
                leagueResults.Add(new SearchResultDto
                {
                    Type = SearchResultType.Competition,
                    Title = L.StrLeague,
                    Subtitle = L.StrCountry ?? "League",
                    Url = $"/competition/{L.IdLeague}",
                    Id = null
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] League search failed: {ex.Message}");
        }

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

        // Player search: v2 API first, soccer only (filter out basketball etc.)
        var playerResults = new List<SearchResultDto>();
        try
        {
            var v2Players = await _sportsDb.SearchPlayersAsync(q, ct);
            var soccerPlayers = v2Players
                .Where(p => string.IsNullOrWhiteSpace(p.StrSport) ||
                            string.Equals(p.StrSport, "Soccer", StringComparison.OrdinalIgnoreCase))
                .Take(per);
            foreach (var P in soccerPlayers)
            {
                playerResults.Add(new SearchResultDto
                {
                    Type = SearchResultType.Player,
                    Title = P.StrPlayer,
                    Subtitle = P.StrPosition ?? P.StrTeam ?? "Player",
                    Url = $"/player/{P.IdPlayer}",
                    Id = null
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] Player search failed: {ex.Message}");
        }

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => p.Name.Contains(q))
            .OrderBy(p => p.Name)
            .Take(per)
            .Select(p => new SearchResultDto
            {
                Type = SearchResultType.Player,
                Title = p.Name,
                Subtitle = p.Position ?? "Player",
                Url = $"/player/{p.Id}",
                Id = p.Id
            })
            .ToListAsync();

        // Venue search: v2 API first (so empty DB still returns results)
        var venueResults = new List<SearchResultDto>();
        try
        {
            var v2Venues = await _sportsDb.SearchVenuesAsync(q, ct);
            foreach (var V in v2Venues.Take(per))
            {
                venueResults.Add(new SearchResultDto
                {
                    Type = SearchResultType.Venue,
                    Title = V.StrVenue,
                    Subtitle = V.StrLocation != null && V.StrCountry != null ? $"{V.StrLocation}, {V.StrCountry}" : (V.StrLocation ?? V.StrCountry ?? "Venue"),
                    Url = $"/venue/{V.IdVenue}",
                    Id = null
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] Venue search failed: {ex.Message}");
        }

        var venues = await _db.Venues
            .AsNoTracking()
            .Where(v => v.Name.Contains(q) || (v.City != null && v.City.Contains(q)) || (v.Country != null && v.Country.Contains(q)))
            .OrderBy(v => v.Name)
            .Take(per)
            .Select(v => new SearchResultDto
            {
                Type = SearchResultType.Venue,
                Title = v.Name,
                Subtitle = v.City != null && v.Country != null ? $"{v.City}, {v.Country}" : (v.City ?? v.Country ?? "Venue"),
                Url = $"/venue/{v.Id}",
                Id = v.Id
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

        // Merge: teams, leagues (v2), comps (DB), players (v2 then DB), venues (v2 then DB), matches, news
        var merged = teamResults
            .Concat(leagueResults)
            .Concat(comps)
            .Concat(playerResults)
            .Concat(players)
            .Concat(venueResults)
            .Concat(venues)
            .Concat(matches)
            .Concat(news)
            .Take(limit)
            .ToList();

        return merged;
    }
}
