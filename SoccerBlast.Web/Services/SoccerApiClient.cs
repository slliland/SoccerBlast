using System.Net.Http.Json;
using System.Text.Json;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

public record SyncResult(int syncedMatches);

/// <summary>API returns camelCase; use case-insensitive so externalId maps to ExternalId, etc.</summary>
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}

public record SyncStatus(
    string message,
    string? syncType,
    string? localDate,
    bool? success,
    int? syncedMatches,
    string? finishedAtUtc
);

public sealed record EnsureRangeResult(
    int syncedMatches,
    int daysChecked,
    int daysSynced,
    List<DateOnly> daysActuallySynced);

public class SoccerApiClient
{
    private readonly HttpClient _http;

    // default fallback if JS interop hasn't set it yet
    public string TimeZoneId { get; private set; } = "America/New_York";

    public SoccerApiClient(HttpClient http)
    {
        _http = http;
    }

    public void SetTimeZone(string? tz)
    {
        if (!string.IsNullOrWhiteSpace(tz))
            TimeZoneId = tz;
    }

    private string TzQuery() => $"tz={Uri.EscapeDataString(TimeZoneId)}";

    public async Task<List<MatchDto>> GetTodayLocalAsync()
    {
        return await _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/today-local?{TzQuery()}")
               ?? new();
    }

    public async Task<List<MatchDto>> GetByLocalDateAsync(DateOnly date)
    {
        var s = date.ToString("yyyy-MM-dd");
        return await _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/date/{s}?{TzQuery()}")
               ?? new();
    }

    public async Task<int> SyncByLocalDateAsync(DateOnly date)
    {
        var s = date.ToString("yyyy-MM-dd");
        var resp = await _http.PostAsync($"api/Matches/date/{s}?{TzQuery()}", content: null);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            var msg = !string.IsNullOrWhiteSpace(errBody) ? errBody : resp.ReasonPhrase ?? "Sync failed";
            throw new HttpRequestException($"Sync failed ({resp.StatusCode}): {msg}");
        }

