using list.Data;
using list.Data.Entities;
using list.Models;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace list.Tests;

/// <summary>
/// Validates the correctness property the whole HA design in the notify-next plan rests on: when
/// multiple replicas race to advance PollCursor for the same list at the same time, exactly one of
/// them wins and goes on to diff/enqueue - never zero, never more than one. Two separate
/// AppDbContext instances (mirroring two separate replica processes, each with its own change
/// tracker) are used deliberately to exercise the real optimistic-concurrency-token behavior,
/// which SQLite (used for the other, non-Postgres-specific tests in this project) doesn't
/// faithfully reproduce.
/// </summary>
public sealed class PollCursorConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "docker.io/library/postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
    ).Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task TryAdvance_OnlyOneReplicaWinsConcurrentRace()
    {
        const string listId = "concurrent-test-list";

        await using (var setup = CreateContext())
        {
            setup.PollCursors.Add(
                new PollCursor
                {
                    ListId = listId,
                    LastSeenVersionId = "1",
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            );
            await setup.SaveChangesAsync();
        }

        // Two separate contexts, each with its own change tracker, simulate two replicas racing
        // to advance the same cursor to the same new version at the same time.
        await using var replicaA = CreateContext();
        await using var replicaB = CreateContext();

        var cursorA = await replicaA.PollCursors.SingleAsync(c => c.ListId == listId);
        var cursorB = await replicaB.PollCursors.SingleAsync(c => c.ListId == listId);

        cursorA.LastSeenVersionId = "2";
        cursorB.LastSeenVersionId = "2";

        var results = await Task.WhenAll(
            TryAdvance(() => replicaA.SaveChangesAsync()),
            TryAdvance(() => replicaB.SaveChangesAsync())
        );

        Assert.Single(results, won => won);

        await using var verify = CreateContext();
        var finalCursor = await verify.PollCursors.SingleAsync(c => c.ListId == listId);
        Assert.Equal("2", finalCursor.LastSeenVersionId);
    }

    private static async Task<bool> TryAdvance(Func<Task<int>> saveChanges)
    {
        try
        {
            await saveChanges();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                _postgres.GetConnectionString(),
                npgsql =>
                    npgsql
                        .MapEnum<TrialPermissionLevel>("trial_permission_level")
                        .MapEnum<NotificationChannel>("notification_channel")
            )
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
