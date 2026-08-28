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
///
/// SCHEMA-RAT-03A (D-1/D-2): identity is resolved through the canonical
/// direct assignment internal_users.template_id -> access_templates ->
/// access_template_profiles. The N27 junction is NOT joined for identity.
///
/// SCHEMA-RAT-03B: the legacy mirrors are RETIRED. No statement in this file
/// reads or writes either legacy mirror structure (the N27 junction table or
/// the user-level profile mirror column); the record's ProfileTitle slot is
/// always NULL (kept only for shape compatibility — it was never a
/// functional-access authority).
/// </summary>
public sealed class DapperInternalUserRepository : IInternalUserRepository
{
    private const string FindByAuthUserIdSql =
        """
        SELECT u.actor_id             AS ActorId,
               u.auth_user_id         AS AuthUserId,
               u.display_name         AS DisplayName,
               NULL::text             AS ProfileTitle, -- retired mirror (SCHEMA-RAT-03B): always NULL
               u.active               AS UserActive,
               t.template_id          AS TemplateId,
               t.name                 AS TemplateName,
               t.active               AS TemplateActive,
               t.modules::text        AS ModulesJson,
               NULL::text             AS ModulesOverrideJson, -- N38 removed dormant override column
               p.functional_profile   AS FunctionalProfile
        FROM internal_users u
        JOIN access_templates t ON t.template_id = u.template_id
        LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
        WHERE u.auth_user_id = @AuthUserId;
        """;

    private const string AdminExistsSql =
        """
        SELECT 1
        FROM internal_users u
        JOIN access_templates t ON t.template_id = u.template_id
        JOIN access_template_profiles p ON p.template_id = t.template_id
        WHERE u.active
          AND p.functional_profile = 'Admin'
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
                                    display_name, active,
                                    created_at_utc, updated_at_utc)
        VALUES (@ActorId, @AuthUserId, @TemplateId,
                @DisplayName,
                TRUE,
                @CreatedAtUtc, @CreatedAtUtc)
        ON CONFLICT (actor_id) DO NOTHING;
        """;

    // SCHEMA-RAT-03B: the N27 junction mirror insert (previously
    // InsertUserTemplateSql) is REMOVED — bootstrap no longer writes the
    // legacy junction table. The direct FK is the only assignment.

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

    private const string AdminGrantPatternJson = "[{\"moduleId\":\"admin\"}]";

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
            var rows = await Db.QueryAsync<IdentityRow>(
                connection, FindByAuthUserIdSql,
                new { AuthUserId = authUserId },
                cancellationToken: cancellationToken);
            var list = rows.ToList();
            if (list.Count == 0)
                return null;

            var actorIds = list.Select(row => row.ActorId).Distinct(StringComparer.Ordinal).ToList();
            if (actorIds.Count != 1)
                throw new AmbiguousIdentityException(authUserId);

            var first = list[0];

            // D-2: the effective template is the canonical direct FK row —
            // exactly one, from the JOIN on internal_users.template_id. No
            // junction enumeration.
            return new InternalUserRecord(
                first.ActorId,
                first.AuthUserId,
                first.DisplayName,
                first.ProfileTitle,
                first.UserActive,
                first.TemplateId,
                first.TemplateName,
                first.TemplateActive,
                first.ModulesJson,
                first.ModulesOverrideJson,
                first.FunctionalProfile);
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
        // The direct FK (internal_users.template_id) is the authority.
        // SCHEMA-RAT-03B: the junction row and the user profile mirror column
        // are RETIRED — bootstrap no longer writes either legacy mirror.
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

    private sealed record IdentityRow(
        string ActorId,
        Guid AuthUserId,
        string DisplayName,
        string? ProfileTitle,
        bool UserActive,
        string TemplateId,
        string TemplateName,
        bool TemplateActive,
        string ModulesJson,
        string? ModulesOverrideJson,
        string? FunctionalProfile);
}
