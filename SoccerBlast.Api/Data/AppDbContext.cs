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
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<NewsItemTeam> NewsItemTeams => Set<NewsItemTeam>();
    public DbSet<TeamExternalMap> TeamExternalMaps => Set<TeamExternalMap>();
    public DbSet<CompetitionExternalMap> CompetitionExternalMaps => Set<CompetitionExternalMap>();
    public DbSet<TeamProfile> TeamProfiles => Set<TeamProfile>();

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

        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.HasIndex(n => n.UrlHash).IsUnique();
            entity.HasIndex(n => n.PublishedAtUtc);
        });

        modelBuilder.Entity<NewsItemTeam>(entity =>
        {
            entity.HasKey(x => new { x.NewsItemId, x.TeamId });
            entity.HasIndex(x => x.TeamId);
            entity.HasIndex(x => x.NewsItemId);
        });
        modelBuilder.Entity<TeamExternalMap>(entity =>
        {
            entity.HasKey(x => new { x.TeamId, x.Provider });

            entity.Property(x => x.Provider).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ExternalId).IsRequired().HasMaxLength(64);

            // Prevent duplicates: same external id cannot map to multiple teams
            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();

            entity.HasIndex(x => x.ExternalId);
            entity.HasIndex(x => x.LastSyncedUtc);

            entity.HasOne(x => x.Team)
                  .WithMany()
                  .HasForeignKey(x => x.TeamId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

         modelBuilder.Entity<CompetitionExternalMap>(entity =>
        {
            entity.HasKey(x => new { x.CompetitionId, x.Provider });

            entity.Property(x => x.Provider).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ExternalId).IsRequired().HasMaxLength(64);

            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();

            entity.HasIndex(x => x.ExternalId);
            entity.HasIndex(x => x.LastSyncedUtc);

            entity.HasOne(x => x.Competition)
                  .WithMany()
                  .HasForeignKey(x => x.CompetitionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamProfile>(entity =>
        {
            entity.HasKey(x => x.TeamId);
            entity.HasIndex(x => x.LastUpdatedUtc);

            entity.HasOne(x => x.Team)
                  .WithOne()
                  .HasForeignKey<TeamProfile>(x => x.TeamId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
