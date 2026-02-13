using System.Net.Http.Json;

namespace SoccerBlast.Api.Services;

public class FootballDataClient
{
    private readonly HttpClient _http;

    public FootballDataClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<MatchItem>> GetMatchesAsync(DateTime dateFromUtc, DateTime dateToUtc)
    {
        // v4 dateTo is exclusive :contentReference[oaicite:5]{index=5}, so we pass tomorrow 00:00 UTC
        string from = dateFromUtc.ToString("yyyy-MM-dd");
        string to = dateToUtc.ToString("yyyy-MM-dd");

        var url = $"matches?dateFrom={from}&dateTo={to}";

        var resp = await _http.GetFromJsonAsync<MatchesResponse>(url);
        return resp?.Matches ?? [];
    }

    public async Task<TeamDetailsResponse?> GetTeamAsync(int teamId)
    {
        return await _http.GetFromJsonAsync<TeamDetailsResponse>($"teams/{teamId}");
    }
}
