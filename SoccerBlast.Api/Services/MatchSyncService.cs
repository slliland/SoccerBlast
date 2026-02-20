using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

public class MatchSyncService
{
    private readonly AppDbContext _db;
    private readonly SportsDbMatchesClient _client;
    private readonly TheSportsDbClient _sportsDb;

    private async Task<bool> IsTooSoonToResyncAsync(DateTime localDate, string syncType, int seconds = 60)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-seconds);

        return await _db.SyncLogs.AnyAsync(l =>
            l.SyncType == syncType &&
            l.LocalDate == localDate.Date &&
            l.Success == true &&
            l.FinishedAtUtc >= cutoff);
    }


    public MatchSyncService(AppDbContext db, SportsDbMatchesClient client, TheSportsDbClient sportsDb)
    {
        _db = db;
        _client = client;
        _sportsDb = sportsDb;
    }

    public sealed record EnsureRangeResult(
        int SyncedMatches,
        int DaysChecked,
        int DaysSynced,
        List<DateOnly> DaysActuallySynced);

    private async Task UpsertTeamAsync(int id, string name, string? crest)
    {
        var team = await _db.Teams.FindAsync(id);

        var sportsDbId = id.ToString();
        if (team == null)
        {
            _db.Teams.Add(new Team
            {
                Id = id,
                Name = name,
                CrestUrl = crest,
                SportsDbId = sportsDbId
            });
        }
        else
        {
            team.Name = name;
            if (string.IsNullOrWhiteSpace(team.SportsDbId))
                team.SportsDbId = sportsDbId;
            if (!string.IsNullOrWhiteSpace(crest))
                team.CrestUrl = crest;
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

            var dateFrom = startUtc.Date;
            var dateTo = endUtc.Date;

            var items = await _client.GetMatchesAsync(dateFrom, dateTo);

            // Filter first and don't delete if nothing is relevant
            var filtered = items
                .Select(m => (m, matchUtc: DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc)))
                .Where(x => x.matchUtc >= startUtc && x.matchUtc < endUtc)
                .ToList();

            // Fill missing league badges via v1 lookupleague.php (eventsday often omits strLeagueBadge)
            var compBadges = new Dictionary<int, string?>();
            foreach (var (m, _) in filtered)
            {
                if (string.IsNullOrWhiteSpace(m.Competition.Crest) && m.Competition.Id != 0 &&
                    !compBadges.ContainsKey(m.Competition.Id))
                {
                    try
                    {
                        var badge = await _sportsDb.GetLeagueBadgeAsync(m.Competition.Id, default);
                        compBadges[m.Competition.Id] = badge;
                    }
                    catch
                    {
                        compBadges[m.Competition.Id] = null;
                    }
                }
            }
            foreach (var (m, _) in filtered)
            {
                if (string.IsNullOrWhiteSpace(m.Competition.Crest) && compBadges.TryGetValue(m.Competition.Id, out var badge) && !string.IsNullOrWhiteSpace(badge))
                    m.Competition.Crest = badge;
            }

            // Transaction, delete and insert as one unit
            await using var tx = await _db.Database.BeginTransactionAsync();

            // Delete all matches in that UTC window
            await _db.Matches
                .Where(x => x.UtcDate >= startUtc && x.UtcDate < endUtc)
                .ExecuteDeleteAsync();

            var ids = filtered.Select(x => x.m.Id).Distinct().ToList();
            if (ids.Count > 0)
            {
                await _db.Matches
                    .Where(x => ids.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }

            // Re-insert matches fresh plus ensure teams/competitions exist
            foreach (var (m, matchUtc) in filtered)
            {
                Console.WriteLine($"HomeTeam {m.HomeTeam.Id} crest(from matches)='{m.HomeTeam.Crest}'");
                Console.WriteLine($"AwayTeam {m.AwayTeam.Id} crest(from matches)='{m.AwayTeam.Crest}'");
                // Upsert Competition
                var comp = await _db.Competitions.FindAsync(m.Competition.Id);
                if (comp == null)
                {
                    comp = new Competition
                    {
                        Id = m.Competition.Id,
                        Name = m.Competition.Name,
                        Country = m.Competition.Area?.Name,
                        BadgeUrl = m.Competition.Crest
                    };
                    _db.Competitions.Add(comp);
                }
                else
                {
                    comp.Name = m.Competition.Name;
                    comp.Country = m.Competition.Area?.Name;
                    if (!string.IsNullOrEmpty(m.Competition.Crest))
                        comp.BadgeUrl = m.Competition.Crest;
                }

                // Upsert Teams
                await UpsertTeamAsync(m.HomeTeam.Id, m.HomeTeam.Name, m.HomeTeam.Crest);
                await UpsertTeamAsync(m.AwayTeam.Id, m.AwayTeam.Name, m.AwayTeam.Crest);

                // Upsert Match
                _db.Matches.Add(new Match
                {
                    Id = m.Id,

                    Provider = "SportsDbMatches",
                    ExternalId = m.Id,

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

        var dateFrom = startUtc.Date;
        var dateTo = endUtc.Date;

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
                
                var dayIds = dayMatches.Select(x => x.m.Id).Distinct().ToList();
                if (dayIds.Count > 0)
                {
                    await _db.Matches
                        .Where(x => dayIds.Contains(x.Id))
                        .ExecuteDeleteAsync();
                }

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
                            Country = m.Competition.Area?.Name,
                            BadgeUrl = m.Competition.Crest
                        };
                        _db.Competitions.Add(comp);
                    }
                    else
                    {
                        comp.Name = m.Competition.Name;
                        comp.Country = m.Competition.Area?.Name;
                        if (!string.IsNullOrEmpty(m.Competition.Crest))
                            comp.BadgeUrl = m.Competition.Crest;
                    }

                    // Upsert Teams
                    await UpsertTeamAsync(m.HomeTeam.Id, m.HomeTeam.Name, m.HomeTeam.Crest);
                    await UpsertTeamAsync(m.AwayTeam.Id, m.AwayTeam.Name, m.AwayTeam.Crest);

                    // Insert Match
                    _db.Matches.Add(new Match
                    {
                        Id = m.Id,

                        Provider = "SportsDbMatches",
                        ExternalId = m.Id,

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

    /// <summary>
    /// Ensure a local date range is fresh in the DB, only syncing days that are missing or stale.
    /// Freshness is controlled by hot/warm/cold TTLs in minutes relative to "today" (local).
    /// </summary>
    public async Task<EnsureRangeResult> EnsureRangeFreshAsync(
        DateOnly from,
        DateOnly to,
        int hotMin,
        int warmMin,
        int coldMin,
        string? timeZoneId,
        CancellationToken ct)
    {
        if (to < from)
            (from, to) = (to, from);

        var tzId = string.IsNullOrWhiteSpace(timeZoneId) ? "America/New_York" : timeZoneId;

        var nowUtc = DateTime.UtcNow;
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));

        int GetTtlMinutes(DateOnly d)
        {
            var delta = Math.Abs(d.DayNumber - todayLocal.DayNumber);
            if (delta <= 1) return hotMin;
            if (delta <= 7) return warmMin;
            return coldMin;
        }

        // load existing sync states in range
        var states = await _db.MatchDaySyncStates
            .Where(x => x.LocalDate >= from && x.LocalDate <= to)
            .ToDictionaryAsync(x => x.LocalDate, x => x, ct);

        var daysToSync = new List<DateOnly>();
        var daysChecked = 0;

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            daysChecked++;
            var ttl = GetTtlMinutes(d);
            if (ttl <= 0) continue; // cold: never resync

            var (dayStartUtc, dayEndUtc) = DateRangeService.GetUtcRangeForLocalDate(d.ToDateTime(TimeOnly.MinValue), tzId);

            // If DB has no matches for this local day, force sync
            var hasAny = await _db.Matches
                .AnyAsync(m => m.UtcDate >= dayStartUtc && m.UtcDate < dayEndUtc, ct);
            if (!hasAny)
            {
                daysToSync.Add(d);
                continue;
            }

            if (!states.TryGetValue(d, out var st))
            {
                daysToSync.Add(d);
                continue;
            }

            if (st.LastSyncedUtc < nowUtc.AddMinutes(-ttl))
                daysToSync.Add(d);
        }

        var totalSynced = 0;
        var actuallySynced = new List<DateOnly>();

        foreach (var d in daysToSync)
        {
            var now = DateTime.UtcNow;

            var localDate = d.ToDateTime(TimeOnly.MinValue);
            var synced = await SyncLocalDateAsync(localDate, tzId, syncType: "ENSURE");

            // Re-check DB to avoid UNIQUE conflicts
            var st = await _db.MatchDaySyncStates
                .SingleOrDefaultAsync(x => x.LocalDate == d, ct);

            if (st is null)
            {
                st = new MatchDaySyncState { LocalDate = d };
                _db.MatchDaySyncStates.Add(st);
            }

            st.LastSyncedUtc = now;
            st.LastSyncedCount = synced;
            st.Provider = "SportsDbMatches";

            // Save per-day to avoid piling up duplicates and to make it resilient
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException se &&
                                            se.SqliteErrorCode == 19) // constraint
            {
                // Another request inserted the row first; reload and update instead
                _db.ChangeTracker.Clear();

                var existing = await _db.MatchDaySyncStates
                    .SingleAsync(x => x.LocalDate == d, ct);

                existing.LastSyncedUtc = now;
                existing.LastSyncedCount = synced;
                existing.Provider = "SportsDbMatches";

                await _db.SaveChangesAsync(ct);
            }

            totalSynced += synced;
            actuallySynced.Add(d);
        }


        if (daysToSync.Count > 0)
            await _db.SaveChangesAsync(ct);

        return new EnsureRangeResult(
            SyncedMatches: totalSynced,
            DaysChecked: daysChecked,
            DaysSynced: actuallySynced.Count,
            DaysActuallySynced: actuallySynced);
    }

    private static DateTime? ParseUtc(string? dateEvent, string? strTime)
    {
        if (string.IsNullOrWhiteSpace(dateEvent)) return null;
        if (!DateTime.TryParse(dateEvent.Trim(), out var d)) return null;

        if (!string.IsNullOrWhiteSpace(strTime) && TimeSpan.TryParse(strTime.Trim(), out var t))
            d = d.Date.Add(t);

        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    public async Task<int> SyncTeamScheduleAsync(
        int teamId,
        DateOnly from,
        DateOnly to,
        string tzId = "America/New_York",
        CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);

        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team == null) return 0;

        var sportsDbTeamId = !string.IsNullOrWhiteSpace(team.SportsDbId) ? team.SportsDbId!.Trim() : teamId.ToString();

        var prev = await _sportsDb.GetPreviousTeamEventsAsync(sportsDbTeamId, ct);
        var next = await _sportsDb.GetNextTeamEventsAsync(sportsDbTeamId, ct);

        var startLocal = from.ToDateTime(TimeOnly.MinValue);
        var endLocal   = to.ToDateTime(TimeOnly.MaxValue);

        var (startUtc, _) = DateRangeService.GetUtcRangeForLocalDate(startLocal, tzId);
        var (_, endUtc)   = DateRangeService.GetUtcRangeForLocalDate(to.AddDays(1).ToDateTime(TimeOnly.MinValue), tzId);

        var combined = prev.Concat(next)
            .Select(e => (e, utc: e.DateUtc ?? ParseUtc(e.DateEvent, e.StrTime)))
            .Where(x => x.utc.HasValue)
            .Select(x => (x.e, utc: DateTime.SpecifyKind(x.utc!.Value, DateTimeKind.Utc)))
            .Where(x => x.utc >= startUtc && x.utc < endUtc)
            .ToList();

        if (combined.Count == 0) return 0;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        int upserts = 0;

        foreach (var (e, utc) in combined)
        {
            ct.ThrowIfCancellationRequested();
            int leagueId = int.TryParse(e.IdLeague, out var lid) ? lid : 0;
            int homeId   = int.TryParse(e.IdHomeTeam, out var hid) ? hid : 0;
            int awayId   = int.TryParse(e.IdAwayTeam, out var aid) ? aid : 0;

            if (leagueId == 0 || homeId == 0 || awayId == 0)
                continue;

            // Upsert competition
            var comp = await _db.Competitions.FindAsync(new object?[] { leagueId }, ct);
            if (comp == null)
            {
                comp = new Competition { Id = leagueId, Name = e.StrLeague ?? "League" };
                _db.Competitions.Add(comp);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(e.StrLeague))
                    comp.Name = e.StrLeague!;
            }

            // Upsert teams
            await UpsertTeamAsync(homeId, e.StrHomeTeam ?? "Home", e.StrHomeTeamBadge);
            await UpsertTeamAsync(awayId, e.StrAwayTeam ?? "Away", e.StrAwayTeamBadge);

            if (!int.TryParse(e.IdEvent, out var eventId))
                continue;

            var existing = await _db.Matches.FindAsync(new object?[] { eventId }, ct);
            if (existing == null)
            {
                _db.Matches.Add(new Match
                {
                    Id = eventId,
                    Provider = "SportsDbSchedule",
                    ExternalId = eventId,
                    UtcDate = utc,
                    Status = e.StrStatus ?? "",
                    CompetitionId = leagueId,
                    HomeTeamId = homeId,
                    AwayTeamId = awayId,
                    HomeScore = e.IntHomeScore,
                    AwayScore = e.IntAwayScore
                });
            }
            else
            {
                existing.Provider = "SportsDbSchedule";
                existing.UtcDate = utc;
                existing.Status = e.StrStatus ?? existing.Status;
                existing.CompetitionId = leagueId;
                existing.HomeTeamId = homeId;
                existing.AwayTeamId = awayId;
                existing.HomeScore = e.IntHomeScore;
                existing.AwayScore = e.IntAwayScore;
            }

            upserts++;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return upserts;
    }
}