        var synced = await resp.Content.ReadFromJsonAsync<int>();
        return synced;
    }

    public async Task<int> SyncTodayAsync()
    {
        var resp = await _http.PostAsync($"api/admin/sync/today?{TzQuery()}", content: null);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<SyncResult>();
        return json?.syncedMatches ?? 0;
    }

    public async Task<SyncStatus?> GetSyncStatusAsync()
    {
        return await _http.GetFromJsonAsync<SyncStatus>($"api/admin/sync/status?{TzQuery()}");
    }

    public async Task<List<NewsDto>> GetRecentNewsAsync(int limit = 10)
    {
        return await _http.GetFromJsonAsync<List<NewsDto>>($"api/News/recent?limit={limit}")
            ?? new();
    }

    public Task<List<NewsDto>> GetRecommendedNewsAsync(IEnumerable<int> teamIds, int limit = 20)
    {
        var ids = teamIds?.ToList() ?? new List<int>();
        if (ids.Count == 0) return GetRecentNewsAsync(limit);

        // teamIds=1&teamIds=2&...
        var qs = string.Join("&", ids.Select(x => $"teamIds={x}"));
        return _http.GetFromJsonAsync<List<NewsDto>>($"api/news/recommended?{qs}&limit={limit}")
            ?? Task.FromResult(new List<NewsDto>());
    }

    public async Task RefreshNewsAsync()
    {
        await _http.PostAsync("api/news/refresh", null);
    }

    public async Task<List<SearchResultDto>> SearchAsync(string q, int limit = 12)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return new();

        return await _http.GetFromJsonAsync<List<SearchResultDto>>(
            $"api/search?q={Uri.EscapeDataString(q)}&limit={limit}"
        ) ?? new();
    }

    public async Task<List<MatchDto>> GetRangeAsync(
        DateOnly from, DateOnly to, int? competitionId = null, CancellationToken ct = default)
    {
    var qs = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&{TzQuery()}";
    if (competitionId.HasValue) qs += $"&competitionId={competitionId.Value}";

    return await _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/range?{qs}", ct)
            ?? new();
    }

    public async Task<int> SyncRangeAsync(DateOnly from, DateOnly to)
    {
        var qs = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&{TzQuery()}";
        var res = await _http.PostAsync($"api/Matches/range?{qs}", content: null);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<int>())!;
    }

    public async Task<int> EnsureTeamScheduleAsync(int teamId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var url = $"api/Matches/team/ensure?teamId={teamId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&{TzQuery()}";
        var res = await _http.PostAsync(url, null, ct);
        res.EnsureSuccessStatusCode();
        var obj = await res.Content.ReadFromJsonAsync<Dictionary<string,int>>(cancellationToken: ct);
        return obj != null && obj.TryGetValue("synced", out var n) ? n : 0;
    }

   public async Task<EnsureRangeResult?> EnsureRangeFreshAsync(
        DateOnly from,
        DateOnly to,
        int hotMin = 10,
        int warmMin = 720,
        int coldMin = 0,
        CancellationToken ct = default)
    {
    var qs =
        $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" +
        $"&hotMin={hotMin}&warmMin={warmMin}&coldMin={coldMin}" +
        $"&{TzQuery()}";

    using var resp = await _http.PostAsync($"api/Matches/range/ensure?{qs}", null, ct);
    if (!resp.IsSuccessStatusCode) return null;

    return await resp.Content.ReadFromJsonAsync<EnsureRangeResult>(cancellationToken: ct);
    }

    public async Task<List<TeamPlayerDto>> GetTeamPlayersAsync(int id)
    {
        return await _http.GetFromJsonAsync<List<TeamPlayerDto>>($"api/teams/{id}/players") ?? new List<TeamPlayerDto>();
    }

    public async Task<List<TeamPlayerDto>> GetTeamPlayersByExternalIdAsync(string sportsDbId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sportsDbId)) return new List<TeamPlayerDto>();
        return await _http.GetFromJsonAsync<List<TeamPlayerDto>>($"api/teams/by-external/{Uri.EscapeDataString(sportsDbId)}/players", ct) ?? new List<TeamPlayerDto>();
    }

    public async Task<List<TeamHonourDto>> GetTeamHonoursAsync(string sportsDbTeamId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TeamHonourDto>>($"api/teams/honours/{Uri.EscapeDataString(sportsDbTeamId)}", ct) ?? new List<TeamHonourDto>();

    /// <summary>GET venue/stadium for a team (id = DB id or external SportsDB id). Returns null on 404.</summary>
    public async Task<StadiumDetailDto?> GetTeamVenueAsync(string teamId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teamId)) return null;
        using var resp = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(teamId)}/venue", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<StadiumDetailDto>(ct);
    }

    // Returns (success, message). On 404 or failure, success is false
    public async Task<(bool ok, string message)> SyncTeamProfileAsync(int id)
    {
        using var resp = await _http.PostAsync($"api/teams/{id}/sync-profile", null);
        var body = await resp.Content.ReadAsStringAsync();
        var msg = string.IsNullOrWhiteSpace(body) ? (resp.ReasonPhrase ?? "Sync failed") : body;
        if (!resp.IsSuccessStatusCode)
            return (false, msg);
        return (true, string.IsNullOrWhiteSpace(body) ? "Synced." : msg);
    }

    /// <summary>GET competition by id (DB int or external string). Single URL.</summary>
    public async Task<CompetitionDetailDto?> GetCompetitionAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var resp = await _http.GetAsync($"api/competitions/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CompetitionDetailDto>(ApiJson.Options, ct);
    }

    // v2 schedule/league: all fixtures/results for a league season. Pass league id (external) when league.Id is 0
    public async Task<List<MatchDto>> GetLeagueScheduleAsync(string leagueId, string season, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(season)) return new List<MatchDto>();
        var list = await _http.GetFromJsonAsync<List<MatchDto>>(
            $"api/competitions/by-external/{Uri.EscapeDataString(leagueId)}/schedule/{Uri.EscapeDataString(season)}", ct);
        return list ?? new List<MatchDto>();
    }

    public async Task<List<SeasonDetailDto>> GetSeasonDetailsAsync(string leagueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId)) return new List<SeasonDetailDto>();
        var list = await _http.GetFromJsonAsync<List<SeasonDetailDto>>(
            $"api/competitions/by-external/{Uri.EscapeDataString(leagueId)}/seasons", ApiJson.Options, ct);
        return list ?? new List<SeasonDetailDto>();
    }

    public async Task<List<LookupTableRowDto>> GetLookupTableAsync(string leagueId, string season, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(season)) return new List<LookupTableRowDto>();
        var list = await _http.GetFromJsonAsync<List<LookupTableRowDto>>(
            $"api/competitions/by-external/{Uri.EscapeDataString(leagueId)}/table/{Uri.EscapeDataString(season)}", ApiJson.Options, ct);
        return list ?? new List<LookupTableRowDto>();
    }

    /// <summary>League analysis (seasons, points/goals trends, table). Champions come from GetChampionsAsync.</summary>
    public async Task<LeagueAnalysisDto?> GetLeagueAnalysisAsync(string leagueId, int? lastN = null, bool filterTitlesByLeagueTeams = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId)) return null;
        var parts = new List<string>();
        if (lastN.HasValue) parts.Add($"lastN={lastN.Value}");
        if (filterTitlesByLeagueTeams) parts.Add("filterTitlesByLeagueTeams=true");
        var qs = parts.Count > 0 ? "?" + string.Join("&", parts) : "";
        return await _http.GetFromJsonAsync<LeagueAnalysisDto>(
            $"api/competitions/by-external/{Uri.EscapeDataString(leagueId)}/analysis{qs}", ApiJson.Options, ct);
    }

    /// <summary>Past champions for a league from honours API (LeagueHonourMap → HonourWinners). Use for Champion Timeline and titles-by-club.</summary>
    public async Task<List<ChampionTimelineItemDto>> GetChampionsAsync(string leagueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId)) return new List<ChampionTimelineItemDto>();
        var list = await _http.GetFromJsonAsync<List<ChampionTimelineItemDto>>(
            $"api/competitions/by-external/{Uri.EscapeDataString(leagueId)}/champions", ApiJson.Options, ct);
        return list ?? new List<ChampionTimelineItemDto>();
    }

    // GET player by id (DB int or external string) with Single URL
    public async Task<PlayerDetailDto?> GetPlayerAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var resp = await _http.GetAsync($"api/players/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PlayerDetailDto>(ct);
    }

    public async Task<List<PlayerContractDto>> GetPlayerContractsAsync(string playerId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PlayerContractDto>>($"api/players/{Uri.EscapeDataString(playerId)}/contracts", ct) ?? new();

    public async Task<List<PlayerFormerTeamDto>> GetPlayerFormerTeamsAsync(string playerId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PlayerFormerTeamDto>>($"api/players/{Uri.EscapeDataString(playerId)}/former-teams", ct) ?? new();

    public async Task<List<PlayerHonourDto>> GetPlayerHonoursAsync(string playerId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PlayerHonourDto>>($"api/players/{Uri.EscapeDataString(playerId)}/honours", ct) ?? new();

    public async Task<List<PlayerMilestoneDto>> GetPlayerMilestonesAsync(string playerId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PlayerMilestoneDto>>($"api/players/{Uri.EscapeDataString(playerId)}/milestones", ct) ?? new();

    public async Task<List<PlayerResultDto>> GetPlayerResultsAsync(string playerId, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<PlayerResultDto>>($"api/players/{Uri.EscapeDataString(playerId)}/results", ct) ?? new();

    // GET team by id (DB int or external string). Single URL
    public async Task<TeamDetailDto?> GetTeamAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var resp = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TeamDetailDto>(ct);
    }

    public async Task<TeamDetailDto?> GetTeamByExternalIdAsync(string sportsDbId, CancellationToken ct = default)
        => await GetTeamAsync(sportsDbId);

    // GET venue by id (DB int or external string). Single URL
    public async Task<VenueDetailDto?> GetVenueAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var resp = await _http.GetAsync($"api/venues/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VenueDetailDto>(ct);
    }

    public async Task<List<VenueEventDto>> GetVenueUpcomingAsync(string venueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(venueId)) return new();
        using var resp = await _http.GetAsync($"api/venues/{Uri.EscapeDataString(venueId)}/upcoming", ct);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<VenueEventDto>>(ct) ?? new();
    }

    public async Task<List<VenueEventDto>> GetVenueRecentAsync(string venueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(venueId)) return new();
        using var resp = await _http.GetAsync($"api/venues/{Uri.EscapeDataString(venueId)}/recent", ct);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<VenueEventDto>>(ct) ?? new();
    }

    public async Task<List<MatchDto>> SearchMatchesAsync(
        string team,
        DateOnly from,
        DateOnly to,
        int? competitionId = null,
        int limit = 200,
        int? teamId = null,
        bool exact = true,
        CancellationToken ct = default
    )
    {
        var qs = $"team={Uri.EscapeDataString(team ?? "")}" +
                $"&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" +
                $"&limit={limit}&{TzQuery()}" +
                $"&exact={(exact ? "true" : "false")}";

        if (competitionId.HasValue) qs += $"&competitionId={competitionId.Value}";
        if (teamId.HasValue && teamId.Value > 0) qs += $"&teamId={teamId.Value}";

        return await _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/search?{qs}", ct)
            ?? new();
    }

    public async Task<List<MatchDto>> GetExternalMatchesAsync(string sportsDbTeamId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var url = $"/api/matches/external?sportsDbTeamId={Uri.EscapeDataString(sportsDbTeamId)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await _http.GetFromJsonAsync<List<MatchDto>>(url, ct) ?? new();
    }

    /// <summary>Get full match detail by TheSportsDB idEvent (event + lineup + timeline + stats + highlights + tv).</summary>
    public async Task<MatchDetailResponseDto?> GetMatchDetailAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return null;
        using var resp = await _http.GetAsync($"api/Matches/event/{Uri.EscapeDataString(idEvent)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MatchDetailResponseDto>(ct);
    }
}
