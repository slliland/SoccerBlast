using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SoccerBlast.Api.Services;

/// <summary>
/// Normalize team names for SportsDB search and provide a small set of curated aliases (max 2–3)
/// so "Paris Saint-Germain" can match SportsDB's "Paris SG" without many suffix variants.
/// </summary>
public static class TeamNameNormalizer
{
    private static readonly string[] Suffixes =
    {
        " Football Club", " FC", " AFC", " CF", " SC", " SS", " SV", " AS", " AC",
        " United", " City", " Town", " Rovers", " Wanderers", " Athletic",
        " Hotspur", " Albion"
    };

    private static readonly HashSet<string> Abbrevs = new(StringComparer.OrdinalIgnoreCase)
    {
        "FC", "CF", "AFC", "SC", "SS", "SV", "AS", "AC", "FK", "IF", "FF",
        "OG", "BK", "IK", "SK", "HK", "US", "CS", "RC", "SD", "FSV", "TSV", "VFB", "VFL", "TSG", "FC", "SV"
    };

    /// <summary>Curated SportsDB search aliases (our name → their name). Max 2–3 used per team.</summary>
    private static readonly Dictionary<string, string[]> CuratedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Paris Saint-Germain"] = new[] { "Paris SG" },
        ["Paris Saint Germain"] = new[] { "Paris SG" },
        ["PSG"] = new[] { "Paris SG" },
        ["Inter Milan"] = new[] { "Inter" },
        ["Inter"] = new[] { "Inter Milano" },
        ["Manchester United"] = new[] { "Manchester Utd" },
        ["Manchester City"] = new[] { "Man City" },
        ["Tottenham Hotspur"] = new[] { "Tottenham" },
        ["West Ham United"] = new[] { "West Ham" },
        ["Newcastle United"] = new[] { "Newcastle" },
        ["Nottingham Forest"] = new[] { "Nott'ham Forest" },
        ["Brighton & Hove Albion"] = new[] { "Brighton" },
        ["Wolverhampton Wanderers"] = new[] { "Wolves" },
        ["Real Madrid"] = new[] { "Real Madrid" }, // exact often works
        ["Bayern Munich"] = new[] { "Bayern München" },
        ["Bayern München"] = new[] { "Bayern Munich" },
        ["VfL Wolfsburg"] = new[] { "Wolfsburg" },
    };

    public static IReadOnlyList<string> GetSearchCandidates(string teamName)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(teamName)) return list;

        var raw = teamName.Trim();
        var n = Normalize(raw);
        if (!string.IsNullOrWhiteSpace(n)) list.Add(n);

        // If name starts with "1" / "1." (very common in Germany), drop it
        // Example: "1 fsv mainz 05" -> "fsv mainz 05"
        var dropLeadingNumber = Regex.Replace(n, @"^\d+\s+", "").Trim();
        if (!string.IsNullOrWhiteSpace(dropLeadingNumber) && dropLeadingNumber != n)
            list.Add(dropLeadingNumber);

        // Drop common abbrev tokens in the middle, keep the “core city/name”
        // Example: "fsv mainz 05" -> "mainz 05"
        var tokens = dropLeadingNumber.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        tokens.RemoveAll(t => Abbrevs.Contains(t));
        var withoutAbbrevs = string.Join(" ", tokens).Trim();
        if (!string.IsNullOrWhiteSpace(withoutAbbrevs) && withoutAbbrevs != dropLeadingNumber)
            list.Add(withoutAbbrevs);

        // If there are digits like "05", also try the pure name (often best)
        // Example: "mainz 05" -> "mainz"
        var noDigits = Regex.Replace(withoutAbbrevs, @"\b\d+\b", "").Trim();
        noDigits = Regex.Replace(noDigits, @"\s+", " ").Trim();
        if (!string.IsNullOrWhiteSpace(noDigits) && noDigits != withoutAbbrevs)
            list.Add(noDigits);

        // curated aliases (still ok, but optional)
        foreach (var a in GetCuratedAliases(raw))
            list.Add(a);

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }


    /// <summary>
    /// Normalize for matching: lowercase, remove accents, strip punctuation, strip suffixes, collapse whitespace, "&" → "and".
    /// Use this for the first search term and for future league-list matching.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var s = name.Trim();
        s = RemoveDiacritics(s);
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"[^\p{L}\p{N}\s]", " "); // keep letters, numbers, spaces
        s = Regex.Replace(s, @"\s+", " ").Trim();

        s = ReplaceKnown(s, " & ", " and ");
        s = ReplaceKnown(s, "&", " and ");

        foreach (var suffix in Suffixes)
        {
            var suf = suffix.Trim().ToLowerInvariant();
            if (s.EndsWith(suf) && s.Length > suf.Length)
                s = s.Substring(0, s.Length - suf.Length).Trim();
        }

        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var last = parts[^1];
            if (last.Length >= 2 && last.Length <= 4 && last.All(char.IsLetter) && Abbrevs.Contains(last))
                parts = parts.Take(parts.Length - 1).ToArray();
            s = string.Join(" ", parts);
        }

        return s.Trim();
    }

    /// <summary>Returns at most 2–3 curated search terms for SportsDB (e.g. "Paris SG" for "Paris Saint-Germain").</summary>
    public static IReadOnlyList<string> GetCuratedAliases(string? teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName)) return Array.Empty<string>();
        var key = teamName.Trim();
        if (CuratedAliases.TryGetValue(key, out var list))
            return list.Take(3).ToArray();

        var normalized = Normalize(key);
        foreach (var (k, v) in CuratedAliases)
        {
            if (Normalize(k) == normalized)
                return v.Take(3).ToArray();
        }

        return Array.Empty<string>();
    }

    private static string ReplaceKnown(string s, string old, string replacement)
    {
        return s.Replace(old, replacement, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveDiacritics(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
