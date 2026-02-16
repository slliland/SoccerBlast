using Microsoft.AspNetCore.Mvc;
using SoccerBlast.Api.Services.Video;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/videos")]
public sealed class VideosController : ControllerBase
{
    private readonly VideoAggregator _agg;

    public VideosController(VideoAggregator agg) => _agg = agg;

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<VideoDto>>> Recent(
        [FromQuery] int limit = 200,
        [FromQuery] int? competitionId = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? q = null,
        [FromQuery] string? source = null,
        [FromQuery] DateTimeOffset? sinceUtc = null,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 500)
        {
            ModelState.AddModelError(nameof(limit), "limit must be between 1 and 500.");
            return ValidationProblem(ModelState);
        }

        var res = await _agg.GetRecentAsync(limit, competitionId, tag, q, source, sinceUtc, ct);
        return Ok(res);
    }
}
