using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompetitionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<CompetitionDto>>> GetAll()
    {
        var comps = await _db.Competitions
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CompetitionDto
            {
                Id = c.Id,
                Name = c.Name,
                Country = c.Country
            })
            .ToListAsync();

        return comps;
    }
    [HttpGet("used")]
    public async Task<ActionResult<List<CompetitionUsedDto>>> GetUsed()
    {
        var used = await _db.Matches
            .AsNoTracking()
            .GroupBy(m => new { m.CompetitionId, m.Competition.Name, m.Competition.Country })
            .Select(g => new CompetitionUsedDto
            {
                Id = g.Key.CompetitionId,
                Name = g.Key.Name,
                Country = g.Key.Country,
                MatchCount = g.Count()
            })
            .OrderByDescending(x => x.MatchCount)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return used;
    }
}
