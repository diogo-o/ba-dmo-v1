using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-17 — Tampões Dapper persistence (N10 <c>tampao_*</c>; GLM-TP). Implements
/// <see cref="ITampaoRepository"/>. Single-row writes self-manage a connection; the
/// atomic multi-row writes (adicionar/remover, alterar estado, alterar configuração)
/// participate in the shared <see cref="IDbUnitOfWork"/> so all involved saldos +
/// the append-only movement + audit_events commit/roll back atomically (GLM-DATA-05).
/// Append-only triggers and RLS are respected.
/// </summary>
public sealed class DapperTampaoRepository : ITampaoRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperTampaoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ---- Fields & values ----------------------------------------------------

    public async Task<IReadOnlyList<TampaoFieldDef>> ListFieldDefsAsync(bool onlyActive, CancellationToken ct = default)
    {
        var sql = @"
SELECT tampao_field_def_id, field_name, unit, precision_digits, display_order, active,
       created_at_utc, updated_at_utc
FROM tampao_field_defs
WHERE (@OnlyActive = FALSE OR active = TRUE)
ORDER BY display_order, field_name;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { OnlyActive = onlyActive }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoFieldDef>(MapField).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<Guid> CreateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_field_defs
    (tampao_field_def_id, field_name, unit, precision_digits, display_order, active, created_at_utc, updated_at_utc)
VALUES (@Id, @FieldName, @Unit, @PrecisionDigits, @DisplayOrder, @Active, @CreatedAtUtc, @UpdatedAtUtc);";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                field.TampaoFieldDefId, field.FieldName, field.Unit, field.PrecisionDigits,
                field.DisplayOrder, field.Active, field.CreatedAtUtc, field.UpdatedAtUtc
            }, cancellationToken: ct);
            return field.TampaoFieldDefId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tampao_field_defs SET field_name = @FieldName, unit = @Unit, precision_digits = @PrecisionDigits,
       display_order = @DisplayOrder, active = @Active, updated_at_utc = @UpdatedAtUtc
WHERE tampao_field_def_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = field.TampaoFieldDefId, field.FieldName, field.Unit, field.PrecisionDigits,
                field.DisplayOrder, field.Active, field.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<TampaoFieldValue>> ListFieldValuesAsync(Guid fieldDefId, bool onlyActive, CancellationToken ct = default)
    {
        var sql = @"
SELECT tampao_field_value_id, tampao_field_def_id, value_numeric, value_label, display_order, active,
       created_at_utc, updated_at_utc
FROM tampao_field_values
WHERE tampao_field_def_id = @FieldDefId
  AND (@OnlyActive = FALSE OR active = TRUE)
ORDER BY value_numeric;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { FieldDefId = fieldDefId, OnlyActive = onlyActive }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoFieldValue>(MapValue).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<Guid> CreateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_field_values
    (tampao_field_value_id, tampao_field_def_id, value_numeric, value_label, display_order, active, created_at_utc, updated_at_utc)
VALUES (@Id, @FieldDefId, @ValueNumeric, @ValueLabel, @DisplayOrder, @Active, @CreatedAtUtc, @UpdatedAtUtc);";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                value.TampaoFieldValueId, value.TampaoFieldDefId, value.ValueNumeric, value.ValueLabel,
                value.DisplayOrder, value.Active, value.CreatedAtUtc, value.UpdatedAtUtc
            }, cancellationToken: ct);
            return value.TampaoFieldValueId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tampao_field_values SET value_label = @ValueLabel, display_order = @DisplayOrder,
       active = @Active, updated_at_utc = @UpdatedAtUtc
