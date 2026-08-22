using System.Data;
using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Connection factory that resolves the real <see cref="DbConnectionFactory"/>
/// lazily on first use. Web startup stays healthy when no database
/// configuration exists; the first database access fails explicitly with the
/// U-03 configuration error instead of crashing composition (fail closed at
/// use, not at startup).
/// </summary>
public sealed class LazyDbConnectionFactory : IDbConnectionFactory
{
    private readonly Func<string, string?> _environment;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DbConnectionFactory? _inner;

    public LazyDbConnectionFactory(Func<string, string?> environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task<IDbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_inner is null)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                _inner ??= DbConnectionFactory.FromEnvironment(_environment);
            }
            finally
            {
                _gate.Release();
            }
        }

        return await _inner.OpenConnectionAsync(cancellationToken);
    }
}

/// <summary>
/// Npgsql connection factory (Plan-V3 U-03, GLM-DATA-01). Built from the
/// approved environment contract; fails explicitly when unconfigured.
///
/// The connection string must use Npgsql keyword/value format
/// (Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=Require).
/// The <c>postgresql://</c> URI form is NOT supported by the Npgsql version
/// pinned by this project (it throws at parse time), so a URI-shaped value is
/// rejected eagerly with an actionable configuration error instead of a
/// confusing parse exception deep in the first query.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new DatabaseConnectionException(
                $"Missing database connection. Set the environment variable " +
                $"'{DatabaseConnectionSettings.ConnectionStringVariable}' (or " +
                $"'{DatabaseConnectionSettings.FallbackConnectionStringVariable}') " +
                $"in keyword/value format, e.g. " +
                $"'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require'.");

        var trimmed = connectionString.Trim();
        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Deliberately does NOT echo the value: it contains credentials.
            throw new DatabaseConnectionException(
                "Invalid database connection configuration: the connection string uses the " +
                "'postgresql://' URI format, which this application's Npgsql version does not " +
                "support. Use keyword/value format instead: " +
                "'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require'. " +
                $"(Set '{DatabaseConnectionSettings.ConnectionStringVariable}' or " +
                $"'{DatabaseConnectionSettings.FallbackConnectionStringVariable}'.)");
        }

        try
        {
            _ = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Parse failure on a non-URI string: surface a configuration
            // error at first use (fail closed) without echoing the value.
            throw new DatabaseConnectionException(
                "Invalid database connection configuration: the connection string is not in " +
                "a supported keyword/value format (expected e.g. " +
                "'Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require').",
                ex);
        }

        _connectionString = connectionString;
    }

    public static DbConnectionFactory FromEnvironment(Func<string, string?> environment)
    {
        var connectionString = DatabaseConnectionSettings.ResolveConnectionString(environment);
        return connectionString is null
            ? throw new DatabaseConnectionException(
                $"Missing database connection. Set the environment variable " +
                $"'{DatabaseConnectionSettings.ConnectionStringVariable}' (or " +
                $"'{DatabaseConnectionSettings.FallbackConnectionStringVariable}'). " +
                "No connection string is ever stored in the repository.")
            : new DbConnectionFactory(connectionString);
    }

    public string ConnectionString => _connectionString;

    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new Npgsql.NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await connection.DisposeAsync();
            throw new DatabaseConnectionException(
                $"Unable to open the database connection ({ex.GetType().Name}: {ex.Message}).",
                ex);
        }
    }
}
