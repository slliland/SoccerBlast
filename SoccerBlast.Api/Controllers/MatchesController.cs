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

    public MatchesController(AppDbContext db)
    {
        _db = db;
    }

    private async Task<List<MatchDto>> QueryMatches(DateTime startUtc, DateTime endUtc, int? competitionId = null)
    {
        var q = _db.Matches
            .AsNoTracking()
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.UtcDate >= startUtc && m.UtcDate < endUtc);

        if (competitionId.HasValue)
            q = q.Where(m => m.CompetitionId == competitionId.Value);

        return await q
            .OrderBy(m => m.UtcDate)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                UtcDate = m.UtcDate,
                CompetitionName = m.Competition.Name,
                HomeTeamName = m.HomeTeam.Name,
                AwayTeamName = m.AwayTeam.Name,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                Status = m.Status
            })
            .ToListAsync();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<MatchDto>>> Search(
        [FromQuery] string team,
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] int? competitionId,
        [FromQuery] int limit = 200)
    {
        if (string.IsNullOrWhiteSpace(team))
            return BadRequest("team is required.");

        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
        {
            return BadRequest("from/to must be yyyy-MM-dd, e.g. from=2026-02-01&to=2026-02-28");
        }

        if (limit <= 0) limit = 200;
        if (limit > 500) limit = 500;

        var tzId = "America/New_York";

        // Convert local from/to dates into an inclusive local range:
        // [fromLocal 00:00, (toLocal+1) 00:00)
        var (startUtc, _) = DateRangeService.GetUtcRangeForLocalDate(fromLocal, tzId);
        var (_, endUtc) = DateRangeService.GetUtcRangeForLocalDate(toLocal.AddDays(1), tzId);

        var term = team.Trim().ToLower();

        var q = _db.Matches
            .AsNoTracking()
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.UtcDate >= startUtc && m.UtcDate < endUtc)
            .Where(m =>
                m.HomeTeam.Name.ToLower().Contains(term) ||
                m.AwayTeam.Name.ToLower().Contains(term));

        if (competitionId.HasValue)
            q = q.Where(m => m.CompetitionId == competitionId.Value);

        var results = await q
            .OrderBy(m => m.UtcDate)
            .Take(limit)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                UtcDate = m.UtcDate,
                CompetitionName = m.Competition.Name,
                HomeTeamName = m.HomeTeam.Name,
                AwayTeamName = m.AwayTeam.Name,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                Status = m.Status
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
    public async Task<ActionResult<List<MatchDto>>> GetTodayLocal([FromQuery] int? competitionId)
    {
        var tzId = "America/New_York";
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tzId));
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(nowLocal.Date, tzId);

        var matches = await QueryMatches(startUtc, endUtc, competitionId);
        return matches;
    }
    [HttpGet("date/{date}")]
    public async Task<ActionResult<List<MatchDto>>> GetByLocalDate(string date, [FromQuery] int? competitionId)
    {
        // Expect yyyy-MM-dd
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var localDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd, e.g. 2026-02-12.");

        var tzId = "America/New_York";
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, tzId);

        var matches = await QueryMatches(startUtc, endUtc, competitionId);
        return matches;
    }
}
