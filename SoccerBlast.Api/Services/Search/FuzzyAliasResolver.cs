using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;
using Raffinert.FuzzySharp;

namespace SoccerBlast.Api.Services.Search;

public class FuzzyAliasResolver
{
    private readonly AppDbContext _db;

    public FuzzyAliasResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<(string canonical, int score)>> ResolveTopAsync(
        AliasType type,
        string userInput,
        int take = 5,
        CancellationToken ct = default)
    {
        var q = SearchText.Normalize(userInput);
        if (string.IsNullOrWhiteSpace(q)) return new();

        // Prefix for candidate narrowing
        var prefix = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? q;

        // Prefer AliasNorm (normalized) for filtering
        var candidates = await _db.SearchAliases
            .AsNoTracking()
            .Where(a => a.Type == type && a.AliasNorm != null && a.AliasNorm != "")
            .Where(a => a.AliasNorm.Contains(prefix))
            .OrderByDescending(a => a.HitCount)
            .ThenBy(a => a.AliasNorm)
            .Take(2000)
            .ToListAsync(ct);

        if (candidates.Count < 50)
        {
            candidates = await _db.SearchAliases
                .AsNoTracking()
                .Where(a => a.Type == type && a.AliasNorm != null && a.AliasNorm != "")
                .OrderByDescending(a => a.HitCount)
                .ThenBy(a => a.AliasNorm)
                .Take(5000)
                .ToListAsync(ct);
        }

        // Score in memory using AliasNorm
        var scored = candidates
            .Select(a =>
            {
                var score = Fuzz.TokenSetRatio(q, a.AliasNorm);
                return (canonical: a.Canonical, score);
            })
            .GroupBy(x => x.canonical)
            .Select(g => (canonical: g.Key, score: g.Max(x => x.score)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.canonical)
            .Take(take)
            .ToList();

        return scored;
    }

    public static bool ShouldAutoPick(List<(string canonical, int score)> top)
    {
        if (top.Count == 0) return false;
        if (top[0].score < 92) return false;
        if (top.Count == 1) return true;
        return (top[0].score - top[1].score) >= 8;
    }
}
