using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

public class MatchSyncService
{
    private readonly AppDbContext _db;
    private readonly FootballDataClient _client;

    public MatchSyncService(AppDbContext db, FootballDataClient client)
    {
        _db = db;
        _client = client;
    }

    public async Task<int> SyncTodayAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var items = await _client.GetMatchesAsync(today, tomorrow);

        foreach (var m in items)
        {
            // Upsert Competition
            var comp = await _db.Competitions.FindAsync(m.Competition.Id);
            if (comp == null)
            {
                comp = new Competition
                {
                    Id = m.Competition.Id,
                    Name = m.Competition.Name,
                    Country = m.Competition.Area?.Name
                };
                _db.Competitions.Add(comp);
            }
            else
            {
                comp.Name = m.Competition.Name;
                comp.Country = m.Competition.Area?.Name;
            }

            // Upsert Teams
            await UpsertTeamAsync(m.HomeTeam);
            await UpsertTeamAsync(m.AwayTeam);

            // Upsert Match
            var match = await _db.Matches.FindAsync(m.Id);
            if (match == null)
            {
                match = new Match
                {
                    Id = m.Id,
                    UtcDate = DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc),
                    Status = m.Status,
                    CompetitionId = m.Competition.Id,
                    HomeTeamId = m.HomeTeam.Id,
                    AwayTeamId = m.AwayTeam.Id,
                    HomeScore = m.Score.FullTime.Home,
                    AwayScore = m.Score.FullTime.Away
                };
                _db.Matches.Add(match);
            }
            else
            {
                match.UtcDate = DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc);
                match.Status = m.Status;
                match.CompetitionId = m.Competition.Id;
                match.HomeTeamId = m.HomeTeam.Id;
                match.AwayTeamId = m.AwayTeam.Id;
                match.HomeScore = m.Score.FullTime.Home;
                match.AwayScore = m.Score.FullTime.Away;
            }
        }

        await _db.SaveChangesAsync();
        return items.Count;
    }

    private async Task UpsertTeamAsync(TeamItem t)
    {
        var team = await _db.Teams.FindAsync(t.Id);
        if (team == null)
        {
            _db.Teams.Add(new Team { Id = t.Id, Name = t.Name });
        }
        else
        {
            team.Name = t.Name;
        }
    }
    
    public async Task<int> SyncLocalDateAsync(DateTime localDate, string timeZoneId = "America/New_York")
    {
        // 1) local day -> UTC range
        var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, timeZoneId);

        // 2) football-data is date-based. dateTo is exclusive, so add +1 day to cover endUtc.Date fully.
        var dateFrom = startUtc.Date;
        var dateTo = endUtc.Date.AddDays(1);

        var items = await _client.GetMatchesAsync(dateFrom, dateTo);

        int kept = 0;

        foreach (var m in items)
        {
            // 3) Only keep matches inside the exact UTC time range for that local day
            var matchUtc = DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc);
            if (matchUtc < startUtc || matchUtc >= endUtc) continue;

            kept++;

            // Upsert Competition
            var comp = await _db.Competitions.FindAsync(m.Competition.Id);
            if (comp == null)
            {
                comp = new Competition
                {
                    Id = m.Competition.Id,
                    Name = m.Competition.Name,
                    Country = m.Competition.Area?.Name
                };
                _db.Competitions.Add(comp);
            }
            else
            {
                comp.Name = m.Competition.Name;
                comp.Country = m.Competition.Area?.Name;
            }

            // Upsert Teams
            await UpsertTeamAsync(m.HomeTeam);
            await UpsertTeamAsync(m.AwayTeam);

            // Upsert Match
            var match = await _db.Matches.FindAsync(m.Id);
            if (match == null)
            {
                match = new Match
                {
                    Id = m.Id,
                    UtcDate = matchUtc,
                    Status = m.Status,
                    CompetitionId = m.Competition.Id,
                    HomeTeamId = m.HomeTeam.Id,
                    AwayTeamId = m.AwayTeam.Id,
                    HomeScore = m.Score.FullTime.Home,
                    AwayScore = m.Score.FullTime.Away
                };
                _db.Matches.Add(match);
            }
            else
            {
                match.UtcDate = matchUtc;
                match.Status = m.Status;
                match.CompetitionId = m.Competition.Id;
                match.HomeTeamId = m.HomeTeam.Id;
                match.AwayTeamId = m.AwayTeam.Id;
                match.HomeScore = m.Score.FullTime.Home;
                match.AwayScore = m.Score.FullTime.Away;
            }
        }

        await _db.SaveChangesAsync();
        return kept;
    }
}
