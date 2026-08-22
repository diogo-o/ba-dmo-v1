using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Identity;

/// <summary>
/// Dapper/Npgsql implementation of the identity persistence contract
/// (tables from U-02 N01_identity.sql). Explicit parameterized SQL with
/// enumerated columns; bootstrap writes run inside ONE unit of work and are
/// audited (GLM-DATA-05, GLM-ACC-11: admin operations are audit_events with
/// moduleId = admin).
/// </summary>
public sealed class DapperInternalUserRepository : IInternalUserRepository
{
    private const string FindByAuthUserIdSql =
        """
        SELECT u.actor_id          AS ActorId,
               u.auth_user_id      AS AuthUserId,
               u.display_name      AS DisplayName,
               u.profile_title     AS ProfileTitle,
               u.active            AS UserActive,
               t.template_id       AS TemplateId,
               t.name              AS TemplateName,
               t.active            AS TemplateActive,
               t.modules::text     AS ModulesJson,
               u.modules_override::text AS ModulesOverrideJson
        FROM internal_users u
        JOIN access_templates t ON t.template_id = u.template_id
        WHERE u.auth_user_id = @AuthUserId;
        """;

    private const string AdminExistsSql =
        """
        SELECT 1
        FROM internal_users u
        JOIN access_templates t ON t.template_id = u.template_id
        WHERE u.active
          AND t.active
          AND t.modules @> @AdminGrantPattern::jsonb
        LIMIT 1;
        """;

    private const string InsertTemplateSql =
        """
        INSERT INTO access_templates (template_id, name, modules, active,
                                      created_at_utc, created_by, updated_at_utc)
        VALUES (@TemplateId, @TemplateName, @ModulesJson::jsonb, TRUE,
                @CreatedAtUtc, NULL, @CreatedAtUtc)
        ON CONFLICT (template_id) DO NOTHING;
        """;

    private const string InsertInternalUserSql =
        """
        INSERT INTO internal_users (actor_id, auth_user_id, template_id,
                                    display_name, profile_title, active,
                                    created_at_utc, updated_at_utc)
        VALUES (@ActorId, @AuthUserId, @TemplateId,
                @DisplayName, NULL, TRUE,
                @CreatedAtUtc, @CreatedAtUtc)
        ON CONFLICT (actor_id) DO NOTHING;
        """;

    private const string InsertAuditEventSql =
        """
        INSERT INTO audit_events (occurred_at_utc, year, actor_user_id,
                                  actor_name_snapshot, module_id, action_code,
                                  entity_type, entity_id, entity_label_snapshot,
                                  result, reason)
        VALUES (@OccurredAtUtc, @Year, @ActorUserId,
                @ActorNameSnapshot, 'admin', 'bootstrap_admin',
                'internal_user', @EntityId, @EntityLabelSnapshot,
                'succeeded', 'One-shot CLI bootstrap of the first Admin (GLM-ACC-13).');
        """;

    private const string AdminGrantPatternJson =
        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]";

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperInternalUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<InternalUserRecord?> FindByAuthUserIdAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            // HI-2: count the rows explicitly. QuerySingleOrDefaultAsync would
            // throw a generic InvalidOperationException on a duplicate row set,
            // which the resolution service would misclassify as a backend
            // outage. A duplicate is a data-integrity condition with its own
            // typed exception (IDENTITY_AMBIGUOUS), never an outage.
            var rows = await Db.QueryAsync<InternalUserRecord>(
                connection, FindByAuthUserIdSql,
                new { AuthUserId = authUserId },
                cancellationToken: cancellationToken);
            var list = rows.ToList();
            return list.Count switch
            {
                0 => null,
                1 => list[0],
                _ => throw new AmbiguousIdentityException(authUserId)
            };
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var row = await Db.QuerySingleOrDefaultAsync<int?>(
                connection, AdminExistsSql,
                new { AdminGrantPattern = AdminGrantPatternJson },
                cancellationToken: cancellationToken);
            return row is not null;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task CreateBootstrapAdminAsync(
        BootstrapAdminCreation creation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creation);

        // Atomic: template + internal user + audit event in ONE transaction.
        await DapperUnitOfWork.RunAsync<int>(_connectionFactory, async (connection, transaction, ct) =>
        {
            await Db.ExecuteAsync(connection, InsertTemplateSql, new
            {
                creation.TemplateId,
                creation.TemplateName,
                creation.ModulesJson,
                creation.CreatedAtUtc
            }, transaction: transaction, cancellationToken: ct);

            await Db.ExecuteAsync(connection, InsertInternalUserSql, new
            {
                creation.ActorId,
                creation.AuthUserId,
                creation.TemplateId,
                creation.DisplayName,
                creation.CreatedAtUtc
            }, transaction: transaction, cancellationToken: ct);

            await Db.ExecuteAsync(connection, InsertAuditEventSql, new
            {
                OccurredAtUtc = creation.CreatedAtUtc,
                Year = creation.CreatedAtUtc.Year,
                ActorUserId = creation.ActorId,
                ActorNameSnapshot = creation.DisplayName,
                EntityId = creation.ActorId,
                EntityLabelSnapshot = creation.DisplayName
            }, transaction: transaction, cancellationToken: ct);

            return 1;
        }, cancellationToken);
    }

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }
}
