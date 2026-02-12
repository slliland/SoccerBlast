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

    private async Task<List<MatchDto>> QueryMatches(DateTime startUtc, DateTime endUtc)
    {
        return await _db.Matches
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.UtcDate >= startUtc && m.UtcDate < endUtc)
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

    [HttpGet("today")]
    public async Task<ActionResult<List<MatchDto>>> GetToday()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        return await QueryMatches(today, tomorrow);
    }

    [HttpGet("today-local")]
    public async Task<ActionResult<List<MatchDto>>> GetTodayLocal()
    {
        var tzId = "America/New_York";
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tzId));
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(nowLocal.Date, tzId);

        var matches = await QueryMatches(startUtc, endUtc);
        return matches;
    }
    [HttpGet("date/{date}")]
    public async Task<ActionResult<List<MatchDto>>> GetByLocalDate(string date)
    {
        // Expect yyyy-MM-dd
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var localDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd, e.g. 2026-02-12.");

        var tzId = "America/New_York";
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, tzId);

        var matches = await QueryMatches(startUtc, endUtc);
        return matches;
    }
}
