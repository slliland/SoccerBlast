using Microsoft.AspNetCore.Mvc;
using SoccerBlast.Api.Services;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/admin/sync")]
public class AdminSyncController : ControllerBase
{
    private readonly MatchSyncService _sync;
    private readonly TeamProfileSyncService _profileSync;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AdminSyncController(MatchSyncService sync, TeamProfileSyncService profileSync, AppDbContext db, IConfiguration config)
    {
        _sync = sync;
        _profileSync = profileSync;
        _db = db;
        _config = config;
    }

    /// <summary>Which DB the API is using (Postgres host or SQLite path). Connection string is redacted.</summary>
    [HttpGet("db-info")]
    public IActionResult GetDbInfo()
    {
        var conn = _config.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
            ?? "";
        var isPostgres = conn.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || conn.TrimStart().StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        string? resolvedPath = null;
        string? host = null;
        if (!string.IsNullOrWhiteSpace(conn))
        {
            if (isPostgres)
            {
                if (conn.TrimStart().StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(conn);
                        host = uri.Host;
                    }
                    catch { host = "[Postgres]"; }
                }
                else
                {
                    foreach (var part in conn.Split(';'))
                    {
                        var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
                        if (kv.Length == 2 && string.Equals(kv[0], "Host", StringComparison.OrdinalIgnoreCase))
                        { host = kv[1]; break; }
                    }
                    if (string.IsNullOrEmpty(host)) host = "[Postgres]";
                }
            }
            else
            {
                const string prefix = "Data Source=";
                var ds = conn.Trim();
                if (ds.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var dataSource = ds.Substring(prefix.Length).Trim().Trim('"');
                    resolvedPath = Path.IsPathRooted(dataSource)
                        ? dataSource
                        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), dataSource));
                }
            }
        }

        return Ok(new
        {
            provider = isPostgres ? "Postgres" : "SQLite",
            host,
            resolvedPath,
            currentDirectory = Directory.GetCurrentDirectory()
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var last = await _db.SyncLogs
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync();

        if (last == null)
            return Ok(new { message = "No sync has been run yet." });

        return Ok(new
        {
            last.SyncType,
            localDate = last.LocalDate.ToString("yyyy-MM-dd"),
            last.StartedAtUtc,
            last.FinishedAtUtc,
            last.Success,
            last.SyncedMatches,
            last.ErrorMessage
        });
    }

    [HttpPost("today")]
    public async Task<ActionResult> SyncToday()
    {
        var count = await _sync.SyncTodayAsync();
        return Ok(new { syncedMatches = count });
    }


    [HttpPost("date/{date}")]
    public async Task<IActionResult> SyncByDate(string date)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var localDate))
        {
            return BadRequest("Invalid date format. Use yyyy-MM-dd, e.g. 2026-02-10.");
        }

        var count = await _sync.SyncLocalDateAsync(localDate, "America/New_York");
        return Ok(new { syncedMatches = count, localDate = date });
    }

    [HttpPost("range")]
    public async Task<IActionResult> SyncRange([FromQuery] string from, [FromQuery] string to)
    {
        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
        {
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-07");
        }

        if ((toLocal - fromLocal).TotalDays > 14)
            return BadRequest("Range too large for free plan demo. Use at most 14 days.");

        var (daysSynced, matchesSynced) = await _sync.SyncRangeAsync(fromLocal, toLocal, "America/New_York");

        return Ok(new { from, to, daysSynced, matchesSynced });
    }

    /// <summary>
    /// Danger zone: wipe all soccer data that can be safely rebuilt from SportsDB.
    /// Deletes rows from Matches, TeamProfiles, Players, Competitions, Venues, Teams.
    /// Leaves news, videos, favorites, and other content untouched.
    /// </summary>
    [HttpPost("reset-soccer-data")]
    public async Task<IActionResult> ResetSoccerData(CancellationToken ct = default)
    {
        var matchCount = await _db.Matches.CountAsync(ct);
        var teamProfileCount = await _db.TeamProfiles.CountAsync(ct);
        var playerCount = await _db.Players.CountAsync(ct);
        var competitionCount = await _db.Competitions.CountAsync(ct);
        var venueCount = await _db.Venues.CountAsync(ct);
        var teamCount = await _db.Teams.CountAsync(ct);

        _db.Matches.RemoveRange(_db.Matches);
        _db.TeamProfiles.RemoveRange(_db.TeamProfiles);
        _db.Players.RemoveRange(_db.Players);
        _db.Competitions.RemoveRange(_db.Competitions);
        _db.Venues.RemoveRange(_db.Venues);
        _db.Teams.RemoveRange(_db.Teams);

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            deletedMatches = matchCount,
            deletedTeamProfiles = teamProfileCount,
            deletedPlayers = playerCount,
            deletedCompetitions = competitionCount,
            deletedVenues = venueCount,
            deletedTeams = teamCount
        });
    }
}
