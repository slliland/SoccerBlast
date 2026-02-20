using System.Text.Json.Serialization;

namespace SoccerBlast.Api.Services;

/// <summary>TheSportsDB v1 eventsday.php response: { "events": [ ... ] }</summary>
public class SportsDbEventsResponse
{
    [JsonPropertyName("events")]
    public List<SportsDbEvent>? Events { get; set; }
}

/// <summary>Single event from eventsday.php (Soccer). IDs are strings; we parse to int for our DB.</summary>
public class SportsDbEvent
{
    [JsonPropertyName("idEvent")]
    public string? IdEvent { get; set; }

    [JsonPropertyName("idLeague")]
    public string? IdLeague { get; set; }

    [JsonPropertyName("strLeague")]
    public string? StrLeague { get; set; }

    [JsonPropertyName("strLeagueBadge")]
    public string? StrLeagueBadge { get; set; }

    [JsonPropertyName("strSeason")]
    public string? StrSeason { get; set; }

    [JsonPropertyName("strHomeTeam")]
    public string? StrHomeTeam { get; set; }

    [JsonPropertyName("strAwayTeam")]
    public string? StrAwayTeam { get; set; }

    [JsonPropertyName("idHomeTeam")]
    public string? IdHomeTeam { get; set; }

    [JsonPropertyName("idAwayTeam")]
    public string? IdAwayTeam { get; set; }

    [JsonPropertyName("strHomeTeamBadge")]
    public string? StrHomeTeamBadge { get; set; }

    [JsonPropertyName("strAwayTeamBadge")]
    public string? StrAwayTeamBadge { get; set; }

    [JsonPropertyName("dateEvent")]
    public string? DateEvent { get; set; }

    [JsonPropertyName("strTime")]
    public string? StrTime { get; set; }

    [JsonPropertyName("strTimestamp")]
    public string? StrTimestamp { get; set; }

    [JsonPropertyName("intHomeScore")]
    public string? IntHomeScore { get; set; }

    [JsonPropertyName("intAwayScore")]
    public string? IntAwayScore { get; set; }

    [JsonPropertyName("strStatus")]
    public string? StrStatus { get; set; }

    [JsonPropertyName("strSport")]
    public string? StrSport { get; set; }

    [JsonPropertyName("strCountry")]
    public string? StrCountry { get; set; }
}
