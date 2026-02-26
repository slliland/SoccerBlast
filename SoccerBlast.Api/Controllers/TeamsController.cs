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
    private readonly PlayerRosterService _roster;
    private readonly HonoursService _honours;

    public TeamsController(AppDbContext db, TeamProfileSyncService profileSync, TheSportsDbClient sportsDb, PlayerRosterService roster, HonoursService honours)
    {
        _db = db;
        _profileSync = profileSync;
        _sportsDb = sportsDb;
        _roster = roster;
        _honours = honours;
    }

    /// <summary>GET by id: int = DB team (SportsDB id is Team.SportsDbId or Team.Id); non-int or not found = lookup by SportsDB id (API only).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TeamDetailDto>> GetByIdOrExternal(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var dto = await GetTeamDetailInternalAsync(id.Trim(), ct);
        return dto != null ? Ok(dto) : NotFound();
    }

    /// <summary>GET team by TheSportsDB v2 external id (no local team). Used when opening from search v2 results.</summary>
    [HttpGet("by-external/{sportsDbId}")]
    public async Task<ActionResult<TeamDetailDto>> GetByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var dto = await GetTeamDetailByExternalIdAsync(sportsDbId.Trim(), ct);
        return dto != null ? Ok(dto) : NotFound();
    }

    /// <summary>Single place for team resolution. Int id = try DB first, then external; else treat as external id. Returns null if not found.</summary>
    private async Task<TeamDetailDto?> GetTeamDetailInternalAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (int.TryParse(id, out var intId) && intId > 0)
        {
            var byId = await GetTeamDetailByIdAsync(intId, ct);
            if (byId != null)
            {
                if (byId.Profile == null)
                {
                    await _profileSync.SyncTeamProfileAsync(intId, ct);
                    byId = await GetTeamDetailByIdAsync(intId, ct);
                }
                return byId;
            }
            return await GetTeamDetailByExternalIdAsync(id, ct);
        }
        return await GetTeamDetailByExternalIdAsync(id, ct);
    }

    private async Task<TeamDetailDto?> GetTeamDetailByIdAsync(int id, CancellationToken ct)
    {
        var team = await _db.Teams
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.Id, t.Name, t.CrestUrl, t.SportsDbId })
            .FirstOrDefaultAsync(ct);
        if (team == null) return null;

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

        return new TeamDetailDto
        {
            Id = team.Id,
            Name = team.Name,
            CrestUrl = team.CrestUrl,
            SportsDbTeamId = sportsDbId,
            Profile = profile
        };
    }

    private async Task<TeamDetailDto?> GetTeamDetailByExternalIdAsync(string sportsDbId, CancellationToken ct)
    {
        var v2Team = await _sportsDb.LookupTeamAsync(sportsDbId, ct);
        if (v2Team == null) return null;

        var profile = SportsDbTeamMapper.ToTeamProfileDto(v2Team, 0);
        if (profile.Leagues != null)
            profile.LeaguesWithBadges = await ResolveLeagueBadgesAsync(profile.Leagues, ct);

        return new TeamDetailDto
        {
            Id = 0,
            Name = v2Team.StrTeam ?? "Unknown",
            CrestUrl = v2Team.StrBadge ?? v2Team.StrLogo,
            SportsDbTeamId = v2Team.IdTeam,
            Profile = profile
        };
    }

    [HttpGet("by-external/{sportsDbId}/players")]
    public async Task<ActionResult<List<TeamPlayerDto>>> GetPlayersByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return Ok(new List<TeamPlayerDto>());
        var players = await _roster.GetEnrichedRosterAsync(sportsDbId.Trim(), ct);
        return Ok(players);
    }

    [HttpPost("{id:int}/sync-profile")]
    public async Task<ActionResult<TeamDetailDto>> SyncProfile(int id, CancellationToken ct)
    {
        var (ok, msg) = await _profileSync.SyncTeamProfileAsync(id, ct);
        if (!ok) return NotFound(new { message = msg });

        var dto = await GetTeamDetailByIdAsync(id, ct);
        return dto != null ? Ok(dto) : NotFound();
    }

    [HttpGet("{id:int}/players")]
    public async Task<ActionResult<List<TeamPlayerDto>>> GetPlayers(int id, CancellationToken ct)
    {
        var team = await _db.Teams.AsNoTracking().Where(t => t.Id == id).Select(t => new { t.SportsDbId }).FirstOrDefaultAsync(ct);
        if (team == null) return NotFound();
        var sportsDbId = !string.IsNullOrWhiteSpace(team.SportsDbId) ? team.SportsDbId : id.ToString();
        var players = await _roster.GetEnrichedRosterAsync(sportsDbId, ct);
        return Ok(players);
    }

    /// <summary>GET venue/stadium for this team. Resolves team to get stadium name, then search + lookup venue. If lookup fails, returns minimal DTO from team profile so UI can still show name/capacity/location.</summary>
    [HttpGet("{id}/venue")]
    public async Task<ActionResult<StadiumDetailDto>> GetTeamVenue(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var team = await GetTeamDetailInternalAsync(id.Trim(), ct);
        var profile = team?.Profile;
        if (profile?.StadiumName is not { } stadiumName || string.IsNullOrWhiteSpace(stadiumName))
            return NotFound();

        var searchResults = await _sportsDb.SearchVenuesAsync(stadiumName, ct);
        var first = searchResults.FirstOrDefault();
        if (first != null)
        {
            var venue = await _sportsDb.LookupVenueAsync(first.IdVenue, ct);
            if (venue != null)
            {
                var (lat, lng) = ParseDmsToDecimal(venue.StrMap);
                var fanarts = new List<string>();
                if (!string.IsNullOrWhiteSpace(venue.StrFanart1)) fanarts.Add(venue.StrFanart1);
                if (!string.IsNullOrWhiteSpace(venue.StrFanart2)) fanarts.Add(venue.StrFanart2);
                if (!string.IsNullOrWhiteSpace(venue.StrFanart3)) fanarts.Add(venue.StrFanart3);
                if (!string.IsNullOrWhiteSpace(venue.StrFanart4)) fanarts.Add(venue.StrFanart4);
                return Ok(new StadiumDetailDto
                {
                    Id = venue.IdVenue,
                    Name = venue.StrVenue,
                    ThumbUrl = venue.StrThumb,
                    FanartUrls = fanarts,
                    FormedYear = venue.IntFormedYear,
                    MapCoordinates = venue.StrMap,
                    Latitude = lat,
                    Longitude = lng,
                    Description = venue.StrDescriptionEN,
                    Capacity = venue.IntCapacity,
                    Location = venue.StrLocation,
                    Country = venue.StrCountry,
                    Cost = venue.StrCost,
                });
            }
        }

        return Ok(new StadiumDetailDto
        {
            Id = "",
            Name = stadiumName,
            ThumbUrl = null,
            FanartUrls = new List<string>(),
            FormedYear = null,
            MapCoordinates = null,
            Latitude = null,
            Longitude = null,
            Description = null,
            Capacity = profile.StadiumCapacity,
            Location = profile.StadiumLocation,
            Country = null,
            Cost = null,
        });
    }

    private static (double? Lat, double? Lng) ParseDmsToDecimal(string? strMap)
    {
        if (string.IsNullOrWhiteSpace(strMap)) return (null, null);
        var s = strMap.Trim();
        // Allow decimal seconds (e.g. 16.08″) so "03°15′16.08″S 79°57′44.99″W" parses correctly
        var dms = System.Text.RegularExpressions.Regex.Matches(s, @"(\d+)[°º]\s*(\d+)[′']\s*(\d+(?:\.\d+)?)[″""]?\s*([NSEW])");
        if (dms.Count < 2) return (null, null);
        double? lat = null, lng = null;
        foreach (System.Text.RegularExpressions.Match m in dms)
        {
            if (!int.TryParse(m.Groups[1].Value, out var d) || !int.TryParse(m.Groups[2].Value, out var min) || !double.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sec))
                continue;
            var dec = d + min / 60.0 + sec / 3600.0;
            var dir = m.Groups[4].Value[0];
            if (dir == 'S') dec = -dec;
            if (dir == 'W') dec = -dec;
            if (dir == 'N' || dir == 'S') lat = dec;
            else lng = dec;
        }
        return (lat, lng);
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

        var compIdByName = comps.ToDictionary(c => c.Name, c => (int?)c.Id);
        return names.Select(n => new LeagueEntryDto(n, badgeByNames.GetValueOrDefault(n), compIdByName.GetValueOrDefault(n))).ToList();
    }

    /// <summary>GET honours/trophies for a team (TheSportsDB id). Reads from honours ETL cache if available.</summary>
    [HttpGet("honours/{sportsDbTeamId}")]
    public async Task<ActionResult<List<TeamHonourDto>>> GetTeamHonours(string sportsDbTeamId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbTeamId)) return NotFound();
        var list = await _honours.GetTeamHonoursAsync(sportsDbTeamId.Trim(), ct);
        return Ok(list);
    }
}
