using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;
using SoccerBlast.Api.Services;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/admin/import")]
public class AdminImportController : ControllerBase
{
    private readonly HonoursImportService _honoursImport;
    private readonly LeagueHonourSeedService _leagueHonourSeed;
    private readonly AppDbContext _db;

    public AdminImportController(HonoursImportService honoursImport, LeagueHonourSeedService leagueHonourSeed, AppDbContext db)
    {
        _honoursImport = honoursImport;
        _leagueHonourSeed = leagueHonourSeed;
        _db = db;
    }

    /// <summary>Import honours from ScrapeTeamHonours cache (sportsdb_cache.db) into the main DB. Run the script first, then call this to load data.</summary>
    [HttpPost("honours")]
    public async Task<ActionResult<HonoursImportResult>> ImportHonours(CancellationToken ct)
    {
        if (!_honoursImport.IsCacheAvailable)
            return BadRequest(new HonoursImportResult { Success = false, Message = "Honours cache file not found. Run: Scripts/ScrapeTeamHonours --db sportsdb_cache.db" });
        var result = await _honoursImport.ImportFromCacheAsync(ct);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Insert LeagueHonourMaps from main DB only: match Competitions (with SportsDB external id) to Honours by name/slug. Idempotent.</summary>
    [HttpPost("league-honour-mapping")]
    public async Task<ActionResult<LeagueHonourSeedResult>> SeedLeagueHonourMapping(CancellationToken ct)
    {
        var result = await _leagueHonourSeed.SeedFromCompetitionsAndHonoursAsync(ct);
        return Ok(result);
    }

    /// <summary>Verify: count and sample of LeagueHonourMaps (leagueId → honourId).</summary>
    [HttpGet("league-honour-mapping")]
    public async Task<ActionResult<LeagueHonourMappingSummary>> GetLeagueHonourMappingSummary(CancellationToken ct)
    {
        var result = await _leagueHonourSeed.GetMappingSummaryAsync(ct);
        return Ok(result);
    }

    /// <summary>Upsert CompetitionExternalMaps for all Competitions: (CompetitionId, SportsDB, ExternalId = Id). Idempotent; run after sync so v2/league lookups resolve.</summary>
    [HttpPost("competition-external-mapping")]
    public async Task<ActionResult<CompetitionExternalMappingResult>> UpsertCompetitionExternalMapping(CancellationToken ct)
    {
        var competitions = await _db.Competitions.AsNoTracking().ToListAsync(ct);
        var existing = await _db.CompetitionExternalMaps
            .Where(m => m.Provider == "SportsDB")
            .ToDictionaryAsync(m => m.CompetitionId, ct);

        var now = DateTime.UtcNow;
        int added = 0, updated = 0;
        foreach (var c in competitions)
        {
            if (existing.TryGetValue(c.Id, out var map))
            {
                if (map.ExternalId != c.Id.ToString() || map.LastSyncedUtc == null)
                {
                    map.ExternalId = c.Id.ToString();
                    map.LastSyncedUtc = now;
                    updated++;
                }
            }
            else
            {
                _db.CompetitionExternalMaps.Add(new CompetitionExternalMap
                {
                    CompetitionId = c.Id,
                    Provider = "SportsDB",
                    ExternalId = c.Id.ToString(),
                    LastSyncedUtc = now
                });
                added++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new CompetitionExternalMappingResult
        {
            Success = true,
            CompetitionsTotal = competitions.Count,
            Added = added,
            Updated = updated,
            Message = $"CompetitionExternalMaps: {added} added, {updated} updated."
        });
    }

    /// <summary>Summary of CompetitionExternalMaps (count and sample).</summary>
    [HttpGet("competition-external-mapping")]
    public async Task<ActionResult<CompetitionExternalMappingSummary>> GetCompetitionExternalMappingSummary(CancellationToken ct)
    {
        var total = await _db.CompetitionExternalMaps.CountAsync(ct);
        var sportsDbCount = await _db.CompetitionExternalMaps.CountAsync(m => m.Provider == "SportsDB", ct);
        var sample = await _db.CompetitionExternalMaps
            .Include(m => m.Competition)
            .Where(m => m.Provider == "SportsDB")
            .OrderBy(m => m.CompetitionId)
            .Take(10)
            .Select(m => new CompetitionExternalMapSampleRow(m.CompetitionId, m.Competition.Name, m.ExternalId, m.LastSyncedUtc))
            .ToListAsync(ct);

        return Ok(new CompetitionExternalMappingSummary
        {
            TotalMaps = total,
            SportsDbMaps = sportsDbCount,
            Sample = sample
        });
    }
}

public record CompetitionExternalMapSampleRow(int CompetitionId, string CompetitionName, string ExternalId, DateTime? LastSyncedUtc);

public class CompetitionExternalMappingResult
{
    public bool Success { get; set; }
    public int CompetitionsTotal { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public string? Message { get; set; }
}

public class CompetitionExternalMappingSummary
{
    public int TotalMaps { get; set; }
    public int SportsDbMaps { get; set; }
    public List<CompetitionExternalMapSampleRow> Sample { get; set; } = new();
}
