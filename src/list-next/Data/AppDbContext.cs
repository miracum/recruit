using list.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace list.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TrialAccessGrant> TrialAccessGrants => Set<TrialAccessGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrialAccessGrant>(entity =>
        {
            // Level is mapped to a native Postgres enum type via MapEnum in Program.cs, not a
            // string/int conversion here - Npgsql's EF Core provider generates the CREATE TYPE ...
            // AS ENUM migration and supports normal LINQ filtering on it directly.
            entity
                .HasIndex(g => new
                {
                    g.TrialIdentifierSystem,
                    g.TrialIdentifierValue,
                    g.Email,
                })
                .IsUnique();
        });
    }
}
