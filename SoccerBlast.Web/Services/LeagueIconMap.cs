using System.Text.RegularExpressions;

namespace SoccerBlast.Web.Services;

public static class LeagueIconMap
{
    /// <summary>Same convention as scripts/FetchLeagueIcons (Wikipedia icon fetcher). Enables script-downloaded icons to work without adding every league to the map.</summary>
    private static string ToSafeFileName(string leagueName)
    {
        var s = leagueName.ToLowerInvariant();
        s = Regex.Replace(s, @"[^\p{L}\p{N}\s\-]", "");
        s = Regex.Replace(s, @"\s+", "-").Trim('-');
        return string.IsNullOrEmpty(s) ? "league" : s;
    }
    // TheSportsDB league ids (idLeague) → icon path (match list, dashboard). Single source: TheSportsDB.
    private static readonly Dictionary<int, string> ByCompetitionId = new()
    {
        [4328] = "images/leagues/premier-league.svg",        // English Premier League
        [4329] = "images/leagues/EFL-championship.svg",    // English League Championship
        [4330] = "images/leagues/scottish-premier-league.png", // Scottish Premier League
        [4331] = "images/leagues/bundesliga.svg",           // German Bundesliga
        [4332] = "images/leagues/serie-a.svg",              // Italian Serie A
        [4334] = "images/leagues/ligue1.svg",               // French Ligue 1
        [4335] = "images/leagues/laliga.svg",               // Spanish La Liga
        [4337] = "images/leagues/eredivisie.svg",            // Dutch Eredivisie
        [4346] = "images/leagues/Primeira_Liga_204_Logo.png", // Portuguese Primeira Liga
        [4351] = "images/leagues/brasileirao.svg",           // Campeonato Brasileiro Série A
        [4356] = "images/leagues/UEFA_Champions_League.svg",  // UEFA Champions League
        [4358] = "images/leagues/uefa-europa-league.svg",   // UEFA Europa League
        [4480] = "images/leagues/libertadores.svg",          // Copa Libertadores
    };

    /// <summary>Exact or alias league name → icon path. Used for team profile "Competitions" (Sports DB names + football-data names).</summary>
    private static readonly Dictionary<string, string> ByLeagueName = new(StringComparer.OrdinalIgnoreCase)
    {
        // UEFA
        ["UEFA Champions League"] = "images/leagues/UEFA_Champions_League.svg",
        ["Champions League"] = "images/leagues/UEFA_Champions_League.svg",
        ["UEFA Europa League"] = "images/leagues/uefa-europa-league.svg",
        ["Europa League"] = "images/leagues/uel.svg",
        ["UEFA Europa Conference League"] = "images/leagues/uefa-europa-conference-league.svg",
        ["Europa Conference League"] = "images/leagues/uecl.svg",
        ["Conference League"] = "images/leagues/uecl.svg",
        ["UECL"] = "images/leagues/uecl.svg",
        // Sports DB league names (team profile)
        ["Belgian Pro League"] = "images/leagues/bundesliga.svg",
        ["Dutch Eredivisie"] = "images/leagues/dutch-eredivisie.svg",
        ["English League Championship"] = "images/leagues/EFL-championship.svg",
        ["English Premier League"] = "images/leagues/premier-league.svg",
        ["French Ligue 1"] = "images/leagues/french-ligue-1.svg",
        ["German Bundesliga"] = "images/leagues/bundesliga.svg",
        ["Greek Superleague Greece"] = "images/leagues/serie-a.svg",
        ["Italian Serie A"] = "images/leagues/serie-a.svg",
        ["Scottish Premier League"] = "images/leagues/scottish-premier-league.png",
        ["Spanish La Liga"] = "images/leagues/spanish-la-liga.svg",
        // Football-data / common names
        ["Premier League"] = "images/leagues/premier-league.svg",
        ["Championship"] = "images/leagues/EFL-championship.svg",
        ["EFL Championship"] = "images/leagues/EFL-championship.svg",
        ["Primera Division"] = "images/leagues/laliga.svg",
        ["La Liga"] = "images/leagues/laliga.svg",
        ["Bundesliga"] = "images/leagues/bundesliga.svg",
        ["Serie A"] = "images/leagues/serie-a.svg",
        ["Ligue 1"] = "images/leagues/ligue1.svg",
        ["Eredivisie"] = "images/leagues/eredivisie.svg",
        ["Primeira Liga"] = "images/leagues/Primeira_Liga_204_Logo.png",
        ["Campeonato Brasileiro Série A"] = "images/leagues/brasileirao.svg",
        ["Brasileirão"] = "images/leagues/brasileirao.svg",
        ["Brasileirao"] = "images/leagues/brasileirao.svg",
        ["Copa Libertadores"] = "images/leagues/libertadores.svg",
        ["Copa Sudamericana"] = "images/leagues/libertadores.svg",
        // Cups (shared icon or Premier League)
        ["FA Cup"] = "images/leagues/FA_Cup_logo_(2020).svg",
        ["Emirates FA Cup"] = "images/leagues/FA_Cup_logo_(2020).svg",
        ["EFL Cup"] = "images/leagues/premier-league.svg",
        ["Carabao Cup"] = "images/leagues/premier-league.svg",
        ["League Cup"] = "images/leagues/premier-league.svg",
        // International (use world-cup placeholder when no specific icon)
        ["FIFA World Cup"] = DefaultLeagueIcon,
        ["UEFA European Championships"] = "images/leagues/uefa-europa-league.svg",
        ["UEFA Nations League"] = DefaultLeagueIcon,
        ["International Friendlies"] = DefaultLeagueIcon,
        ["World Cup Qualifying UEFA"] = DefaultLeagueIcon,
    };

