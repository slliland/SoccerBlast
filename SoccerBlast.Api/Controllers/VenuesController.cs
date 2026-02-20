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
        return new VenueDetailDto
        {
            Id = 0,
            Name = venue.StrVenue,
            City = venue.StrLocation,
            Country = venue.StrCountry,
            Capacity = venue.IntCapacity,
            ImageUrl = venue.StrThumb
        };
    }

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
}

