using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services;

/// <summary>Reads team honours from the main DB. Data is imported from ScrapeTeamHonours cache via HonoursImportService.</summary>
public class HonoursService
{
    private readonly AppDbContext _db;

    public HonoursService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TeamHonourDto>> GetTeamHonoursAsync(string sportsDbTeamId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbTeamId))
            return new List<TeamHonourDto>();

        var tid = sportsDbTeamId.Trim();
        var teamHonours = await _db.TeamHonours
            .AsNoTracking()
            .Where(th => th.TeamId == tid)
            .Include(th => th.Honour)
            .OrderBy(th => th.Honour.Title ?? th.Honour.Slug)
            .ToListAsync(ct);

        var list = new List<TeamHonourDto>();
        foreach (var th in teamHonours)
        {
            var years = await _db.HonourWinners
                .AsNoTracking()
                .Where(hw => hw.HonourId == th.HonourId && hw.TeamId == tid)
                .OrderBy(hw => hw.YearLabel)
                .Select(hw => hw.YearLabel)
                .ToListAsync(ct);
            list.Add(new TeamHonourDto
            {
                IdHonour = th.Honour.Id,
                Slug = th.Honour.Slug,
                Title = th.Honour.Title,
                TrophyImageUrl = th.Honour.TrophyImageUrl,
                HonourUrl = th.Honour.HonourUrl,
                TypeGuess = th.Honour.TypeGuess,
                Wins = years.Count,
                WinnerYears = years.Count > 0 ? years : null
            });
        }
        return list;
    }
}
