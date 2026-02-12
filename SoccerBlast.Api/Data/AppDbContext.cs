using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Match> Matches => Set<Match>();

    // EF won’t try to generate IDs
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>().Property(t => t.Id).ValueGeneratedNever();
        modelBuilder.Entity<Competition>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<Match>().Property(m => m.Id).ValueGeneratedNever();
    }
}
