using Microsoft.AspNetCore.Mvc;
using SoccerBlast.Api.Services;
using System.Globalization;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/admin/sync")]
public class AdminSyncController : ControllerBase
{
    private readonly MatchSyncService _sync;

    public AdminSyncController(MatchSyncService sync)
    {
        _sync = sync;
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
}
