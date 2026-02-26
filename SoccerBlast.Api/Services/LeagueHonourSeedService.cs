using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

/// <summary>Seeds LeagueHonourMaps by matching Competitions (with SportsDB external id) to Honours, or by calling TheSportsDB all/leagues and matching league name→Honour slug/title. No cache.</summary>
public class LeagueHonourSeedService
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;

    public LeagueHonourSeedService(AppDbContext db, TheSportsDbClient sportsDb)
    {
        _db = db;
        _sportsDb = sportsDb;
    }

    /// <summary>Insert league–honour mappings: from CompetitionExternalMaps+Competition name, or from TheSportsDB all/leagues API when no maps exist. Idempotent.</summary>
    public async Task<LeagueHonourSeedResult> SeedFromCompetitionsAndHonoursAsync(CancellationToken ct = default)
    {
        var result = new LeagueHonourSeedResult();
        var honours = await _db.Honours.AsNoTracking().Select(h => new { h.Id, h.Slug, h.Title }).ToListAsync(ct);
        var slugToHonourId = honours
            .Where(h => !string.IsNullOrWhiteSpace(h.Slug))
            .GroupBy(h => h.Slug.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var titleToHonourId = honours
            .Where(h => !string.IsNullOrWhiteSpace(h.Title))
            .GroupBy(h => NormalizeTitle(h.Title))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var existing = await _db.LeagueHonourMaps
            .AsNoTracking()
            .Select(x => new { x.LeagueId, x.HonourId })
            .ToListAsync(ct);
        var existingSet = existing.Select(x => (x.LeagueId, x.HonourId)).ToHashSet();
        // Each honour maps to at most one league (avoid same honour linked to multiple leagues)
        var honourToLeagueId = existing.GroupBy(x => x.HonourId).ToDictionary(g => g.Key, g => g.First().LeagueId);

        // Remove redundant rows: same HonourId mapped to multiple leagues — keep one per honour
        var byHonour = await _db.LeagueHonourMaps
            .AsNoTracking()
            .GroupBy(x => x.HonourId)
            .Where(g => g.Count() > 1)
            .Select(g => new { HonourId = g.Key, KeepLeagueId = g.OrderBy(x => x.LeagueId).Select(x => x.LeagueId).First() })
            .ToListAsync(ct);
        foreach (var r in byHonour)
        {
            var toRemove = await _db.LeagueHonourMaps
                .Where(x => x.HonourId == r.HonourId && x.LeagueId != r.KeepLeagueId)
                .ToListAsync(ct);
            _db.LeagueHonourMaps.RemoveRange(toRemove);
            result.RemovedRedundant = (result.RemovedRedundant ?? 0) + toRemove.Count;
        }
        if (result.RemovedRedundant > 0)
        {
            await _db.SaveChangesAsync(ct);
            existing = await _db.LeagueHonourMaps.AsNoTracking().Select(x => new { x.LeagueId, x.HonourId }).ToListAsync(ct);
            existingSet = existing.Select(x => (x.LeagueId, x.HonourId)).ToHashSet();
            honourToLeagueId = existing.GroupBy(x => x.HonourId).ToDictionary(g => g.Key, g => g.First().LeagueId);
        }

        var leagues = await _db.CompetitionExternalMaps
            .AsNoTracking()
            .Where(m => m.Provider == "SportsDB" && !string.IsNullOrWhiteSpace(m.ExternalId))
            .Join(_db.Competitions, m => m.CompetitionId, c => c.Id, (m, c) => new { LeagueId = m.ExternalId.Trim(), Name = c.Name })
            .ToListAsync(ct);

        if (leagues.Count > 0)
        {
            foreach (var league in leagues)
            {
                var slug = Slugify(league.Name);
                if (string.IsNullOrEmpty(slug)) continue;
                if (!slugToHonourId.TryGetValue(slug, out var honourId)) continue;
                var key = (league.LeagueId, honourId);
                if (existingSet.Contains(key)) continue;
                if (honourToLeagueId.TryGetValue(honourId, out var existingLeague) && existingLeague != league.LeagueId)
                    continue; // honour already mapped to another league
                _db.LeagueHonourMaps.Add(new LeagueHonourMap { LeagueId = league.LeagueId, HonourId = honourId });
                existingSet.Add(key);
                honourToLeagueId[honourId] = league.LeagueId;
                result.Inserted++;
            }
        }
        else
        {
            List<SportsDbLeagueListItem> apiLeagues;
            try
            {
                apiLeagues = await _sportsDb.ListAllLeaguesAsync(ct);
            }
            catch (Exception ex)
            {
                result.Message = $"CompetitionExternalMaps empty and all/leagues API failed: {ex.Message}";
                result.Success = false;
                return result;
            }
            if (apiLeagues.Count == 0)
            {
                result.Message = "No CompetitionExternalMaps found and all/leagues returned no Soccer leagues.";
                await _db.SaveChangesAsync(ct);
                result.Success = true;
                return result;
            }
            foreach (var league in apiLeagues)
            {
                var leagueId = league.IdLeague.Trim();
                if (string.IsNullOrEmpty(leagueId)) continue;
                int? honourId = null;
                var slug = Slugify(league.StrLeague);
                if (!string.IsNullOrEmpty(slug) && slugToHonourId.TryGetValue(slug, out var bySlug))
                    honourId = bySlug;
                if (honourId == null)
                {
                    var titleNorm = NormalizeTitle(league.StrLeague);
                    if (!string.IsNullOrEmpty(titleNorm) && titleToHonourId.TryGetValue(titleNorm, out var byTitle))
                        honourId = byTitle;
                }
                if (honourId == null) continue;
                var key = (leagueId, honourId.Value);
                if (existingSet.Contains(key)) continue;
                if (honourToLeagueId.TryGetValue(honourId.Value, out var existingLeague) && existingLeague != leagueId)
                    continue; // honour already mapped to another league
                _db.LeagueHonourMaps.Add(new LeagueHonourMap { LeagueId = leagueId, HonourId = honourId.Value });
                existingSet.Add(key);
                honourToLeagueId[honourId.Value] = leagueId;
                result.Inserted++;
            }
            if (result.Inserted == 0)
                result.Message = "No CompetitionExternalMaps found; used all/leagues but no league name matched any Honour slug or title. Ensure Honours are imported.";
        }

        await _db.SaveChangesAsync(ct);
        result.Success = true;
        if (result.Inserted == 0 && result.Message == null)
            result.Message = "No new mappings inserted (all already exist or no slug match).";
        return result;
    }

    /// <summary>Returns count and a sample of (LeagueId, HonourId) for verification.</summary>
    public async Task<LeagueHonourMappingSummary> GetMappingSummaryAsync(CancellationToken ct = default)
    {
        var count = await _db.LeagueHonourMaps.AsNoTracking().CountAsync(ct);
        var sample = await _db.LeagueHonourMaps
            .AsNoTracking()
            .OrderBy(x => x.LeagueId)
            .Take(20)
            .Select(x => new LeagueHonourMappingItem { LeagueId = x.LeagueId, HonourId = x.HonourId })
            .ToListAsync(ct);
        return new LeagueHonourMappingSummary { Count = count, Sample = sample };
    }

    private static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var s = name.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        s = s.Trim('-');
        return string.IsNullOrEmpty(s) ? "" : s;
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var s = title.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }
}

public class LeagueHonourMappingSummary
{
    public int Count { get; set; }
    public List<LeagueHonourMappingItem> Sample { get; set; } = new();
}

public class LeagueHonourMappingItem
{
    public string LeagueId { get; set; } = "";
    public int HonourId { get; set; }
}

public class LeagueHonourSeedResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int Inserted { get; set; }
    /// <summary>Number of redundant rows removed (same honour mapped to multiple leagues).</summary>
    public int? RemovedRedundant { get; set; }
}
