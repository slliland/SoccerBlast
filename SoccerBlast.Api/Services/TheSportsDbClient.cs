using System.Text.Json;
using System.Text.RegularExpressions;
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

    private HttpClient Create() => _httpFactory.CreateClient("sportsdb");

    private async Task<string?> GetStringOrNullAsync(string path, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync(path, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private async Task<string?> SearchJsonWithSlugFallbackAsync(string prefix, string query, CancellationToken ct)
    {
        var q1 = NormalizeSearchText(query);
        if (!string.IsNullOrWhiteSpace(q1))
        {
            var json1 = await GetStringOrNullAsync($"{prefix}/{Uri.EscapeDataString(q1)}", ct);
            if (!string.IsNullOrWhiteSpace(json1)) return json1;
        }

        var q2 = ToSlug(query);
        if (!string.IsNullOrWhiteSpace(q2) && !string.Equals(q2, q1, StringComparison.OrdinalIgnoreCase))
        {
            var json2 = await GetStringOrNullAsync($"{prefix}/{Uri.EscapeDataString(q2)}", ct);
            if (!string.IsNullOrWhiteSpace(json2)) return json2;
        }

        return null;
    }

    /// <summary>v2: GET lookup/team/{id}. Response: {"lookup": [teamObj]}.</summary>
    public async Task<SportsDbTeam?> LookupTeamAsync(string idTeam, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/team/{Uri.EscapeDataString(idTeam)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseSingleTeamV2(json, "lookup");
    }

    public async Task<SportsDbTeam?> SearchTeamByNameAsync(string teamName, CancellationToken ct)
    {
        var list = await SearchTeamsAsync(teamName, ct);
        return list.Count == 1 ? list[0] : (list.Count > 1 ? null : null);
    }

    /// <summary>v2: GET search/team/{query}. Returns all teams for disambiguation. Response: {"search": [teamObj, ...]}.</summary>
    public async Task<List<SportsDbTeam>> SearchTeamsAsync(string teamName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(teamName)) return new();

        // Try "human" query first (spaces preserved)
        var q1 = NormalizeSearchText(teamName);
        var r1 = await SearchTeamsRawAsync(q1, ct);
        if (r1.Count > 0) return r1;

        // Fallback: v2 slug (lowercase + underscores)
        var q2 = ToSlug(teamName);
        if (!string.IsNullOrWhiteSpace(q2) && !string.Equals(q2, q1, StringComparison.OrdinalIgnoreCase))
        {
            var r2 = await SearchTeamsRawAsync(q2, ct);
            if (r2.Count > 0) return r2;
        }

        // Extra fallback: strip common club prefixes and retry (FC/CF/SC/etc.)
        var simplified = SimplifyTeamQuery(teamName);
        if (!string.IsNullOrWhiteSpace(simplified) &&
            !string.Equals(simplified, teamName, StringComparison.OrdinalIgnoreCase))
        {
            var q3 = NormalizeSearchText(simplified);
            var r3 = await SearchTeamsRawAsync(q3, ct);
            if (r3.Count > 0) return r3;

            var q4 = ToSlug(simplified);
            if (!string.IsNullOrWhiteSpace(q4) && !string.Equals(q4, q3, StringComparison.OrdinalIgnoreCase))
            {
                var r4 = await SearchTeamsRawAsync(q4, ct);
                if (r4.Count > 0) return r4;
            }
        }

        return new();

        async Task<List<SportsDbTeam>> SearchTeamsRawAsync(string q, CancellationToken ct2)
        {
            using var http = Create();
            using var resp = await http.GetAsync($"search/team/{Uri.EscapeDataString(q)}", ct2);
            if (!resp.IsSuccessStatusCode) return new();
            var json = await resp.Content.ReadAsStringAsync(ct2);
            return ParseTeamListV2(json, "search");
        }

        static string SimplifyTeamQuery(string s)
        {
            s = s.Trim();

            // remove leading common prefixes (very common in user input)
            s = Regex.Replace(s, @"^(fc|cf|sc|afc|ac|cd|ud|sd)\s+", "", RegexOptions.IgnoreCase);

            // handle "FC-Barcelona" / "FC.Barcelona"
            s = Regex.Replace(s, @"^(fc|cf|sc|afc|ac|cd|ud|sd)[\.\-]\s*", "", RegexOptions.IgnoreCase);

            return s.Trim();
        }
    }

    /// <summary>v2: GET list/teams/{leagueId}. Response: {"list": [teamObj, ...]}.</summary>
    public async Task<List<SportsDbTeam>> LookupAllTeamsInLeagueAsync(string leagueId, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"list/teams/{Uri.EscapeDataString(leagueId)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseTeamListV2(json, "list");
    }

    /// <summary>v2: GET lookup/league/{id}. Response: {"lookup": [leagueObj]} with strBadge, strLogo, strPoster, strBanner, strTrophy, strFanart1..4.</summary>
    public async Task<string?> GetLeagueBadgeAsync(int leagueId, CancellationToken ct = default)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/league/{leagueId}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseLeagueBadgeFromJson(json, "lookup");
    }

    /// <summary>v2: GET search/league/{query}. Returns strBadge from first result for team profile competition icons.</summary>
    public async Task<string?> GetLeagueBadgeBySearchAsync(string leagueName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return null;
        var q = NormalizeSearchText(leagueName);
        if (string.IsNullOrEmpty(q)) return null;
        using var http = Create();
        using var resp = await http.GetAsync($"search/league/{Uri.EscapeDataString(q)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseLeagueBadgeFromJson(json, "search");
    }

    /// <summary>v2: GET search/league/{query}. Returns idLeague of first result so we can call lookup/league and list/teams.</summary>
    public async Task<string?> GetLeagueIdBySearchAsync(string leagueName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return null;
        var q = NormalizeSearchText(leagueName);
        if (string.IsNullOrEmpty(q)) return null;
        using var http = Create();
        using var resp = await http.GetAsync($"search/league/{Uri.EscapeDataString(q)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("search", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        var id = GetValue(first, "idLeague");
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }

    /// <summary>v2: GET search/league/{query}. Returns all matching leagues for search results (API-first, no DB required).</summary>
    public async Task<List<SportsDbLeagueSearchResult>> SearchLeaguesAsync(string query, CancellationToken ct = default)
    {
        var list = new List<SportsDbLeagueSearchResult>();
        if (string.IsNullOrWhiteSpace(query)) return list;

        var json = await SearchJsonWithSlugFallbackAsync("search/league", query, ct);
        if (string.IsNullOrWhiteSpace(json)) return list;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("search", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var id = GetValue(el, "idLeague");
            var name = GetValue(el, "strLeague");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;

            list.Add(new SportsDbLeagueSearchResult(
                id.Trim(),
                name.Trim(),
                NullTrim(GetValue(el, "strBadge")),
                NullTrim(GetValue(el, "strCountry"))
            ));
        }

        return list;

        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>v2: GET search/player/{query}. Returns players matching query (API-first). Response: {"search": [playerObj, ...]}.</summary>
    public async Task<List<SportsDbPlayerSearchResult>> SearchPlayersAsync(string query, CancellationToken ct = default)
    {
        var list = new List<SportsDbPlayerSearchResult>();
        if (string.IsNullOrWhiteSpace(query)) return list;

        var json = await SearchJsonWithSlugFallbackAsync("search/player", query, ct);
        if (string.IsNullOrWhiteSpace(json)) return list;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("search", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var id = GetValue(el, "idPlayer");
            var name = GetValue(el, "strPlayer");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;

            list.Add(new SportsDbPlayerSearchResult(
                id.Trim(),
                name.Trim(),
                NullTrim(GetValue(el, "strPosition")),
                NullTrim(GetValue(el, "strTeam")),
                NullTrim(GetValue(el, "strSport")),
                NullTrim(GetValue(el, "strThumb"))
            ));
        }

        return list;

        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>v2: GET search/venue/{query}. Tries slug first (e.g. emirates_stadium), then full name. Response: {"search": [venueObj, ...]}.</summary>
    public async Task<List<SportsDbVenueSearchResult>> SearchVenuesAsync(string query, CancellationToken ct = default)
    {
        var list = new List<SportsDbVenueSearchResult>();
        if (string.IsNullOrWhiteSpace(query)) return list;

        var slug = ToSlug(query);
        var normalized = NormalizeSearchText(query);
        var toTry = new List<string>();
        if (!string.IsNullOrWhiteSpace(slug)) toTry.Add(slug);
        if (!string.IsNullOrWhiteSpace(normalized) && !toTry.Contains(normalized, StringComparer.OrdinalIgnoreCase)) toTry.Add(normalized);
        if (toTry.Count == 0) return list;

        foreach (var q in toTry)
        {
            var json = await GetStringOrNullAsync($"search/venue/{Uri.EscapeDataString(q)}", ct);
            if (string.IsNullOrWhiteSpace(json)) continue;
            var parsed = ParseVenueSearchJson(json);
            if (parsed.Count > 0) return parsed;
        }

        return list;
    }

    private static List<SportsDbVenueSearchResult> ParseVenueSearchJson(string json)
    {
        var list = new List<SportsDbVenueSearchResult>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("search", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var id = GetValue(el, "idVenue");
            var name = GetValue(el, "strVenue");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new SportsDbVenueSearchResult(
                id.Trim(),
                name.Trim(),
                NullTrim(GetValue(el, "strLocation")),
                NullTrim(GetValue(el, "strCountry")),
                NullTrim(GetValue(el, "strThumb"))
            ));
        }
        return list;
        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>v2: GET lookup/venue/{id}. Response: {"lookup": [venueObj]}.</summary>
    public async Task<SportsDbVenueLookup?> LookupVenueAsync(string venueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(venueId)) return null;
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/venue/{Uri.EscapeDataString(venueId)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound || !resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseVenueLookup(json);
    }

    private static SportsDbVenueLookup? ParseVenueLookup(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("lookup", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        var id = GetValue(first, "idVenue");
        var name = GetValue(first, "strVenue");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;
        var capStr = GetValue(first, "intCapacity");
        int? capacity = !string.IsNullOrWhiteSpace(capStr) && int.TryParse(capStr, out var cap) ? cap : null;
        var formedStr = GetValue(first, "intFormedYear");
        int? formedYear = !string.IsNullOrWhiteSpace(formedStr) && int.TryParse(formedStr, out var fy) ? fy : null;
        return new SportsDbVenueLookup(
            id.Trim(),
            name.Trim(),
            NullTrim(GetValue(first, "strLocation")),
            NullTrim(GetValue(first, "strCountry")),
            capacity,
            NullTrim(GetValue(first, "strThumb")),
            formedYear,
            NullTrim(GetValue(first, "strMap")),
            NullTrim(GetValue(first, "strDescriptionEN")),
            NullTrim(GetValue(first, "strCost")),
            NullTrim(GetValue(first, "strFanart1")),
            NullTrim(GetValue(first, "strFanart2")),
            NullTrim(GetValue(first, "strFanart3")),
            NullTrim(GetValue(first, "strFanart4")),
            NullTrim(GetValue(first, "strWebsite")),
            NullTrim(GetValue(first, "strAlternate")),
            NullTrim(GetValue(first, "strTimezone")),
            NullTrim(GetValue(first, "strLogo"))
        );
        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static string? ParseLeagueBadgeFromJson(string json, string arrayKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        var badge = GetValue(first, "strBadge");
        return string.IsNullOrWhiteSpace(badge) ? null : badge.Trim();
    }

    /// <summary>Clean text for v2 search/*: trim, collapse whitespace. Do NOT convert spaces to underscores.</summary>
    private static string NormalizeSearchText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Trim();
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    /// <summary>Build v2 search slug: lowercase, spaces and apostrophes to underscore, strip other non-alphanumeric.</summary>
    private static string ToSlug(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var s = name.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"['’\u2019]", " "); // apostrophes -> space
        s = Regex.Replace(s, @"[^\p{L}\p{N}\s]", " "); // keep letters, numbers, spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return string.Join("_", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static SportsDbTeam? ParseSingleTeamV2(string json, string arrayKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        return SportsDbTeam.FromJson(first);
    }

    private static List<SportsDbTeam> ParseTeamListV2(string json, string arrayKey)
    {
        var list = new List<SportsDbTeam>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var t in arr.EnumerateArray())
        {
            if (t.ValueKind == JsonValueKind.Object)
                list.Add(SportsDbTeam.FromJson(t));
        }
        return list;
    }

    /// <summary>v2: GET all/leagues. Returns all leagues with strSport = Soccer (idLeague, strLeague).</summary>
    public async Task<List<SportsDbLeagueListItem>> ListAllLeaguesAsync(CancellationToken ct = default)
    {
        using var http = Create();
        using var resp = await http.GetAsync("all/leagues", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("leagues", out var arr) && !doc.RootElement.TryGetProperty("all", out arr))
            return new List<SportsDbLeagueListItem>();
        if (arr.ValueKind != JsonValueKind.Array) return new List<SportsDbLeagueListItem>();
        var list = new List<SportsDbLeagueListItem>();
        foreach (var el in arr.EnumerateArray())
        {
            var sport = GetValue(el, "strSport");
            if (string.IsNullOrWhiteSpace(sport) || !sport.Equals("Soccer", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = GetValue(el, "idLeague");
            var name = GetValue(el, "strLeague");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new SportsDbLeagueListItem(NullTrim(id) ?? id, NullTrim(name) ?? name));
        }
        return list;
    }

    /// <summary>v2: GET lookup/league/{id}. Response: {"lookup": [leagueObj]} with rich league metadata.</summary>
    public async Task<SportsDbLeague?> LookupLeagueAsync(string leagueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leagueId)) return null;
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/league/{Uri.EscapeDataString(leagueId)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("lookup", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        return SportsDbLeague.FromJson(first);
    }

    /// <summary>v2: GET list/seasons/{leagueId}. Response: {"list": [{"strSeason": "2024-2025"}, ...]}.</summary>
    public async Task<List<string>> ListSeasonsAsync(string leagueId, CancellationToken ct = default)
    {
        var seasons = new List<string>();
        if (string.IsNullOrWhiteSpace(leagueId)) return seasons;

        using var http = Create();
        using var resp = await http.GetAsync($"list/seasons/{Uri.EscapeDataString(leagueId)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("list", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return seasons;
        foreach (var s in arr.EnumerateArray())
        {
            var season = GetValue(s, "strSeason");
            if (!string.IsNullOrWhiteSpace(season))
                seasons.Add(season.Trim()!);
        }
        return seasons;
    }

    /// <summary>v2: GET list/seasons/{leagueId}. Returns list with strSeason, strBadge, strPoster, strDescriptionEN when present.</summary>
    public async Task<List<SeasonDetailDto>> ListSeasonDetailsAsync(string leagueId, CancellationToken ct = default)
    {
        var list = new List<SeasonDetailDto>();
        if (string.IsNullOrWhiteSpace(leagueId)) return list;

        using var http = Create();
        using var resp = await http.GetAsync($"list/seasons/{Uri.EscapeDataString(leagueId.Trim())}", ct);
        if (!resp.IsSuccessStatusCode) return list;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("list", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var s in arr.EnumerateArray())
        {
            var strSeason = GetValue(s, "strSeason");
            if (string.IsNullOrWhiteSpace(strSeason)) continue;
            list.Add(new SeasonDetailDto
            {
                StrSeason = strSeason.Trim()!,
                StrBadge = GetValue(s, "strBadge"),
                StrPoster = GetValue(s, "strPoster"),
                StrDescriptionEN = GetValue(s, "strDescriptionEN")
            });
        }
        return list;
    }

    /// <summary>v1: GET lookuptable.php?l={leagueId}&s={season}. Returns standings table (soccer only).</summary>
    public async Task<List<LookupTableRowDto>> GetLookupTableAsync(string leagueId, string season, CancellationToken ct = default)
    {
        var rows = new List<LookupTableRowDto>();
        if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(season)) return rows;

        using var http = _httpFactory.CreateClient("sportsdb-v1");
        var path = $"lookuptable.php?l={Uri.EscapeDataString(leagueId.Trim())}&s={Uri.EscapeDataString(season.Trim())}";
        using var resp = await http.GetAsync(path, ct);
        if (!resp.IsSuccessStatusCode) return rows;
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return rows;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return rows; // API returned non-JSON (e.g. HTML error, empty)
        }
        using (doc)
        {
        if (!doc.RootElement.TryGetProperty("table", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return rows;
        foreach (var e in arr.EnumerateArray())
        {
            var rank = TryParseInt(GetValue(e, "intRank")) ?? TryParseInt(GetValue(e, "idStanding")) ?? 0;
            rows.Add(new LookupTableRowDto
            {
                Rank = rank,
                TeamName = GetValue(e, "strTeam") ?? "",
                TeamBadgeUrl = GetValue(e, "strTeamBadge") ?? GetValue(e, "strBadge"),
                Played = TryParseInt(GetValue(e, "intPlayed")) ?? 0,
                Win = TryParseInt(GetValue(e, "intWin")) ?? 0,
                Draw = TryParseInt(GetValue(e, "intDraw")) ?? 0,
                Loss = TryParseInt(GetValue(e, "intLoss")) ?? 0,
                GoalsFor = TryParseInt(GetValue(e, "intGoalsFor")) ?? 0,
                GoalsAgainst = TryParseInt(GetValue(e, "intGoalsAgainst")) ?? 0,
                GoalDifference = TryParseInt(GetValue(e, "intGoalDifference")) ?? 0,
                Points = TryParseInt(GetValue(e, "intPoints")) ?? 0,
                Form = GetValue(e, "strForm")
            });
        }
        return rows.OrderBy(r => r.Rank).ToList();
        }
    }

    /// <summary>v2: GET schedule/league/{idLeague}/{season}. Returns all events (fixtures/results) for that league season.</summary>
    public async Task<List<SportsDbScheduleEvent>> GetLeagueScheduleAsync(string leagueId, string season, CancellationToken ct = default)
    {
        var list = new List<SportsDbScheduleEvent>();
        if (string.IsNullOrWhiteSpace(leagueId) || string.IsNullOrWhiteSpace(season)) return list;
        var leagueIdTrim = leagueId.Trim();
        var seasonTrim = season.Trim();
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/league/{Uri.EscapeDataString(leagueIdTrim)}/{Uri.EscapeDataString(seasonTrim)}", ct);
        if (!resp.IsSuccessStatusCode) return list;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("events", out var arr))
            doc.RootElement.TryGetProperty("schedule", out arr);
        if (arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var home = GetValue(el, "strHomeTeam");
            var away = GetValue(el, "strAwayTeam");
            var dateEvent = GetValue(el, "dateEvent");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;
            var time = GetValue(el, "strTime");
            var dateUtc = ParseEventDateUtc(dateEvent, time);
            var idHome =
                GetValue(el, "idHomeTeam") ??
                GetValue(el, "idTeamHome");

            var idAway =
                GetValue(el, "idAwayTeam") ??
                GetValue(el, "idTeamAway");
            list.Add(new SportsDbScheduleEvent(
                IdEvent: GetValue(el, "idEvent"),
                IdLeague: GetValue(el, "idLeague"),
                StrLeague: GetValue(el, "strLeague"),

                IdHomeTeam: idHome,
                IdAwayTeam: idAway,

                StrHomeTeam: home.Trim(),
                StrAwayTeam: away.Trim(),

                DateEvent: string.IsNullOrWhiteSpace(dateEvent) ? null : dateEvent.Trim(),
                StrTime: time,
                DateUtc: dateUtc,

                IntHomeScore: TryParseInt(GetValue(el, "intHomeScore")),
                IntAwayScore: TryParseInt(GetValue(el, "intAwayScore")),
                StrStatus: GetValue(el, "strStatus") ?? GetValue(el, "strProgress"),

                StrHomeTeamBadge: NullTrim(GetValue(el, "strHomeTeamBadge")),
                StrAwayTeamBadge: NullTrim(GetValue(el, "strAwayTeamBadge"))
            ));
        }
        return list;

        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static int? TryParseInt(string? s) => int.TryParse(s, out var n) ? n : null;
        static DateTime? ParseEventDateUtc(string? dateEvent, string? strTime)
        {
            if (string.IsNullOrWhiteSpace(dateEvent)) return null;
            if (!DateTime.TryParse(dateEvent.Trim(), out var d)) return null;
            if (!string.IsNullOrWhiteSpace(strTime) && TimeSpan.TryParse(strTime.Trim(), out var t))
                d = d.Date.Add(t);
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
    }

    /// <summary>v2: GET lookup/player/{id}. Response: {"lookup": [playerObj]}.</summary>
    public async Task<SportsDbPlayerLookup?> LookupPlayerAsync(string playerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return null;
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/player/{Uri.EscapeDataString(playerId)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParsePlayerLookup(json);
    }

    private static SportsDbPlayerLookup? ParsePlayerLookup(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("lookup", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        var id = GetValue(first, "idPlayer");
        var name = GetValue(first, "strPlayer");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;
        return new SportsDbPlayerLookup(
            IdPlayer: id.Trim(),
            StrPlayer: name.Trim(),
            StrPosition: NullTrim(GetValue(first, "strPosition")),
            StrNationality: NullTrim(GetValue(first, "strNationality")),
            DateBorn: NullTrim(GetValue(first, "dateBorn")),
            StrThumb: NullTrim(GetValue(first, "strThumb")),
            StrTeam: NullTrim(GetValue(first, "strTeam")),
            StrNumber: NullTrim(GetValue(first, "strNumber")),
            StrHeight: NullTrim(GetValue(first, "strHeight")),
            StrWeight: NullTrim(GetValue(first, "strWeight")),
            StrWage: NullTrim(GetValue(first, "strWage")),
            StrSigning: NullTrim(GetValue(first, "strSigning")),
            StrCartoon: NullTrim(GetValue(first, "strCartoon")),
            StrCutout: NullTrim(GetValue(first, "strCutout")),
            StrRender: NullTrim(GetValue(first, "strRender")),
            StrBanner: NullTrim(GetValue(first, "strBanner")),
            StrInstagram: NullTrim(GetValue(first, "strInstagram")),
            StrFacebook: NullTrim(GetValue(first, "strFacebook")),
            StrTwitter: NullTrim(GetValue(first, "strTwitter")),
            StrYoutube: NullTrim(GetValue(first, "strYoutube")),
            StrWebsite: NullTrim(GetValue(first, "strWebsite")),
            StrDescriptionEN: NullTrim(GetValue(first, "strDescriptionEN")),
            StrForm: NullTrim(GetValue(first, "strForm")),
            StrStats: NullTrim(GetValue(first, "strStats")),
            StrStatus: NullTrim(GetValue(first, "strStatus")),
            StrGender: NullTrim(GetValue(first, "strGender")),
            StrSide: NullTrim(GetValue(first, "strSide")),
            StrCollege: NullTrim(GetValue(first, "strCollege")),
            StrPoster: NullTrim(GetValue(first, "strPoster")),
            StrFanart1: NullTrim(GetValue(first, "strFanart1")),
            StrFanart2: NullTrim(GetValue(first, "strFanart2")),
            StrFanart3: NullTrim(GetValue(first, "strFanart3")),
            StrFanart4: NullTrim(GetValue(first, "strFanart4")),
            StrKit: NullTrim(GetValue(first, "strKit")) ?? NullTrim(GetValue(first, "strJersey")),
            StrAgent: NullTrim(GetValue(first, "strAgent")),
            StrBirthLocation: NullTrim(GetValue(first, "strBirthLocation")),
            StrEthnicity: NullTrim(GetValue(first, "strEthnicity"))
        );
        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>v2: GET lookup/event/{idEvent}. Core match info.</summary>
    public async Task<MatchDetailDto?> LookupEventAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return null;
        var json = await GetStringOrNullAsync($"lookup/event/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? null : ParseEventLookup(json);
    }

    private static MatchDetailDto? ParseEventLookup(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("lookup", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var first = arr.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object) return null;
        var id = GetValue(first, "idEvent");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var dateEvent = GetValue(first, "dateEvent");
        var strTime = GetValue(first, "strTime");
        var utc = ParseEventDateUtc(dateEvent, strTime);
        return new MatchDetailDto
        {
            IdEvent = id.Trim(),
            StrEvent = NullTrim(GetValue(first, "strEvent")),
            StrFilename = NullTrim(GetValue(first, "strFilename")),
            StrSport = NullTrim(GetValue(first, "strSport")),
            IdLeague = NullTrim(GetValue(first, "idLeague")),
            StrLeague = NullTrim(GetValue(first, "strLeague")),
            StrLeagueBadge = NullTrim(GetValue(first, "strLeagueBadge")),
            StrSeason = NullTrim(GetValue(first, "strSeason")),
            StrRound = NullTrim(GetValue(first, "strRound")),
            IdHomeTeam = NullTrim(GetValue(first, "idHomeTeam") ?? GetValue(first, "idTeamHome")),
            IdAwayTeam = NullTrim(GetValue(first, "idAwayTeam") ?? GetValue(first, "idTeamAway")),
            StrHomeTeam = NullTrim(GetValue(first, "strHomeTeam")),
            StrAwayTeam = NullTrim(GetValue(first, "strAwayTeam")),
            StrHomeTeamBadge = NullTrim(GetValue(first, "strHomeTeamBadge")),
            StrAwayTeamBadge = NullTrim(GetValue(first, "strAwayTeamBadge")),
            DateEvent = NullTrim(dateEvent),
            StrTime = NullTrim(strTime),
            UtcDate = utc,
            IntHomeScore = TryParseInt(GetValue(first, "intHomeScore")),
            IntAwayScore = TryParseInt(GetValue(first, "intAwayScore")),
            StrStatus = NullTrim(GetValue(first, "strStatus")),
            StrProgress = NullTrim(GetValue(first, "strProgress")),
            StrResult = NullTrim(GetValue(first, "strResult")),
            StrVenue = NullTrim(GetValue(first, "strVenue")),
            IdVenue = NullTrim(GetValue(first, "idVenue")),
            StrCountry = NullTrim(GetValue(first, "strCountry")),
            StrThumb = NullTrim(GetValue(first, "strThumb")),
            StrBanner = NullTrim(GetValue(first, "strBanner")),
            StrPoster = NullTrim(GetValue(first, "strPoster")),
            StrFanart = NullTrim(GetValue(first, "strFanart"))
        };

        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static int? TryParseInt(string? s) => int.TryParse(s, out var n) ? n : null;
        static DateTime? ParseEventDateUtc(string? dateEvent, string? strTime)
        {
            if (string.IsNullOrWhiteSpace(dateEvent)) return null;
            if (!DateTime.TryParse(dateEvent.Trim(), out var d)) return null;
            if (!string.IsNullOrWhiteSpace(strTime) && TimeSpan.TryParse(strTime.Trim(), out var t))
                d = d.Date.Add(t);
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
    }

    /// <summary>v2: GET lookup/event_lineup/{idEvent}. Home/away lineups.</summary>
    public async Task<EventLineupDto?> LookupEventLineupAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return null;
        var json = await GetStringOrNullAsync($"lookup/event_lineup/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? null : ParseEventLineup(json);
    }

    private static EventLineupDto? ParseEventLineup(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var lineup = new EventLineupDto();
        foreach (var key in new[] { "lookup", "lineup", "lineups" })
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            var homePlayers = new List<EventLineupPlayerDto>();
            var awayPlayers = new List<EventLineupPlayerDto>();
            string? idHome = GetValue(doc.RootElement, "idHomeTeam");
            string? idAway = GetValue(doc.RootElement, "idAwayTeam");
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var idTeam = GetValue(el, "idTeam");
                var strTeam = GetValue(el, "strTeam");
                var isHome = !string.IsNullOrEmpty(idHome) && idTeam == idHome
                    || !string.IsNullOrEmpty(idAway) && idTeam != idAway
                    || (string.IsNullOrEmpty(idHome) && string.IsNullOrEmpty(idAway) && homePlayers.Count < 12);
                var p = new EventLineupPlayerDto
                {
                    IdPlayer = NullTrim(GetValue(el, "idPlayer")),
                    StrPlayer = NullTrim(GetValue(el, "strPlayer")),
                    StrPosition = NullTrim(GetValue(el, "strPosition")),
                    StrNumber = NullTrim(GetValue(el, "strNumber")),
                    StrGrid = NullTrim(GetValue(el, "strGrid")),
                    StrRole = NullTrim(GetValue(el, "strRole")),
                    IdTeam = NullTrim(idTeam),
                    StrTeam = NullTrim(strTeam),
                    StrThumb = NullTrim(GetValue(el, "strThumb")),
                    StrCutout = NullTrim(GetValue(el, "strCutout")),
                    StrNationality = NullTrim(GetValue(el, "strNationality"))
                };
                if (isHome) homePlayers.Add(p); else awayPlayers.Add(p);
            }
            if (homePlayers.Count > 0 || awayPlayers.Count > 0)
            {
                lineup.Home = new EventLineupTeamDto { IdTeam = idHome, StrTeam = homePlayers.FirstOrDefault()?.StrTeam, StrTeamBadge = NullTrim(GetValue(doc.RootElement, "strHomeTeamBadge")), Players = homePlayers };
                lineup.Away = new EventLineupTeamDto { IdTeam = idAway, StrTeam = awayPlayers.FirstOrDefault()?.StrTeam, StrTeamBadge = NullTrim(GetValue(doc.RootElement, "strAwayTeamBadge")), Players = awayPlayers };
                return lineup;
            }
        }
        return null;
    }

    /// <summary>v2: GET lookup/event_timeline/{idEvent}. Goals, cards, subs.</summary>
    public async Task<List<EventTimelineItemDto>> LookupEventTimelineAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return new List<EventTimelineItemDto>();
        var json = await GetStringOrNullAsync($"lookup/event_timeline/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? new List<EventTimelineItemDto>() : ParseEventTimeline(json);
    }

    private static List<EventTimelineItemDto> ParseEventTimeline(string json)
    {
        var list = new List<EventTimelineItemDto>();
        using var doc = JsonDocument.Parse(json);
        foreach (var key in new[] { "lookup", "timeline", "timelines" })
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                list.Add(new EventTimelineItemDto
                {
                    StrTime = NullTrim(GetValue(el, "strTime")),
                    StrTimeline = NullTrim(GetValue(el, "strTimeline")),
                    StrPlayer = NullTrim(GetValue(el, "strPlayer")),
                    StrTeam = NullTrim(GetValue(el, "strTeam")),
                    IdPlayer = NullTrim(GetValue(el, "idPlayer")),
                    IdTeam = NullTrim(GetValue(el, "idTeam")),
                    StrDetail = NullTrim(GetValue(el, "strDetail")),
                    StrEvent = NullTrim(GetValue(el, "strEvent"))
                });
            }
            break;
        }
        return list.OrderBy(x => int.TryParse(x.StrTime, out var m) ? m : 999).ToList();
    }

    /// <summary>v2: GET lookup/event_stats/{idEvent}. Possession, shots, etc.</summary>
    public async Task<List<EventStatRowDto>> LookupEventStatsAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return new List<EventStatRowDto>();
        var json = await GetStringOrNullAsync($"lookup/event_stats/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? new List<EventStatRowDto>() : ParseEventStats(json);
    }

    private static List<EventStatRowDto> ParseEventStats(string json)
    {
        var list = new List<EventStatRowDto>();
        using var doc = JsonDocument.Parse(json);
        foreach (var key in new[] { "lookup", "stats", "statistics" })
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                list.Add(new EventStatRowDto
                {
                    StrStat = NullTrim(GetValue(el, "strStat")),
                    IntHome = TryParseInt(GetValue(el, "intHome")),
                    IntAway = TryParseInt(GetValue(el, "intAway"))
                });
            }
            break;
        }
        return list;
    }

    /// <summary>v2: GET lookup/event_highlights/{idEvent}. YouTube links.</summary>
    public async Task<List<EventHighlightDto>> LookupEventHighlightsAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return new List<EventHighlightDto>();
        var json = await GetStringOrNullAsync($"lookup/event_highlights/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? new List<EventHighlightDto>() : ParseEventHighlights(json);
    }

    private static List<EventHighlightDto> ParseEventHighlights(string json)
    {
        var list = new List<EventHighlightDto>();
        using var doc = JsonDocument.Parse(json);
        foreach (var key in new[] { "lookup", "highlights", "videos" })
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var url = GetValue(el, "strVideo") ?? GetValue(el, "strURL");
                if (string.IsNullOrWhiteSpace(url)) continue;
                list.Add(new EventHighlightDto
                {
                    StrVideo = url.Trim(),
                    StrThumb = NullTrim(GetValue(el, "strThumb")),
                    StrTitle = NullTrim(GetValue(el, "strTitle"))
                });
            }
            break;
        }
        return list;
    }

    /// <summary>v2: GET lookup/event_tv/{idEvent}. TV channels/regions.</summary>
    public async Task<List<EventTvDto>> LookupEventTvAsync(string idEvent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idEvent)) return new List<EventTvDto>();
        var json = await GetStringOrNullAsync($"lookup/event_tv/{Uri.EscapeDataString(idEvent)}", ct);
        return string.IsNullOrWhiteSpace(json) ? new List<EventTvDto>() : ParseEventTv(json);
    }

    private static List<EventTvDto> ParseEventTv(string json)
    {
        var list = new List<EventTvDto>();
        using var doc = JsonDocument.Parse(json);
        foreach (var key in new[] { "lookup", "tv", "tvlist" })
        {
            if (!doc.RootElement.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                list.Add(new EventTvDto
                {
                    StrChannel = NullTrim(GetValue(el, "strChannel")),
                    StrCountry = NullTrim(GetValue(el, "strCountry"))
                });
            }
            break;
        }
        return list;
    }

    /// <summary>v2: GET lookup/player_contracts/{id}. Response often: {"lookup": [...]} or {"contracts": [...]}.</summary>
    public async Task<List<PlayerContractDto>> LookupPlayerContractsAsync(string playerId, CancellationToken ct = default)
    {
        return await LookupPlayerArrayAsync(playerId, "player_contracts", new[] { "lookup", "contracts", "player_contracts" }, ParseContract, ct);
    }

    /// <summary>v2: GET lookup/player_teams/{id}. Former teams / career.</summary>
    public async Task<List<PlayerFormerTeamDto>> LookupPlayerFormerTeamsAsync(string playerId, CancellationToken ct = default)
    {
        return await LookupPlayerArrayAsync(playerId, "player_teams", new[] { "lookup", "formerteams", "player_teams", "teams" }, ParseFormerTeam, ct);
    }

    /// <summary>v2: GET lookup/player_honours/{id}.</summary>
    public async Task<List<PlayerHonourDto>> LookupPlayerHonoursAsync(string playerId, CancellationToken ct = default)
    {
        return await LookupPlayerArrayAsync(playerId, "player_honours", new[] { "lookup", "honours", "player_honours" }, ParseHonour, ct);
    }

    /// <summary>v2: GET lookup/player_milestones/{id}.</summary>
    public async Task<List<PlayerMilestoneDto>> LookupPlayerMilestonesAsync(string playerId, CancellationToken ct = default)
    {
        return await LookupPlayerArrayAsync(playerId, "player_milestones", new[] { "lookup", "milestones", "milestone", "player_milestones" }, ParseMilestone, ct);
    }

    /// <summary>v2: GET lookup/player_results/{id}. Recent results.</summary>
    public async Task<List<PlayerResultDto>> LookupPlayerResultsAsync(string playerId, CancellationToken ct = default)
    {
        return await LookupPlayerArrayAsync(playerId, "player_results", new[] { "lookup", "results", "player_results" }, ParsePlayerResult, ct);
    }

    private async Task<List<T>> LookupPlayerArrayAsync<T>(string playerId, string pathSegment, string[] arrayKeys, Func<JsonElement, T> parse, CancellationToken ct)
    {
        var list = new List<T>();
        if (string.IsNullOrWhiteSpace(playerId)) return list;
        using var http = Create();
        using var resp = await http.GetAsync($"lookup/{pathSegment}/{Uri.EscapeDataString(playerId)}", ct);
        if (!resp.IsSuccessStatusCode) return list;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        foreach (var key in arrayKeys)
        {
            if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    try
                    {
                        list.Add(parse(el));
                    }
                    catch
                    {
                        // skip malformed item
                    }
                }
                break;
            }
        }
        return list;
    }

    private static string? GetValueOr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
        {
            var v = GetValue(el, k);
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    private static PlayerContractDto ParseContract(JsonElement el)
    {
        return new PlayerContractDto
        {
            TeamId = NullTrim(GetValue(el, "idTeam")),
            TeamName = GetValueOr(el, "strTeam", "strTeamName"),
            TeamBadge = GetValueOr(el, "strTeamBadge", "strBadge"),
            Sport = GetValue(el, "strSport"),
            YearStart = GetValueOr(el, "strYearStart", "strSeasonStart"),
            YearEnd = GetValueOr(el, "strYearEnd", "strSeasonEnd"),
            Wage = GetValue(el, "strWage"),
            Signing = GetValue(el, "strSigning")
        };
    }

    private static PlayerFormerTeamDto ParseFormerTeam(JsonElement el)
    {
        var teamName = GetValueOr(el, "strTeam", "strTeamName", "strFormerTeam");
        var contractType = GetValueOr(el, "strContractType", "strMoveType", "strType");
        if (string.IsNullOrWhiteSpace(contractType) && !string.IsNullOrWhiteSpace(teamName) && teamName.Contains("Youth", StringComparison.OrdinalIgnoreCase))
            contractType = "Youth";
        return new PlayerFormerTeamDto
        {
            TeamId = NullTrim(GetValue(el, "idTeam")),
            TeamName = teamName,
            TeamBadge = GetValueOr(el, "strTeamBadge", "strBadge"),
            Sport = GetValue(el, "strSport"),
            YearStart = GetValueOr(el, "strYearStart", "strSeasonStart"),
            YearEnd = GetValueOr(el, "strYearEnd", "strSeasonEnd"),
            Joined = GetValue(el, "strJoined"),
            Departed = GetValue(el, "strDeparted"),
            ContractType = contractType
        };
    }

    private static PlayerHonourDto ParseHonour(JsonElement el)
    {
        return new PlayerHonourDto
        {
            Title = GetValueOr(el, "strTitle", "strHonour"),
            Competition = GetValueOr(el, "strCompetition", "strLeague"),
            Season = GetValue(el, "strSeason"),
            Honour = GetValueOr(el, "strHonour", "strTitle"),
            HonourLogoUrl = GetValueOr(el, "strHonourLogo", "strHonourLogos", "strBadge", "strLogo"),
            TrophyUrl = GetValueOr(el, "strHonourTrophy", "strTrophy")
        };
    }

    private static PlayerMilestoneDto ParseMilestone(JsonElement el)
    {
        return new PlayerMilestoneDto
        {
            Name = GetValueOr(el, "strName", "strDescription", "strMilestone"),
            Description = GetValueOr(el, "strDescription", "strName", "strMilestone"),
            Season = GetValue(el, "strSeason"),
            Stat = GetValue(el, "strStat"),
            Team = GetValue(el, "strTeam"),
            MilestoneLogoUrl = GetValue(el, "strMilestoneLogo"),
            DateMilestone = GetValue(el, "dateMilestone"),
            BadgeUrl = GetValueOr(el, "strMilestoneLogo", "strBadge", "strThumb", "strLogo")
        };
    }

    private static PlayerResultDto ParsePlayerResult(JsonElement el)
    {
        var homeScoreStr = GetValue(el, "intHomeScore");
        var awayScoreStr = GetValue(el, "intAwayScore");
        int? homeScore = int.TryParse(homeScoreStr, out var h) ? h : null;
        int? awayScore = int.TryParse(awayScoreStr, out var a) ? a : null;
        return new PlayerResultDto
        {
            EventName = GetValue(el, "strEvent"),
            League = GetValue(el, "strLeague"),
            Season = GetValue(el, "strSeason"),
            DateEvent = GetValue(el, "dateEvent"),
            HomeTeam = GetValue(el, "strHomeTeam"),
            AwayTeam = GetValue(el, "strAwayTeam"),
            HomeScore = homeScore,
            AwayScore = awayScore,
            Result = GetValue(el, "strResult")
        };
    }

    /// <summary>v2: GET list/players/{teamId}. Response: {"list": [playerObj, ...]}.</summary>
    public async Task<List<TeamPlayerDto>> GetTeamPlayersAsync(string sportsDbTeamId, CancellationToken ct)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"list/players/{Uri.EscapeDataString(sportsDbTeamId)}", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParsePlayersV2(json);
    }

    private static List<TeamPlayerDto> ParsePlayersV2(string json)
    {
        var players = new List<TeamPlayerDto>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            return players;

        foreach (var p in list.EnumerateArray())
        {
            var name = GetValue(p, "strPlayer");
            if (string.IsNullOrEmpty(name)) continue;

            players.Add(new TeamPlayerDto
            {
                SportsDbPlayerId = GetValue(p, "idPlayer"),
                Name = name,
                Position = GetValue(p, "strPosition"),
                Nationality = GetValue(p, "strNationality"),
                ThumbUrl = GetValue(p, "strThumb"),
                CutoutUrl = GetValue(p, "strCutout"),
                RenderUrl = GetValue(p, "strRender"),
            });
        }

        return players;
    }

    private static string? GetValue(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

    private static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static int? TryParseInt(string? s) => int.TryParse(s, out var n) ? n : null;
    
    public async Task<List<SportsDbScheduleEvent>> GetNextTeamEventsAsync(string teamId, CancellationToken ct = default)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/next/team/{Uri.EscapeDataString(teamId)}", ct);
        if (!resp.IsSuccessStatusCode) return new();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseScheduleEvents(json);
    }

    public async Task<List<SportsDbScheduleEvent>> GetPreviousTeamEventsAsync(string teamId, CancellationToken ct = default)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/previous/team/{Uri.EscapeDataString(teamId)}", ct);
        if (!resp.IsSuccessStatusCode) return new();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseScheduleEvents(json);
    }

    /// <summary>v2: GET schedule/next/venue/{venueId}. Upcoming events at this venue.</summary>
    public async Task<List<SportsDbScheduleEvent>> GetNextVenueEventsAsync(string venueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(venueId)) return new();
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/next/venue/{Uri.EscapeDataString(venueId.Trim())}", ct);
        if (!resp.IsSuccessStatusCode) return new();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseScheduleEvents(json);
    }

    /// <summary>v2: GET schedule/previous/venue/{venueId}. Recent events at this venue.</summary>
    public async Task<List<SportsDbScheduleEvent>> GetPreviousVenueEventsAsync(string venueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(venueId)) return new();
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/previous/venue/{Uri.EscapeDataString(venueId.Trim())}", ct);
        if (!resp.IsSuccessStatusCode) return new();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseScheduleEvents(json);
    }

    public async Task<List<SportsDbScheduleEvent>> GetFullTeamScheduleAsync(string teamId, CancellationToken ct = default)
    {
        using var http = Create();
        using var resp = await http.GetAsync($"schedule/full/team/{Uri.EscapeDataString(teamId)}", ct);
        if (!resp.IsSuccessStatusCode) return new();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseScheduleEvents(json);
    }

    private static List<SportsDbScheduleEvent> ParseScheduleEvents(string json)
    {
        var list = new List<SportsDbScheduleEvent>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("events", out var arr))
            doc.RootElement.TryGetProperty("schedule", out arr);

        if (arr.ValueKind != JsonValueKind.Array) return list;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;

            var home = GetValue(el, "strHomeTeam");
            var away = GetValue(el, "strAwayTeam");
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;

            var dateEvent = GetValue(el, "dateEvent");
            var time = GetValue(el, "strTime");
            var dateUtc = ParseEventDateUtc(dateEvent, time);
            var idHome =
                GetValue(el, "idHomeTeam") ??
                GetValue(el, "idTeamHome");

            var idAway =
                GetValue(el, "idAwayTeam") ??
                GetValue(el, "idTeamAway");

            list.Add(new SportsDbScheduleEvent(
                IdEvent: GetValue(el, "idEvent"),
                IdLeague: GetValue(el, "idLeague"),
                StrLeague: GetValue(el, "strLeague"),

                IdHomeTeam: idHome,
                IdAwayTeam: idAway,

                StrHomeTeam: home.Trim(),
                StrAwayTeam: away.Trim(),
                DateEvent: string.IsNullOrWhiteSpace(dateEvent) ? null : dateEvent.Trim(),
                StrTime: time,
                DateUtc: dateUtc,
                IntHomeScore: TryParseInt(GetValue(el, "intHomeScore")),
                IntAwayScore: TryParseInt(GetValue(el, "intAwayScore")),
                StrStatus: GetValue(el, "strStatus") ?? GetValue(el, "strProgress"),
                StrHomeTeamBadge: NullTrim(GetValue(el, "strHomeTeamBadge")),
                StrAwayTeamBadge: NullTrim(GetValue(el, "strAwayTeamBadge"))
            ));
        }

        return list;

        static string? NullTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static int? TryParseInt(string? s) => int.TryParse(s, out var n) ? n : null;

        static DateTime? ParseEventDateUtc(string? dateEvent, string? strTime)
        {
            if (string.IsNullOrWhiteSpace(dateEvent)) return null;
            if (!DateTime.TryParse(dateEvent.Trim(), out var d)) return null;

            if (!string.IsNullOrWhiteSpace(strTime) && TimeSpan.TryParse(strTime.Trim(), out var t))
                d = d.Date.Add(t);

            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
    }
}

