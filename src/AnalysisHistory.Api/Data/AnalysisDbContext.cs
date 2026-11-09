using Microsoft.EntityFrameworkCore;

namespace AnalysisHistory.Api.Data;

/// <summary>
/// EF Core database context for the analysis history microservice.
/// </summary>
public class AnalysisDbContext : DbContext
{
    /// <summary>
    /// Receives SQLite connection options from dependency injection.
    /// </summary>
    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> options)
        : base(options)
    {
    }

    /// <summary>Table containing all analyzed images.</summary>
    public DbSet<AnalysisRecord> Analyses => Set<AnalysisRecord>();

    /// <summary>
    /// Configures keys and field limits for the SQLite schema.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRecord>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<AnalysisRecord>()
            .Property(x => x.FileName)
            .HasMaxLength(260);
    }
}