WHERE tampao_field_value_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = value.TampaoFieldValueId, value.ValueLabel, value.DisplayOrder, value.Active, value.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Configurations & saldos ---------------------------------------------

    public async Task<TampaoConfiguration?> FindConfigurationByKeyAsync(string valuesJson, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_configuration_id, values_json, active, created_at_utc, created_by
FROM tampao_configurations WHERE values_json = @ValuesJson;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { ValuesJson = valuesJson }, cancellationToken: ct);
            return row is null ? null : MapConfiguration(row, valuesJson);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<TampaoConfiguration?> GetConfigurationByIdAsync(Guid configurationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_configuration_id, values_json, active, created_at_utc, created_by
FROM tampao_configurations WHERE tampao_configuration_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = configurationId }, cancellationToken: ct);
            return row is null ? null : MapConfiguration(row, row.values_json as string);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsAsync(bool onlyActive, CancellationToken ct = default)
    {
        var sql = @"
SELECT tampao_configuration_id, values_json, active, created_at_utc, created_by
FROM tampao_configurations
WHERE (@OnlyActive = FALSE OR active = TRUE)
ORDER BY created_at_utc;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { OnlyActive = onlyActive }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoConfiguration>(r => MapConfiguration(r, r.values_json as string)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<TampaoSaldo?> GetSaldoByConfigurationAsync(Guid configurationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_saldo_id, tampao_configuration_id, enchidos, por_encher, updated_at_utc
FROM tampao_saldos WHERE tampao_configuration_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = configurationId }, cancellationToken: ct);
            return row is null ? null : MapSaldo(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<Guid> CreateConfigurationAsync(IDbUnitOfWork uow, TampaoConfiguration config, string valuesJson, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_configurations (tampao_configuration_id, values_json, active, created_at_utc, created_by)
VALUES (@Id, @ValuesJson, @Active, @CreatedAtUtc, @CreatedBy);";
        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = config.TampaoConfigurationId, ValuesJson = valuesJson, config.Active, config.CreatedAtUtc,
            CreatedBy = (object?)config.CreatedBy ?? DBNull.Value
        }, uow.Transaction, ct);
        return config.TampaoConfigurationId;
    }

    public async Task<TampaoSaldo?> GetSaldoInTransactionAsync(IDbUnitOfWork uow, Guid configurationId, CancellationToken ct = default)
    {
        // Take an exclusive row lock (SELECT ... FOR UPDATE) so the read→compute→absolute
        // rewrite in SetSaldoAsync is serialized against concurrent transformations on the
        // same configuration (GLM-TP balance atomicity / lost-update hardening A4). The lock
        // is held for the life of the shared transaction and released on commit/rollback.
        const string sql = @"
SELECT tampao_saldo_id, tampao_configuration_id, enchidos, por_encher, updated_at_utc
FROM tampao_saldos WHERE tampao_configuration_id = @Id FOR UPDATE;";
        dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(uow.Connection, sql, new { Id = configurationId }, uow.Transaction, cancellationToken: ct);
        return row is null ? null : MapSaldo(row);
    }

    public Task SetSaldoAsync(IDbUnitOfWork uow, Guid configurationId, int enchidos, int porEncher, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_saldos (tampao_saldo_id, tampao_configuration_id, enchidos, por_encher, updated_at_utc)
VALUES (gen_random_uuid(), @Id, @Enchidos, @PorEncher, now())
ON CONFLICT (tampao_configuration_id)
DO UPDATE SET enchidos = @Enchidos, por_encher = @PorEncher, updated_at_utc = now();";
        return Db.ExecuteAsync(uow.Connection, sql, new { Id = configurationId, Enchidos = enchidos, PorEncher = porEncher }, uow.Transaction, ct);
    }

    public Task InsertMovementAsync(IDbUnitOfWork uow, TampaoMovement movement, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_movements
    (tampao_movement_id, movement_type, origin_configuration_id, destination_configuration_id,
     qty, balances_before, balances_after, actor_id, occurred_at_utc)
VALUES
    (@Id, @MovementType, @Origin, @Destination, @Qty, @BalancesBefore, @BalancesAfter, @ActorId, @OccurredAtUtc);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = movement.TampaoMovementId,
            MovementType = TampaoMovementTypeCodec.ToStorage(movement.MovementType),
            Origin = (object?)movement.OriginConfigurationId ?? DBNull.Value,
            Destination = (object?)movement.DestinationConfigurationId ?? DBNull.Value,
            movement.Qty,
            BalancesBefore = (object?)movement.BalancesBefore ?? DBNull.Value,
            BalancesAfter = (object?)movement.BalancesAfter ?? DBNull.Value,
            ActorId = (object?)movement.ActorId ?? DBNull.Value,
            OccurredAtUtc = movement.OccurredAtUtc
        }, uow.Transaction, ct);
    }

    // ---- Movements / history ---------------------------------------------------

    public async Task<IReadOnlyList<TampaoMovement>> ListMovementsAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? configurationId, TampaoMovementType? type,
        string? operatorId, CancellationToken ct = default)
    {
        var sql = @"
SELECT tampao_movement_id, movement_type, origin_configuration_id, destination_configuration_id,
       qty, balances_before, balances_after, actor_id, occurred_at_utc
FROM tampao_movements
WHERE (@From IS NULL OR occurred_at_utc >= @From)
  AND (@To IS NULL OR occurred_at_utc <= @To)
  AND (@ConfigurationId IS NULL OR origin_configuration_id = @ConfigurationId OR destination_configuration_id = @ConfigurationId)
  AND (@Type IS NULL OR movement_type = @Type)
  AND (@OperatorId IS NULL OR actor_id = @OperatorId)
ORDER BY occurred_at_utc DESC;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                From = from, To = to, ConfigurationId = configurationId,
                Type = type is null ? null : TampaoMovementTypeCodec.ToStorage(type.Value),
                OperatorId = operatorId
            }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoMovement>(MapMovement).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Planning ---------------------------------------------------------------

    public async Task<Guid> CreatePlanoAsync(TampaoPlano plano, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tampao_planos
    (tampao_plano_id, tampao_configuration_id, planned_qty, planned_for_date, job_on_id,
     production_code, notes, canceled, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ConfigurationId, @PlannedQty, @PlannedForDate, @JobOnId, @ProductionCode,
     @Notes, @Canceled, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = plano.TampaoPlanoId, plano.TampaoConfigurationId, plano.PlannedQty, plano.PlannedForDate,
                JobOnId = (object?)plano.JobOnId ?? DBNull.Value, plano.ProductionCode,
                Notes = (object?)plano.Notes ?? DBNull.Value, plano.Canceled,
                plano.CreatedAtUtc, CreatedBy = (object?)plano.CreatedBy ?? DBNull.Value, plano.UpdatedAtUtc
            }, cancellationToken: ct);
            return plano.TampaoPlanoId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<TampaoPlano?> GetPlanoByIdAsync(Guid planoId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_plano_id, tampao_configuration_id, planned_qty, planned_for_date, job_on_id,
       production_code, notes, canceled, created_at_utc, created_by, updated_at_utc
FROM tampao_planos WHERE tampao_plano_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = planoId }, cancellationToken: ct);
            return row is null ? null : MapPlano(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public Task CancelPlanoAsync(IDbUnitOfWork uow, Guid planoId, CancellationToken ct = default)
    {
        const string sql = "UPDATE tampao_planos SET canceled = TRUE, updated_at_utc = now() WHERE tampao_plano_id = @Id;";
        return Db.ExecuteAsync(uow.Connection, sql, new { Id = planoId }, uow.Transaction, ct);
    }

    public async Task<IReadOnlyList<TampaoPlano>> ListPlanosAsync(
        bool includeCanceled, Guid? configurationId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var sql = @"
SELECT tampao_plano_id, tampao_configuration_id, planned_qty, planned_for_date, job_on_id,
       production_code, notes, canceled, created_at_utc, created_by, updated_at_utc
FROM tampao_planos
WHERE (@IncludeCanceled = TRUE OR canceled = FALSE)
  AND (@ConfigurationId IS NULL OR tampao_configuration_id = @ConfigurationId)
  AND (@From IS NULL OR planned_for_date >= @From)
  AND (@To IS NULL OR planned_for_date <= @To)
ORDER BY planned_for_date NULLS LAST, created_at_utc DESC;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                IncludeCanceled = includeCanceled, ConfigurationId = configurationId, From = from, To = to
            }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoPlano>(MapPlano).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Machines & notes (R008) ------------------------------------------------

    public async Task<IReadOnlySet<string>> GetMachinesByConfigurationAsync(Guid configurationId, CancellationToken ct = default)
    {
        const string sql = "SELECT machine FROM tampao_configuration_machines WHERE tampao_configuration_id = @Id ORDER BY machine;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<string>(conn, sql, new { Id = configurationId }, cancellationToken: ct);
            return rows.ToHashSet(StringComparer.Ordinal);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task ReplaceConfigurationMachinesAsync(IDbUnitOfWork uow, Guid configurationId, IEnumerable<string> machines, CancellationToken ct = default)
    {
        await Db.ExecuteAsync(uow.Connection,
            "DELETE FROM tampao_configuration_machines WHERE tampao_configuration_id = @Id;",
            new { Id = configurationId }, uow.Transaction, ct);
        foreach (var machine in machines.Distinct(StringComparer.Ordinal))
        {
            await Db.ExecuteAsync(uow.Connection, @"
INSERT INTO tampao_configuration_machines (tampao_configuration_id, machine) VALUES (@Id, @Machine);",
                new { Id = configurationId, Machine = machine }, uow.Transaction, ct);
        }
    }

    public Task InsertMachineEventAsync(IDbUnitOfWork uow, TampaoMachineEvent evt, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO tampao_configuration_machine_event
    (tampao_configuration_machine_event_id, tampao_configuration_id, machine, action, actor_id, occurred_at_utc)
VALUES (@Id, @ConfigurationId, @Machine, @Action, @ActorId, @OccurredAtUtc);", new
        {
            Id = evt.TampaoConfigurationMachineEventId, ConfigurationId = evt.TampaoConfigurationId,
            evt.Machine, evt.Action, ActorId = (object?)evt.ActorId ?? DBNull.Value, evt.OccurredAtUtc
        }, uow.Transaction, ct);

    public async Task<IReadOnlyList<TampaoMachineEvent>> ListMachineEventsAsync(Guid configurationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_configuration_machine_event_id, tampao_configuration_id, machine, action, actor_id, occurred_at_utc
FROM tampao_configuration_machine_event WHERE tampao_configuration_id = @Id ORDER BY occurred_at_utc, tampao_configuration_machine_event_id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { Id = configurationId }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoMachineEvent>(MapMachineEvent).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public Task AddConfigurationNoteAsync(IDbUnitOfWork uow, TampaoConfigurationNote note, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO tampao_configuration_notes
    (tampao_configuration_note_id, tampao_configuration_id, note, actor_id, occurred_at_utc)
VALUES (@Id, @ConfigurationId, @Note, @ActorId, @OccurredAtUtc);", new
        {
            Id = note.TampaoConfigurationNoteId, ConfigurationId = note.TampaoConfigurationId,
            note.Note, ActorId = (object?)note.ActorId ?? DBNull.Value, note.OccurredAtUtc
        }, uow.Transaction, ct);

    public async Task<IReadOnlyList<TampaoConfigurationNote>> ListConfigurationNotesAsync(Guid configurationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tampao_configuration_note_id, tampao_configuration_id, note, actor_id, occurred_at_utc
FROM tampao_configuration_notes WHERE tampao_configuration_id = @Id ORDER BY occurred_at_utc, tampao_configuration_note_id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { Id = configurationId }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoConfigurationNote>(MapNote).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsByMachineAsync(string machine, CancellationToken ct = default)
    {
        var sql = @"
SELECT c.tampao_configuration_id, c.values_json, c.active, c.created_at_utc, c.created_by
FROM tampao_configurations c
JOIN tampao_configuration_machines m ON m.tampao_configuration_id = c.tampao_configuration_id
WHERE m.machine = @Machine AND c.active = TRUE
ORDER BY c.values_json;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { Machine = machine }, cancellationToken: ct);
            return rows.Select<dynamic, TampaoConfiguration>(r => MapConfiguration(r, r.values_json as string)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Audit -------------------------------------------------------------------

    public Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (@OccurredAtUtc, EXTRACT(YEAR FROM @OccurredAtUtc), @Actor, 'tampoes', @Action,
        @EntityType, @EntityId, @Result, @Before, @After);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            OccurredAtUtc = occurredAtUtc, Actor = actorId, Action = actionCode,
            EntityType = entityType, EntityId = entityId, Result = result,
            Before = (object?)beforeSummary ?? DBNull.Value, After = (object?)afterSummary ?? DBNull.Value
        }, uow.Transaction, ct);
    }

    // ---- Mapping / helpers ---------------------------------------------------------

    private static TampaoFieldDef MapField(dynamic row) => new()
    {
        TampaoFieldDefId = row.tampao_field_def_id,
        FieldName = (string)row.field_name,
        Unit = row.unit as string,
        PrecisionDigits = row.precision_digits as int?,
        DisplayOrder = row.display_order,
        Active = row.active,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static TampaoFieldValue MapValue(dynamic row) => new()
    {
        TampaoFieldValueId = row.tampao_field_value_id,
        TampaoFieldDefId = row.tampao_field_def_id,
        ValueNumeric = (decimal)row.value_numeric,
        ValueLabel = (string)row.value_label,
        DisplayOrder = row.display_order,
        Active = row.active,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static TampaoConfiguration MapConfiguration(dynamic row, string? valuesJson) => new()
    {
        TampaoConfigurationId = row.tampao_configuration_id,
        Values = ParseValues(valuesJson),
        Active = row.active,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        CreatedBy = row.created_by as string
    };

    private static TampaoSaldo MapSaldo(dynamic row) => new()
    {
        TampaoSaldoId = row.tampao_saldo_id,
        TampaoConfigurationId = row.tampao_configuration_id,
        Enchidos = row.enchidos,
        PorEncher = row.por_encher,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static TampaoMovement MapMovement(dynamic row) => new()
    {
        TampaoMovementId = row.tampao_movement_id,
        MovementType = TampaoMovementTypeCodec.FromStorage(row.movement_type as string),
        OriginConfigurationId = row.origin_configuration_id as Guid?,
        DestinationConfigurationId = row.destination_configuration_id as Guid?,
        Qty = row.qty,
        BalancesBefore = row.balances_before as string,
        BalancesAfter = row.balances_after as string,
        ActorId = row.actor_id as string,
        OccurredAtUtc = (DateTimeOffset)row.occurred_at_utc
    };

    private static TampaoPlano MapPlano(dynamic row) => new()
    {
        TampaoPlanoId = row.tampao_plano_id,
        TampaoConfigurationId = row.tampao_configuration_id,
        PlannedQty = row.planned_qty,
        PlannedForDate = ToDateOnly(row.planned_for_date),
        JobOnId = row.job_on_id as Guid?,
        ProductionCode = row.production_code as string,
        Notes = row.notes as string,
        Canceled = row.canceled,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        CreatedBy = row.created_by as string,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static IReadOnlyDictionary<string, decimal> ParseValues(string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(valuesJson))
            return new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(valuesJson);
            var dict = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var dec))
                    dict[prop.Name] = dec;
            }
            return dict;
        }
        catch (JsonException)
        {
            return new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        }
    }

    private static DateOnly? ToDateOnly(object? value) => value switch
    {
        null => null,
        DateOnly d => d,
        DateTime dt => DateOnly.FromDateTime(dt),
        _ => null
    };

    private static TampaoMachineEvent MapMachineEvent(dynamic row) => new()
    {
        TampaoConfigurationMachineEventId = row.tampao_configuration_machine_event_id,
        TampaoConfigurationId = row.tampao_configuration_id,
        Machine = row.machine,
        Action = row.action,
        ActorId = row.actor_id as string,
        OccurredAtUtc = (DateTimeOffset)row.occurred_at_utc
    };

    private static TampaoConfigurationNote MapNote(dynamic row) => new()
    {
        TampaoConfigurationNoteId = row.tampao_configuration_note_id,
        TampaoConfigurationId = row.tampao_configuration_id,
        Note = row.note,
        ActorId = row.actor_id as string,
        OccurredAtUtc = (DateTimeOffset)row.occurred_at_utc
    };

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}