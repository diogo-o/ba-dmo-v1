using Dapper;

namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Base mapping conventions of the persistence foundation (Plan-V3 U-03
/// "mappings base"). The U-02 schema uses snake_case columns; C# records use
/// PascalCase. Dapper matches names case-insensitively and, once enabled,
/// matches underscored columns to PascalCase members. Mappings never rely on
/// column order and business queries enumerate columns explicitly.
///
/// Timestamp policy (06_DATA §2): timestamptz UTC everywhere; parameters pass
/// DateTimeOffset/DateTime with UTC kind (Npgsql rejects non-UTC for
/// timestamptz). Business dates are plain 'date'. Authorship columns receive
/// the server-side resolved actor (PersistenceAuthorship) — never a value
/// accepted from the client.
/// </summary>
public static class PersistenceMappings
{
    private static readonly object Sync = new();
    private static bool _configured;

    /// <summary>Idempotent; call once at application composition/startup.</summary>
    public static void Configure()
    {
        lock (Sync)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            _configured = true;
        }
    }

    /// <summary>Visible for tests.</summary>
    public static bool IsConfigured
    {
        get { lock (Sync) { return _configured; } }
    }
}
