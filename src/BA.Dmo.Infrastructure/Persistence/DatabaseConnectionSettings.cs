namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Server-side database connection configuration contract (established by
/// U-02, reused by U-03). Credentials live ONLY in user
/// secrets/environment (06_DATA §6.5, §14) — never in the repository, never
/// browser-visible, never service_role for normal runtime access.
/// </summary>
public static class DatabaseConnectionSettings
{
    public const string ConnectionStringVariable = "BA_DMO_DB_CONNECTION_STRING";
    public const string FallbackConnectionStringVariable = "DATABASE_URL";

    /// <summary>
    /// Resolves the connection string from the environment contract:
    /// BA_DMO_DB_CONNECTION_STRING first, DATABASE_URL as fallback.
    /// Returns null when no configuration exists.
    /// </summary>
    public static string? ResolveConnectionString(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var connectionString = environment(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = environment(FallbackConnectionStringVariable);

        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }
}

/// <summary>
/// Failure to configure or open the application database connection.
/// Diagnostic but SAFE: never includes the connection string itself.
/// </summary>
public sealed class DatabaseConnectionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
