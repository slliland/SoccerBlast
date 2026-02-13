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

    public SoccerApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<MatchDto>> GetTodayLocalAsync()
    {
        return await _http.GetFromJsonAsync<List<MatchDto>>("api/Matches/today-local")
               ?? new List<MatchDto>();
    }

    public async Task<List<MatchDto>> GetByLocalDateAsync(DateOnly date)
    {
        var s = date.ToString("yyyy-MM-dd");
        return await _http.GetFromJsonAsync<List<MatchDto>>($"api/Matches/date/{s}")
               ?? new List<MatchDto>();
    }

    public async Task<int> SyncTodayAsync()
    {
        Console.WriteLine(">>> SyncTodayAsync called");
        var resp = await _http.PostAsync("api/admin/sync/today", content: null);
        Console.WriteLine($">>> SyncTodayAsync status: {(int)resp.StatusCode}");

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<SyncResult>();
        return json?.syncedMatches ?? 0;
    }

    public async Task<SyncStatus?> GetSyncStatusAsync()
    {
        return await _http.GetFromJsonAsync<SyncStatus>("api/admin/sync/status");
    }
}
