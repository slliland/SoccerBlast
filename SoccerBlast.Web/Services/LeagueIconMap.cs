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
    private static readonly Dictionary<int, string> ByCompetitionId = new()
    {
        [2001] = "images/leagues/UEFA_Champions_League.svg", // UEFA Champions League
        [2021] = "images/leagues/premier-league.svg",
        [2016] = "images/leagues/championship.svg",
        [2003] = "images/leagues/eredivisie.svg",
        [2017] = "images/leagues/primeira-liga.svg",
        [2013] = "images/leagues/brasileirao.svg",
        [2015] = "images/leagues/ligue1.svg",
        [2002] = "images/leagues/bundesliga.svg",
        [2014] = "images/leagues/laliga.svg",
        [2019] = "images/leagues/serie-a.svg",
        [2152] = "images/leagues/libertadores.svg",
    };

    /// <summary>Exact or alias league name → icon path. Used for team profile "Competitions" (e.g. FA Cup, EFL Cup).</summary>
    private static readonly Dictionary<string, string> ByLeagueName = new(StringComparer.OrdinalIgnoreCase)
    {
        // UEFA
        ["UEFA Champions League"] = "images/leagues/UEFA_Champions_League.svg",
        ["Champions League"] = "images/leagues/UEFA_Champions_League.svg",
        ["UEFA Europa League"] = "images/leagues/uel.svg",
        ["Europa League"] = "images/leagues/uel.svg",
        ["UEFA Europa Conference League"] = "images/leagues/uecl.svg",
        ["Europa Conference League"] = "images/leagues/uecl.svg",
        ["Conference League"] = "images/leagues/uecl.svg",
        ["UECL"] = "images/leagues/uecl.svg",
        // England
        ["English Premier League"] = "images/leagues/premier-league.svg",
        ["Premier League"] = "images/leagues/premier-league.svg",
        ["Championship"] = "images/leagues/championship.svg",
        ["EFL Championship"] = "images/leagues/championship.svg",
        ["FA Cup"] = "images/leagues/premier-league.svg",
        ["Emirates FA Cup"] = "images/leagues/premier-league.svg",
        ["EFL Cup"] = "images/leagues/premier-league.svg",
        ["Carabao Cup"] = "images/leagues/premier-league.svg",
        ["League Cup"] = "images/leagues/premier-league.svg",
        // Top 5 + others
        ["Bundesliga"] = "images/leagues/bundesliga.svg",
        ["La Liga"] = "images/leagues/laliga.svg",
        ["Primera Division"] = "images/leagues/laliga.svg",
        ["Serie A"] = "images/leagues/serie-a.svg",
        ["Ligue 1"] = "images/leagues/ligue1.svg",
        ["Eredivisie"] = "images/leagues/eredivisie.svg",
        ["Primeira Liga"] = "images/leagues/primeira-liga.svg",
        ["Brasileirão"] = "images/leagues/brasileirao.svg",
        ["Brasileirao"] = "images/leagues/brasileirao.svg",
        ["Copa Libertadores"] = "images/leagues/libertadores.svg",
        ["Copa Sudamericana"] = "images/leagues/libertadores.svg",
    };

    /// <summary>Keyword phrase → path when exact name not found (e.g. "FA Cup" inside "The FA Cup").</summary>
    private static readonly (string phrase, string path)[] NameContainsFallbacks =
    {
        ("Champions League", "images/leagues/UEFA_Champions_League.svg"),
        ("Europa League", "images/leagues/uel.svg"),
        ("Conference League", "images/leagues/uecl.svg"),
        ("UECL", "images/leagues/uecl.svg"),
        ("FA Cup", "images/leagues/premier-league.svg"),
        ("EFL Cup", "images/leagues/premier-league.svg"),
        ("Carabao Cup", "images/leagues/premier-league.svg"),
        ("League Cup", "images/leagues/premier-league.svg"),
        ("Premier League", "images/leagues/premier-league.svg"),
        ("Championship", "images/leagues/championship.svg"),
        ("Bundesliga", "images/leagues/bundesliga.svg"),
        ("La Liga", "images/leagues/laliga.svg"),
        ("Serie A", "images/leagues/serie-a.svg"),
        ("Ligue 1", "images/leagues/ligue1.svg"),
        ("Eredivisie", "images/leagues/eredivisie.svg"),
        ("Primeira Liga", "images/leagues/primeira-liga.svg"),
        ("Libertadores", "images/leagues/libertadores.svg"),
        ("Sudamericana", "images/leagues/libertadores.svg"),
    };

    public static string Get(int competitionId, string? name = null)
    {
        if (ByCompetitionId.TryGetValue(competitionId, out var path))
            return path;
        return "images/leagues/ball.svg";
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

        // Fallback: path used by scripts/FetchLeagueIcons (Wikipedia). If you run that script for a league, the icon works without adding a map entry.
        return "images/leagues/" + ToSafeFileName(trimmed) + ".png";
    }
}
