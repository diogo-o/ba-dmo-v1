using System.Text;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Dapper/Npgsql implementation of the Administration persistence port
/// (U-02 N01 tables, U-03 foundation). Explicit parameterized SQL with
/// enumerated columns; optimistic concurrency via updated_at + ConcurrencyGuard
/// (GLM-ACC-12); the self-lockout invariant (GLM-ACC-10) is validated inside
/// the SAME transaction as the write: the change is applied, the surviving
/// admin path is counted, and a zero count rolls the write back.
/// </summary>
public sealed class DapperAdminRepository : IAdminRepository
{
    private const string AdminGrantPatternJson =
        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]";

    // PostgreSQL SQLSTATE 42703 = undefined_column. Detected internally ONLY to
// recognise the N26-not-applied schema condition (internal_users.modules_override);
// it is translated to SchemaMigrationRequiredException and the user is shown a
// safe Portuguese message — the SQLSTATE itself is never surfaced.
private const string UndefinedColumnSqlState = "42703";

private const string UserColumns =
        """
        u.actor_id        AS ActorId,
        u.auth_user_id    AS AuthUserId,
        u.display_name    AS DisplayName,
        u.profile_title   AS ProfileTitle,
        u.template_id     AS TemplateId,
        u.active          AS Active,
        u.updated_at_utc  AS UpdatedAtUtc,
        NULL::text        AS AuthEmail,
        u.modules_override::text AS ModulesOverrideJson
        """;

    private const string TemplateColumns =
        """
        t.template_id     AS TemplateId,
        t.name            AS Name,
        t.modules::text   AS ModulesJson,
        t.active          AS Active,
        t.updated_at_utc  AS UpdatedAtUtc
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAdminRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ------------------------------------------------------------------
    // internal users
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var sql = $"SELECT {UserColumns} FROM internal_users u";
            object? parameters = null;
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += """
                        WHERE u.display_name ILIKE @Search
                           OR u.actor_id ILIKE @Search
                           OR u.profile_title ILIKE @Search
                        """;
                parameters = new { Search = $"%{search.Trim()}%" };
            }

            sql += " ORDER BY u.display_name, u.actor_id;";
            var rows = await Db.QueryAsync<AdminUserRow>(
                connection, sql, parameters, cancellationToken: cancellationToken);
            return rows;
        }
        catch (PostgresException ex) when (ex.SqlState == UndefinedColumnSqlState)
        {
            // The projection references internal_users.modules_override (N26),
            // which is missing — a schema/migration configuration failure, NOT
            // absence of data. Surface it as a typed internal signal for the
            // use case to translate into a safe backend-unavailable error.
            // Only this exact condition (42703) is mapped; every other database
            // error continues to propagate through its established handling.
            throw new SchemaMigrationRequiredException();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<AdminUserRow?> GetUserAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<AdminUserRow>(
                connection,
                $"SELECT {UserColumns} FROM internal_users u WHERE u.actor_id = @ActorId;",
                new { ActorId = actorId },
                cancellationToken: cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == UndefinedColumnSqlState)
        {
            // Same schema-migration-not-applied condition as ListUsersAsync:
            // translate to a typed internal signal, never to a false not-found.
            throw new SchemaMigrationRequiredException();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<bool> AuthUserIdAlreadyRegisteredAsync(
        Guid authUserId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var row = await Db.QuerySingleOrDefaultAsync<int?>(
                connection,
                "SELECT 1 FROM internal_users WHERE auth_user_id = @AuthUserId LIMIT 1;",
                new { AuthUserId = authUserId },
                cancellationToken: cancellationToken);
            return row is not null;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task CreateInternalUserAsync(
        string actorId,
        Guid authUserId,
        string displayName,
        string? profileTitle,
        string templateId,
        bool active,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection,
                """
                INSERT INTO internal_users (actor_id, auth_user_id, template_id,
                                            display_name, profile_title, active,
                                            created_at_utc, updated_at_utc)
                VALUES (@ActorId, @AuthUserId, @TemplateId,
                        @DisplayName, @ProfileTitle, @Active,
                        @CreatedAtUtc, @CreatedAtUtc)
                ON CONFLICT (actor_id) DO NOTHING;
                """,
                new
                {
                    ActorId = actorId,
                    AuthUserId = authUserId,
                    TemplateId = templateId,
                    DisplayName = displayName,
                    ProfileTitle = profileTitle,
                    Active = active,
                    CreatedAtUtc = createdAtUtc
                },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task UpdateUserAsync(
        string actorId,
        string displayName,
        string? profileTitle,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.ExecuteAsync(connection,
                """
                UPDATE internal_users
                SET display_name = @DisplayName,
                    profile_title = @ProfileTitle,
                    updated_at_utc = @UpdatedAtUtc
                WHERE actor_id = @ActorId AND updated_at_utc = @ExpectedUpdatedAt;
                """,
                new
                {
                    ActorId = actorId,
                    DisplayName = displayName,
                    ProfileTitle = profileTitle,
                    UpdatedAtUtc = updatedAtUtc,
                    ExpectedUpdatedAt = expectedUpdatedAt
                },
                cancellationToken: cancellationToken);
            ConcurrencyGuard.EnsureSingleRowUpdated(rows, "utilizador interno");
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public Task<bool> ChangeUserTemplateAsync(
        string actorId,
        string templateId,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        GuardedUserWriteAsync(
            """
            UPDATE internal_users
            SET template_id = @TemplateId,
                updated_at_utc = @UpdatedAtUtc
            WHERE actor_id = @ActorId AND updated_at_utc = @ExpectedUpdatedAt;
            """,
            new
            {
                ActorId = actorId,
                TemplateId = templateId,
                UpdatedAtUtc = updatedAtUtc,
                ExpectedUpdatedAt = expectedUpdatedAt
            },
            cancellationToken);

    public Task<bool> SetUserActiveAsync(
        string actorId,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default) =>
        GuardedUserWriteAsync(
            """
            UPDATE internal_users
            SET active = @Active,
                updated_at_utc = @UpdatedAtUtc
            WHERE actor_id = @ActorId AND updated_at_utc = @ExpectedUpdatedAt;
            """,
            new
            {
                ActorId = actorId,
                Active = active,
                UpdatedAtUtc = updatedAtUtc,
                ExpectedUpdatedAt = expectedUpdatedAt
            },
            cancellationToken);

    /// <summary>
    /// Guarded write of the per-user module override (N26). Writes ONLY
    /// modules_override (+ updated_at) for THIS actor; template rows are never
    /// touched (other users on the same template unaffected). Optimistic
    /// concurrency via updated_at (GLM-ACC-12); the module grant surface does
    /// not participate in the self-lockout count (admin.gerir still resolves
    /// through the shared template) so no admins-count guard is applied here.
    /// </summary>
    public async Task SetUserModulesOverrideAsync(
        string actorId,
        string modulesJson,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.ExecuteAsync(connection,
                """
                UPDATE internal_users
                SET modules_override = @ModulesJson::jsonb,
                    updated_at_utc = @UpdatedAtUtc
                WHERE actor_id = @ActorId AND updated_at_utc = @ExpectedUpdatedAt;
                """,
                new
                {
                    ActorId = actorId,
                    ModulesJson = modulesJson,
                    UpdatedAtUtc = updatedAtUtc,
                    ExpectedUpdatedAt = expectedUpdatedAt
                },
                cancellationToken: cancellationToken);
            ConcurrencyGuard.EnsureSingleRowUpdated(rows, "utilizador interno");
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    /// <summary>
    /// Optimistic update + self-lockout invariant in ONE transaction:
    /// apply the write, count surviving active admins, roll back on zero.
    /// Concurrency violations propagate as ConcurrencyConflictException.
    /// </summary>
    private async Task<bool> GuardedUserWriteAsync(
        string updateSql, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            await DapperUnitOfWork.RunAsync<int>(_connectionFactory,
                async (connection, transaction, ct) =>
                {
                    var rows = await Db.ExecuteAsync(
                        connection, updateSql, parameters,
                        transaction: transaction, cancellationToken: ct);
                    ConcurrencyGuard.EnsureSingleRowUpdated(rows, "utilizador interno");

                    var admins = await CountActiveAdminsOnAsync(
                        connection, transaction, excludeActorId: null, ct);
                    if (admins == 0)
                        throw new LockoutViolationException();

                    return 1;
                }, cancellationToken);
            return true;
        }
        catch (LockoutViolationException)
        {
            return false;
        }
    }

    public async Task<int> CountActiveAdminsAsync(
        string? excludeActorId = null, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await CountActiveAdminsOnAsync(
                connection, transaction: null, excludeActorId, cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    private static async Task<int> CountActiveAdminsOnAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        string? excludeActorId,
        CancellationToken cancellationToken)
    {
        var row = await Db.QuerySingleOrDefaultAsync<int?>(connection,
            """
            SELECT COUNT(*)
            FROM internal_users u
            JOIN access_templates t ON t.template_id = u.template_id
            WHERE u.active
              AND t.active
              AND t.modules @> @AdminGrantPattern::jsonb
              AND (@ExcludeActorId::text IS NULL OR u.actor_id <> @ExcludeActorId);
            """,
            new { AdminGrantPattern = AdminGrantPatternJson, ExcludeActorId = excludeActorId },
            transaction: transaction,
            cancellationToken: cancellationToken);
        return row ?? 0;
    }

    // ------------------------------------------------------------------
    // access templates
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QueryAsync<AdminTemplateRow>(connection,
                $"SELECT {TemplateColumns} FROM access_templates t ORDER BY t.name;",
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<AdminTemplateRow?> GetTemplateAsync(
        string templateId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<AdminTemplateRow>(connection,
                $"SELECT {TemplateColumns} FROM access_templates t WHERE t.template_id = @TemplateId;",
                new { TemplateId = templateId },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task CreateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection,
                """
                INSERT INTO access_templates (template_id, name, modules, active,
                                              created_at_utc, created_by, updated_at_utc)
                VALUES (@TemplateId, @Name, @ModulesJson::jsonb, TRUE,
                        @CreatedAtUtc, NULL, @CreatedAtUtc);
                """,
                new
                {
                    TemplateId = templateId,
                    Name = name,
                    ModulesJson = modulesJson,
                    CreatedAtUtc = createdAtUtc
                },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<bool> UpdateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await DapperUnitOfWork.RunAsync<int>(_connectionFactory,
                async (connection, transaction, ct) =>
                {
                    var rows = await Db.ExecuteAsync(connection,
                        """
                        UPDATE access_templates
                        SET name = @Name,
                            modules = @ModulesJson::jsonb,
                            active = @Active,
                            updated_at_utc = @UpdatedAtUtc
                        WHERE template_id = @TemplateId
                          AND updated_at_utc = @ExpectedUpdatedAt;
                        """,
                        new
                        {
                            TemplateId = templateId,
                            Name = name,
                            ModulesJson = modulesJson,
                            Active = active,
                            UpdatedAtUtc = updatedAtUtc,
                            ExpectedUpdatedAt = expectedUpdatedAt
                        },
                        transaction: transaction, cancellationToken: ct);
                    ConcurrencyGuard.EnsureSingleRowUpdated(rows, "template de acesso");

                    var admins = await CountActiveAdminsOnAsync(
                        connection, transaction, excludeActorId: null, ct);
                    if (admins == 0)
                        throw new LockoutViolationException();

                    return 1;
                }, cancellationToken);
            return true;
        }
        catch (LockoutViolationException)
        {
            return false;
        }
    }

    // ------------------------------------------------------------------
    // audit
    // ------------------------------------------------------------------

    public async Task InsertAuditEventAsync(
        AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection,
                """
                INSERT INTO audit_events (occurred_at_utc, year, actor_user_id,
                                          actor_name_snapshot, module_id, action_code,
                                          entity_type, entity_id, entity_label_snapshot,
                                          result, reason, before_summary, after_summary)
                VALUES (@OccurredAtUtc, @Year, @ActorUserId,
                        @ActorNameSnapshot, @ModuleId, @ActionCode,
                        @EntityType, @EntityId, @EntityLabelSnapshot,
                        @Result, @Reason, @BeforeSummary::jsonb, @AfterSummary::jsonb);
                """,
                new
                {
                    entry.OccurredAtUtc,
                    Year = entry.OccurredAtUtc.Year,
                    entry.ActorUserId,
                    entry.ActorNameSnapshot,
                    entry.ModuleId,
                    entry.ActionCode,
                    entry.EntityType,
                    entry.EntityId,
                    entry.EntityLabelSnapshot,
                    entry.Result,
                    entry.Reason,
                    BeforeSummary = (string?)null,
                    AfterSummary = (string?)null
                },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<AuditQueryResult> QueryAuditAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var where = new StringBuilder("WHERE TRUE");
            var parameters = new DynamicParameters();

            if (filter.Year is not null)
            {
                where.Append(" AND year = @Year");
                parameters.Add("Year", filter.Year);
            }

            if (!string.IsNullOrWhiteSpace(filter.ActorUserId))
            {
                where.Append(" AND actor_user_id = @ActorUserId");
                parameters.Add("ActorUserId", filter.ActorUserId);
            }

            if (!string.IsNullOrWhiteSpace(filter.ModuleId))
            {
                where.Append(" AND module_id = @ModuleId");
                parameters.Add("ModuleId", filter.ModuleId);
            }

            if (!string.IsNullOrWhiteSpace(filter.ActionCode))
            {
                where.Append(" AND action_code = @ActionCode");
                parameters.Add("ActionCode", filter.ActionCode);
            }

            if (!string.IsNullOrWhiteSpace(filter.Result))
            {
                where.Append(" AND result = @Result");
                parameters.Add("Result", filter.Result);
            }

            if (filter.FromUtc is not null)
            {
                where.Append(" AND occurred_at_utc >= @FromUtc");
                parameters.Add("FromUtc", filter.FromUtc);
            }

            if (filter.ToUtc is not null)
            {
                where.Append(" AND occurred_at_utc <= @ToUtc");
                parameters.Add("ToUtc", filter.ToUtc);
            }

            var totalRow = await Db.QuerySingleOrDefaultAsync<int?>(connection,
                $"SELECT COUNT(*) FROM audit_events {where};",
                parameters, cancellationToken: cancellationToken);
            var total = totalRow ?? 0;

            var pageSql = new StringBuilder(
                $"""
                SELECT occurred_at_utc      AS OccurredAtUtc,
                       year                 AS Year,
                       actor_user_id        AS ActorUserId,
                       actor_name_snapshot  AS ActorNameSnapshot,
                       module_id            AS ModuleId,
                       action_code          AS ActionCode,
                       entity_type          AS EntityType,
                       entity_id            AS EntityId,
                       entity_label_snapshot AS EntityLabelSnapshot,
                       result               AS Result,
                       reason               AS Reason
                FROM audit_events
                {where}
                ORDER BY occurred_at_utc DESC
                """);

            if (filter.PageSize > 0)
            {
                pageSql.Append(" LIMIT @PageSize OFFSET @Offset");
                parameters.Add("PageSize", filter.PageSize);
                parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);
            }

            pageSql.Append(';');

            var rows = await Db.QueryAsync<AuditEventRow>(
                connection, pageSql.ToString(), parameters,
                cancellationToken: cancellationToken);

            return new AuditQueryResult(rows, total, filter.Page, filter.PageSize);
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

    /// <summary>Rolled-back marker for the self-lockout invariant.</summary>
    private sealed class LockoutViolationException : Exception;
}