    /// <summary>Keyword phrase → path when exact name not found (e.g. "FA Cup" inside "The FA Cup").</summary>
    private static readonly (string phrase, string path)[] NameContainsFallbacks =
    {
        ("World Cup", DefaultLeagueIcon),
        ("Nations League", DefaultLeagueIcon),
        ("International Friendlies", DefaultLeagueIcon),
        ("Qualifying UEFA", DefaultLeagueIcon),
        ("Champions League", "images/leagues/UEFA_Champions_League.svg"),
        ("Europa League", "images/leagues/uel.svg"),
        ("Conference League", "images/leagues/uecl.svg"),
        ("UECL", "images/leagues/uecl.svg"),
        ("FA Cup", "images/leagues/FA_Cup_logo_(2020).svg"),
        ("EFL Cup", "images/leagues/premier-league.svg"),
        ("Carabao Cup", "images/leagues/premier-league.svg"),
        ("League Cup", "images/leagues/premier-league.svg"),
        ("Premier League", "images/leagues/premier-league.svg"),
        ("Championship", "images/leagues/EFL-championship.svg"),
        ("Bundesliga", "images/leagues/bundesliga.svg"),
        ("La Liga", "images/leagues/laliga.svg"),
        ("Primera Division", "images/leagues/laliga.svg"),
        ("Serie A", "images/leagues/serie-a.svg"),
        ("Ligue 1", "images/leagues/ligue1.svg"),
        ("Eredivisie", "images/leagues/eredivisie.svg"),
        ("Primeira Liga", "images/leagues/Primeira_Liga_204_Logo.png"),
        ("Libertadores", "images/leagues/libertadores.svg"),
        ("Sudamericana", "images/leagues/libertadores.svg"),
        ("Belgian Pro League", "images/leagues/bundesliga.svg"),
        ("Dutch Eredivisie", "images/leagues/dutch-eredivisie.svg"),
        ("English League Championship", "images/leagues/EFL-championship.svg"),
        ("English Premier League", "images/leagues/premier-league.svg"),
        ("French Ligue 1", "images/leagues/french-ligue-1.svg"),
        ("German Bundesliga", "images/leagues/bundesliga.svg"),
        ("Greek Superleague", "images/leagues/serie-a.svg"),
        ("Italian Serie A", "images/leagues/serie-a.svg"),
        ("Scottish Premier League", "images/leagues/scottish-premier-league.png"),
        ("Spanish La Liga", "images/leagues/spanish-la-liga.svg"),
        ("Campeonato Brasileiro", "images/leagues/brasileirao.svg"),
    };

    /// <summary>Default icon when no league badge is found (dashboard, match list).</summary>
    public const string DefaultLeagueIcon = "images/icons/world-cup.png";

    public static string Get(int competitionId, string? name = null)
    {
        if (ByCompetitionId.TryGetValue(competitionId, out var path))
            return path;
        return DefaultLeagueIcon;
    }

    /// <summary>Resolve icon path from league/competition name (e.g. team profile "FA Cup", "EFL Cup"). Exact match first, then contains-phrase.</summary>
    public static string? GetByName(string? leagueName)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return null;
        var trimmed = leagueName.Trim();

        if (ByLeagueName.TryGetValue(trimmed, out var path))
            return path;

        foreach (var (phrase, p) in NameContainsFallbacks)
        {
            if (trimmed.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        return DefaultLeagueIcon;
    }
}
