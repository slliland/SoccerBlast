using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;

    public PlayersController(AppDbContext db, TheSportsDbClient sportsDb)
    {
        _db = db;
        _sportsDb = sportsDb;
    }

    /// <summary>GET by id (int = DB, else by-external). Single URL, no sportsdb in path.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PlayerDetailDto>> GetByIdOrExternal(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        if (int.TryParse(id.Trim(), out var intId) && intId > 0)
        {
            var byId = await GetById(intId);
            if (byId.Result is NotFoundResult)
                return await GetByExternalId(id.Trim());
            return byId;
        }
        return await GetByExternalId(id.Trim());
    }

    /// <summary>GET player by TheSportsDB v2 id only (no DB). API-first.</summary>
    [HttpGet("by-external/{sportsDbId}")]
    public async Task<ActionResult<PlayerDetailDto>> GetByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var player = await _sportsDb.LookupPlayerAsync(sportsDbId.Trim(), ct);
        if (player == null) return NotFound();
        var dto = new PlayerDetailDto
        {
            Id = 0,
            Name = player.StrPlayer,
            Position = player.StrPosition,
            Nationality = player.StrNationality,
            DateOfBirth = ParseDate(player.DateBorn),
            PhotoUrl = player.StrThumb,
            CurrentTeamId = null,
            CurrentTeamName = player.StrTeam
        };
        return dto;
    }

    private async Task<ActionResult<PlayerDetailDto>> GetById(int id)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.CurrentTeam)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (player == null) return NotFound();

        var dto = new PlayerDetailDto
        {
            Id = player.Id,
            Name = player.Name,
            Position = player.Position,
            Nationality = player.Nationality,
            DateOfBirth = player.DateOfBirth,
            PhotoUrl = player.PhotoUrl,
            CurrentTeamId = player.CurrentTeamId,
            CurrentTeamName = player.CurrentTeam?.Name
        };

        return Ok(dto);
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s.Trim(), out var d) ? d : null;
    }
}