public sealed record SportsDbLeague(
    string? IdLeague,
    string? StrLeague,
    string? StrLeagueAlternate,
    string? StrSport,
    string? StrGender,
    string? IntFormedYear,
    string? DateFirstEvent,
    string? StrCurrentSeason,
    string? IntCurrentRound,
    string? StrCountry,
    string? StrDescriptionEN,
    string? StrTrophy,
    string? StrPoster,
    string? StrBanner,
    string? StrFanart1,
    string? StrFanart2,
    string? StrFanart3,
    string? StrFanart4,
    string? StrBadge,
    string? StrLogo,
    string? StrTvRights,
    string? StrWebsite,
    string? StrFacebook,
    string? StrTwitter,
    string? StrInstagram,
    string? StrYoutube,
    string? StrRSS
)
{
    public static SportsDbLeague FromJson(JsonElement e)
    {
        static string? Get(JsonElement el, string name)
            => el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null;

        return new SportsDbLeague(
            IdLeague: Get(e, "idLeague"),
            StrLeague: Get(e, "strLeague"),
            StrLeagueAlternate: Get(e, "strLeagueAlternate"),
            StrSport: Get(e, "strSport"),
            StrGender: Get(e, "strGender"),
            IntFormedYear: Get(e, "intFormedYear"),
            DateFirstEvent: Get(e, "dateFirstEvent"),
            StrCurrentSeason: Get(e, "strCurrentSeason"),
            IntCurrentRound: Get(e, "intCurrentRound"),
            StrCountry: Get(e, "strCountry"),
            StrDescriptionEN: Get(e, "strDescriptionEN"),
            StrTrophy: Get(e, "strTrophy"),
            StrPoster: Get(e, "strPoster"),
            StrBanner: Get(e, "strBanner"),
            StrFanart1: Get(e, "strFanart1"),
            StrFanart2: Get(e, "strFanart2"),
            StrFanart3: Get(e, "strFanart3"),
            StrFanart4: Get(e, "strFanart4"),
            StrBadge: Get(e, "strBadge"),
            StrLogo: Get(e, "strLogo"),
            StrTvRights: Get(e, "strTVRights"),
            StrWebsite: Get(e, "strWebsite"),
            StrFacebook: Get(e, "strFacebook"),
            StrTwitter: Get(e, "strTwitter"),
            StrInstagram: Get(e, "strInstagram"),
            StrYoutube: Get(e, "strYoutube"),
            StrRSS: Get(e, "strRSS")
        );
    }
}

