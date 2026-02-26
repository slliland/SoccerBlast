namespace SoccerBlast.Web.Helpers;

/// <summary>OpenStreetMap embed and link URLs for reuse on Team (stadium) and Venue pages.</summary>
public static class MapHelper
{
    /// <summary>Bbox for embed: lng±0.02, lat±0.01.</summary>
    public static string GetEmbedBbox(double lat, double lng)
        => $"{lng - 0.02},{lat - 0.01},{lng + 0.02},{lat + 0.01}";

    /// <summary>URL for iframe embed (OpenStreetMap export/embed).</summary>
    public static string GetEmbedUrl(double lat, double lng)
    {
        var bbox = GetEmbedBbox(lat, lng);
        return $"https://www.openstreetmap.org/export/embed.html?bbox={bbox}&layer=mapnik&marker={lat},{lng}";
    }

    /// <summary>URL to open the location in OpenStreetMap in a new tab.</summary>
    public static string GetOpenUrl(double lat, double lng)
        => $"https://www.openstreetmap.org/?mlat={lat}&mlon={lng}&zoom=16";
}
