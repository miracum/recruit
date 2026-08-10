using list.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace list.Tests;

/// <summary>
/// A fresh in-memory SQLite-backed AppDbContext per test - lighter than spinning up real Postgres
/// for unit tests, sufficient since these tests exercise TrialAccessService's logic, not
/// Postgres-specific SQL. The connection must stay open for the object's lifetime: SQLite's
/// ":memory:" database is dropped the moment its one connection closes.
/// </summary>
internal sealed class SqliteDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
