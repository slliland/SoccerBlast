using System.Text.Json.Serialization;

namespace SoccerBlast.Api.Services;

// Response: { "matches": [ ... ] }
public class MatchesResponse
{
    [JsonPropertyName("matches")]
    public List<MatchItem> Matches { get; set; } = [];
}

public class MatchItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("utcDate")]
    public DateTime UtcDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("competition")]
    public CompetitionItem Competition { get; set; } = new();

    [JsonPropertyName("homeTeam")]
    public TeamItem HomeTeam { get; set; } = new();

    [JsonPropertyName("awayTeam")]
    public TeamItem AwayTeam { get; set; } = new();

    [JsonPropertyName("score")]
    public ScoreItem Score { get; set; } = new();
}

public class CompetitionItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("area")]
    public AreaItem? Area { get; set; }
}

public class TeamDetailsResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("crest")]
    public string? Crest { get; set; }
}

public class AreaItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class TeamItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("crest")]
    public string? Crest { get; set; }
}

public class ScoreItem
{
    [JsonPropertyName("fullTime")]
    public ScoreTime FullTime { get; set; } = new();
}

public class ScoreTime
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}
