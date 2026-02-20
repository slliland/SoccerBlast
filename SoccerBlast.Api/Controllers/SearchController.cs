using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Shared.Contracts;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Api.Models;
using SoccerBlast.Api.Services.Search;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;
    private readonly FuzzyAliasResolver _fuzzy;

    public SearchController(AppDbContext db, TheSportsDbClient sportsDb, FuzzyAliasResolver fuzzy)
    {
        _db = db;
        _sportsDb = sportsDb;
        _fuzzy = fuzzy;
    }

    private async Task UpsertAliasAsync(
        AliasType type,
        string canonical,
        string alias,
        string? externalId,
        CancellationToken ct)
    {
        canonical = (canonical ?? "").Trim();
        alias = (alias ?? "").Trim();
        if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(alias)) return;

        // avoid saving "fc" / "cf" garbage as aliases
        if (alias.Length < 3) return;

        // normalized alias for fuzzy search
        var aliasNorm = SearchText.Normalize(alias);
        if (string.IsNullOrWhiteSpace(aliasNorm)) return;

        var existing = await _db.SearchAliases
            .FirstOrDefaultAsync(a => a.Type == type && a.Canonical == canonical && a.Alias == alias, ct);

        if (existing == null)
        {
            _db.SearchAliases.Add(new SearchAlias
            {
                Type = type,
                Canonical = canonical,
                Alias = alias,
                AliasNorm = aliasNorm,
                ExternalId = externalId,
                HitCount = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.HitCount += 1;
            existing.ExternalId ??= externalId;
            existing.AliasNorm = aliasNorm;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static List<SportsDbTeam> DistinctTeamsById(IEnumerable<SportsDbTeam> teams)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<SportsDbTeam>();
        foreach (var t in teams)
        {
            if (string.IsNullOrWhiteSpace(t.IdTeam)) continue;
            if (seen.Add(t.IdTeam)) list.Add(t);
        }
        return list;
    }


    [HttpGet]
    public async Task<ActionResult<List<SearchResultDto>>> Search([FromQuery] string q, [FromQuery] int limit = 12, CancellationToken ct = default)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return new List<SearchResultDto>();

        var per = Math.Max(2, limit / 4);

        var teamResults = new List<SearchResultDto>();
        try
        {
            // First attempt: SportsDB with raw user input (fast path)
            var v2Teams = await _sportsDb.SearchTeamsAsync(q, ct);

            // If SportsDB returned nothing, use fuzzy guesses from alias table,
            //    then call SportsDB again with the best canonical name(s).
            if (v2Teams.Count == 0)
            {
                var guesses = await _fuzzy.ResolveTopAsync(AliasType.Team, q, take: 3, ct: ct);

                // If have a strong single guess, try it first.
                // Otherwise try top 2-3 guesses (bounded) and merge results.
                var retryTeams = new List<SportsDbTeam>();

                foreach (var g in guesses)
                {
                    // keep a floor so we don't retry on weak guesses
                    if (g.score < 80) continue;

                    Console.WriteLine($"[Search] Team fuzzy guess: '{q}' -> '{g.canonical}' (score {g.score})");

                    var tmp = await _sportsDb.SearchTeamsAsync(g.canonical, ct);
                    if (tmp.Count > 0) retryTeams.AddRange(tmp);

                    // If it’s a clear winner, stop early.
                    if (FuzzyAliasResolver.ShouldAutoPick(guesses) && retryTeams.Count > 0)
                        break;
                }

                v2Teams = retryTeams.DistinctByKey(t => t.IdTeam);
            }

            // Filter soccer teams and keep more than `per`
            var soccerTeams = v2Teams
                .Where(t => string.IsNullOrWhiteSpace(t.StrSport) ||
                            string.Equals(t.StrSport, "Soccer", StringComparison.OrdinalIgnoreCase))
                .Take(30)
                .ToList();

            // Learn aliases (canonical + user query)
            // Canonical: SportsDB's own name (e.g. "Barcelona")
            // Alias: canonical itself, and also the user's query if it led to a good match
            foreach (var t in soccerTeams)
            {
                if (string.IsNullOrWhiteSpace(t.StrTeam)) continue;

                await UpsertAliasAsync(AliasType.Team, t.StrTeam!, t.StrTeam!, t.IdTeam, ct);

                // If user typed something longer, store it too
                if (q.Length >= 3)
                    await UpsertAliasAsync(AliasType.Team, t.StrTeam!, q, t.IdTeam, ct);
            }

            // Build results
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

            if (v2Leagues.Count == 0)
            {
                var guesses = await _fuzzy.ResolveTopAsync(AliasType.League, q, take: 3, ct: ct);
                var retryLeagues = new List<SportsDbLeagueSearchResult>();

                foreach (var g in guesses)
                {
                    if (g.score < 80) continue;

                    Console.WriteLine($"[Search] League fuzzy guess: '{q}' -> '{g.canonical}' (score {g.score})");

                    var tmp = await _sportsDb.SearchLeaguesAsync(g.canonical, ct);
                    if (tmp.Count > 0) retryLeagues.AddRange(tmp);

                    if (FuzzyAliasResolver.ShouldAutoPick(guesses) && retryLeagues.Count > 0)
                        break;
                }

                v2Leagues = retryLeagues.DistinctByKey(l => l.IdLeague);
            }

            // Learn aliases
            foreach (var L in v2Leagues.Take(30))
            {
                if (string.IsNullOrWhiteSpace(L.StrLeague)) continue;

                await UpsertAliasAsync(AliasType.League, L.StrLeague!, L.StrLeague!, L.IdLeague, ct);
                if (q.Length >= 3)
                    await UpsertAliasAsync(AliasType.League, L.StrLeague!, q, L.IdLeague, ct);
            }

            // Build results
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

            if (v2Players.Count == 0)
            {
                var guesses = await _fuzzy.ResolveTopAsync(AliasType.Player, q, take: 3, ct: ct);
                var retryPlayers = new List<SportsDbPlayerSearchResult>();

                foreach (var g in guesses)
                {
                    if (g.score < 80) continue;

                    Console.WriteLine($"[Search] Player fuzzy guess: '{q}' -> '{g.canonical}' (score {g.score})");

                    var tmp = await _sportsDb.SearchPlayersAsync(g.canonical, ct);
                    if (tmp.Count > 0) retryPlayers.AddRange(tmp);

                    if (FuzzyAliasResolver.ShouldAutoPick(guesses) && retryPlayers.Count > 0)
                        break;
                }

                v2Players = retryPlayers.DistinctByKey(p => p.IdPlayer);
            }

            // soccer-only filter AFTER retry
            var soccerPlayers = v2Players
                .Where(p => string.IsNullOrWhiteSpace(p.StrSport) ||
                            string.Equals(p.StrSport, "Soccer", StringComparison.OrdinalIgnoreCase))
                .Take(30)
                .ToList();

            // Learn aliases
            foreach (var P in soccerPlayers)
            {
                if (string.IsNullOrWhiteSpace(P.StrPlayer)) continue;

                await UpsertAliasAsync(AliasType.Player, P.StrPlayer!, P.StrPlayer!, P.IdPlayer, ct);
                if (q.Length >= 3)
                    await UpsertAliasAsync(AliasType.Player, P.StrPlayer!, q, P.IdPlayer, ct);
            }

            // Build results
            foreach (var P in soccerPlayers.Take(per))
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

            if (v2Venues.Count == 0)
            {
                var guesses = await _fuzzy.ResolveTopAsync(AliasType.Venue, q, take: 3, ct: ct);
                var retryVenues = new List<SportsDbVenueSearchResult>();

                foreach (var g in guesses)
                {
                    if (g.score < 80) continue;

                    Console.WriteLine($"[Search] Venue fuzzy guess: '{q}' -> '{g.canonical}' (score {g.score})");

                    var tmp = await _sportsDb.SearchVenuesAsync(g.canonical, ct);
                    if (tmp.Count > 0) retryVenues.AddRange(tmp);

                    if (FuzzyAliasResolver.ShouldAutoPick(guesses) && retryVenues.Count > 0)
                        break;
                }

                v2Venues = retryVenues.DistinctByKey(v => v.IdVenue);
            }

            // Learn aliases
            foreach (var V in v2Venues.Take(30))
            {
                if (string.IsNullOrWhiteSpace(V.StrVenue)) continue;

                await UpsertAliasAsync(AliasType.Venue, V.StrVenue!, V.StrVenue!, V.IdVenue, ct);
                if (q.Length >= 3)
                    await UpsertAliasAsync(AliasType.Venue, V.StrVenue!, q, V.IdVenue, ct);
            }

            // Build results
            foreach (var V in v2Venues.Take(per))
            {
                venueResults.Add(new SearchResultDto
                {
                    Type = SearchResultType.Venue,
                    Title = V.StrVenue,
                    Subtitle = V.StrLocation != null && V.StrCountry != null
                        ? $"{V.StrLocation}, {V.StrCountry}"
                        : (V.StrLocation ?? V.StrCountry ?? "Venue"),
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

        // Save alias updates
        await _db.SaveChangesAsync(ct);
        return merged;
    }
}