public sealed record SportsDbLeagueSearchResult(string IdLeague, string StrLeague, string? StrBadge, string? StrCountry);

/// <summary>Minimal league item from v2 all/leagues (Soccer only).</summary>
public sealed record SportsDbLeagueListItem(string IdLeague, string StrLeague);

public sealed record SportsDbPlayerSearchResult(string IdPlayer, string StrPlayer, string? StrPosition, string? StrTeam, string? StrSport, string? StrThumb);

public sealed record SportsDbPlayerLookup(
    string IdPlayer,
    string StrPlayer,
    string? StrPosition,
    string? StrNationality,
    string? DateBorn,
    string? StrThumb,
    string? StrTeam,
    string? StrNumber = null,
    string? StrHeight = null,
    string? StrWeight = null,
    string? StrWage = null,
    string? StrSigning = null,
    string? StrCartoon = null,
    string? StrCutout = null,
    string? StrRender = null,
    string? StrBanner = null,
    string? StrInstagram = null,
    string? StrFacebook = null,
    string? StrTwitter = null,
    string? StrYoutube = null,
    string? StrWebsite = null,
    string? StrDescriptionEN = null,
    string? StrForm = null,
    string? StrStats = null,
    string? StrStatus = null,
    string? StrGender = null,
    string? StrSide = null,
    string? StrCollege = null,
    string? StrPoster = null,
    string? StrFanart1 = null,
    string? StrFanart2 = null,
    string? StrFanart3 = null,
    string? StrFanart4 = null,
    string? StrKit = null,
    string? StrAgent = null,
    string? StrBirthLocation = null,
    string? StrEthnicity = null);

