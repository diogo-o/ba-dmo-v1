using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-15 — Reparação Externa Dapper persistence (N08, GLM-RE). Implements
/// IRepairRepository, owning Reparação persistence only. Single-table writes
/// self-manage a connection; create-exit and coordinated pickup/return writes can
/// participate in a shared <see cref="IDbUnitOfWork"/> so their complete use cases
/// commit or roll back atomically. Append-only triggers and RLS are respected;
/// repair_events is append-only.
/// </summary>
public sealed class DapperRepairRepository : IRepairRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRepairRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    private static async Task<IDbConnection> Open(IDbConnectionFactory factory, CancellationToken ct)
        => await factory.OpenConnectionAsync(ct);

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }

    // ---- Exit lists ----------------------------------------------------------

    public async Task<Guid> CreateExitAsync(
        RepairExit exit, RepairerSnapshot? repairerSnapshot, string? snapshotJson, CancellationToken ct = default)
    {
        var conn = await Open(_connectionFactory, ct);
        try { return await CreateExitCoreAsync(conn, null, exit, snapshotJson, ct); }
        finally { await DisposeAsync(conn); }
    }

    public Task<Guid> CreateExitAsync(
        IDbUnitOfWork uow, RepairExit exit, RepairerSnapshot? repairerSnapshot,
        string? snapshotJson, CancellationToken ct = default) =>
        CreateExitCoreAsync(uow.Connection, uow.Transaction, exit, snapshotJson, ct);

    private static async Task<Guid> CreateExitCoreAsync(
        IDbConnection connection, IDbTransaction? transaction, RepairExit exit,
        string? snapshotJson, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO repair_exits
    (repair_exit_id, repair_type, repairer_id, repairer_snapshot, planned_date,
     status, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @RepairType, @RepairerId, @Snapshot, @PlannedDate,
     @Status, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        await Db.ExecuteAsync(connection, sql, new
        {
            Id = exit.RepairExitId,
            RepairType = RepairTypeCodec.ToStorage(exit.RepairType),
            RepairerId = (object?)exit.RepairerId ?? DBNull.Value,
            Snapshot = (object?)snapshotJson ?? DBNull.Value,
            PlannedDate = (object?)exit.PlannedDate ?? DBNull.Value,
            Status = RepairExitStatusCodec.ToStorage(exit.Status),
            CreatedAtUtc = exit.CreatedAtUtc,
            CreatedBy = (object?)exit.CreatedBy ?? DBNull.Value,
            UpdatedAtUtc = exit.UpdatedAtUtc
        }, transaction, ct);
        return exit.RepairExitId;
    }

    public async Task<RepairExit?> GetExitByIdAsync(Guid repairExitId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT repair_exit_id, repair_type, repairer_id, repairer_snapshot, planned_date,
       status, created_at_utc, created_by, updated_at_utc
FROM repair_exits WHERE repair_exit_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = repairExitId }, cancellationToken: ct);
            return row is null ? null : MapExit(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<RepairExitItem>> GetExitItemsAsync(Guid repairExitId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT repair_exit_item_id, repair_exit_id, bq_lote_id, physical_piece_id, qty,
       individual_number, picked, out_at_utc, out_operator_id, in_at_utc, in_operator_id, status
FROM repair_exit_items WHERE repair_exit_id = @Id ORDER BY repair_exit_item_id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { Id = repairExitId }, cancellationToken: ct);
            return rows.Select<dynamic, RepairExitItem>(MapItem).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<RepairExit>> ListExitsAsync(
        RepairType? type, RepairExitStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var sql = @"
SELECT repair_exit_id, repair_type, repairer_id, repairer_snapshot, planned_date,
       status, created_at_utc, created_by, updated_at_utc
FROM repair_exits
WHERE (@Type IS NULL OR repair_type = @Type)
  AND (@Status IS NULL OR status = @Status)
  AND (@From IS NULL OR planned_date >= @From)
  AND (@To IS NULL OR planned_date <= @To)
ORDER BY created_at_utc DESC;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Type = type is null ? null : RepairTypeCodec.ToStorage(type.Value),
                Status = status is null ? null : RepairExitStatusCodec.ToStorage(status.Value),
                From = from,
                To = to
            }, cancellationToken: ct);
            return rows.Select<dynamic, RepairExit>(MapExit).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<bool> ExistsItemInOpenExitAsync(Guid physicalPieceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT EXISTS (
    SELECT 1
    FROM repair_exit_items i
    JOIN repair_exits e ON e.repair_exit_id = i.repair_exit_id
    WHERE i.physical_piece_id = @PieceId
      AND i.in_at_utc IS NULL
      AND e.status IN ('preparacao', 'a_retirar', 'enviado', 'retorno_parcial'));";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            return await Db.ExecuteScalarAsync<bool>(conn, sql, new { PieceId = physicalPieceId }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Exit items ----------------------------------------------------------

    public async Task<Guid> AddItemAsync(RepairExitItem item, CancellationToken ct = default)
    {
        var conn = await Open(_connectionFactory, ct);
        try { return await AddItemCoreAsync(conn, null, item, ct); }
        finally { await DisposeAsync(conn); }
    }

    public Task<Guid> AddItemAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) =>
        AddItemCoreAsync(uow.Connection, uow.Transaction, item, ct);

    private static async Task<Guid> AddItemCoreAsync(
        IDbConnection connection, IDbTransaction? transaction, RepairExitItem item, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO repair_exit_items
    (repair_exit_item_id, repair_exit_id, bq_lote_id, physical_piece_id, qty,
     individual_number, picked, out_at_utc, out_operator_id, in_at_utc, in_operator_id, status)
VALUES
    (@Id, @ExitId, @BqLoteId, @PhysicalPieceId, @Qty,
     @IndividualNumber, @Picked, @OutAtUtc, @OutOperatorId, @InAtUtc, @InOperatorId, @Status);";
        await Db.ExecuteAsync(connection, sql, ToItemParams(item), transaction, ct);
        return item.RepairExitItemId;
    }

    public async Task<RepairExitItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT repair_exit_item_id, repair_exit_id, bq_lote_id, physical_piece_id, qty,
       individual_number, picked, out_at_utc, out_operator_id, in_at_utc, in_operator_id, status
