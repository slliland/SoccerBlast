namespace SoccerBlast.Web.Services;

public static class LeagueIconMap
{
    private static readonly Dictionary<int, string> ByCompetitionId = new()
    {
        [2021] = "images/leagues/premier-league.svg",
        [2016] = "images/leagues/championship.svg",          // add this file
        [2003] = "images/leagues/eredivisie.svg",
        [2017] = "images/leagues/primeira-liga.svg",
        [2013] = "images/leagues/brasileirao.svg",           // add this file
        [2015] = "images/leagues/ligue1.svg",
        [2002] = "images/leagues/bundesliga.svg",
        [2014] = "images/leagues/laliga.svg",                // Primera Division
        [2019] = "images/leagues/serie-a.svg",
        [2152] = "images/leagues/libertadores.svg",          // add this file
    };

    public static string Get(int competitionId, string? name = null)
    {
        if (ByCompetitionId.TryGetValue(competitionId, out var path))
            return path;

        // fallback if not mapped yet
        return "images/leagues/ball.svg";
    }
}
