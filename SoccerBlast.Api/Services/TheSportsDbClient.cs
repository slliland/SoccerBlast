using System.Text.Json;
using Microsoft.Extensions.Options;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services;

public sealed class TheSportsDbClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly TheSportsDbOptions _opt;

    public TheSportsDbClient(IHttpClientFactory httpFactory, IOptions<TheSportsDbOptions> opt)
    {
        _httpFactory = httpFactory;
        _opt = opt.Value;
    }

    private HttpClient Create()
    {
        var c = _httpFactory.CreateClient("sportsdb");
        c.BaseAddress = new Uri($"{_opt.BaseUrl.TrimEnd('/')}/{_opt.ApiKey.Trim()}/");
        return c;
    }

    public async Task<SportsDbTeam?> LookupTeamAsync(string idTeam, CancellationToken ct)
    {
        using var http = Create();
        // v1: lookupteam.php?id=133602
        using var resp = await http.GetAsync($"lookupteam.php?id={Uri.EscapeDataString(idTeam)}", ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseSingleTeam(json);
    }

    public async Task<SportsDbTeam?> SearchTeamByNameAsync(string teamName, CancellationToken ct)
    {
        var list = await SearchTeamsAsync(teamName, ct);
        return list.Count == 1 ? list[0] : (list.Count > 1 ? null : null);
    }

    /// <summary>Returns all teams from searchteams.php for disambiguation (multi-match).</summary>
    public async Task<List<SportsDbTeam>> SearchTeamsAsync(string teamName, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"searchteams.php?t={Uri.EscapeDataString(teamName)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseTeamList(json);
    }

    /// <summary>Bulk: lookup_all_teams.php?id=leagueId for league-based sync (canonical ids and names).</summary>
    public async Task<List<SportsDbTeam>> LookupAllTeamsInLeagueAsync(string leagueId, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"lookup_all_teams.php?id={Uri.EscapeDataString(leagueId)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseTeamList(json);
    }

    private static SportsDbTeam? ParseSingleTeam(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("teams", out var teams) || teams.ValueKind != JsonValueKind.Array)
            return null;

        var first = teams.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;

        return SportsDbTeam.FromJson(first);
    }

    private static List<SportsDbTeam> ParseTeamList(string json)
    {
        var list = new List<SportsDbTeam>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("teams", out var teams) || teams.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var t in teams.EnumerateArray())
        {
            if (t.ValueKind == JsonValueKind.Object)
                list.Add(SportsDbTeam.FromJson(t));
        }
        return list;
    }

    public async Task<List<TeamPlayerDto>> GetTeamPlayersAsync(string sportsDbTeamId, CancellationToken ct)
    {
        using var http = Create();
        // v1: lookup_all_players.php?id=133604
        using var resp = await http.GetAsync($"lookup_all_players.php?id={Uri.EscapeDataString(sportsDbTeamId)}", ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParsePlayers(json);
    }

    private static List<TeamPlayerDto> ParsePlayers(string json)
    {
        var players = new List<TeamPlayerDto>();
        
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("player", out var playersArray) || playersArray.ValueKind == JsonValueKind.Null)
        {
            return players;
        }

        foreach (var playerElement in playersArray.EnumerateArray())
        {
            var name = GetValue(playerElement, "strPlayer");
            if (string.IsNullOrEmpty(name)) continue;

            players.Add(new TeamPlayerDto
            {
                Name = name,
                Position = GetValue(playerElement, "strPosition"),
                Nationality = GetValue(playerElement, "strNationality"),
                ThumbUrl = GetValue(playerElement, "strThumb")
            });
        }

        return players;
    }

    private static string? GetValue(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;
}

public sealed record SportsDbTeam(
    string? IdTeam,
    string? StrTeam,
    string? IntFormedYear,
    string? StrLocation,
    string? StrKeywords,
    string? StrStadium,
    string? IntStadiumCapacity,
    string? StrStadiumLocation,
    string? StrLeague,
    string? StrLeague2,
    string? StrLeague3,
    string? StrLeague4,
    string? StrLeague5,
    string? StrLeague6,
    string? StrLeague7,
    string? StrDescriptionEN,
    string? StrBanner,
    string? StrEquipment,
    string? StrBadge,
    string? StrLogo,
    string? StrColour1,
    string? StrColour2,
    string? StrColour3,
    string? StrWebsite,
    string? StrFacebook,
    string? StrTwitter,
    string? StrInstagram,
    string? StrYoutube
)
{
    public static SportsDbTeam FromJson(JsonElement e)
    {
        static string? Get(JsonElement el, string name)
            => el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

        return new SportsDbTeam(
            IdTeam: Get(e, "idTeam"),
            StrTeam: Get(e, "strTeam"),
            IntFormedYear: Get(e, "intFormedYear"),
            StrLocation: Get(e, "strLocation"),
            StrKeywords: Get(e, "strKeywords"),
            StrStadium: Get(e, "strStadium"),
            IntStadiumCapacity: Get(e, "intStadiumCapacity"),
            StrStadiumLocation: Get(e, "strStadiumLocation"),
            StrLeague: Get(e, "strLeague"),
            StrLeague2: Get(e, "strLeague2"),
            StrLeague3: Get(e, "strLeague3"),
            StrLeague4: Get(e, "strLeague4"),
            StrLeague5: Get(e, "strLeague5"),
            StrLeague6: Get(e, "strLeague6"),
            StrLeague7: Get(e, "strLeague7"),
            StrDescriptionEN: Get(e, "strDescriptionEN"),
            StrBanner: Get(e, "strBanner"),
            StrEquipment: Get(e, "strEquipment"),
            StrBadge: Get(e, "strBadge"),
            StrLogo: Get(e, "strLogo"),
            StrColour1: Get(e, "strColour1"),
            StrColour2: Get(e, "strColour2"),
            StrColour3: Get(e, "strColour3"),
            StrWebsite: Get(e, "strWebsite"),
            StrFacebook: Get(e, "strFacebook"),
            StrTwitter: Get(e, "strTwitter"),
            StrInstagram: Get(e, "strInstagram"),
            StrYoutube: Get(e, "strYoutube")
        );
    }
}
