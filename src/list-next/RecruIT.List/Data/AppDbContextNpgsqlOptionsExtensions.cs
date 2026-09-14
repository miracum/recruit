using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using RecruIT.List.Models;

namespace RecruIT.List.Data;

public static class AppDbContextNpgsqlOptionsExtensions
{
    /// <summary>
    /// Native Postgres enum mappings AppDbContext's model relies on - shared by every place that
    /// builds a UseNpgsql(...) call against it (Program.cs's normal and migrate-only startup paths,
    /// and tests that construct their own DbContextOptions), so they can't drift apart.
    /// </summary>
    public static NpgsqlDbContextOptionsBuilder MapAppDbEnums(
        this NpgsqlDbContextOptionsBuilder npgsql
    ) =>
        npgsql
            .MapEnum<TrialPermissionLevel>("trial_permission_level")
            .MapEnum<NotificationChannel>("notification_channel");
}
