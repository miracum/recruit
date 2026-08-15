using list.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace list.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TrialAccessGrant> TrialAccessGrants => Set<TrialAccessGrant>();

    public DbSet<PollCursor> PollCursors => Set<PollCursor>();

    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    public DbSet<ScreeningNote> ScreeningNotes => Set<ScreeningNote>();

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

        modelBuilder.Entity<PollCursor>(entity =>
        {
            entity.HasKey(c => c.ListId);
            // The version id itself is the concurrency token: SaveChangesAsync() generates
            // `UPDATE ... WHERE list_id = @p0 AND last_seen_version_id = @original`, and throws
            // DbUpdateConcurrencyException when another replica already advanced it - this is the
            // compare-and-swap the whole HA design rests on, with no hand-written SQL needed.
            entity.Property(c => c.LastSeenVersionId).IsConcurrencyToken();
        });

        modelBuilder.Entity<NotificationRecipient>(entity =>
        {
            entity
                .HasIndex(r => new
                {
                    r.TrialIdentifierSystem,
                    r.TrialIdentifierValue,
                    r.Email,
                    r.Channel,
                })
                .IsUnique();
        });

        modelBuilder.Entity<ScreeningNote>(entity =>
        {
            // The hot lookup path - GetNotesAsync/GetTimelineAsync both key off this.
            entity.HasIndex(n => n.ResearchSubjectIdentifier);
        });
    }
}
