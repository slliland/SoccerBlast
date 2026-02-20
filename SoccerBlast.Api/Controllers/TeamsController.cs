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
    private readonly TheSportsDbClient _sportsDb;

    public TeamsController(AppDbContext db, TeamProfileSyncService profileSync, TheSportsDbClient sportsDb)
    {
        _db = db;
        _profileSync = profileSync;
        _sportsDb = sportsDb;
    }

    /// <summary>GET by id: int = DB team (SportsDB id is Team.SportsDbId or Team.Id); non-int or not found = lookup by SportsDB id (API only).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TeamDetailDto>> GetByIdOrExternal(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var trimmed = id.Trim();
        if (int.TryParse(trimmed, out var intId) && intId > 0)
        {
            var byId = await GetById(intId, ct);
            if (byId.Result is NotFoundResult)
                return await GetByExternalId(trimmed, ct);
            if (byId.Value?.Profile == null)
            {
                await _profileSync.SyncTeamProfileAsync(intId, ct);
                return await GetById(intId, ct);
            }
            return byId;
        }
        return await GetByExternalId(trimmed, ct);
    }

    /// <summary>GET team by TheSportsDB v2 external id (no local team). Used when opening from search v2 results.</summary>
    [HttpGet("by-external/{sportsDbId}")]
    public async Task<ActionResult<TeamDetailDto>> GetByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var v2Team = await _sportsDb.LookupTeamAsync(sportsDbId.Trim(), ct);
        if (v2Team == null) return NotFound();

        var profile = SportsDbTeamMapper.ToTeamProfileDto(v2Team, 0);
        if (profile.Leagues != null)
            profile.LeaguesWithBadges = await ResolveLeagueBadgesAsync(profile.Leagues, ct);

        return Ok(new TeamDetailDto
        {
            Id = 0,
            Name = v2Team.StrTeam ?? "Unknown",
            CrestUrl = v2Team.StrBadge ?? v2Team.StrLogo,
            SportsDbTeamId = v2Team.IdTeam,
            Profile = profile
        });
    }

    [HttpGet("by-external/{sportsDbId}/players")]
    public async Task<ActionResult<List<TeamPlayerDto>>> GetPlayersByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return Ok(new List<TeamPlayerDto>());
        var players = await _sportsDb.GetTeamPlayersAsync(sportsDbId.Trim(), ct);
        return Ok(players);
    }

    private async Task<ActionResult<TeamDetailDto>> GetById(int id, CancellationToken ct = default)
    {
        var team = await _db.Teams
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Name, t.CrestUrl, t.SportsDbId })
            .FirstOrDefaultAsync(ct);
        if (team == null) return NotFound();

        var sportsDbId = !string.IsNullOrWhiteSpace(team.SportsDbId) ? team.SportsDbId : id.ToString();
        var profile = await _db.TeamProfiles
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

        if (!string.IsNullOrEmpty(sportsDbId))
        {
            var v2Team = await _sportsDb.LookupTeamAsync(sportsDbId, ct);
            if (v2Team != null)
                profile = SportsDbTeamMapper.ToTeamProfileDto(v2Team, id);
        }

        if (profile?.Leagues != null)
            profile.LeaguesWithBadges = await ResolveLeagueBadgesAsync(profile.Leagues, ct);

        return Ok(new TeamDetailDto
        {
            Id = team.Id,
            Name = team.Name,
            CrestUrl = team.CrestUrl,
            SportsDbTeamId = sportsDbId,
            Profile = profile
        });
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
        var team = await _db.Teams.AsNoTracking().Where(t => t.Id == id).Select(t => new { t.SportsDbId }).FirstOrDefaultAsync(ct);
        if (team == null) return NotFound();
        var sportsDbId = !string.IsNullOrWhiteSpace(team.SportsDbId) ? team.SportsDbId : id.ToString();
        var players = await _sportsDb.GetTeamPlayersAsync(sportsDbId, ct);
        return Ok(players);
    }

    /// <summary>Resolve badge URL for each league name: DB first, then v2 search/league for missing.</summary>
    private async Task<List<LeagueEntryDto>> ResolveLeagueBadgesAsync(string leaguesCommaSeparated, CancellationToken ct)
    {
        var names = leaguesCommaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        if (names.Count == 0) return new List<LeagueEntryDto>();

        var comps = await _db.Competitions
            .AsNoTracking()
            .Where(c => names.Contains(c.Name))
            .ToListAsync(ct);
        var badgeByNames = comps
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First().BadgeUrl);

        var missing = names.Where(n => string.IsNullOrWhiteSpace(badgeByNames.GetValueOrDefault(n))).ToList();
        if (missing.Count > 0)
        {
            var tasks = missing.Select(async n =>
            {
                try
                {
                    var badge = await _sportsDb.GetLeagueBadgeBySearchAsync(n, ct);
                    return (n, badge);
                }
                catch
                {
                    return (n, (string?)null);
                }
            });
            var results = await Task.WhenAll(tasks);
            foreach (var (n, badge) in results)
            {
                if (!string.IsNullOrWhiteSpace(badge))
                    badgeByNames[n] = badge;
            }
        }

        return names.Select(n => new LeagueEntryDto(n, badgeByNames.GetValueOrDefault(n))).ToList();
    }
}
