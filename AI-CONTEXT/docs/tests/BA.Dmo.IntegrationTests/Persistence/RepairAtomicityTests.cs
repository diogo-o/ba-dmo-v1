using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL rollback proofs for REPAIR-01. The disposable database is
/// supplied through BA_DMO_TEST_DATABASE, following RemediationGuardTests.
/// </summary>
public sealed class RepairAtomicityTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task CreateExitAsync_Success_CommitsExitAllItemsAndAudits()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync(pieceExistsInDatabase: true);
        try
        {
            var service = CreateService(context);
            var result = await service.CreateExitAsync(new CreateExitRequest(
                RepairType.CM, null, new DateOnly(2026, 8, 27),
                [new NewExitItemRequest(context.Piece.PhysicalPieceId, context.Piece.Number)], null));

            Assert.True(result.IsSuccess);
            Assert.Equal(1, await CountAsync("repair_exits", "repair_exit_id", result.Value));
            Assert.Equal(1, await CountAsync("repair_exit_items", "repair_exit_id", result.Value));
            Assert.Equal(2, await CountAuditsAsync(result.Value));
        }
        finally
        {
            await CleanupContextAsync(context);
        }
    }

    [Fact]
    public async Task CreateExitAsync_ItemInsertFailure_RollsBackExitItemsAndAudits()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync(pieceExistsInDatabase: false);
        try
        {
            var service = CreateService(context);
            Guid? attemptedExitId = null;

            var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                try
                {
                    await service.CreateExitAsync(new CreateExitRequest(
                        RepairType.CM, null, new DateOnly(2026, 8, 27),
                        [new NewExitItemRequest(context.Piece.PhysicalPieceId, context.Piece.Number)], null));
                }
                finally
                {
                    attemptedExitId = context.Repository.LastCreatedExitId;
                }
            });

            Assert.NotNull(exception);
            Assert.NotNull(attemptedExitId);
            Assert.Equal(0, await CountAsync("repair_exits", "repair_exit_id", attemptedExitId!.Value));
            Assert.Equal(0, await CountAsync("repair_exit_items", "repair_exit_id", attemptedExitId.Value));
            Assert.Equal(0, await CountAuditsAsync(attemptedExitId.Value));
        }
        finally
        {
            await CleanupContextAsync(context);
        }
    }

    [Fact]
    public async Task SetRepairerRepairTypesAsync_Success_CommitsCompleteReplacement()
    {
        if (SkipIfNoDatabase()) return;
        var repairerId = await SeedRepairerAsync();
        try
        {
            var repository = new DapperRepairRepository(new DbConnectionFactory(ConnectionString!));
            await repository.SetRepairerRepairTypesAsync(repairerId, ["CM", "MF"]);

            Assert.Equal(["CM", "MF"], await ReadRepairTypesAsync(repairerId));
        }
        finally
        {
            await CleanupRepairerAsync(repairerId);
        }
    }

    [Fact]
    public async Task SetRepairerRepairTypesAsync_InsertFailure_RestoresPreviousSet()
    {
        if (SkipIfNoDatabase()) return;
        var repairerId = await SeedRepairerAsync();
        try
        {
            var repository = new DapperRepairRepository(new DbConnectionFactory(ConnectionString!));
            await repository.SetRepairerRepairTypesAsync(repairerId, ["BQ"]);

            await Assert.ThrowsAsync<PostgresException>(() =>
                repository.SetRepairerRepairTypesAsync(repairerId, ["CM", "INVALID"]));

            Assert.Equal(["BQ"], await ReadRepairTypesAsync(repairerId));
        }
        finally
        {
            await CleanupRepairerAsync(repairerId);
        }
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] RepairAtomicityTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL rollback assertions were not executed.");
        return true;
    }

    private static async Task<TestContext> SeedContextAsync(bool pieceExistsInDatabase)
    {
        var actorId = "repair01-" + Guid.NewGuid().ToString("N");
        var templateId = "repair01-tpl-" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, 'Repair atomic test', '["reparacao_externa"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, profile_title, active)
            VALUES
                (@ActorId, @AuthUserId, @TemplateId, 'Repair Atomic Test', 'Admin', TRUE);
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()));

        var referenceId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var pieceId = Guid.NewGuid();
        var reference = "CM-R01-" + Guid.NewGuid().ToString("N")[..8];
        var lot = "R01-LOT-" + Guid.NewGuid().ToString("N")[..8];
        var number = "R01-N-" + Guid.NewGuid().ToString("N")[..8];
        if (pieceExistsInDatabase)
        {
            await ExecuteAsync(
                """
                INSERT INTO tool_references (tool_reference_id, tool_type, ref_code)
                VALUES (@ReferenceId, 'CM', @Reference);
                INSERT INTO tool_lotes (tool_lote_id, tool_reference_id, lote)
                VALUES (@LotId, @ReferenceId, @Lot);
                INSERT INTO physical_pieces (physical_piece_id, tool_lote_id, sequence, number)
                VALUES (@PieceId, @LotId, 1, @Number);
                """,
                new NpgsqlParameter("ReferenceId", referenceId),
                new NpgsqlParameter("Reference", reference),
                new NpgsqlParameter("LotId", lotId),
                new NpgsqlParameter("Lot", lot),
                new NpgsqlParameter("PieceId", pieceId),
                new NpgsqlParameter("Number", number));
        }

        var piece = new RepairToolIdentity(
            pieceId, lotId, referenceId, RepairType.CM, reference, lot, number, null);
        var factory = new DbConnectionFactory(ConnectionString!);
        var repository = new TrackingRepairRepository(new DapperRepairRepository(factory));
        return new TestContext(actorId, templateId, piece, repository, factory);
    }

    private static ReparacaoExternaService CreateService(TestContext context)
    {
        var currentUser = new FixedCurrentUser(context.ActorId);
        var gate = new ReparacaoExternaAuthorizationGate(
            currentUser, new FixedAuthorship(context.ActorId));
        return new ReparacaoExternaService(
            context.Repository,
            new FixedToolResolver(context.Piece),
            new UnusedArmazemPort(),
            new DapperRepairUnitOfWorkFactory(context.ConnectionFactory),
            gate,
            new FixedClock());
    }

    private static async Task<Guid> SeedRepairerAsync()
    {
        var repairerId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO repairers (repairer_id, name, active) VALUES (@Id, @Name, TRUE);",
            new NpgsqlParameter("Id", repairerId),
            new NpgsqlParameter("Name", "REPAIR-01 Atomic Test " + repairerId.ToString("N")));
        return repairerId;
    }

    private static async Task<string[]> ReadRepairTypesAsync(Guid repairerId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT repair_type FROM repairer_repair_types WHERE repairer_id = @Id ORDER BY repair_type;",
            connection);
        command.Parameters.AddWithValue("Id", repairerId);
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    private static async Task<int> CountAsync(string table, string keyColumn, Guid id)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT count(*)::int FROM {table} WHERE {keyColumn} = @Id;", connection);
        command.Parameters.AddWithValue("Id", id);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountAuditsAsync(Guid exitId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int FROM audit_events
            WHERE module_id = 'reparacao_externa' AND entity_id = @EntityId;
            """, connection);
        command.Parameters.AddWithValue("EntityId", exitId.ToString());
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task CleanupContextAsync(TestContext context)
    {
        await ExecuteAsync(
            """
            DELETE FROM audit_events WHERE actor_user_id = @ActorId;
            DELETE FROM repair_exit_items
            WHERE repair_exit_id IN (SELECT repair_exit_id FROM repair_exits WHERE created_by = @ActorId);
            DELETE FROM repair_exits WHERE created_by = @ActorId;
            DELETE FROM physical_pieces WHERE physical_piece_id = @PieceId;
            DELETE FROM tool_lotes WHERE tool_lote_id = @LotId;
            DELETE FROM tool_references WHERE tool_reference_id = @ReferenceId;
            DELETE FROM internal_users WHERE actor_id = @ActorId;
            DELETE FROM access_templates WHERE template_id = @TemplateId;
            """,
            new NpgsqlParameter("ActorId", context.ActorId),
            new NpgsqlParameter("TemplateId", context.TemplateId),
            new NpgsqlParameter("PieceId", context.Piece.PhysicalPieceId),
            new NpgsqlParameter("LotId", context.Piece.ToolLoteId),
            new NpgsqlParameter("ReferenceId", context.Piece.ToolReferenceId));

        Assert.Equal(0, await CountTextAsync("internal_users", "actor_id", context.ActorId));
        Assert.Equal(0, await CountTextAsync("access_templates", "template_id", context.TemplateId));
        Assert.Equal(0, await CountAsync("physical_pieces", "physical_piece_id", context.Piece.PhysicalPieceId));
    }

    private static async Task CleanupRepairerAsync(Guid repairerId)
    {
        await ExecuteAsync(
            """
            DELETE FROM repairer_repair_types WHERE repairer_id = @Id;
            DELETE FROM repairers WHERE repairer_id = @Id;
            """,
            new NpgsqlParameter("Id", repairerId));

        Assert.Equal(0, await CountAsync("repairer_repair_types", "repairer_id", repairerId));
        Assert.Equal(0, await CountAsync("repairers", "repairer_id", repairerId));
    }

    private static async Task<int> CountTextAsync(string table, string keyColumn, string value)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT count(*)::int FROM {table} WHERE {keyColumn} = @Value;", connection);
        command.Parameters.AddWithValue("Value", value);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record TestContext(
        string ActorId,
        string TemplateId,
        RepairToolIdentity Piece,
        TrackingRepairRepository Repository,
        DbConnectionFactory ConnectionFactory);

    private sealed class TrackingRepairRepository(DapperRepairRepository inner) : IRepairRepository
    {
        public Guid? LastCreatedExitId { get; private set; }

        public async Task<Guid> CreateExitAsync(IDbUnitOfWork uow, RepairExit exit, RepairerSnapshot? snapshot, string? json, CancellationToken ct = default)
        {
            LastCreatedExitId = exit.RepairExitId;
            return await inner.CreateExitAsync(uow, exit, snapshot, json, ct);
        }

        public Task<Guid> CreateExitAsync(RepairExit exit, RepairerSnapshot? snapshot, string? json, CancellationToken ct = default) => inner.CreateExitAsync(exit, snapshot, json, ct);
        public Task<RepairExit?> GetExitByIdAsync(Guid id, CancellationToken ct = default) => inner.GetExitByIdAsync(id, ct);
        public Task<IReadOnlyList<RepairExitItem>> GetExitItemsAsync(Guid id, CancellationToken ct = default) => inner.GetExitItemsAsync(id, ct);
        public Task<IReadOnlyList<RepairExit>> ListExitsAsync(RepairType? type, RepairExitStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default) => inner.ListExitsAsync(type, status, from, to, ct);
        public Task<bool> ExistsItemInOpenExitAsync(Guid pieceId, CancellationToken ct = default) => inner.ExistsItemInOpenExitAsync(pieceId, ct);
        public Task<Guid> AddItemAsync(RepairExitItem item, CancellationToken ct = default) => inner.AddItemAsync(item, ct);
        public Task<Guid> AddItemAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) => inner.AddItemAsync(uow, item, ct);
        public Task<RepairExitItem?> GetItemByIdAsync(Guid id, CancellationToken ct = default) => inner.GetItemByIdAsync(id, ct);
        public Task DeleteItemAsync(Guid id, CancellationToken ct = default) => inner.DeleteItemAsync(id, ct);
        public Task ConfirmItemPickedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) => inner.ConfirmItemPickedAsync(uow, item, ct);
        public Task ConfirmItemReturnedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) => inner.ConfirmItemReturnedAsync(uow, item, ct);
        public Task UpdateExitStatusAsync(IDbUnitOfWork uow, Guid id, string status, CancellationToken ct = default) => inner.UpdateExitStatusAsync(uow, id, status, ct);
        public Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid id, string? notes, string actor, DateTimeOffset at, CancellationToken ct = default) => inner.InsertRepairEventAsync(uow, id, notes, actor, at, ct);
        public Task<Guid> CreateRepairerAsync(Repairer repairer, CancellationToken ct = default) => inner.CreateRepairerAsync(repairer, ct);
        public Task UpdateRepairerAsync(Repairer repairer, CancellationToken ct = default) => inner.UpdateRepairerAsync(repairer, ct);
        public Task DeactivateRepairerAsync(Guid id, CancellationToken ct = default) => inner.DeactivateRepairerAsync(id, ct);
        public Task<Repairer?> GetRepairerByIdAsync(Guid id, CancellationToken ct = default) => inner.GetRepairerByIdAsync(id, ct);
        public Task<IReadOnlyList<Repairer>> ListRepairersAsync(CancellationToken ct = default) => inner.ListRepairersAsync(ct);
        public Task UpsertLineDefaultAsync(LineRepairerDefault value, CancellationToken ct = default) => inner.UpsertLineDefaultAsync(value, ct);
        public Task<IReadOnlyList<LineRepairerDefault>> ListLineDefaultsAsync(CancellationToken ct = default) => inner.ListLineDefaultsAsync(ct);
        public Task SetRepairerRepairTypesAsync(Guid id, IEnumerable<string> types, CancellationToken ct = default) => inner.SetRepairerRepairTypesAsync(id, types, ct);
        public Task<IReadOnlySet<string>> ListRepairerRepairTypesAsync(Guid id, CancellationToken ct = default) => inner.ListRepairerRepairTypesAsync(id, ct);
        public Task InsertAuditEventAsync(Guid? id, string type, string? before, string? after, string actor, CancellationToken ct = default) => inner.InsertAuditEventAsync(id, type, before, after, actor, ct);
        public Task InsertAuditEventAsync(IDbUnitOfWork uow, Guid? id, string type, string? before, string? after, string actor, CancellationToken ct = default) => inner.InsertAuditEventAsync(uow, id, type, before, after, actor, ct);
    }

    private sealed class FixedToolResolver(RepairToolIdentity piece) : IToolPieceResolver
    {
        public Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(RepairType type, string? reference, string? lot, string? number, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepairToolIdentity>>([piece]);
        public Task<RepairToolIdentity?> ResolveAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == piece.PhysicalPieceId ? piece : null);
    }

    private sealed class FixedCurrentUser(string actorId) : ICurrentUserAccessor
    {
        public CurrentUser? Current { get; } = new(
            Guid.NewGuid(), actorId, [ReparacaoExternaModuleCatalog.ModuleId], []);
    }

    private sealed class FixedAuthorship(string actorId) : IPersistenceAuthorshipAccessor
    {
        public PersistenceAuthorship Current { get; } = new(actorId, DateTimeOffset.UtcNow);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class UnusedArmazemPort : IArmazemRepairMovementPort
    {
        public Task<Result<bool, DomainError>> ConfirmPickupAsync(IDbUnitOfWork uow, Guid exitId, Guid lotId, string actor, DateTimeOffset at, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<bool, DomainError>> ConfirmReturnAsync(IDbUnitOfWork uow, Guid exitId, Guid lotId, string position, string actor, DateTimeOffset at, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
