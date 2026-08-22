using System.Data;
using Dapper;

namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Dapper execution foundation (Plan-V3 U-03, GLM-DATA-01). All SQL is
/// PARAMETERIZED through Dapper command definitions — never concatenated or
/// interpolated with user values (GLM-DATA-06.6); cancellation flows through
/// <see cref="CommandDefinition"/>. SQL text stays explicit and reviewable
/// at the call site; no automatic CRUD generation.
/// </summary>
public static class Db
{
    static Db()
    {
        // PostgreSQL timestamptz comes back as DateTime; domain records use
        // DateTimeOffset — Dapper needs an explicit bridge to match constructors.
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    /// <summary>
    /// Queries rows mapped to <typeparamref name="T"/> with explicit column
    /// lists at the call site (no SELECT * against business tables; mappings
    /// never rely on column order).
    /// </summary>
    public static async Task<IReadOnlyList<T>> QueryAsync<T>(
        IDbConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<T>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    /// <summary>Single-row query; null when absent.</summary>
    public static Task<T?> QuerySingleOrDefaultAsync<T>(
        IDbConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        connection.QuerySingleOrDefaultAsync<T?>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

    /// <summary>
    /// Executes a parameterized command and returns the affected row count.
    /// Guarded edits pass the result to
    /// <c>ConcurrencyGuard.EnsureSingleRowUpdated</c> (06_DATA §8).
    /// </summary>
    public static Task<int> ExecuteAsync(
        IDbConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

    /// <summary>
    /// Executes a parameterized command and returns the first column of the first row in the result set.
    /// Additional columns or rows are ignored.
    /// </summary>
    public static Task<T> ExecuteScalarAsync<T>(
        IDbConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken))!;
}
