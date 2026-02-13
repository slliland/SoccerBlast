using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    // EF won’t try to generate IDs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Match>(entity =>
        {
            // Common queries: by date range
            entity.HasIndex(m => m.UtcDate);

            // Date + competition filter
            entity.HasIndex(m => new { m.CompetitionId, m.UtcDate });

            // Team search helpers (if you later query by IDs)
            entity.HasIndex(m => new { m.HomeTeamId, m.UtcDate });
            entity.HasIndex(m => new { m.AwayTeamId, m.UtcDate });
        });
    }
}
