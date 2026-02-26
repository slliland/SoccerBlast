using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

public class MatchSyncService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _syncLocks = new();

    private readonly AppDbContext _db;
    private readonly SportsDbMatchesClient _client;
    private readonly TheSportsDbClient _sportsDb;
    private readonly ILogger<MatchSyncService> _log;

    private static DateTime AsUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt
        };
    }

    private async Task<bool> IsTooSoonToResyncAsync(DateTime localDate, string syncType, int seconds = 60)
    {
        var localDateOnly = DateOnly.FromDateTime(localDate);
        var cutoffUtc = DateTime.UtcNow.AddSeconds(-seconds);

        _log.LogInformation("[Sync] IsTooSoonToResyncAsync querying SyncLogs...");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var cutoffOffset = new DateTimeOffset(cutoffUtc, TimeSpan.Zero);
            var result = await _db.SyncLogs
                .AsNoTracking()
                .AnyAsync(l =>
                    l.SyncType == syncType &&
                    l.LocalDate == localDateOnly &&
                    l.Success == true &&
                    l.FinishedAtUtc >= cutoffOffset, cts.Token);
            _log.LogInformation("[Sync] IsTooSoonToResyncAsync result={Result}", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("[Sync] IsTooSoonToResyncAsync timed out after 2s, proceeding with sync");
            return false; // proceed when query hangs (e.g. DB lock, connection issue)
        }
    }


    public MatchSyncService(AppDbContext db, SportsDbMatchesClient client, TheSportsDbClient sportsDb, ILogger<MatchSyncService> log)
    {
        _db = db;
        _client = client;
        _sportsDb = sportsDb;
        _log = log;
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

    private void UpsertTeamInMemory(Dictionary<int, Team> existingTeams, int id, string name, string? crest)
    {
        if (!existingTeams.TryGetValue(id, out var team))
        {
            team = new Team
            {
                Id = id,
                Name = name,
                CrestUrl = crest,
                SportsDbId = id.ToString()
            };
            _db.Teams.Add(team);
            existingTeams[id] = team;
        }
        else
        {
            team.Name = name;
            if (string.IsNullOrWhiteSpace(team.SportsDbId))
                team.SportsDbId = id.ToString();
            if (!string.IsNullOrWhiteSpace(crest))
                team.CrestUrl = crest;
        }
    }


    public async Task<int> SyncLocalDateAsync(DateTime localDate, string timeZoneId = "America/New_York", string syncType = "DATE")
    {
        localDate = localDate.Date;
        var dateKey = localDate.ToString("yyyy-MM-dd");
        _log.LogInformation("[Sync] Start date={Date} tz={Tz}", dateKey, timeZoneId);

        // Prevent duplicate concurrent sync for same date
        var sem = _syncLocks.GetOrAdd(dateKey, _ => new SemaphoreSlim(1, 1));
        if (!await sem.WaitAsync(TimeSpan.Zero))
        {
            _log.LogInformation("[Sync] Another sync already in progress for {Date}, skipping", dateKey);
            return 0;
        }
        try
        {
            return await SyncLocalDateInternalAsync(localDate, timeZoneId, syncType);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<int> SyncLocalDateInternalAsync(DateTime localDate, string timeZoneId, string syncType)
    {
        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        _log.LogInformation("[Sync] SyncLocalDateInternalAsync started | syncType={SyncType} | elapsed=0ms", syncType);

        // Warmup: first use of DbContext gets a connection; with pooler this can block. Log how long it takes.
        var tWarm = swTotal.ElapsedMilliseconds;
        _log.LogInformation("[Sync] Step: connection warmup (SELECT 1) starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
        await _db.Database.ExecuteSqlRawAsync("SELECT 1");
        _log.LogInformation("[Sync] Step: connection warmup done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - tWarm, swTotal.ElapsedMilliseconds);

        // Rate-limit: skip for DIAG (avoids slow/hanging SyncLogs query and saves ~7s); apply for dashboard/background sync
        if (!string.Equals(syncType, "DIAG", StringComparison.OrdinalIgnoreCase))
        {
            var t0 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Step: IsTooSoonToResyncAsync starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            if (await IsTooSoonToResyncAsync(localDate, syncType, seconds: 60))
            {
                _log.LogInformation("[Sync] Rate-limited, skipping (took {Ms}ms)", swTotal.ElapsedMilliseconds - t0);
                return 0;
            }
            _log.LogInformation("[Sync] Step: IsTooSoonToResyncAsync done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - t0, swTotal.ElapsedMilliseconds);
        }

        var t1a = swTotal.ElapsedMilliseconds;
        _log.LogInformation("[Sync] Step: creating SyncLog entity... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
        var log = new SyncLog
        {
            SyncType = syncType,
            LocalDate = DateOnly.FromDateTime(localDate),
            StartedAtUtc = DateTimeOffset.UtcNow,
            Success = false,
            SyncedMatches = 0
        };

        var t1b = swTotal.ElapsedMilliseconds;
        _log.LogInformation("[Sync] Step: Add(SyncLog) to context... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
        _db.SyncLogs.Add(log);

        var t1c = swTotal.ElapsedMilliseconds;
        _log.LogInformation("[Sync] Step: SaveChangesAsync (INSERT SyncLog) starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
        await _db.SaveChangesAsync(); // save early so status endpoint can see "in progress"
        _log.LogInformation("[Sync] Step: SaveChangesAsync (SyncLog) done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - t1c, swTotal.ElapsedMilliseconds);

        try
        {
            var t1d = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Step: GetUtcRangeForLocalDate... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            var (startUtc, endUtc) = DateRangeService.GetUtcRangeForLocalDate(localDate, timeZoneId);
            var dateFrom = startUtc.Date;
            var dateTo = endUtc.Date;
            _log.LogInformation("[Sync] Step: UTC range {Start}..{End} | took {Ms}ms | total={TotalMs}ms", startUtc, endUtc, swTotal.ElapsedMilliseconds - t1d, swTotal.ElapsedMilliseconds);

            var t2 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Step: SportsDB GetMatchesAsync starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            var items = await _client.GetMatchesAsync(dateFrom, dateTo);
            _log.LogInformation("[Sync] Step: SportsDB GetMatchesAsync done | count={Count} | took {Ms}ms | total={TotalMs}ms", items.Count, swTotal.ElapsedMilliseconds - t2, swTotal.ElapsedMilliseconds);

            var t3 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Step: filter+dedupe starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            // Filter first and dedupe by match Id (API can return same event twice across date boundaries)
            var filtered = items
                .Select(m => (m, matchUtc: DateTime.SpecifyKind(m.UtcDate, DateTimeKind.Utc)))
                .Where(x => x.matchUtc >= startUtc && x.matchUtc < endUtc)
                .GroupBy(x => x.m.Id)
                .Select(g => g.First())
                .ToList();
            _log.LogInformation("[Sync] Step: filter+dedupe done | matches={Count} | took {Ms}ms | total={TotalMs}ms", filtered.Count, swTotal.ElapsedMilliseconds - t3, swTotal.ElapsedMilliseconds);

            var t4 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Step: fetching badges starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            // Fill missing league badges via v1 lookupleague.php (eventsday often omits strLeagueBadge)
            var compsNeedingBadge = filtered.Where(x => string.IsNullOrWhiteSpace(x.m.Competition.Crest) && x.m.Competition.Id != 0).Select(x => x.m.Competition.Id).Distinct().ToList();
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
            _log.LogInformation("[Sync] Step: badges done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - t4, swTotal.ElapsedMilliseconds);

            var startOffset = new DateTimeOffset(startUtc, TimeSpan.Zero);
            var endOffset = new DateTimeOffset(endUtc, TimeSpan.Zero);

            var compIds = filtered.Select(x => x.m.Competition.Id).Distinct().ToList();
            var teamIds = filtered.SelectMany(x => new[] { x.m.HomeTeam.Id, x.m.AwayTeam.Id }).Distinct().ToList();

            var strategy = _db.Database.CreateExecutionStrategy();
            var t5 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Tx1: ExecuteDelete (date range) starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            int totalDeleted = 0;
            await strategy.ExecuteAsync(async () =>
            {
                totalDeleted = await _db.Matches
                    .Where(x => x.UtcDate >= startOffset && x.UtcDate < endOffset)
                    .ExecuteDeleteAsync();
            });
            _log.LogInformation("[Sync] Tx1: ExecuteDelete done | deleted={N} | took {Ms}ms | total={TotalMs}ms", totalDeleted, swTotal.ElapsedMilliseconds - t5, swTotal.ElapsedMilliseconds);

            var t7 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Tx2: upsert competitions starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            await strategy.ExecuteAsync(async () =>
            {
                var existingComps = await _db.Competitions.Where(c => compIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
                foreach (var (m, _) in filtered)
                {
                    var cid = m.Competition.Id;
                    if (!existingComps.TryGetValue(cid, out var comp))
                    {
                        comp = new Competition { Id = cid, Name = m.Competition.Name, Country = m.Competition.Area?.Name, BadgeUrl = m.Competition.Crest };
                        _db.Competitions.Add(comp);
                        existingComps[cid] = comp;
                    }
                    else
                    {
                        comp.Name = m.Competition.Name;
                        comp.Country = m.Competition.Area?.Name;
                        if (!string.IsNullOrEmpty(m.Competition.Crest)) comp.BadgeUrl = m.Competition.Crest;
                    }
                }
                await _db.SaveChangesAsync();
            });
            _log.LogInformation("[Sync] Tx2: upsert competitions done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - t7, swTotal.ElapsedMilliseconds);

            var t8 = swTotal.ElapsedMilliseconds;
            _log.LogInformation("[Sync] Tx3: upsert teams starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            await strategy.ExecuteAsync(async () =>
            {
                var existingTeams = await _db.Teams.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
                foreach (var (m, _) in filtered)
                {
                    UpsertTeamInMemory(existingTeams, m.HomeTeam.Id, m.HomeTeam.Name, m.HomeTeam.Crest);
                    UpsertTeamInMemory(existingTeams, m.AwayTeam.Id, m.AwayTeam.Name, m.AwayTeam.Crest);
                }
                await _db.SaveChangesAsync();
            });
            _log.LogInformation("[Sync] Tx3: upsert teams done | took {Ms}ms | total={TotalMs}ms", swTotal.ElapsedMilliseconds - t8, swTotal.ElapsedMilliseconds);

            _log.LogInformation("[Sync] Tx4: insert matches (batches) starting... | elapsed={Ms}ms", swTotal.ElapsedMilliseconds);
            const int matchInsertBatchSize = 25;
            var t9Start = swTotal.ElapsedMilliseconds;
            var insertedTotal = 0;
            for (var i = 0; i < filtered.Count; i += matchInsertBatchSize)
            {
                var batch = filtered.Skip(i).Take(matchInsertBatchSize).ToList();
                if (batch.Count == 0) continue;
                var t9 = swTotal.ElapsedMilliseconds;
                await strategy.ExecuteAsync(async () =>
                {
                    // Use raw SQL with ON CONFLICT DO UPDATE so duplicate Ids (from API or retries) update instead of failing
                    var valueRows = new List<string>();
                    var paramList = new List<object>();
                    var idx = 0;
                    foreach (var (m, matchUtc) in batch)
                    {
                        var utcOffset = new DateTimeOffset(DateTime.SpecifyKind(matchUtc, DateTimeKind.Utc), TimeSpan.Zero);
                        valueRows.Add($"({{{idx}}}, {{{idx + 1}}}, {{{idx + 2}}}, {{{idx + 3}}}, {{{idx + 4}}}, {{{idx + 5}}}, {{{idx + 6}}}, {{{idx + 7}}}, {{{idx + 8}}}, {{{idx + 9}}})");
                        paramList.Add(m.Id);
                        paramList.Add("SportsDbMatches");
                        paramList.Add(m.Id);
                        paramList.Add(utcOffset);
                        paramList.Add(m.Status ?? "");
                        paramList.Add((object?)m.Score?.FullTime?.Home);
                        paramList.Add((object?)m.Score?.FullTime?.Away);
                        paramList.Add(m.Competition.Id);
                        paramList.Add(m.HomeTeam.Id);
                        paramList.Add(m.AwayTeam.Id);
                        idx += 10;
                    }
                    var valuesClause = string.Join(", ", valueRows);
                    var sql = $"""
                        INSERT INTO "Matches" ("Id", "Provider", "ExternalId", "UtcDate", "Status", "HomeScore", "AwayScore", "CompetitionId", "HomeTeamId", "AwayTeamId")
                        VALUES {valuesClause}
                        ON CONFLICT ("Id") DO UPDATE SET
                            "Provider" = EXCLUDED."Provider",
                            "ExternalId" = EXCLUDED."ExternalId",
                            "UtcDate" = EXCLUDED."UtcDate",
                            "Status" = EXCLUDED."Status",
                            "HomeScore" = EXCLUDED."HomeScore",
                            "AwayScore" = EXCLUDED."AwayScore",
                            "CompetitionId" = EXCLUDED."CompetitionId",
                            "HomeTeamId" = EXCLUDED."HomeTeamId",
                            "AwayTeamId" = EXCLUDED."AwayTeamId"
                        """;
                    await _db.Database.ExecuteSqlRawAsync(sql, paramList.ToArray());
                });
                insertedTotal += batch.Count;
                _log.LogInformation("[Sync] Tx4: upserted matches batch {Batch} ({Count}), took {Ms}ms", (i / matchInsertBatchSize) + 1, batch.Count, swTotal.ElapsedMilliseconds - t9);
            }
            _log.LogInformation("[Sync] Tx4 total: upserted {N} matches in {Batches} batches, took {Ms}ms", insertedTotal, (filtered.Count + matchInsertBatchSize - 1) / matchInsertBatchSize, swTotal.ElapsedMilliseconds - t9Start);

            var t12 = swTotal.ElapsedMilliseconds;
            // Update SyncLog via raw SQL so we don't run SaveChanges on a context with many tracked entities (avoids NoData/ParseCompleteMessage)
            var finishedUtc = DateTimeOffset.UtcNow;
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE \"SyncLogs\" SET \"Success\" = {0}, \"SyncedMatches\" = {1}, \"FinishedAtUtc\" = {2}, \"ErrorMessage\" = {3} WHERE \"Id\" = {4}",
                true, filtered.Count, finishedUtc, (string?)null, log.Id);
            swTotal.Stop();
            _log.LogInformation("[Sync] Step: update SyncLog, took {Ms}ms | total sync {TotalMs}ms", swTotal.ElapsedMilliseconds - t12, swTotal.ElapsedMilliseconds);
            return filtered.Count;
        }
        catch (Exception ex)
        {
            var finishedUtc = DateTimeOffset.UtcNow;
            var errMsg = ex.Message ?? "";
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE \"SyncLogs\" SET \"Success\" = {0}, \"FinishedAtUtc\" = {1}, \"ErrorMessage\" = {2} WHERE \"Id\" = {3}",
                false, finishedUtc, errMsg, log.Id);
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

        // Range-focused fetch via existing matches client
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
                LocalDate = DateOnly.FromDateTime(d),
                StartedAtUtc = DateTimeOffset.UtcNow,
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

                var dayStartOffset = new DateTimeOffset(dayStartUtc, TimeSpan.Zero);
                var dayEndOffset = new DateTimeOffset(dayEndUtc, TimeSpan.Zero);

                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync();

                    await _db.Matches
                        .Where(x => x.UtcDate >= dayStartOffset && x.UtcDate < dayEndOffset)
                        .ExecuteDeleteAsync();

                    var dayIds = dayMatches.Select(x => x.m.Id).Distinct().ToList();
                    if (dayIds.Count > 0)
                    {
                        await _db.Matches
                            .Where(x => dayIds.Contains(x.Id))
                            .ExecuteDeleteAsync();
                    }

                    var compIds = dayMatches.Select(x => x.m.Competition.Id).Distinct().ToList();
                    var teamIds = dayMatches.SelectMany(x => new[] { x.m.HomeTeam.Id, x.m.AwayTeam.Id }).Distinct().ToList();

                    var existingComps = await _db.Competitions
                        .Where(c => compIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id);
                    var existingTeams = await _db.Teams
                        .Where(t => teamIds.Contains(t.Id))
                        .ToDictionaryAsync(t => t.Id);

                    foreach (var (m, _) in dayMatches)
                    {
                        var cid = m.Competition.Id;
                        if (!existingComps.TryGetValue(cid, out var comp))
                        {
                            comp = new Competition
                            {
                                Id = cid,
                                Name = m.Competition.Name,
                                Country = m.Competition.Area?.Name,
                                BadgeUrl = m.Competition.Crest
                            };
                            _db.Competitions.Add(comp);
                            existingComps[cid] = comp;
                        }
                        else
                        {
                            comp.Name = m.Competition.Name;
                            comp.Country = m.Competition.Area?.Name;
                            if (!string.IsNullOrEmpty(m.Competition.Crest))
                                comp.BadgeUrl = m.Competition.Crest;
                        }
                    }

                    foreach (var (m, _) in dayMatches)
                    {
                        UpsertTeamInMemory(existingTeams, m.HomeTeam.Id, m.HomeTeam.Name, m.HomeTeam.Crest);
                        UpsertTeamInMemory(existingTeams, m.AwayTeam.Id, m.AwayTeam.Name, m.AwayTeam.Crest);
                    }

                    foreach (var (m, matchUtc) in dayMatches)
                    {
                        _db.Matches.Add(new Match
                        {
                            Id = m.Id,
                            Provider = "SportsDbMatches",
                            ExternalId = m.Id,
                            UtcDate = new DateTimeOffset(DateTime.SpecifyKind(matchUtc, DateTimeKind.Utc), TimeSpan.Zero),
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
                });

                log.Success = true;
                log.SyncedMatches = dayMatches.Count;
                log.FinishedAtUtc = DateTimeOffset.UtcNow;
                log.ErrorMessage = null;

                await _db.SaveChangesAsync();

                daysSynced++;
                matchesSynced += dayMatches.Count;
            }
            catch (Exception ex)
            {
                log.Success = false;
                log.FinishedAtUtc = DateTimeOffset.UtcNow;
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
            var dayStartOffset = new DateTimeOffset(dayStartUtc, TimeSpan.Zero);
            var dayEndOffset = new DateTimeOffset(dayEndUtc, TimeSpan.Zero);
            var hasAny = await _db.Matches
                .AnyAsync(m => m.UtcDate >= dayStartOffset && m.UtcDate < dayEndOffset, ct);
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

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async (cancellationToken) =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            int upserts = 0;

            foreach (var (e, utc) in combined)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int leagueId = int.TryParse(e.IdLeague, out var lid) ? lid : 0;
                int homeId   = int.TryParse(e.IdHomeTeam, out var hid) ? hid : 0;
                int awayId   = int.TryParse(e.IdAwayTeam, out var aid) ? aid : 0;

                if (leagueId == 0 || homeId == 0 || awayId == 0)
                    continue;

                var comp = await _db.Competitions.FindAsync(new object?[] { leagueId }, cancellationToken);
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

                await UpsertTeamAsync(homeId, e.StrHomeTeam ?? "Home", e.StrHomeTeamBadge);
                await UpsertTeamAsync(awayId, e.StrAwayTeam ?? "Away", e.StrAwayTeamBadge);

                if (!int.TryParse(e.IdEvent, out var eventId))
                    continue;

                var existing = await _db.Matches.FindAsync(new object?[] { eventId }, cancellationToken);
                var utcOffset = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
                if (existing == null)
                {
                    _db.Matches.Add(new Match
                    {
                        Id = eventId,
                        Provider = "SportsDbSchedule",
                        ExternalId = eventId,
                        UtcDate = utcOffset,
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
                    existing.UtcDate = utcOffset;
                    existing.Status = e.StrStatus ?? existing.Status;
                    existing.CompetitionId = leagueId;
                    existing.HomeTeamId = homeId;
                    existing.AwayTeamId = awayId;
                    existing.HomeScore = e.IntHomeScore;
                    existing.AwayScore = e.IntAwayScore;
                }

                upserts++;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return upserts;
        }, ct);
    }
}
