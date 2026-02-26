using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace SoccerBlast.Api.Services;

/// <summary>Fetches soccer matches from TheSportsDB v1 eventsday.php (v2 has no events-by-day endpoint).</summary>
public class SportsDbMatchesClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SportsDbMatchesClient> _log;

    public SportsDbMatchesClient(IHttpClientFactory httpFactory, IOptions<TheSportsDbOptions> _, ILogger<SportsDbMatchesClient> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    private HttpClient Create() => _httpFactory.CreateClient("sportsdb-v1");

    /// <summary>Get soccer events for a single day (v1 eventsday.php?d=yyyy-MM-dd&amp;s=Soccer). Premium: 1500/day.</summary>
    public async Task<List<SportsDbEvent>> GetEventsByDayAsync(DateTime dateUtc, CancellationToken ct = default)
    {
        var dateStr = dateUtc.ToString("yyyy-MM-dd");
        var url = $"eventsday.php?d={dateStr}&s=Soccer";
        _log.LogInformation("[SportsDB] eventsday d={Date} ...", dateStr);
        using var http = Create();
        var resp = await http.GetFromJsonAsync<SportsDbEventsResponse>(url, ct);
        var count = resp?.Events?.Count ?? 0;
        _log.LogInformation("[SportsDB] eventsday d={Date} returned {Count} events", dateStr, count);
        return resp?.Events ?? [];
    }

    /// <summary>Get soccer events for a date range; calls eventsday for each day. Returns events mapped to our MatchItem shape (int IDs, crests).</summary>
    public async Task<List<MatchItem>> GetMatchesAsync(DateTime dateFromUtc, DateTime dateToUtc, CancellationToken ct = default)
    {
        var results = new List<MatchItem>();
        var from = dateFromUtc.Date;
        var to = dateToUtc.Date;
        _log.LogInformation("[SportsDB] GetMatchesAsync from={From} to={To}", from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var events = await GetEventsByDayAsync(d, ct);
            foreach (var e in events)
            {
                var item = ToMatchItem(e);
                if (item != null)
                    results.Add(item);
            }
        }

        return results;
    }

    private static MatchItem? ToMatchItem(SportsDbEvent e)
    {
        if (string.IsNullOrEmpty(e.IdEvent) || !int.TryParse(e.IdEvent, out var matchId)) return null;
        if (string.IsNullOrEmpty(e.IdLeague) || !int.TryParse(e.IdLeague, out var compId)) return null;
        if (string.IsNullOrEmpty(e.IdHomeTeam) || !int.TryParse(e.IdHomeTeam, out var homeId)) return null;
        if (string.IsNullOrEmpty(e.IdAwayTeam) || !int.TryParse(e.IdAwayTeam, out var awayId)) return null;

        var utcDate = ParseEventUtc(e.DateEvent, e.StrTime, e.StrTimestamp);
        int? homeScore = int.TryParse(e.IntHomeScore, out var hs) ? hs : null;
        int? awayScore = int.TryParse(e.IntAwayScore, out var @as) ? @as : null;

        return new MatchItem
        {
            Id = matchId,
            UtcDate = utcDate,
            Status = MapStatus(e.StrStatus),
            Competition = new CompetitionItem
            {
                Id = compId,
                Name = e.StrLeague ?? "",
                Area = string.IsNullOrEmpty(e.StrCountry) ? null : new AreaItem { Name = e.StrCountry },
                Crest = e.StrLeagueBadge
            },
            HomeTeam = new TeamItem
            {
                Id = homeId,
                Name = e.StrHomeTeam ?? "",
                Crest = e.StrHomeTeamBadge
            },
            AwayTeam = new TeamItem
            {
                Id = awayId,
                Name = e.StrAwayTeam ?? "",
                Crest = e.StrAwayTeamBadge
            },
            Score = new ScoreItem
            {
                FullTime = new ScoreTime { Home = homeScore, Away = awayScore }
            }
        };
    }

    private static DateTime ParseEventUtc(string? dateEvent, string? strTime, string? strTimestamp)
    {
        if (DateTime.TryParse(strTimestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        if (!DateTime.TryParse(dateEvent, out var d)) d = DateTime.UtcNow.Date;
        if (!string.IsNullOrEmpty(strTime) && TimeSpan.TryParse(strTime, out var t))
            d = d.Add(t);
        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    private static string MapStatus(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "SCHEDULED";
        var u = (s ?? "").ToUpperInvariant();
        if (u.Contains("FINISHED") || u == "FT" || u == "AET" || u == "PEN_LIVE") return "FINISHED";
        return u switch
        {
            "NS" or "TBD" => "SCHEDULED",
            "1H" or "2H" or "HT" or "ET" or "P" or "SUSP" or "INT" => "IN_PLAY",
            "PST" or "CANC" or "ABD" or "AWD" or "WO" => "CANCELLED",
            _ => s ?? "SCHEDULED"
        };
    }
}
