using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Web.Pages.Admin;

/// <summary>
/// Small persistence helper for the template-owned functional profile introduced
/// by N31. Module grants remain owned by the existing access template model; this
/// store only keeps the one closed functional profile associated with a template.
/// </summary>
public sealed class TemplateProfileStore
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TemplateProfileStore(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyDictionary<string, string>> ListAsync(
        CancellationToken cancellationToken = default)
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

    public async Task<string?> GetAsync(
        string templateId,
        CancellationToken cancellationToken = default)
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

    public async Task UpsertAsync(
        string templateId,
        string functionalProfile,
        CancellationToken cancellationToken = default)
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

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }

    private sealed record TemplateProfileRow(string TemplateId, string FunctionalProfile);
}
