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
            Seasons = seasons
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
            Seasons = seasons
        };
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
                Status = e.StrStatus ?? ""
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
