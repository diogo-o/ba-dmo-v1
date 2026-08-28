using System.Text;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Access;
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
///
/// SCHEMA-RAT-03A (D-1/D-2): user assignment is single-template and the
/// canonical store is internal_users.template_id (direct FK). The functional
/// profile is template-owned (access_template_profiles): template create/
/// update write the profile in the same transaction; the admin user
/// projections read it through a join — never from a user-level column.
///
/// SCHEMA-RAT-03B: the legacy mirror structures (the N27 junction table and
/// the user-level profile mirror column) are RETIRED. No runtime statement in
/// this file reads or writes either structure;
/// N33_legacy_access_mirror_quiescence.sql revokes ba_dmo_app privileges on
/// both as the mechanical kill switch. Both structures stay physically
/// present until the later, separately designed destructive phase.
/// </summary>
public sealed class DapperAdminRepository : IAdminRepository
{
    private const string AdminGrantPatternJson = "[{\"moduleId\":\"admin\"}]";

    private const string UserColumns =
        """
        u.actor_id        AS ActorId,
        u.auth_user_id    AS AuthUserId,
        u.display_name    AS DisplayName,
        pt.functional_profile AS ProfileTitle,
        u.template_id     AS TemplateId,
        u.active          AS Active,
        u.updated_at_utc  AS UpdatedAtUtc,
        NULL::text        AS AuthEmail,
        NULL::text        AS ModulesOverrideJson
        , ARRAY[u.template_id] AS TemplateIds
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
            var sql = $"SELECT {UserColumns} FROM internal_users u LEFT JOIN access_template_profiles pt ON pt.template_id = u.template_id";
            object? parameters = null;
            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += """
                        WHERE u.display_name ILIKE @Search
                           OR u.actor_id ILIKE @Search
                           OR pt.functional_profile ILIKE @Search
                        """;
                parameters = new { Search = $"%{search.Trim()}%" };
            }

            sql += " ORDER BY u.display_name, u.actor_id;";
            var rows = await Db.QueryAsync<AdminUserRow>(
                connection, sql, parameters, cancellationToken: cancellationToken);
            return rows;
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
                $"SELECT {UserColumns} FROM internal_users u LEFT JOIN access_template_profiles pt ON pt.template_id = u.template_id WHERE u.actor_id = @ActorId;",
                new { ActorId = actorId },
                cancellationToken: cancellationToken);
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
        string templateId,
        bool active,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            // SCHEMA-RAT-03B: the insert writes the canonical direct FK only.
            // The profile mirror column is left NULL for new rows (the
            // retired mirror — N33 makes the column nullable; deploy order is
            // migrate N33 BEFORE this build's first user write) and the
            // junction mirror is no longer written at all.
            try
            {
                await Db.ExecuteAsync(connection,
                    """
                    INSERT INTO internal_users (actor_id, auth_user_id, template_id,
                                                display_name, active,
                                                created_at_utc, updated_at_utc)
                    VALUES (@ActorId, @AuthUserId, @TemplateId,
                            @DisplayName,
                            @Active,
                            @CreatedAtUtc, @CreatedAtUtc)
                    ON CONFLICT (actor_id) DO NOTHING;
                    """,
                    new
                    {
                        ActorId = actorId,
                        AuthUserId = authUserId,
                        TemplateId = templateId,
                        DisplayName = displayName,
                        Active = active,
                        CreatedAtUtc = createdAtUtc
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // uq_internal_users_auth_user — the (actor_id) ON CONFLICT does not
                // absorb a same-Auth-user duplicate with a different actor_id
                // (audit ADM-06/ON-02).
                throw new InternalUserAuthDuplicateException(
                    "Já existe um utilizador interno associado a esta conta de autenticação.");
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task UpdateUserAsync(
        string actorId,
        string displayName,
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
                    updated_at_utc = @UpdatedAtUtc
                WHERE actor_id = @ActorId AND updated_at_utc = @ExpectedUpdatedAt;
                """,
                new
                {
                    ActorId = actorId,
                    DisplayName = displayName,
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

    public async Task<bool> ChangeUserTemplateAsync(
        string actorId,
        string templateId,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await DapperUnitOfWork.RunAsync<int>(_connectionFactory,
                async (connection, transaction, ct) =>
                {
                    // 1. Canonical single assignment: internal_users.template_id.
                    // (SCHEMA-RAT-03B: the N27 junction mirror is RETIRED — no
                    // mirror row is written or updated here anymore.)
                    var rows = await Db.ExecuteAsync(connection,
                        """
                        UPDATE internal_users
                        SET template_id = @TemplateId,
                            updated_at_utc = @UpdatedAtUtc
                        WHERE actor_id = @ActorId
                          AND updated_at_utc = @ExpectedUpdatedAt;
                        """,
                        new
                        {
                            ActorId = actorId,
                            TemplateId = templateId,
                            UpdatedAtUtc = updatedAtUtc,
                            ExpectedUpdatedAt = expectedUpdatedAt
                        }, transaction: transaction, cancellationToken: ct);
                    ConcurrencyGuard.EnsureSingleRowUpdated(rows, "utilizador interno");

                    // 2. Self-lockout invariant (GLM-ACC-10).
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

    private static async Task<int> CountActiveAdminsOnAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        string? excludeActorId,
        CancellationToken cancellationToken)
    {
        var row = await Db.QuerySingleOrDefaultAsync<int?>(connection,
            """
            SELECT COUNT(DISTINCT u.actor_id)
            FROM internal_users u
            JOIN access_templates t ON t.template_id = u.template_id
            JOIN access_template_profiles p ON p.template_id = t.template_id
            WHERE u.active
              AND p.functional_profile = 'Admin'
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
    // access templates / template-owned functional profile
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

    public async Task<string?> GetTemplateFunctionalProfileAsync(
        string templateId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<string>(connection,
                """
                SELECT p.functional_profile
                FROM access_template_profiles p
                WHERE p.template_id = @TemplateId;
                """,
                new { TemplateId = templateId },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ListTemplateFunctionalProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.QueryAsync<TemplateProfileRow>(connection,
                """
                SELECT p.template_id AS TemplateId, p.functional_profile AS FunctionalProfile
                FROM access_template_profiles p
                ORDER BY p.template_id;
                """,
                cancellationToken: cancellationToken);
            return rows.ToDictionary(row => row.TemplateId, row => row.FunctionalProfile, StringComparer.Ordinal);
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
        string functionalProfile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await DapperUnitOfWork.RunAsync<int>(_connectionFactory,
                async (connection, transaction, ct) =>
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
                        transaction: transaction, cancellationToken: ct);

                    // Template-owned functional profile (D-1), written in the
                    // same transaction. The N31 AFTER INSERT trigger derives a
                    // deterministic initial profile; the explicit choice wins.
                    await Db.ExecuteAsync(connection,
                        """
                        INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
                        VALUES (@TemplateId, @FunctionalProfile, @CreatedAtUtc)
                        ON CONFLICT (template_id) DO UPDATE
                        SET functional_profile = EXCLUDED.functional_profile,
                            updated_at_utc = EXCLUDED.updated_at_utc;
                        """,
                        new
                        {
                            TemplateId = templateId,
                            FunctionalProfile = functionalProfile,
                            CreatedAtUtc = createdAtUtc
                        },
                        transaction: transaction, cancellationToken: ct);

                    return 1;
                }, cancellationToken);
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
        string functionalProfile,
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

                    // Template-owned functional profile (D-1), written in the
                    // same transaction. SCHEMA-RAT-03B: the one-way user
                    // profile mirror is RETIRED — the profile authority write
                    // below is the ONLY profile write; users of the template
                    // resolve their profile through the read join.
                    // (The mirror update never touched
                    // internal_users.updated_at_utc — that no longer applies.)
                    await Db.ExecuteAsync(connection,
                        """
                        INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
                        VALUES (@TemplateId, @FunctionalProfile, @UpdatedAtUtc)
                        ON CONFLICT (template_id) DO UPDATE
                        SET functional_profile = EXCLUDED.functional_profile,
                            updated_at_utc = EXCLUDED.updated_at_utc;
                        """,
                        new
                        {
                            TemplateId = templateId,
                            FunctionalProfile = functionalProfile,
                            UpdatedAtUtc = updatedAtUtc
                        },
                        transaction: transaction, cancellationToken: ct);

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
                    // Defensive jsonb hardening (audit PC-11/ADM-08): summaries
                    // are normalized through AuditJson.Normalize so a future
                    // non-null NON-JSON payload can never 22P02 against the
                    // ::jsonb cast. NULL stays NULL (Manual-compliant).
                    BeforeSummary = AuditJson.Normalize(entry.BeforeSummary),
                    AfterSummary = AuditJson.Normalize(entry.AfterSummary)
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

    private sealed record TemplateProfileRow(string TemplateId, string FunctionalProfile);

    /// <summary>Rolled-back marker for the self-lockout invariant.</summary>
    private sealed class LockoutViolationException : Exception;
}
