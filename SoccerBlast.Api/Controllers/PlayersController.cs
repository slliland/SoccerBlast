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
        var fanarts = new List<string>();
        if (!string.IsNullOrWhiteSpace(player.StrFanart1)) fanarts.Add(player.StrFanart1);
        if (!string.IsNullOrWhiteSpace(player.StrFanart2)) fanarts.Add(player.StrFanart2);
        if (!string.IsNullOrWhiteSpace(player.StrFanart3)) fanarts.Add(player.StrFanart3);
        if (!string.IsNullOrWhiteSpace(player.StrFanart4)) fanarts.Add(player.StrFanart4);

        var dto = new PlayerDetailDto
        {
            Id = 0,
            Name = player.StrPlayer,
            Position = player.StrPosition,
            Nationality = player.StrNationality,
            DateOfBirth = ParseDate(player.DateBorn),
            PhotoUrl = player.StrThumb,
            BannerUrl = player.StrBanner,
            InstagramUrl = player.StrInstagram,
            FacebookUrl = player.StrFacebook,
            TwitterUrl = player.StrTwitter,
            YoutubeUrl = player.StrYoutube,
            WebsiteUrl = player.StrWebsite,
            Description = player.StrDescriptionEN,
            SeasonStats = player.StrStats ?? player.StrForm,
            CurrentTeamId = null,
            CurrentTeamName = player.StrTeam,
            Status = player.StrStatus,
            Gender = player.StrGender,
            Side = player.StrSide,
            College = player.StrCollege,
            Height = player.StrHeight,
            Weight = player.StrWeight,
            PosterUrl = player.StrPoster,
            Agent = player.StrAgent,
            BirthLocation = player.StrBirthLocation,
            Ethnicity = player.StrEthnicity,
            Wage = player.StrWage,
            CutoutUrl = player.StrCutout,
            ThumbUrl = player.StrThumb,
            CartoonUrl = player.StrCartoon,
            RenderUrl = player.StrRender,
            KitUrl = player.StrKit,
            FanartUrls = fanarts.Count > 0 ? fanarts : null
        };
        return Ok(dto);
    }

    [HttpGet("{id}/contracts")]
    public async Task<ActionResult<List<PlayerContractDto>>> GetContracts(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return Ok(new List<PlayerContractDto>());
        var playerId = await ResolvePlayerExternalIdAsync(id.Trim(), ct);
        if (playerId == null) return Ok(new List<PlayerContractDto>());
        var list = await _sportsDb.LookupPlayerContractsAsync(playerId, ct);
        return Ok(list);
    }

    [HttpGet("{id}/former-teams")]
    public async Task<ActionResult<List<PlayerFormerTeamDto>>> GetFormerTeams(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return Ok(new List<PlayerFormerTeamDto>());
        var playerId = await ResolvePlayerExternalIdAsync(id.Trim(), ct);
        if (playerId == null) return Ok(new List<PlayerFormerTeamDto>());
        var list = await _sportsDb.LookupPlayerFormerTeamsAsync(playerId, ct);
        return Ok(list);
    }

    [HttpGet("{id}/honours")]
    public async Task<ActionResult<List<PlayerHonourDto>>> GetHonours(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return Ok(new List<PlayerHonourDto>());
        var playerId = await ResolvePlayerExternalIdAsync(id.Trim(), ct);
        if (playerId == null) return Ok(new List<PlayerHonourDto>());
        var list = await _sportsDb.LookupPlayerHonoursAsync(playerId, ct);
        return Ok(list);
    }

    [HttpGet("{id}/milestones")]
    public async Task<ActionResult<List<PlayerMilestoneDto>>> GetMilestones(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return Ok(new List<PlayerMilestoneDto>());
        var playerId = await ResolvePlayerExternalIdAsync(id.Trim(), ct);
        if (playerId == null) return Ok(new List<PlayerMilestoneDto>());
        var list = await _sportsDb.LookupPlayerMilestonesAsync(playerId, ct);
        return Ok(list);
    }

    [HttpGet("{id}/results")]
    public async Task<ActionResult<List<PlayerResultDto>>> GetResults(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return Ok(new List<PlayerResultDto>());
        var playerId = await ResolvePlayerExternalIdAsync(id.Trim(), ct);
        if (playerId == null) return Ok(new List<PlayerResultDto>());
        var list = await _sportsDb.LookupPlayerResultsAsync(playerId, ct);
        return Ok(list);
    }

    /// <summary>Player enrichment endpoints use SportsDB id. Page is typically /player/{sportsDbId}; we pass id through.</summary>
    private static Task<string?> ResolvePlayerExternalIdAsync(string id, CancellationToken _)
    {
        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(id) ? null : id.Trim());
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

