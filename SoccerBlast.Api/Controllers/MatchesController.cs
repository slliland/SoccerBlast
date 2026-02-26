using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Shared.Contracts;
using System.Globalization;
using SoccerBlast.Api.Services;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MatchSyncService _sync;
    private readonly TheSportsDbClient _sportsDb;
    private readonly ILogger<MatchesController> _logger;

    public MatchesController(AppDbContext db, MatchSyncService sync, TheSportsDbClient sportsDb, ILogger<MatchesController> logger)
    {
        _db = db;
        _sync = sync;
        _sportsDb = sportsDb;
        _logger = logger;
    }

    private async Task<List<MatchDto>> QueryMatches(DateTime startUtc, DateTime endUtc, int? competitionId = null)
    {
        var startOffset = new DateTimeOffset(startUtc, TimeSpan.Zero);
        var endOffset = new DateTimeOffset(endUtc, TimeSpan.Zero);
        var q = _db.Matches
            .AsNoTracking()
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.UtcDate >= startOffset && m.UtcDate < endOffset);

        if (competitionId.HasValue)
            q = q.Where(m => m.CompetitionId == competitionId.Value);

        return await q
            .OrderBy(m => m.UtcDate)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                UtcDate = m.UtcDate,
                CompetitionId = m.CompetitionId,
                CompetitionName = m.Competition.Name,
                CompetitionBadgeUrl = m.Competition.BadgeUrl,

                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam.Name,
                HomeTeamCrestUrl = m.HomeTeam.CrestUrl,

                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam.Name,
                AwayTeamCrestUrl = m.AwayTeam.CrestUrl,

                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                Status = m.Status,
                ExternalId = m.ExternalId > 0 ? m.ExternalId.ToString() : null
            })
            .ToListAsync();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<MatchDto>>> Search(
        [FromQuery] string? team,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] int? competitionId,
        [FromQuery] int limit = 200,
        [FromQuery] int? teamId = null,
        [FromQuery] bool exact = true,
        [FromQuery] string? tz = null
    )
    {
        if (string.IsNullOrWhiteSpace(team) && !teamId.HasValue)
            return BadRequest("team or teamId is required.");

        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
        {
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-28");
        }

        if (limit <= 0) limit = 200;
        if (limit > 500) limit = 500;

        tz ??= "America/New_York";
        var (startUtc, _) = DateRangeService.GetUtcRangeForLocalDate(fromLocal, tz);
        var (_, endUtc) = DateRangeService.GetUtcRangeForLocalDate(toLocal.AddDays(1), tz);

        var startOffset = new DateTimeOffset(startUtc, TimeSpan.Zero);
        var endOffset = new DateTimeOffset(endUtc, TimeSpan.Zero);
        var q = _db.Matches
            .AsNoTracking()
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.UtcDate >= startOffset && m.UtcDate < endOffset);

        if (competitionId.HasValue)
            q = q.Where(m => m.CompetitionId == competitionId.Value);

        // ID match
        if (teamId.HasValue && teamId.Value > 0)
        {
            q = q.Where(m => m.HomeTeamId == teamId.Value || m.AwayTeamId == teamId.Value);
        }
        else
        {
            var teamName = (team ?? "").Trim();
            var teamLower = teamName.ToLowerInvariant();

            if (exact)
            {
                q = q.Where(m =>
                    (m.HomeTeam.Name ?? "").ToLower() == teamLower ||
                    (m.AwayTeam.Name ?? "").ToLower() == teamLower
                );
            }
            else
            {
                q = q.Where(m =>
                    (m.HomeTeam.Name ?? "").ToLower().Contains(teamLower) ||
                    (m.AwayTeam.Name ?? "").ToLower().Contains(teamLower)
                );
            }
        }

        var results = await q
            .OrderBy(m => m.UtcDate)
            .Take(limit)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                UtcDate = m.UtcDate,
                CompetitionId = m.CompetitionId,
                CompetitionName = m.Competition.Name,
                CompetitionBadgeUrl = m.Competition.BadgeUrl,

                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam.Name,
                HomeTeamCrestUrl = m.HomeTeam.CrestUrl,

                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam.Name,
                AwayTeamCrestUrl = m.AwayTeam.CrestUrl,

                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                Status = m.Status,
                ExternalId = m.ExternalId > 0 ? m.ExternalId.ToString() : null
            })
            .ToListAsync();

        return results;
    }

    [HttpGet("today")]
    public async Task<ActionResult<List<MatchDto>>> GetToday()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        return await QueryMatches(today, tomorrow);
    }

    [HttpGet("today-local")]
    public async Task<ActionResult<List<MatchDto>>> GetTodayLocal(
        [FromQuery] int? competitionId,
        [FromQuery] string? tz)
    {
        tz ??= "America/New_York"; // fallback
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tz));
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(nowLocal.Date, tz);
        return await QueryMatches(startUtc, endUtc, competitionId);
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<List<MatchDto>>> GetByLocalDate(
        string date,
        [FromQuery] int? competitionId,
        [FromQuery] string? tz)
    {
        _logger.LogInformation("[Matches] GET date/{Date} tz={Tz}", date, tz ?? "null");
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        tz ??= "America/New_York";
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, tz);
        var matches = await QueryMatches(startUtc, endUtc, competitionId);
        _logger.LogInformation("[Matches] GET date/{Date} returning {Count} matches", date, matches.Count);
        return matches;
    }

    [HttpPost("date/{date}")]
    public async Task<ActionResult<int>> SyncByLocalDate(string date, [FromQuery] string? tz)
    {
        _logger.LogInformation("[Matches] POST sync date/{Date} tz={Tz}", date, tz ?? "null");
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        tz ??= "America/New_York";
        try
        {
            var synced = await _sync.SyncLocalDateAsync(localDate.Date, tz);
            _logger.LogInformation("[Matches] POST sync date/{Date} done synced={Count}", date, synced);
            return Ok(synced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncByLocalDate failed for date={Date}", date);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>GET api/Matches/range?from=yyyy-MM-dd&to=yyyy-MM-dd (and optional tz, competitionId).</summary>
    [HttpGet("range")]
    public async Task<ActionResult<List<MatchDto>>> GetRange(
        [FromQuery(Name = "from")] string fromDate,
        [FromQuery(Name = "to")] string toDate,
        [FromQuery] int? competitionId,
        [FromQuery] string? tz)
    {
        if (string.IsNullOrEmpty(fromDate) || string.IsNullOrEmpty(toDate))
            return BadRequest("from and to query parameters required (yyyy-MM-dd).");

        if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal) ||
            !DateTime.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
        {
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-28");
        }

        if (toLocal < fromLocal)
            return BadRequest("to must be >= from");

        tz ??= "America/New_York";

        // Inclusive local range [from, to]: UTC window [startOf(from), startOf(to+1))
        var (startUtc, _) = DateRangeService.GetUtcRangeForLocalDate(fromLocal.Date, tz);
        var (endUtc, _) = DateRangeService.GetUtcRangeForLocalDate(toLocal.Date.AddDays(1), tz);

        var res = await QueryMatches(startUtc, endUtc, competitionId);
        Response.Headers["X-Applied-Range"] = $"{fromDate}..{toDate} (tz={tz}) -> {res.Count} matches";
        return Ok(res);
    }

    [HttpPost("range")]
    public async Task<ActionResult<int>> SyncRange(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string? tz)
    {
        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
        {
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-28");
        }

        if (toLocal < fromLocal)
            return BadRequest("to must be >= from");

        tz ??= "America/New_York";

        var total = 0;
        for (var d = fromLocal.Date; d <= toLocal.Date; d = d.AddDays(1))
        {
            total += await _sync.SyncLocalDateAsync(d, tz);
        }

        return Ok(total);
    }

    /// <summary>
    /// Ensure a local-date range is fresh in the DB. Only missing or stale days are synced from provider.
    /// Freshness windows (minutes): hot (today +/-1), warm (last 7 days), cold (older).
    /// </summary>
    [HttpPost("range/ensure")]
    public async Task<ActionResult<MatchSyncService.EnsureRangeResult>> EnsureRange(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] int hotMin = 10,
        [FromQuery] int warmMin = 720,
        [FromQuery] int coldMin = 0,
        [FromQuery] string? tz = null,
        CancellationToken ct = default)
    {
        if (!DateOnly.TryParse(from, out var fromDate) || !DateOnly.TryParse(to, out var toDate))
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-28");

        var res = await _sync.EnsureRangeFreshAsync(fromDate, toDate, hotMin, warmMin, coldMin, tz, ct);
        return Ok(res);
    }

    [HttpGet("external")]
    public async Task<ActionResult<List<MatchDto>>> SearchExternal(
        [FromQuery] string sportsDbTeamId,
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbTeamId))
            return BadRequest("sportsDbTeamId is required.");

        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            return BadRequest("from/to must be yyyy-MM-dd");

        var start = fromDate.Date;
        var end = toDate.Date;

        var teamId = sportsDbTeamId.Trim();

        // v2: combine previous + next (v2 does not provide arbitrary range by team)
        var prev = await _sportsDb.GetPreviousTeamEventsAsync(teamId, ct);
        var next = await _sportsDb.GetNextTeamEventsAsync(teamId, ct);

        var combined = prev.Concat(next)
            .Select(ToMatchDto) // your ToMatchDto(SportsDbScheduleEvent e)
            .Where(m => m.UtcDate.Date >= start && m.UtcDate.Date <= end)
            .OrderBy(m => m.UtcDate)
            .ToList();

        return Ok(combined);
    }

    private static MatchDto ToMatchDto(SportsDbScheduleEvent e)
    {
        var utc = e.DateUtc ?? ParseUtc(e.DateEvent, e.StrTime)
                ?? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        int extMatchId = -StableHashInt($"{e.IdEvent}|{utc:O}|{e.StrHomeTeam}|{e.StrAwayTeam}");

        return new MatchDto
        {
            Id = extMatchId,
            UtcDate = new DateTimeOffset(utc, TimeSpan.Zero),

            CompetitionId = int.TryParse(e.IdLeague, out var lid) ? lid : -1,
            CompetitionName = string.IsNullOrWhiteSpace(e.StrLeague) ? "Match" : e.StrLeague!,
            CompetitionBadgeUrl = null,

            HomeTeamId = -StableHashInt(e.StrHomeTeam),
            HomeTeamName = e.StrHomeTeam,
            HomeTeamCrestUrl = e.StrHomeTeamBadge,

            AwayTeamId = -StableHashInt(e.StrAwayTeam),
            AwayTeamName = e.StrAwayTeam,
            AwayTeamCrestUrl = e.StrAwayTeamBadge,

            HomeScore = e.IntHomeScore,
            AwayScore = e.IntAwayScore,
            Status = e.StrStatus ?? "",
            ExternalId = e.IdEvent
        };
    }

    private static int StableHashInt(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 1;
        unchecked
        {
            int h = 23;
            foreach (var ch in s)
                h = (h * 31) + ch;
            if (h == int.MinValue) h = int.MaxValue;
            return Math.Abs(h);
        }
    }


    private static DateTime? ParseUtc(string? dateEvent, string? strTime)
    {
        if (string.IsNullOrWhiteSpace(dateEvent)) return null;
        if (!DateTime.TryParse(dateEvent.Trim(), out var d)) return null;

        if (!string.IsNullOrWhiteSpace(strTime) && TimeSpan.TryParse(strTime.Trim(), out var t))
            d = d.Date.Add(t);
        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    // [HttpGet("debug/utc-range")]
    // public async Task<ActionResult<object>> DebugUtcRange()
    // {
    //     var min = await _db.Matches.MinAsync(m => (DateTime?)m.UtcDate);
    //     var max = await _db.Matches.MaxAsync(m => (DateTime?)m.UtcDate);
    //     var count = await _db.Matches.CountAsync();
    //     return Ok(new { count, min, max });
    // }

    [HttpPost("team/ensure")]
    public async Task<ActionResult<object>> EnsureTeamSchedule(
        [FromQuery] int teamId,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string? tz = null,
        CancellationToken ct = default)
    {
        if (!DateOnly.TryParse(from, out var f) || !DateOnly.TryParse(to, out var t))
            return BadRequest("from/to must be yyyy-MM-dd");

        tz ??= "America/New_York";
        var synced = await _sync.SyncTeamScheduleAsync(teamId, f, t, tz, ct);
        return Ok(new { synced });
    }

    /// <summary>Get full match detail by TheSportsDB idEvent (lookup/event + lineup + timeline + stats + highlights + tv).</summary>
    [HttpGet("event/{idEvent}")]
    public async Task<ActionResult<MatchDetailResponseDto>> GetEventByExternalId(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return BadRequest();
        var ev = await _sportsDb.LookupEventAsync(idEvent.Trim(), ct);
        if (ev == null) return NotFound();

        var lineupTask = _sportsDb.LookupEventLineupAsync(idEvent.Trim(), ct);
        var timelineTask = _sportsDb.LookupEventTimelineAsync(idEvent.Trim(), ct);
        var statsTask = _sportsDb.LookupEventStatsAsync(idEvent.Trim(), ct);
        var highlightsTask = _sportsDb.LookupEventHighlightsAsync(idEvent.Trim(), ct);
        var tvTask = _sportsDb.LookupEventTvAsync(idEvent.Trim(), ct);
        await Task.WhenAll(lineupTask, timelineTask, statsTask, highlightsTask, tvTask);

        return Ok(new MatchDetailResponseDto
        {
            Event = ev,
            Lineup = await lineupTask,
            Timeline = await timelineTask,
            Stats = await statsTask,
            Highlights = await highlightsTask,
            Tv = await tvTask
        });
    }
}
