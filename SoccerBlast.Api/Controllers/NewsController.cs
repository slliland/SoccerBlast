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
    {
        var items = await _news.GetRecentAsync(limit);
        return Ok(items);
    }
}
