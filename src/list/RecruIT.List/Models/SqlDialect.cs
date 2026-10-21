namespace RecruIT.List.Models;

/// <summary>
/// The SQL dialect a newly-authored criterion's SQL is written in - stored as the Library's
/// application/sql;dialect=&lt;dialect&gt; content, which query-sql-on-fhir runs directly when it
/// matches its configured query-sql-on-fhir.sql-dialect.
/// </summary>
public enum SqlDialect
{
    Trino = 0,
    DuckDb = 1,
}

public static class SqlDialectExtensions
{
    /// <summary>The dialect parameter value of the application/sql content type, e.g. "trino".</summary>
    public static string ContentTypeParameter(this SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.Trino => "trino",
            SqlDialect.DuckDb => "duckdb",
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };

    public static string DisplayName(this SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.Trino => "Trino",
            SqlDialect.DuckDb => "DuckDB",
            _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null),
        };
}
