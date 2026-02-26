using System.Linq.Expressions;
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

    /// <summary>Candidate for batch alias upsert. Canonical and Alias must be trimmed; Alias length >= 3.</summary>
    private record AliasCandidate(AliasType Type, string Canonical, string Alias, string? ExternalId);

    /// <summary>EF Core cannot translate Contains(tuple); build (key1) OR (key2) OR ... so one query works.</summary>
    private static IQueryable<SearchAlias> QuerySearchAliasesByKeys(
        IQueryable<SearchAlias> source,
        List<(AliasType Type, string Canonical, string Alias)> keys)
    {
        if (keys.Count == 0) return source.Where(_ => false);
        var param = Expression.Parameter(typeof(SearchAlias), "a");
        Expression? body = null;
        foreach (var (type, canonical, alias) in keys)
        {
            var term = Expression.AndAlso(
                Expression.AndAlso(
                    Expression.Equal(Expression.Property(param, "Type"), Expression.Constant(type)),
                    Expression.Equal(Expression.Property(param, "Canonical"), Expression.Constant(canonical))),
                Expression.Equal(Expression.Property(param, "Alias"), Expression.Constant(alias)));
            body = body == null ? term : Expression.OrElse(body, term);
        }
        if (body == null) return source.Where(_ => false);
        var lambda = Expression.Lambda<Func<SearchAlias, bool>>(body, param);
        return source.Where(lambda);
    }

    private static void AddAliasCandidates(
        List<AliasCandidate> list,
        AliasType type,
        IEnumerable<(string Canonical, string? ExternalId)> items,
        string? userQuery)
    {
        foreach (var (canonical, externalId) in items)
        {
            if (string.IsNullOrWhiteSpace(canonical)) continue;
            var c = canonical.Trim();
            list.Add(new AliasCandidate(type, c, c, externalId));
            if (userQuery != null && userQuery.Length >= 3)
                list.Add(new AliasCandidate(type, c, userQuery.Trim(), externalId));
        }
    }

    /// <summary>Batch upsert: one query to resolve existing, then updates/adds and a single SaveChanges.</summary>
    private async Task BatchUpsertAliasesAsync(List<AliasCandidate> candidates, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var normalized = new List<(AliasCandidate c, string AliasNorm)>();
        foreach (var c in candidates)
        {
            var canon = (c.Canonical ?? "").Trim();
            var alias = (c.Alias ?? "").Trim();
            if (string.IsNullOrWhiteSpace(canon) || string.IsNullOrWhiteSpace(alias) || alias.Length < 3) continue;
            var aliasNorm = SearchText.Normalize(alias);
            if (string.IsNullOrWhiteSpace(aliasNorm)) continue;
            normalized.Add((c with { Canonical = canon, Alias = alias }, aliasNorm));
        }

        var deduped = normalized
            .GroupBy(x => (x.c.Type, x.c.Canonical, x.c.Alias))
            .Select(g =>
            {
                var first = g.First();
                var externalId = g.Select(x => x.c.ExternalId).FirstOrDefault(e => !string.IsNullOrEmpty(e)) ?? first.c.ExternalId;
                return (c: first.c with { ExternalId = externalId }, first.AliasNorm);
            })
            .ToList();
        if (deduped.Count == 0) return;

        var keys = deduped.Select(x => (x.c.Type, x.c.Canonical, x.c.Alias)).Distinct().ToList();
        var existingList = keys.Count == 0
            ? new List<SearchAlias>()
            : await QuerySearchAliasesByKeys(_db.SearchAliases, keys.Take(120).ToList())
                .ToListAsync(ct);

        var existingByKey = existingList.ToDictionary(a => (a.Type, a.Canonical, a.Alias));

        foreach (var (c, aliasNorm) in deduped)
        {
            var key = (c.Type, c.Canonical, c.Alias);
            var existingLocal = _db.SearchAliases.Local
                .FirstOrDefault(a => a.Type == c.Type && a.Canonical == c.Canonical && a.Alias == c.Alias);
            if (existingLocal != null)
            {
                existingLocal.HitCount += 1;
                existingLocal.ExternalId ??= c.ExternalId;
                existingLocal.AliasNorm = aliasNorm;
                existingLocal.UpdatedAtUtc = now;
                continue;
            }

            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.HitCount += 1;
                existing.ExternalId ??= c.ExternalId;
                existing.AliasNorm = aliasNorm;
                existing.UpdatedAtUtc = now;
                continue;
            }

            _db.SearchAliases.Add(new SearchAlias
            {
                Type = c.Type,
                Canonical = c.Canonical,
                Alias = c.Alias,
                AliasNorm = aliasNorm,
                ExternalId = c.ExternalId,
                HitCount = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // SearchAliases.Id may have no default in DB (e.g. table created from SQLite migration).
            // Log and continue so search still returns results; apply migration to fix Id column.
            Console.WriteLine($"[Search] Alias save failed (search results still returned): {ex.InnerException?.Message ?? ex.Message}");
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
        var aliasCandidates = new List<AliasCandidate>();

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

            AddAliasCandidates(aliasCandidates, AliasType.Team,
                soccerTeams.Where(t => !string.IsNullOrWhiteSpace(t.StrTeam)).Select(t => (t.StrTeam!, t.IdTeam)), q);

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

            AddAliasCandidates(aliasCandidates, AliasType.League,
                v2Leagues.Take(30).Where(L => !string.IsNullOrWhiteSpace(L.StrLeague)).Select(L => (L.StrLeague!, (string?)L.IdLeague)), q);

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

            AddAliasCandidates(aliasCandidates, AliasType.Player,
                soccerPlayers.Where(P => !string.IsNullOrWhiteSpace(P.StrPlayer)).Select(P => (P.StrPlayer!, (string?)P.IdPlayer)), q);

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

            AddAliasCandidates(aliasCandidates, AliasType.Venue,
                v2Venues.Take(30).Where(V => !string.IsNullOrWhiteSpace(V.StrVenue)).Select(V => (V.StrVenue!, (string?)V.IdVenue)), q);

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
                When = m.UtcDate
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
                When = n.PublishedAtUtc
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

        await BatchUpsertAliasesAsync(aliasCandidates, ct);
        return merged;
    }
}
