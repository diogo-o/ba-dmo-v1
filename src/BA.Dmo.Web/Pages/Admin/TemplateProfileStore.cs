using System.Collections.Concurrent;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Web.Pages.Admin;

/// <summary>
/// Persistence helper for the template-owned functional profile introduced by
/// N31. Production uses PostgreSQL. The tiny in-memory fallback is used only
/// when the host has no database connection at all (the existing isolated web
/// test hosts); a reachable database with a missing/invalid N31 still fails and
/// therefore cannot silently mask deployment drift.
/// </summary>
public sealed class TemplateProfileStore
{
    private static readonly ConcurrentDictionary<string, string> NoDatabaseFallback =
        new(StringComparer.Ordinal)
        {
            ["tpl-admin"] = "Admin",
            ["tpl-op"] = "Operador / Controlador",
            ["tpl-operator"] = "Operador / Controlador",
            ["tpl-responsible"] = "Responsável"
        };

    private readonly IDbConnectionFactory _connectionFactory;

    public TemplateProfileStore(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            try
            {
                var rows = await Db.QueryAsync<TemplateProfileRow>(
                    connection,
                    """
                    SELECT template_id AS TemplateId,
                           functional_profile AS FunctionalProfile
                    FROM access_template_profiles
                    ORDER BY template_id;
                    """,
                    cancellationToken: cancellationToken);

                return rows.ToDictionary(
                    row => row.TemplateId,
                    row => row.FunctionalProfile,
                    StringComparer.Ordinal);
            }
            finally
            {
                await DisposeAsync(connection);
            }
        }
        catch (DatabaseConnectionException)
        {
            return new Dictionary<string, string>(NoDatabaseFallback, StringComparer.Ordinal);
        }
    }

    public async Task<string?> GetAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            try
            {
                return await Db.QuerySingleOrDefaultAsync<string>(
                    connection,
                    """
                    SELECT functional_profile
                    FROM access_template_profiles
                    WHERE template_id = @TemplateId;
                    """,
                    new { TemplateId = templateId },
                    cancellationToken: cancellationToken);
            }
            finally
            {
                await DisposeAsync(connection);
            }
        }
        catch (DatabaseConnectionException)
        {
            return NoDatabaseFallback.TryGetValue(templateId, out var profile)
                ? profile
                : null;
        }
    }

    public async Task UpsertAsync(
        string templateId,
        string functionalProfile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
            try
            {
                await Db.ExecuteAsync(
                    connection,
                    """
                    INSERT INTO access_template_profiles (
                        template_id, functional_profile, updated_at_utc)
                    VALUES (@TemplateId, @FunctionalProfile, now())
                    ON CONFLICT (template_id) DO UPDATE
                    SET functional_profile = EXCLUDED.functional_profile,
                        updated_at_utc = now();

                    UPDATE internal_users
                    SET profile_title = @FunctionalProfile,
                        updated_at_utc = now()
                    WHERE template_id = @TemplateId
                      AND profile_title IS DISTINCT FROM @FunctionalProfile;
                    """,
                    new { TemplateId = templateId, FunctionalProfile = functionalProfile },
                    cancellationToken: cancellationToken);
            }
            finally
            {
                await DisposeAsync(connection);
            }
        }
        catch (DatabaseConnectionException)
        {
            NoDatabaseFallback[templateId] = functionalProfile;
        }
    }

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }

    private sealed record TemplateProfileRow(string TemplateId, string FunctionalProfile);
}
