using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;

namespace SoccerBlast.Api.Services.Search;

public static class AliasNormBackfill
{
    public static async Task RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        // only rows missing AliasNorm
        var rows = await db.SearchAliases
            .Where(a => a.AliasNorm == null || a.AliasNorm == "")
            .ToListAsync(ct);

        if (rows.Count == 0) return;

        foreach (var a in rows)
            a.AliasNorm = SearchText.Normalize(a.Alias);

        await db.SaveChangesAsync(ct);
        Console.WriteLine($"[Backfill] Updated AliasNorm for {rows.Count} SearchAliases rows.");
    }
}
