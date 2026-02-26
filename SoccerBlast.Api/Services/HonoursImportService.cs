using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

/// <summary>Imports honours data from the ScrapeTeamHonours SQLite cache (sportsdb_cache.db) into the main DB. Run script first, then call ImportFromCacheAsync (e.g. via POST /api/admin/import/honours).</summary>
public class HonoursImportService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public HonoursImportService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    private string? GetCacheDbPath()
    {
        var path = _configuration["HonoursCacheDbPath"]?.Trim();
        if (!string.IsNullOrEmpty(path))
        {
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            return path;
        }
        var cwd = Directory.GetCurrentDirectory();
        var fallback = Path.Combine(cwd, "sportsdb_cache.db");
        if (File.Exists(fallback)) return fallback;
        var parentDb = Path.Combine(Path.GetDirectoryName(cwd) ?? cwd, "sportsdb_cache.db");
        return File.Exists(parentDb) ? parentDb : null;
    }

    /// <summary>Returns true if the cache file exists and can be used for import.</summary>
    public bool IsCacheAvailable => !string.IsNullOrEmpty(GetCacheDbPath());

    /// <summary>Imports honours, team_honours, and honour_winners from the cache DB into the main DB. Upserts by primary key; does not clear existing data (adds/updates).</summary>
    public async Task<HonoursImportResult> ImportFromCacheAsync(CancellationToken ct = default)
    {
        var path = GetCacheDbPath();
        if (string.IsNullOrEmpty(path))
            return new HonoursImportResult { Success = false, Message = "Honours cache file (sportsdb_cache.db) not found. Run Scripts/ScrapeTeamHonours --db sportsdb_cache.db first." };

        var result = new HonoursImportResult { Success = true };
        try
        {
            await using var conn = new SqliteConnection($"Data Source={path}");
            await conn.OpenAsync(ct);

            // Read honours
            var honours = new List<(int Id, string Slug, string? Title, string? TrophyImageUrl, string HonourUrl, string? TypeGuess)>();
            await using (var cmd = new SqliteCommand("SELECT id_honour, slug, title, trophy_image_url, honour_url, type_guess FROM honours", conn))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                while (await r.ReadAsync(ct))
                {
                    honours.Add((
                        r.GetInt32(0),
                        r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.GetString(4),
                        r.IsDBNull(5) ? null : r.GetString(5)
                    ));
                }
            }
            result.HonoursUpserted = honours.Count;

            // Read team_honours
            var teamHonours = new List<(string TeamId, int HonourId)>();
            await using (var cmd = new SqliteCommand("SELECT team_id, honour_id FROM team_honours", conn))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                while (await r.ReadAsync(ct))
                    teamHonours.Add((r.GetString(0).Trim(), r.GetInt32(1)));
            }
            result.TeamHonoursUpserted = teamHonours.Count;

            // Read honour_winners
            var winners = new List<(int HonourId, string YearLabel, string TeamId, string? TeamName, string? TeamBadgeUrl)>();
            await using (var cmd = new SqliteCommand("SELECT honour_id, year_label, team_id, team_name, team_badge_url FROM honour_winners", conn))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                while (await r.ReadAsync(ct))
                {
                    winners.Add((
                        r.GetInt32(0),
                        r.GetString(1),
                        r.GetString(2).Trim(),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4)
                    ));
                }
            }
            result.HonourWinnersUpserted = winners.Count;

            // Upsert into main DB (replace all honours data so re-import is idempotent after script refresh)
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Clear existing so we have a full replace from cache
                await _db.HonourWinners.ExecuteDeleteAsync(ct);
                await _db.TeamHonours.ExecuteDeleteAsync(ct);
                await _db.Honours.ExecuteDeleteAsync(ct);
                await _db.SaveChangesAsync(ct);

                foreach (var h in honours)
                {
                    _db.Honours.Add(new Honour
                    {
                        Id = h.Id,
                        Slug = h.Slug,
                        Title = h.Title,
                        TrophyImageUrl = h.TrophyImageUrl,
                        HonourUrl = h.HonourUrl,
                        TypeGuess = h.TypeGuess
                    });
                }
                await _db.SaveChangesAsync(ct);

                foreach (var th in teamHonours)
                    _db.TeamHonours.Add(new TeamHonour { TeamId = th.TeamId, HonourId = th.HonourId });
                await _db.SaveChangesAsync(ct);

                foreach (var w in winners)
                {
                    _db.HonourWinners.Add(new HonourWinner
                    {
                        HonourId = w.HonourId,
                        YearLabel = w.YearLabel,
                        TeamId = w.TeamId,
                        TeamName = w.TeamName,
                        TeamBadgeUrl = w.TeamBadgeUrl
                    });
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
        }
        return result;
    }
}

public class HonoursImportResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int HonoursUpserted { get; set; }
    public int TeamHonoursUpserted { get; set; }
    public int HonourWinnersUpserted { get; set; }
}
