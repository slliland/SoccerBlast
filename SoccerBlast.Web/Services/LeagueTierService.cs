using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

/// <summary>Groups leagues by category for UI. Names matched flexibly (API may differ).</summary>
public static class LeagueTierService
{
    // User-facing category names (elegant, no "Tier X")
    public const string InternationalNational = "International — National Teams";
    public const string ContinentalClub = "Continental — Club";
    public const string DomesticTop = "Domestic — Top Leagues";
    public const string DomesticCups = "Domestic — Cups";
    public const string InternationalClub = "International — Club";
    public const string OtherRegional = "Other / Regional";

    private static readonly (string Tier, string[] Keywords)[] TierKeywords =
    {
        (InternationalNational, new[]
        {
            "FIFA World Cup", "World Cup",
            "European Championship", "UEFA European", "EURO",
            "Copa América", "Copa America",
            "Nations League", "UEFA Nations League",
            "AFCON", "Africa Cup", "African Cup of Nations",
            "Asian Cup", "AFC Asian Cup",
            "Friendlies", "Qualifiers", "World Cup qualification", "EURO qualification"
        }),
        (ContinentalClub, new[]
        {
            "Champions League", "UEFA Champions",
            "Europa League", "UEFA Europa",
            "Europa Conference", "Conference League",
            "Copa Libertadores", "Libertadores",
            "Copa Sudamericana", "Sudamericana",
            "AFC Champions League", "AFC Champions",
            "CAF Champions League", "CAF Champions", "African Champions League"
        }),
        (DomesticTop, new[]
        {
            "Premier League", "English Premier",
            "La Liga", "Spanish La Liga",
            "Bundesliga", "German Bundesliga",
            "Serie A", "Italian Serie A",
            "Ligue 1", "French Ligue",
            "Eredivisie", "Dutch Eredivisie",
            "Primeira Liga", "Portuguese Liga", "Liga Portugal",
            "Belgian Pro League", "Belgium Pro League", "Jupiler",
            "Scottish Premiership", "Scotland Premiership",
            "Süper Lig", "Super Lig", "Turkish Super", "Turkey Super",
            "Brasileirão", "Brasileiro", "Serie A Brazil", "Campeonato Brasileiro",
            "Argentine Primera", "Argentina Primera", "Liga Profesional",
            "MLS", "Major League Soccer",
            "Saudi Pro League", "Saudi League", "Roshn Saudi"
        }),
        (InternationalClub, new[]
        {
            "FIFA Club World Cup", "Club World Cup",
            "Recopa", "UEFA Super Cup",
            "international club", "club friendlies", "club tour"
        }),
        (DomesticCups, new[]
        {
            "FA Cup", "English FA Cup",
            "EFL Cup", "Carabao Cup", "League Cup",
            "Copa del Rey",
            "DFB-Pokal", "DFB Pokal", "German Cup",
            "Coppa Italia", "Italian Cup",
            "Coupe de France", "French Cup",
            "Super Cup", "Supercup", "Domestic Super Cup"
        })
    };

    private static readonly string[] TierOrder =
    {
        InternationalNational,
        ContinentalClub,
        DomesticTop,
        DomesticCups,
        InternationalClub,
        OtherRegional
    };

    /// <summary>Returns the category label for a league name (flexible contains match).</summary>
    public static string GetTier(string leagueName)
    {
        if (string.IsNullOrWhiteSpace(leagueName)) return OtherRegional;
        var n = leagueName.Trim();

        foreach (var (tier, keywords) in TierKeywords)
        {
            foreach (var kw in keywords)
            {
                if (n.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return tier;
            }
        }

        return OtherRegional;
    }

    /// <summary>Curated "Popular" league IDs (TheSportsDB idLeague). Display order for sidebar.</summary>
    private static readonly int[] PopularLeagueIdsOrdered =
    {
        4429, 5513, 5514, 5515, 5516, 5517, 5518, 4502, 5519, 4490, 4499, 4873, 4866, 4496,
        4480, 4481, 5071, 4328, 4335, 4332, 4331, 4334,
        4346, 4501
    };

    private static readonly HashSet<int> PopularLeagueIds = new(PopularLeagueIdsOrdered);

    /// <summary>Ordered popular league IDs for sidebar display.</summary>
    public static IReadOnlyList<int> GetPopularLeagueIdsOrdered() => PopularLeagueIdsOrdered;

    /// <summary>True if the competition is in the recommended "Popular" set. Only ID is used; name is ignored.</summary>
    public static bool IsRecommendedPopular(int? competitionId, string? _)
    {
        return competitionId.HasValue && competitionId.Value > 0 && PopularLeagueIds.Contains(competitionId.Value);
    }

    /// <summary>Groups leagues by category. Only categories with at least one league are included.</summary>
    public static IReadOnlyList<(string TierLabel, List<LeagueDto> Leagues)> GroupByTier(List<LeagueDto> leagues)
    {
        var byTier = new Dictionary<string, List<LeagueDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in TierOrder)
            byTier[t] = new List<LeagueDto>();

        foreach (var l in leagues)
        {
            var tier = GetTier(l.Name);
            if (!byTier.ContainsKey(tier))
                byTier[tier] = new List<LeagueDto>();
            byTier[tier].Add(l);
        }

        foreach (var list in byTier.Values)
        {
            list.Sort((a, b) =>
            {
                int c = b.LiveCount.CompareTo(a.LiveCount);
                if (c != 0) return c;
                c = b.MatchCount.CompareTo(a.MatchCount);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        return TierOrder
            .Where(t => byTier[t].Count > 0)
            .Select(t => (t, byTier[t]))
            .ToList();
    }

    /// <summary>Same grouping for simple (Name, Count) options e.g. All.razor dropdown.</summary>
    public static IReadOnlyList<(string TierLabel, List<(string Name, int Count)> Leagues)> GroupOptionsByTier(
        IEnumerable<(string Name, int Count)> options)
    {
        var byTier = new Dictionary<string, List<(string Name, int Count)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in TierOrder)
            byTier[t] = new List<(string Name, int Count)>();

        foreach (var o in options)
        {
            var tier = GetTier(o.Name);
            if (!byTier.ContainsKey(tier))
                byTier[tier] = new List<(string Name, int Count)>();
            byTier[tier].Add(o);
        }

        foreach (var list in byTier.Values)
            list.Sort((a, b) => b.Count.CompareTo(a.Count));

        return TierOrder
            .Where(t => byTier[t].Count > 0)
            .Select(t => (t, byTier[t]))
            .ToList();
    }
}
