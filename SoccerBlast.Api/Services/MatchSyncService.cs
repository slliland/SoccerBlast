using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

public class MatchSyncService
{
    private readonly AppDbContext _db;
    private readonly FootballDataClient _client;

    private async Task<bool> IsTooSoonToResyncAsync(DateTime localDate, string syncType, int seconds = 60)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-seconds);

        return await _db.SyncLogs.AnyAsync(l =>
            l.SyncType == syncType &&
            l.LocalDate == localDate.Date &&
            l.Success == true &&
            l.FinishedAtUtc >= cutoff);
    }


    public MatchSyncService(AppDbContext db, FootballDataClient client)
    {
        _db = db;
        _client = client;
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

    public async Task<int> SyncLocalDateAsync(DateTime localDate, string timeZoneId = "America/New_York", string syncType = "DATE")
    {
        localDate = localDate.Date;

        // Rate-limit protection
        if (await IsTooSoonToResyncAsync(localDate, syncType, seconds: 60))
            return 0;

        var log = new SyncLog
        {
            SyncType = syncType,
            LocalDate = localDate,
            StartedAtUtc = DateTime.UtcNow,
            Success = false,
            SyncedMatches = 0
        };

        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync(); // save early so status endpoint can see "in progress"

        try
        {
            // local day to UTC range
            var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, timeZoneId);

            // football-data works with dates, dateTo is exclusive
            var dateFrom = startUtc.Date;
            var dateTo = endUtc.Date.AddDays(1);

            var items = await _client.GetMatchesAsync(dateFrom, dateTo);

            // Filter first and don't delete if nothing is relevant
            var filtered = items
                .Select(m => (m, matchUtc: DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc)))
                .Where(x => x.matchUtc >= startUtc && x.matchUtc < endUtc)
                .ToList();
            
            // Transaction, delete and insert as one unit
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Delete all matches in that UTC window
            await _db.Matches
                .Where(x => x.UtcDate >= startUtc && x.UtcDate < endUtc)
                .ExecuteDeleteAsync();

            // Re-insert matches fresh plus ensure teams/competitions exist
            foreach (var (m, matchUtc) in filtered)
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
                _db.Matches.Add(new Match
                {
                    Id = m.Id,
                    UtcDate = matchUtc,
                    Status = m.Status,
                    CompetitionId = m.Competition.Id,
                    HomeTeamId = m.HomeTeam.Id,
                    AwayTeamId = m.AwayTeam.Id,
                    HomeScore = m.Score.FullTime.Home,
                    AwayScore = m.Score.FullTime.Away
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            log.Success = true;
            log.SyncedMatches = filtered.Count;
            log.FinishedAtUtc = DateTime.UtcNow;
            log.ErrorMessage = null;

            await _db.SaveChangesAsync();
            return filtered.Count;
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.FinishedAtUtc = DateTime.UtcNow;
            log.ErrorMessage = ex.Message;

            await _db.SaveChangesAsync();
            throw; // so Swagger shows the error too (during dev)
        }
    }

    public Task<int> SyncTodayAsync()
    {
        var tzId = "America/New_York";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;

        return SyncLocalDateAsync(todayLocal, tzId, syncType: "TODAY");
    }

    public async Task<(int daysSynced, int matchesSynced)> SyncRangeAsync(
        DateTime fromLocal,
        DateTime toLocal,
        string timeZoneId = "America/New_York")
    {
        fromLocal = fromLocal.Date;
        toLocal = toLocal.Date;

        if (toLocal < fromLocal)
            throw new ArgumentException("toLocal must be >= fromLocal");

        // Build a UTC range that fully covers the local-date range
        var (startUtc, _) = DateRangeService.GetUtcRangeForLocalDate(fromLocal, timeZoneId);
        var (_, endUtc) = DateRangeService.GetUtcRangeForLocalDate(toLocal.AddDays(1), timeZoneId);

        // football-data uses dateFrom/dateTo (dateTo exclusive)
        var dateFrom = startUtc.Date;
        var dateTo = endUtc.Date.AddDays(1);

        // ONE provider call for the whole range
        var items = await _client.GetMatchesAsync(dateFrom, dateTo);

        // Precompute Utc kind once
        var all = items
            .Select(m => (m, matchUtc: DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc)))
            .ToList();

        int daysSynced = 0;
        int matchesSynced = 0;

        for (var d = fromLocal; d <= toLocal; d = d.AddDays(1))
        {
            // Still keep 60s per-day guard
            if (await IsTooSoonToResyncAsync(d, "RANGE", seconds: 60))
                continue;

            var log = new SyncLog
            {
                SyncType = "RANGE",
                LocalDate = d,
                StartedAtUtc = DateTime.UtcNow,
                Success = false,
                SyncedMatches = 0
            };

            _db.SyncLogs.Add(log);
            await _db.SaveChangesAsync();

            try
            {
                var (dayStartUtc, dayEndUtc) = DateRangeService.GetUtcRangeForLocalDate(d, timeZoneId);

                var dayMatches = all
                    .Where(x => x.matchUtc >= dayStartUtc && x.matchUtc < dayEndUtc)
                    .ToList();

                await using var tx = await _db.Database.BeginTransactionAsync();

                // Hard replace only this day window
                await _db.Matches
                    .Where(x => x.UtcDate >= dayStartUtc && x.UtcDate < dayEndUtc)
                    .ExecuteDeleteAsync();

                foreach (var (m, matchUtc) in dayMatches)
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

                    // Insert Match
                    _db.Matches.Add(new Match
                    {
                        Id = m.Id,
                        UtcDate = matchUtc,
                        Status = m.Status,
                        CompetitionId = m.Competition.Id,
                        HomeTeamId = m.HomeTeam.Id,
                        AwayTeamId = m.AwayTeam.Id,
                        HomeScore = m.Score.FullTime.Home,
                        AwayScore = m.Score.FullTime.Away
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                log.Success = true;
                log.SyncedMatches = dayMatches.Count;
                log.FinishedAtUtc = DateTime.UtcNow;
                log.ErrorMessage = null;

                await _db.SaveChangesAsync();

                daysSynced++;
                matchesSynced += dayMatches.Count;
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.FinishedAtUtc = DateTime.UtcNow;
                log.ErrorMessage = ex.Message;

                await _db.SaveChangesAsync();
                throw;
            }
        }

        return (daysSynced, matchesSynced);
    }

}
