using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;

    public VenuesController(AppDbContext db, TheSportsDbClient sportsDb)
    {
        _db = db;
        _sportsDb = sportsDb;
    }

    /// <summary>GET by id (int = DB, else by-external). Single URL, no sportsdb in path.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<VenueDetailDto>> GetByIdOrExternal(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        if (int.TryParse(id.Trim(), out var intId) && intId > 0)
        {
            var byId = await GetById(intId);
            if (byId.Result is NotFoundResult)
                return await GetByExternalId(id.Trim(), ct);
            return byId;
        }
        return await GetByExternalId(id.Trim(), ct);
    }

    [HttpGet("by-external/{sportsDbId}")]
    public async Task<ActionResult<VenueDetailDto>> GetByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var venue = await _sportsDb.LookupVenueAsync(sportsDbId.Trim(), ct);
        if (venue == null) return NotFound();
        var (lat, lng) = ParseDmsToDecimal(venue.StrMap);
        var fanarts = new List<string>();
        if (!string.IsNullOrWhiteSpace(venue.StrFanart1)) fanarts.Add(venue.StrFanart1);
        if (!string.IsNullOrWhiteSpace(venue.StrFanart2)) fanarts.Add(venue.StrFanart2);
        if (!string.IsNullOrWhiteSpace(venue.StrFanart3)) fanarts.Add(venue.StrFanart3);
        if (!string.IsNullOrWhiteSpace(venue.StrFanart4)) fanarts.Add(venue.StrFanart4);
        return new VenueDetailDto
        {
            Id = 0,
            ExternalId = venue.IdVenue,
            Name = venue.StrVenue,
            AlternateName = venue.StrAlternate,
            City = venue.StrLocation,
            Country = venue.StrCountry,
            Capacity = venue.IntCapacity,
            ImageUrl = venue.StrFanart1 ?? venue.StrThumb,
            ThumbUrl = venue.StrThumb,
            LogoUrl = venue.StrLogo,
            FormedYear = venue.IntFormedYear,
            MapCoordinates = venue.StrMap,
            Latitude = lat,
            Longitude = lng,
            Description = venue.StrDescriptionEN,
            Cost = venue.StrCost,
            Website = venue.StrWebsite,
            Timezone = venue.StrTimezone,
            FanartUrls = fanarts
        };
    }

    /// <summary>Upcoming events at this venue (v2 schedule/next/venue). Use external id e.g. 15528.</summary>
    [HttpGet("{id}/upcoming")]
    public async Task<ActionResult<List<VenueEventDto>>> GetUpcoming(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var events = await _sportsDb.GetNextVenueEventsAsync(id.Trim(), ct);
        return events.Select(MapToVenueEventDto).ToList();
    }

    /// <summary>Recent events at this venue (v2 schedule/previous/venue). Use external id e.g. 15528.</summary>
    [HttpGet("{id}/recent")]
    public async Task<ActionResult<List<VenueEventDto>>> GetRecent(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        var events = await _sportsDb.GetPreviousVenueEventsAsync(id.Trim(), ct);
        return events.Select(MapToVenueEventDto).ToList();
    }

    private static VenueEventDto MapToVenueEventDto(SportsDbScheduleEvent e) => new VenueEventDto
    {
        IdEvent = e.IdEvent,
        LeagueId = e.IdLeague,
        LeagueName = e.StrLeague,
        HomeTeamId = e.IdHomeTeam,
        HomeTeamName = e.StrHomeTeam,
        AwayTeamId = e.IdAwayTeam,
        AwayTeamName = e.StrAwayTeam,
        UtcDate = e.DateUtc,
        DateEvent = e.DateEvent,
        StrTime = e.StrTime,
        HomeScore = e.IntHomeScore,
        AwayScore = e.IntAwayScore,
        Status = e.StrStatus,
        HomeTeamBadge = e.StrHomeTeamBadge,
        AwayTeamBadge = e.StrAwayTeamBadge
    };

    private async Task<ActionResult<VenueDetailDto>> GetById(int id)
    {
        var v = await _db.Venues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();

        var dto = new VenueDetailDto
        {
            Id = v.Id,
            Name = v.Name,
            City = v.City,
            Country = v.Country,
            Capacity = v.Capacity,
            ImageUrl = v.ImageUrl
        };

        return dto;
    }

    private static (double? Lat, double? Lng) ParseDmsToDecimal(string? strMap)
    {
        if (string.IsNullOrWhiteSpace(strMap)) return (null, null);
        var s = strMap.Trim();
        var dms = Regex.Matches(s, @"(\d+)[°º]\s*(\d+)[′']\s*(\d+(?:\.\d+)?)[″""]?\s*([NSEW])");
        if (dms.Count < 2) return (null, null);
        double? lat = null, lng = null;
        foreach (Match m in dms)
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
}