public sealed record SportsDbVenueSearchResult(string IdVenue, string StrVenue, string? StrLocation, string? StrCountry, string? StrThumb);

public sealed record SportsDbVenueLookup(
    string IdVenue,
    string StrVenue,
    string? StrLocation,
    string? StrCountry,
    int? IntCapacity,
    string? StrThumb,
    int? IntFormedYear = null,
    string? StrMap = null,
    string? StrDescriptionEN = null,
    string? StrCost = null,
    string? StrFanart1 = null,
    string? StrFanart2 = null,
    string? StrFanart3 = null,
    string? StrFanart4 = null,
    string? StrWebsite = null,
    string? StrAlternate = null,
    string? StrTimezone = null,
    string? StrLogo = null);

public sealed record SportsDbScheduleEvent(
    string? IdEvent,
    string? IdLeague,
    string? StrLeague,

    string? IdHomeTeam,
    string? IdAwayTeam,

    string StrHomeTeam,
    string StrAwayTeam,

    string? DateEvent,
    string? StrTime,
    DateTime? DateUtc,

    int? IntHomeScore,
    int? IntAwayScore,
    string? StrStatus,

    string? StrHomeTeamBadge,
    string? StrAwayTeamBadge
);

public sealed record SportsDbTeam(
    string? IdTeam,
    string? StrTeam,
    string? StrSport,
    string? StrGender,
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
    string? StrFanart1,
    string? StrFanart2,
    string? StrFanart3,
    string? StrFanart4,
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
            StrSport: Get(e, "strSport"),
            StrGender: Get(e, "strGender"),
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
            StrFanart1: Get(e, "strFanart1"),
            StrFanart2: Get(e, "strFanart2"),
            StrFanart3: Get(e, "strFanart3"),
            StrFanart4: Get(e, "strFanart4"),
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
