using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;
using SoccerBlast.Api.Services;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;

    public CompetitionsController(AppDbContext db, TheSportsDbClient sportsDb)
    {
        _db = db;
        _sportsDb = sportsDb;
    }

    [HttpGet]
    public async Task<ActionResult<List<CompetitionDto>>> GetAll()
    {
        var comps = await _db.Competitions
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CompetitionDto
            {
                Id = c.Id,
                Name = c.Name,
                Country = c.Country
            })
            .ToListAsync();

        return comps;
    }

    /// <summary>GET by id (int = DB, else try by-external). Single URL, no sportsdb in path.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CompetitionDetailDto>> GetByIdOrExternal(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        if (int.TryParse(id.Trim(), out var intId) && intId > 0)
        {
            var byId = await GetById(intId, ct);
            if (byId.Result is NotFoundResult)
                return await GetByExternalId(id.Trim(), ct);
            return byId;
        }
        return await GetByExternalId(id.Trim(), ct);
    }

    private async Task<ActionResult<CompetitionDetailDto>> GetById(int id, CancellationToken ct = default)
        {
        var comp = await _db.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
            if (comp == null) return NotFound();

            var matchCount = await _db.Matches
                .AsNoTracking()
            .CountAsync(m => m.CompetitionId == id, ct);

        LeagueProfileDto? profile = null;
        List<LeagueTeamDto>? teams = null;
        List<string>? seasons = null;
        var badgeUrl = comp.BadgeUrl;

        var leagueId = (await _db.CompetitionExternalMaps
            .AsNoTracking()
            .Where(m => m.CompetitionId == id && m.Provider == "SportsDB")
            .Select(m => m.ExternalId)
            .FirstOrDefaultAsync(ct));

        if (string.IsNullOrWhiteSpace(leagueId))
        {
            try
            {
                leagueId = await _sportsDb.GetLeagueIdBySearchAsync(comp.Name, ct);
            }
            catch
            {
                leagueId = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(leagueId))
        {
            try
            {
                var leagueTask = _sportsDb.LookupLeagueAsync(leagueId!, ct);
                var teamsTask = _sportsDb.LookupAllTeamsInLeagueAsync(leagueId!, ct);
                var seasonsTask = _sportsDb.ListSeasonsAsync(leagueId!, ct);

                await Task.WhenAll(leagueTask, teamsTask, seasonsTask);

                var league = leagueTask.Result;
                var v2Teams = teamsTask.Result;
                seasons = seasonsTask.Result;

                if (league != null)
                {
                    badgeUrl ??= string.IsNullOrWhiteSpace(league.StrBadge) ? null : league.StrBadge.Trim();

                    profile = new LeagueProfileDto
                    {
                        Sport = league.StrSport,
                        Gender = league.StrGender,
                        FormedYear = TryParseInt(league.IntFormedYear),
                        Country = league.StrCountry,
                        FirstEventDateUtc = ParseDate(league.DateFirstEvent),
                        CurrentSeason = league.StrCurrentSeason,
                        CurrentRound = TryParseInt(league.IntCurrentRound),
                        DescriptionEn = league.StrDescriptionEN,
                        BannerUrl = league.StrBanner,
                        PosterUrl = league.StrPoster,
                        TrophyUrl = league.StrTrophy,
                        FanartUrls = new[] { league.StrFanart1, league.StrFanart2, league.StrFanart3, league.StrFanart4 }
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s!.Trim())
                            .ToList(),
                        TvRights = league.StrTvRights,
                        Website = CleanUrl(league.StrWebsite),
                        Facebook = CleanUrl(league.StrFacebook),
                        Twitter = CleanUrl(league.StrTwitter),
                        Instagram = CleanUrl(league.StrInstagram),
                        Youtube = CleanUrl(league.StrYoutube)
                    };
                }

                if (v2Teams != null && v2Teams.Count > 0)
                {
                    teams = v2Teams
                        .Where(t => !string.IsNullOrWhiteSpace(t.IdTeam) && !string.IsNullOrWhiteSpace(t.StrTeam))
                        .Select(t => new LeagueTeamDto
                        {
                            IdTeam = t.IdTeam!,
                            Name = t.StrTeam!.Trim(),
                            BadgeUrl = string.IsNullOrWhiteSpace(t.StrBadge) ? null : t.StrBadge.Trim()
                        })
                        .ToList();
                }
            }
            catch
            {
                // If v2 lookup fails (rate limit, network), fall back to DB-only info.
            }
        }

            var dto = new CompetitionDetailDto
            {
                Id = comp.Id,
                Name = comp.Name,
                Country = comp.Country,
                BadgeUrl = badgeUrl,
                MatchCount = matchCount,
                Profile = profile,
                Teams = teams,
                Seasons = seasons,
                ExternalId = leagueId // so frontend uses SportsDB id for table/schedule, not internal id (l=5 returns empty)
            };

            return dto;
        }
    /// <summary>GET league by TheSportsDB v2 id only (no DB). API-first: full profile, teams, seasons from v2.</summary>
    [HttpGet("by-external/{sportsDbId}")]
    public async Task<ActionResult<CompetitionDetailDto>> GetByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var leagueId = sportsDbId.Trim();

        LeagueProfileDto? profile = null;
        List<LeagueTeamDto>? teams = null;
        List<string>? seasons = null;
        string? name = null;
        string? country = null;
        string? badgeUrl = null;

        try
        {
            var leagueTask = _sportsDb.LookupLeagueAsync(leagueId, ct);
            var teamsTask = _sportsDb.LookupAllTeamsInLeagueAsync(leagueId, ct);
            var seasonsTask = _sportsDb.ListSeasonsAsync(leagueId, ct);
            await Task.WhenAll(leagueTask, teamsTask, seasonsTask);

            var league = leagueTask.Result;
            var v2Teams = teamsTask.Result;
            seasons = seasonsTask.Result;

            if (league != null)
            {
                name = league.StrLeague?.Trim();
                country = string.IsNullOrWhiteSpace(league.StrCountry) ? null : league.StrCountry.Trim();
                badgeUrl = string.IsNullOrWhiteSpace(league.StrBadge) ? null : league.StrBadge.Trim();
                profile = new LeagueProfileDto
                {
                    LeagueAlternate = league.StrLeagueAlternate,
                    Sport = league.StrSport,
                    Gender = league.StrGender,
                    FormedYear = TryParseInt(league.IntFormedYear),
                    Country = country,
                    FirstEventDateUtc = ParseDate(league.DateFirstEvent),
                    CurrentSeason = league.StrCurrentSeason,
                    CurrentRound = TryParseInt(league.IntCurrentRound),
                    DescriptionEn = league.StrDescriptionEN,
                    BannerUrl = league.StrBanner,
                    PosterUrl = league.StrPoster,
                    TrophyUrl = league.StrTrophy,
                    LogoUrl = league.StrLogo,
                    FanartUrls = new[] { league.StrFanart1, league.StrFanart2, league.StrFanart3, league.StrFanart4 }
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!.Trim())
                        .ToList(),
                    TvRights = league.StrTvRights,
                    Website = CleanUrl(league.StrWebsite),
                    Facebook = CleanUrl(league.StrFacebook),
                    Twitter = CleanUrl(league.StrTwitter),
                    Instagram = CleanUrl(league.StrInstagram),
                    Youtube = CleanUrl(league.StrYoutube),
                    Rss = CleanUrl(league.StrRSS)
                };
            }

            if (v2Teams != null && v2Teams.Count > 0)
            {
                teams = v2Teams
                    .Where(t => !string.IsNullOrWhiteSpace(t.IdTeam) && !string.IsNullOrWhiteSpace(t.StrTeam))
                    .Select(t => new LeagueTeamDto
                    {
                        IdTeam = t.IdTeam!,
                        Name = t.StrTeam!.Trim(),
                        BadgeUrl = string.IsNullOrWhiteSpace(t.StrBadge) ? null : t.StrBadge.Trim()
                    })
                    .ToList();
            }
        }
        catch
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(name)) return NotFound();

        return new CompetitionDetailDto
        {
            Id = 0,
            Name = name,
            Country = country,
            BadgeUrl = badgeUrl,
            MatchCount = 0,
            Profile = profile,
            Teams = teams,
            Seasons = seasons,
            ExternalId = leagueId
        };
    }

    /// <summary>v2: GET list/seasons/{sportsDbId}. Returns season list with strSeason, strBadge, strPoster, strDescriptionEN when present.</summary>
    [HttpGet("by-external/{sportsDbId}/seasons")]
    public async Task<ActionResult<List<SeasonDetailDto>>> GetSeasonDetailsByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return new List<SeasonDetailDto>();
        var list = await _sportsDb.ListSeasonDetailsAsync(sportsDbId.Trim(), HttpContext.RequestAborted);
        return list;
    }

    /// <summary>Standings for league+season. Served from DB if populated by ScrapeLeagueTables script; otherwise from TheSportsDB v1 lookuptable.</summary>
    [HttpGet("by-external/{sportsDbId}/table/{season}")]
    public async Task<ActionResult<List<LookupTableRowDto>>> GetTableByExternalId(string sportsDbId, string season, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId) || string.IsNullOrWhiteSpace(season)) return new List<LookupTableRowDto>();
        var lid = sportsDbId.Trim();
        var s = season.Trim();

        var fromDb = await _db.LeagueSeasonStandings
            .AsNoTracking()
            .Where(x => x.LeagueId == lid && x.Season == s)
            .OrderBy(x => x.Rank)
            .Select(x => new LookupTableRowDto
            {
                Rank = x.Rank,
                TeamName = x.TeamName,
                TeamBadgeUrl = x.TeamBadgeUrl,
                Played = x.Played,
                Win = x.Win,
                Draw = x.Draw,
                Loss = x.Loss,
                GoalsFor = x.GoalsFor,
                GoalsAgainst = x.GoalsAgainst,
                GoalDifference = x.GoalDifference,
                Points = x.Points,
                Form = x.Form
            })
            .ToListAsync(ct);

        if (fromDb.Count > 0)
            return fromDb;

        // No rows in LeagueSeasonStandings (run ScrapeLeagueTables with default DB); v1 returns truncated rows
        var rows = await _sportsDb.GetLookupTableAsync(lid, s, ct);
        return rows;
    }

    /// <summary>League analysis derived from LeagueSeasonStandings: champions, titles, top-4, points/goals trends.</summary>
    /// <param name="filterTitlesByLeagueTeams">When true, TitlesByClub and Top4ByClub only include teams that are in this league (strLeague == league id from SportsDB).</param>
    [HttpGet("by-external/{sportsDbId}/analysis")]
    public async Task<ActionResult<LeagueAnalysisDto>> GetAnalysisByExternalId(string sportsDbId, [FromQuery] int? lastN, [FromQuery] bool filterTitlesByLeagueTeams = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var lid = sportsDbId.Trim();

        var all = await _db.LeagueSeasonStandings
            .AsNoTracking()
            .Where(x => x.LeagueId == lid)
            .OrderBy(x => x.Season)
            .ThenBy(x => x.Rank)
            .Select(x => new { x.Season, x.Rank, x.TeamId, x.TeamName, x.TeamBadgeUrl, x.Points, x.GoalsFor })
            .ToListAsync(ct);

        if (all.Count == 0)
            return new LeagueAnalysisDto();

        HashSet<string>? leagueTeamIds = null;
        if (filterTitlesByLeagueTeams)
        {
            try
            {
                var leagueTeams = await _sportsDb.LookupAllTeamsInLeagueAsync(lid, ct);
                if (leagueTeams != null && leagueTeams.Count > 0)
                    leagueTeamIds = leagueTeams.Where(t => !string.IsNullOrWhiteSpace(t.IdTeam)).Select(t => t.IdTeam!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch { /* fall back to unfiltered */ }
        }

        var seasonsOrdered = all.Select(x => x.Season).Distinct().OrderBy(x => x).ToList();
        if (lastN.HasValue && lastN.Value > 0)
        {
            seasonsOrdered = seasonsOrdered.TakeLast(lastN.Value).ToList();
            var set = seasonsOrdered.ToHashSet();
            all = all.Where(x => set.Contains(x.Season)).ToList();
        }

        // Helper: pick most recent non-empty name and badge from a group (by Season desc)
        static (string name, string? badge) PickDisplayNameAndBadge(IEnumerable<(string Season, string TeamId, string TeamName, string? TeamBadgeUrl)> rows)
        {
            var ordered = rows.OrderByDescending(x => x.Season).ToList();
            var name = ordered.Select(x => x.TeamName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";
            var badge = ordered.Select(x => x.TeamBadgeUrl).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            return (name, badge);
        }

        // Champion timeline (rank 1 per season): one row per season, dedupe by (Season, TeamId) so same team with different name/badge merges
        var championTimeline = all
            .Where(x => x.Rank == 1)
            .GroupBy(x => new { x.Season, x.TeamId })
            .Select(g =>
            {
                var pick = PickDisplayNameAndBadge(g.Select(x => (x.Season, x.TeamId, x.TeamName, x.TeamBadgeUrl)));
                return new ChampionTimelineItemDto
                {
                    Season = g.Key.Season,
                    TeamId = g.Key.TeamId,
                    TeamName = pick.name,
                    TeamBadgeUrl = pick.badge
                };
            })
            .OrderBy(x => x.Season)
            .ToList();

        // Titles by club: group by TeamId only, then pick display name/badge (most recent)
        var titlesByClubRaw = all
            .Where(x => x.Rank == 1)
            .GroupBy(x => x.TeamId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var pick = PickDisplayNameAndBadge(g.Select(x => (x.Season, x.TeamId, x.TeamName, x.TeamBadgeUrl)));
                return new TitlesByClubDto
                {
                    TeamId = g.Key,
                    TeamName = pick.name,
                    TeamBadgeUrl = pick.badge,
                    Titles = g.Count()
                };
            })
            .OrderByDescending(x => x.Titles);
        var titlesByClub = (leagueTeamIds != null
            ? titlesByClubRaw.Where(t => leagueTeamIds.Contains(t.TeamId))
            : titlesByClubRaw).ToList();

        // Top 4 by club: group by TeamId only
        var top4ByClubRaw = all
            .Where(x => x.Rank <= 4)
            .GroupBy(x => x.TeamId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var pick = PickDisplayNameAndBadge(g.Select(x => (x.Season, x.TeamId, x.TeamName, x.TeamBadgeUrl)));
                return new TopNByClubDto
                {
                    TeamId = g.Key,
                    TeamName = pick.name,
                    TeamBadgeUrl = pick.badge,
                    Top4Count = g.Count()
                };
            })
            .OrderByDescending(x => x.Top4Count);
        var top4ByClub = (leagueTeamIds != null
            ? top4ByClubRaw.Where(t => leagueTeamIds.Contains(t.TeamId))
            : top4ByClubRaw).ToList();

        // Points by club by season: group by TeamId only; one (Season, Points) per season per team (dedupe by Season within team)
        var pointsByClub = all
            .GroupBy(x => x.TeamId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var pick = PickDisplayNameAndBadge(g.Select(x => (x.Season, x.TeamId, x.TeamName, x.TeamBadgeUrl)));
                var seasonPoints = g
                    .GroupBy(x => x.Season, StringComparer.OrdinalIgnoreCase)
                    .Select(sg => new SeasonPointsDto { Season = sg.Key, Points = sg.Max(x => x.Points) })
                    .OrderBy(x => x.Season)
                    .ToList();
                return new PointsByClubSeasonDto
                {
                    TeamId = g.Key,
                    TeamName = pick.name,
                    TeamBadgeUrl = pick.badge,
                    SeasonPoints = seasonPoints
                };
            })
            .Where(x => x.SeasonPoints.Count > 0)
            .ToList();

        // League goals by season (sum of GoalsFor = total league goals; team count)
        var leagueGoalsBySeason = all
            .GroupBy(x => x.Season)
            .Select(g => new LeagueGoalsBySeasonDto
            {
                Season = g.Key,
                TotalGoals = g.Sum(x => x.GoalsFor),
                TeamCount = g.Count()
            })
            .OrderBy(x => x.Season)
            .ToList();

        var dto = new LeagueAnalysisDto
        {
            ChampionTimeline = championTimeline,
            TitlesByClub = titlesByClub,
            Top4ByClub = top4ByClub,
            PointsByClubBySeason = pointsByClub,
            LeagueGoalsBySeason = leagueGoalsBySeason,
            Seasons = seasonsOrdered
        };
        return dto;
    }

    /// <summary>Past champions for a league from honours (LeagueHonourMap → HonourWinners). Returns empty if no mapping exists.</summary>
    [HttpGet("by-external/{sportsDbId}/champions")]
    public async Task<ActionResult<List<ChampionTimelineItemDto>>> GetChampionsByExternalId(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return NotFound();
        var lid = sportsDbId.Trim();

        var honourIds = await _db.LeagueHonourMaps
            .AsNoTracking()
            .Where(m => m.LeagueId == lid)
            .Select(m => m.HonourId)
            .ToListAsync(ct);
        if (honourIds.Count == 0)
            return new List<ChampionTimelineItemDto>();

        var winners = await _db.HonourWinners
            .AsNoTracking()
            .Where(w => honourIds.Contains(w.HonourId))
            .OrderByDescending(w => w.YearLabel)
            .Select(w => new ChampionTimelineItemDto
            {
                Season = w.YearLabel,
                TeamId = w.TeamId,
                TeamName = w.TeamName ?? "",
                TeamBadgeUrl = w.TeamBadgeUrl
            })
            .ToListAsync(ct);
        return winners;
    }

    /// <summary>v2: GET schedule/league/{sportsDbId}/{season}. Returns all fixtures/results for that league season (API-first).</summary>
    [HttpGet("by-external/{sportsDbId}/schedule/{season}")]
    public async Task<ActionResult<List<MatchDto>>> GetScheduleByExternalId(string sportsDbId, string season, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId) || string.IsNullOrWhiteSpace(season)) return new List<MatchDto>();
        var events = await _sportsDb.GetLeagueScheduleAsync(sportsDbId.Trim(), season.Trim(), ct);
        var list = events
            .Where(e => e.DateUtc.HasValue)
            .Select(e => new MatchDto
            {
                Id = 0,
                UtcDate = e.DateUtc!.Value,
                CompetitionId = 0,
                CompetitionName = "",
                HomeTeamId = 0,
                HomeTeamName = e.StrHomeTeam,
                HomeTeamCrestUrl = e.StrHomeTeamBadge,
                AwayTeamId = 0,
                AwayTeamName = e.StrAwayTeam,
                AwayTeamCrestUrl = e.StrAwayTeamBadge,
                HomeScore = e.IntHomeScore,
                AwayScore = e.IntAwayScore,
                Status = e.StrStatus ?? "",
                ExternalId = e.IdEvent
            })
            .OrderBy(m => m.UtcDate)
            .ToList();
        return list;
    }

    [HttpGet("used")]
    public async Task<ActionResult<List<CompetitionUsedDto>>> GetUsed()
    {
        var used = await _db.Matches
            .AsNoTracking()
            .GroupBy(m => new { m.CompetitionId, m.Competition.Name, m.Competition.Country })
            .Select(g => new CompetitionUsedDto
            {
                Id = g.Key.CompetitionId,
                Name = g.Key.Name,
                Country = g.Key.Country,
                MatchCount = g.Count()
            })
            .OrderByDescending(x => x.MatchCount)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return used;
    }
    private static int? TryParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }

    private static string? CleanUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var cleaned = url.Trim();
        if (!cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = "https://" + cleaned;
        }
        return cleaned;
    }
}
