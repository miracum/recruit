using Microsoft.EntityFrameworkCore;
using RecruIT.List.Data.Entities;

namespace RecruIT.List.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TrialAccessGrant> TrialAccessGrants => Set<TrialAccessGrant>();

    public DbSet<PollCursor> PollCursors => Set<PollCursor>();

    public DbSet<ScreeningNote> ScreeningNotes => Set<ScreeningNote>();

    public DbSet<NotificationSubscription> NotificationSubscriptions =>
        Set<NotificationSubscription>();

    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

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

        modelBuilder.Entity<ScreeningNote>(entity =>
        {
            // The hot lookup path - GetNotesAsync/GetTimelineAsync both key off this.
            entity.HasIndex(n => n.ResearchSubjectIdentifier);
        });

        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.HasIndex(s => new { s.TrialIdentifier, s.SubjectId }).IsUnique();
        });

        modelBuilder.Entity<NotificationEvent>(entity =>
        {
            entity.HasIndex(e => e.DedupeKey).IsUnique();
            // The detector's per-trial materialization query - all events for a trial, newest first.
            entity.HasIndex(e => new { e.TrialIdentifier, e.OccurredAt });
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            // The bell/feed's hot "unread in-app deliveries for this subject" query.
            entity.HasIndex(d => new
            {
                d.SubjectId,
                d.Channel,
                d.ReadAt,
            });
            // The sender job's "due email deliveries" query.
            entity.HasIndex(d => new
            {
                d.Channel,
                d.SentAt,
                d.ScheduledFor,
            });
            entity.HasIndex(d => d.NotificationEventId);
            entity.HasIndex(d => d.NotificationSubscriptionId);
        });
    }
}
