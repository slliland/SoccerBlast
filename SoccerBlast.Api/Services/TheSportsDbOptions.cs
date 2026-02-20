namespace SoccerBlast.Api.Services;

public class TheSportsDbOptions
{
    /// <summary>v2 base URL (team/league/players lookup, search, list). Auth via X-API-KEY header.</summary>
    public string BaseUrl { get; set; } = "https://www.thesportsdb.com/api/v2/json";

    /// <summary>v1 base URL with key in path (eventsday only; v2 has no events-by-day endpoint).</summary>
    public string BaseUrlV1 { get; set; } = "https://www.thesportsdb.com/api/v1/json";

    public string ApiKey { get; set; } = "3";
}
