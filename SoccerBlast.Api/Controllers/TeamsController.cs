using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Shared.Contracts;
using SoccerBlast.Api.Services;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TeamProfileSyncService _profileSync;
    public TeamsController(AppDbContext db, TeamProfileSyncService profileSync /* + others */)
    {
        _db = db;
        _profileSync = profileSync;
    }

    // GET /api/teams/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TeamDetailDto>> GetById(int id, CancellationToken ct = default)
    {
        // Run team, profile, and mapping queries in parallel for faster response
        var teamTask = _db.Teams
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Name, t.CrestUrl })
            .FirstOrDefaultAsync(ct);

        var profileTask = _db.TeamProfiles
            .AsNoTracking()
            .Where(p => p.TeamId == id)
            .Select(p => new TeamProfileDto
            {
                TeamId = p.TeamId,
                FormedYear = p.FormedYear,
                Location = p.Location,
                Keywords = p.Keywords,
                StadiumName = p.StadiumName,
                StadiumCapacity = p.StadiumCapacity,
                StadiumLocation = p.StadiumLocation,
                Leagues = p.Leagues,
                DescriptionEn = p.DescriptionEn,
                BannerUrl = p.BannerUrl,
                JerseyUrl = p.JerseyUrl,
                BadgeUrl = p.BadgeUrl,
                LogoUrl = p.LogoUrl,
                PrimaryColor = p.PrimaryColor,
                SecondaryColor = p.SecondaryColor,
                TertiaryColor = p.TertiaryColor,
                Website = p.Website,
                Facebook = p.Facebook,
                Twitter = p.Twitter,
                Instagram = p.Instagram,
                Youtube = p.Youtube,
                LastUpdatedUtc = p.LastUpdatedUtc
            })
            .FirstOrDefaultAsync(ct);

        var sportsDbIdTask = _db.TeamExternalMaps
            .AsNoTracking()
            .Where(m => m.TeamId == id && m.Provider == "SportsDB")
            .Select(m => m.ExternalId)
            .FirstOrDefaultAsync(ct);

        await Task.WhenAll(teamTask, profileTask, sportsDbIdTask);

        var team = await teamTask;
        if (team == null) return NotFound();

        var dto = new TeamDetailDto
        {
            Id = team.Id,
            Name = team.Name,
            CrestUrl = team.CrestUrl,
            SportsDbTeamId = await sportsDbIdTask,
            Profile = await profileTask
        };

        return Ok(dto);
    }

    [HttpPost("{id:int}/sync-profile")]
    public async Task<ActionResult<TeamDetailDto>> SyncProfile(int id, CancellationToken ct)
    {
        var (ok, msg) = await _profileSync.SyncTeamProfileAsync(id, ct);
        if (!ok) return NotFound(new { message = msg });

        // return the updated team immediately
        return await GetById(id, ct);
    }

    [HttpGet("{id:int}/players")]
    public async Task<ActionResult<List<TeamPlayerDto>>> GetPlayers(int id, CancellationToken ct)
    {
        // Get the SportsDB team ID
        var sportsDbId = await _db.TeamExternalMaps
            .AsNoTracking()
            .Where(m => m.TeamId == id && m.Provider == "SportsDB")
            .Select(m => m.ExternalId)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(sportsDbId))
        {
            return Ok(new List<TeamPlayerDto>());
        }

        // Fetch players from TheSportsDB
        var sportsDbClient = HttpContext.RequestServices.GetRequiredService<TheSportsDbClient>();
        var players = await sportsDbClient.GetTeamPlayersAsync(sportsDbId, ct);

        return Ok(players);
    }
}
