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
    public DbSet<CompetitionExternalMap> CompetitionExternalMaps => Set<CompetitionExternalMap>();
    public DbSet<TeamProfile> TeamProfiles => Set<TeamProfile>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerExternalMap> PlayerExternalMaps => Set<PlayerExternalMap>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueExternalMap> VenueExternalMaps => Set<VenueExternalMap>();
    public DbSet<MatchDaySyncState> MatchDaySyncStates => Set<MatchDaySyncState>();
    public DbSet<SearchAlias> SearchAliases => Set<SearchAlias>();

    // EF won’t try to generate IDs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Match>(entity =>
        {
            entity.Property(m => m.Id).ValueGeneratedOnAdd();

            // Provider identity
            entity.Property(m => m.Provider).IsRequired().HasMaxLength(32);
            entity.HasIndex(m => new { m.Provider, m.ExternalId }).IsUnique();
            entity.HasIndex(m => m.UtcDate);
            entity.HasIndex(m => new { m.CompetitionId, m.UtcDate });
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

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(t => t.SportsDbId).HasMaxLength(32);
            entity.HasIndex(t => t.SportsDbId).IsUnique();
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

        modelBuilder.Entity<PlayerExternalMap>(entity =>
        {
            entity.HasKey(x => new { x.PlayerId, x.Provider });

            entity.Property(x => x.Provider).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ExternalId).IsRequired().HasMaxLength(64);

            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();
            entity.HasIndex(x => x.ExternalId);
            entity.HasIndex(x => x.LastSyncedUtc);

            entity.HasOne(x => x.Player)
                  .WithMany()
                  .HasForeignKey(x => x.PlayerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VenueExternalMap>(entity =>
        {
            entity.HasKey(x => new { x.VenueId, x.Provider });

            entity.Property(x => x.Provider).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ExternalId).IsRequired().HasMaxLength(64);

            entity.HasIndex(x => new { x.Provider, x.ExternalId }).IsUnique();

            entity.HasIndex(x => x.ExternalId);
            entity.HasIndex(x => x.LastSyncedUtc);

            entity.HasOne(x => x.Venue)
                  .WithMany()
                  .HasForeignKey(x => x.VenueId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchDaySyncState>(entity =>
        {
            entity.HasKey(x => x.LocalDate);
        });

        modelBuilder.Entity<SearchAlias>(entity =>
        {
            entity.Property(x => x.Canonical).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Alias).IsRequired().HasMaxLength(128);
            entity.Property(x => x.AliasNorm).IsRequired().HasMaxLength(128);

            // prevent duplicates
            entity.HasIndex(x => new { x.Type, x.Canonical, x.Alias }).IsUnique();

            // fast lookups for fuzzy resolver (prefix/contains on AliasNorm)
            entity.HasIndex(x => new { x.Type, x.AliasNorm });

            // optional but useful for admin/debug queries
            entity.HasIndex(x => x.HitCount);
            entity.HasIndex(x => x.UpdatedAtUtc);
        });
    }
}
