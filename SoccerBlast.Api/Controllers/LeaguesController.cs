using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Shared.Contracts;
using System.Globalization;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaguesController : ControllerBase
{
    private readonly AppDbContext _db;

    public LeaguesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<List<LeagueDto>>> GetLeaguesByLocalDate(
        string date,
        [FromQuery] string? tz)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDate))
            return BadRequest("Invalid date format. Use yyyy-MM-dd.");

        tz ??= "America/New_York";
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate.Date, tz);

        var leagues = await _db.Matches
            .AsNoTracking()
            .Where(m => m.UtcDate >= startUtc && m.UtcDate < endUtc)
            .GroupBy(m => new { m.CompetitionId, m.Competition.Name, m.Competition.Country, m.Competition.BadgeUrl })
            .Select(g => new LeagueDto
            {
                CompetitionId = g.Key.CompetitionId,
                Name = g.Key.Name,
                Country = g.Key.Country,
                BadgeUrl = g.Key.BadgeUrl,
                MatchCount = g.Count(),
                LiveCount = g.Count(x => x.Status == "IN_PLAY" || x.Status == "PAUSED" || x.Status == "LIVE")
            })
            .OrderByDescending(x => x.LiveCount > 0)
            .ThenByDescending(x => x.MatchCount)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return leagues;
    }
}
