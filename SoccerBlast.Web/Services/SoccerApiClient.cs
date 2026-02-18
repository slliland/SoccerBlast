using System.Net.Http.Json;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

public record SyncResult(int syncedMatches);

public record SyncStatus(
    string message,
    string? syncType,
    string? localDate,
    bool? success,
    int? syncedMatches,
    string? finishedAtUtc
);

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
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<int>();
        return body;
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

    public Task<List<MatchDto>> GetRangeAsync(DateOnly from, DateOnly to, int? competitionId = null)
    {
        var qs = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&{TzQuery()}";
        if (competitionId.HasValue) qs += $"&competitionId={competitionId.Value}";
        return _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/range?{qs}")!;
    }

    public async Task<int> SyncRangeAsync(DateOnly from, DateOnly to)
    {
        var qs = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&{TzQuery()}";
        var res = await _http.PostAsync($"api/Matches/range?{qs}", content: null);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<int>())!;
    }

    public async Task<TeamDetailDto?> GetTeamAsync(int id, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<TeamDetailDto>($"api/teams/{id}", ct);
    }

    public async Task<List<TeamPlayerDto>> GetTeamPlayersAsync(int id)
    {
        return await _http.GetFromJsonAsync<List<TeamPlayerDto>>($"api/teams/{id}/players") ?? new List<TeamPlayerDto>();
    }

    /// <summary>Returns (success, message). On 404 or failure, success is false.</summary>
    public async Task<(bool ok, string message)> SyncTeamProfileAsync(int id)
    {
        using var resp = await _http.PostAsync($"api/teams/{id}/sync-profile", null);
        var body = await resp.Content.ReadAsStringAsync();
        var msg = string.IsNullOrWhiteSpace(body) ? (resp.ReasonPhrase ?? "Sync failed") : body;
        if (!resp.IsSuccessStatusCode)
            return (false, msg);
        return (true, string.IsNullOrWhiteSpace(body) ? "Synced." : msg);
    }
}
