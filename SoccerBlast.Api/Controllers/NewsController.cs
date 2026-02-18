using Microsoft.AspNetCore.Mvc;
using SoccerBlast.Api.Services;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly NewsService _news;

    public NewsController(NewsService news)
    {
        _news = news;
    }

    // GET /api/news/recent?limit=10
    [HttpGet("recent")]
    public async Task<ActionResult<List<NewsDto>>> Recent([FromQuery] int limit = 10)
        => Ok(await _news.GetRecentAsync(limit));

    // GET /api/news/recommended?teamIds=1&teamIds=2&limit=20
    [HttpGet("recommended")]
    public async Task<ActionResult<List<NewsDto>>> Recommended(
        [FromQuery] List<int> teamIds,
        [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);

        if (teamIds == null || teamIds.Count == 0)
            return Ok(await _news.GetRecentAsync(limit));

        return Ok(await _news.GetRecommendedAsync(teamIds, limit));
    }

    // POST /api/news/refresh
    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh()
    {
        await _news.ClearCacheAndRefreshAsync();
        return Ok(new { message = "News cache cleared and refreshed" });
    }
}
