namespace SoccerBlast.Api.Services;

public class TheSportsDbOptions
{
    /// <summary>v2 base URL (team/league/players lookup, search, list). Auth via X-API-KEY header.</summary>
    public string BaseUrl { get; set; } = "https://www.thesportsdb.com/api/v2/json";

    /// <summary>v1 base URL with key in path (lookuptable, eventsday). V1 uses key 123; v2 uses paid key from .env.</summary>
    public string BaseUrlV1 { get; set; } = "https://www.thesportsdb.com/api/v1/json";

    /// <summary>v2 API key (X-API-KEY header). Set via THESPORTSDB_API_KEY in .env.</summary>
    public string ApiKey { get; set; } = "3";

    /// <summary>v1 API key (in URL path). Use 123 for free lookuptable/eventsday. Set via THESPORTSDB_API_KEY_V1 in .env.</summary>
    public string ApiKeyV1 { get; set; } = "123";
}