FROM repair_exit_items WHERE repair_exit_item_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = itemId }, cancellationToken: ct);
            return row is null ? null : MapItem(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task DeleteItemAsync(Guid itemId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM repair_exit_items WHERE repair_exit_item_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try { await Db.ExecuteAsync(conn, sql, new { Id = itemId }, cancellationToken: ct); }
        finally { await DisposeAsync(conn); }
    }

    // ---- Coordinated writes (shared unit of work) -----------------------------

    public Task ConfirmItemPickedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE repair_exit_items SET
    picked = @Picked,
    out_at_utc = @OutAtUtc,
    out_operator_id = @OutOperatorId,
    status = @Status
WHERE repair_exit_item_id = @Id;";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = item.RepairExitItemId,
            Picked = item.Picked,
            OutAtUtc = (object?)item.OutAtUtc ?? DBNull.Value,
            OutOperatorId = (object?)item.OutOperatorId ?? DBNull.Value,
            Status = item.Status
        }, uow.Transaction, ct);
    }

    public Task ConfirmItemReturnedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE repair_exit_items SET
    in_at_utc = @InAtUtc,
    in_operator_id = @InOperatorId,
    status = @Status
WHERE repair_exit_item_id = @Id;";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = item.RepairExitItemId,
            InAtUtc = (object?)item.InAtUtc ?? DBNull.Value,
            InOperatorId = (object?)item.InOperatorId ?? DBNull.Value,
            Status = item.Status
        }, uow.Transaction, ct);
    }

    public Task UpdateExitStatusAsync(IDbUnitOfWork uow, Guid repairExitId, string statusStorage, CancellationToken ct = default)
    {
        const string sql = "UPDATE repair_exits SET status = @Status, updated_at_utc = now() WHERE repair_exit_id = @Id;";
        return Db.ExecuteAsync(uow.Connection, sql, new { Id = repairExitId, Status = statusStorage }, uow.Transaction, ct);
    }

    public Task InsertRepairEventAsync(
        IDbUnitOfWork uow, Guid repairExitItemId, string? notes, string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO repair_events (repair_scope, repair_exit_item_id, canceled, notes, actor_id, occurred_at_utc)
VALUES ('externa', @ExitItemId, FALSE, @Notes, @ActorId, @OccurredAtUtc);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            ExitItemId = repairExitItemId,
            Notes = (object?)notes ?? DBNull.Value,
            ActorId = (object?)actorId ?? DBNull.Value,
            OccurredAtUtc = occurredAtUtc
        }, uow.Transaction, ct);
    }

    // ---- Repairers / line defaults --------------------------------------------

    public async Task<Guid> CreateRepairerAsync(Repairer repairer, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO repairers (repairer_id, name, active, created_at_utc, updated_at_utc)
VALUES (@Id, @Name, @Active, @CreatedAtUtc, @UpdatedAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = repairer.RepairerId,
                Name = repairer.Name,
                Active = repairer.Active,
                CreatedAtUtc = repairer.CreatedAtUtc,
                UpdatedAtUtc = repairer.UpdatedAtUtc
            }, cancellationToken: ct);
            return repairer.RepairerId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateRepairerAsync(Repairer repairer, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE repairers SET name = @Name, updated_at_utc = @UpdatedAtUtc WHERE repairer_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new { Id = repairer.RepairerId, Name = repairer.Name, UpdatedAtUtc = repairer.UpdatedAtUtc }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task DeactivateRepairerAsync(Guid repairerId, CancellationToken ct = default)
    {
        const string sql = "UPDATE repairers SET active = FALSE, updated_at_utc = now() WHERE repairer_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try { await Db.ExecuteAsync(conn, sql, new { Id = repairerId }, cancellationToken: ct); }
        finally { await DisposeAsync(conn); }
    }

    public async Task<Repairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT repairer_id, name, active, created_at_utc, updated_at_utc
FROM repairers WHERE repairer_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = repairerId }, cancellationToken: ct);
            return row is null ? null : MapRepairer(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<Repairer>> ListRepairersAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT repairer_id, name, active, created_at_utc, updated_at_utc
FROM repairers ORDER BY name;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, cancellationToken: ct);
            return rows.Select<dynamic, Repairer>(MapRepairer).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpsertLineDefaultAsync(LineRepairerDefault lineDefault, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO line_repairer_defaults (line, tool_type, repairer_id, updated_at_utc, updated_by)
VALUES (@Line, @ToolType, @RepairerId, @UpdatedAtUtc, @UpdatedBy)
ON CONFLICT (line, tool_type)
DO UPDATE SET repairer_id = @RepairerId, updated_at_utc = @UpdatedAtUtc, updated_by = @UpdatedBy;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Line = lineDefault.Line,
                ToolType = lineDefault.ToolType,
                RepairerId = lineDefault.RepairerId,
                UpdatedAtUtc = lineDefault.UpdatedAtUtc,
                UpdatedBy = (object?)lineDefault.UpdatedBy ?? DBNull.Value
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<LineRepairerDefault>> ListLineDefaultsAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT line, tool_type, repairer_id, updated_at_utc, updated_by
FROM line_repairer_defaults ORDER BY line, tool_type;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, cancellationToken: ct);
            return rows.Select<dynamic, LineRepairerDefault>(MapLineDefault).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task SetRepairerRepairTypesAsync(Guid repairerId, IEnumerable<string> repairTypes, CancellationToken ct = default)
    {
        var types = repairTypes.Distinct(StringComparer.Ordinal).ToArray();
        await DapperUnitOfWork.RunAsync<int>(_connectionFactory, async (connection, transaction, token) =>
        {
            await Db.ExecuteAsync(connection,
                "DELETE FROM repairer_repair_types WHERE repairer_id = @RepairerId;",
                new { RepairerId = repairerId }, transaction, token);
            foreach (var type in types)
            {
                await Db.ExecuteAsync(connection, @"
INSERT INTO repairer_repair_types (repairer_id, repair_type) VALUES (@RepairerId, @Type);",
                    new { RepairerId = repairerId, Type = type }, transaction, token);
            }
            return 0;
        }, ct);
    }

    public async Task<IReadOnlySet<string>> ListRepairerRepairTypesAsync(Guid repairerId, CancellationToken ct = default)
    {
        const string sql = "SELECT repair_type FROM repairer_repair_types WHERE repairer_id = @RepairerId;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<string>(conn, sql, new { RepairerId = repairerId }, cancellationToken: ct);
            return rows.ToHashSet(StringComparer.Ordinal);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Audit --------------------------------------------------------------

    public async Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct = default)
    {
        var conn = await Open(_connectionFactory, ct);
        try { await InsertAuditEventCoreAsync(conn, null, entityId, eventType, beforeSnapshot, afterSnapshot, actorId, ct); }
        finally { await DisposeAsync(conn); }
    }

    public Task InsertAuditEventAsync(
        IDbUnitOfWork uow, Guid? entityId, string eventType, string? beforeSnapshot,
        string? afterSnapshot, string actorId, CancellationToken ct = default) =>
        InsertAuditEventCoreAsync(
            uow.Connection, uow.Transaction, entityId, eventType,
            beforeSnapshot, afterSnapshot, actorId, ct);

    private static Task InsertAuditEventCoreAsync(
        IDbConnection connection, IDbTransaction? transaction, Guid? entityId,
        string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (now(), EXTRACT(YEAR FROM now()), @Actor, 'reparacao_externa', @Action,
        'reparacao_externa', @EntityId, 'succeeded', @Before::jsonb, @After::jsonb);";
        return Db.ExecuteAsync(connection, sql, new
        {
            Actor = actorId,
            Action = eventType,
            EntityId = entityId?.ToString(),
            Before = AuditJson.Normalize(beforeSnapshot),
            After = AuditJson.Normalize(afterSnapshot)
        }, transaction, ct);
    }

    // ---- Mapping / parameter helpers -----------------------------------------

    private static object ToItemParams(RepairExitItem item) => new
    {
        Id = item.RepairExitItemId,
        ExitId = item.RepairExitId,
        BqLoteId = (object?)item.BqLoteId ?? DBNull.Value,
        PhysicalPieceId = (object?)item.PhysicalPieceId ?? DBNull.Value,
        Qty = (object?)item.Qty ?? DBNull.Value,
        IndividualNumber = (object?)item.IndividualNumber ?? DBNull.Value,
        Picked = item.Picked,
        OutAtUtc = (object?)item.OutAtUtc ?? DBNull.Value,
        OutOperatorId = (object?)item.OutOperatorId ?? DBNull.Value,
        InAtUtc = (object?)item.InAtUtc ?? DBNull.Value,
        InOperatorId = (object?)item.InOperatorId ?? DBNull.Value,
        Status = item.Status
    };

    private static RepairExit MapExit(dynamic row)
    {
        string? snapshotJson = row.repairer_snapshot as string;
        RepairerSnapshot? snapshot = null;
        if (!string.IsNullOrWhiteSpace(snapshotJson))
        {
            try { snapshot = JsonSerializer.Deserialize<RepairerSnapshot>(snapshotJson); }
            catch { snapshot = null; }
        }
        return new RepairExit
        {
            RepairExitId = row.repair_exit_id,
            RepairType = RepairTypeCodec.FromStorage(row.repair_type),
            RepairerId = row.repairer_id as Guid?,
            RepairerSnapshot = snapshot,
            PlannedDate = ToDateOnly(row.planned_date),
            Status = RepairExitStatusCodec.FromStorage(row.status),
            CreatedAtUtc = row.created_at_utc,
            CreatedBy = row.created_by as string,
            UpdatedAtUtc = row.updated_at_utc
        };
    }

    private static RepairExitItem MapItem(dynamic row) => new()
    {
        RepairExitItemId = row.repair_exit_item_id,
        RepairExitId = row.repair_exit_id,
        BqLoteId = row.bq_lote_id as Guid?,
        PhysicalPieceId = row.physical_piece_id as Guid?,
        Qty = row.qty as decimal?,
        IndividualNumber = row.individual_number as string,
        Picked = row.picked,
        OutAtUtc = row.out_at_utc as DateTimeOffset?,
        OutOperatorId = row.out_operator_id as string,
        InAtUtc = row.in_at_utc as DateTimeOffset?,
        InOperatorId = row.in_operator_id as string,
        Status = row.status as string ?? "pendente"
    };

    private static Repairer MapRepairer(dynamic row) => new()
    {
        RepairerId = row.repairer_id,
        Name = row.name,
        Active = row.active,
        CreatedAtUtc = row.created_at_utc,
        UpdatedAtUtc = row.updated_at_utc
    };

    private static LineRepairerDefault MapLineDefault(dynamic row) => new()
    {
        Line = row.line,
        ToolType = row.tool_type,
        RepairerId = row.repairer_id,
        UpdatedAtUtc = row.updated_at_utc,
        UpdatedBy = row.updated_by as string
    };

    private static DateOnly? ToDateOnly(object? value) => value switch
    {
        null => null,
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => null
    };
}
