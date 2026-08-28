using System.Data;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;
using Npgsql;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-12 — Ferramentas Dapper persistence (N04, GLM-FERR-08). Implements
/// IFerramentasRepository. Owns Ferramentas persistence only. The atomic
/// reference+lote creation (CreateReferenceWithFirstLoteAsync) and the
/// lot duplication with its copied verification-rule configuration +
/// audit event (CreateLoteWithRulesAtomicallyAsync) each run inside ONE
/// DapperUnitOfWork transaction (GLM-DATA-05; audit FA-03).
/// </summary>
public sealed class DapperFerramentasRepository : IFerramentasRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperFerramentasRepository(IDbConnectionFactory connectionFactory)
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

    // ---- References --------------------------------------------------------

    public async Task<Guid> CreateReferenceAsync(ToolReference reference, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tool_references
    (tool_reference_id, tool_type, ref_code, technical_name, owner_plant,
     created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ToolType, @RefCode, @TechnicalName, @OwnerPlant,
     @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, ToReferenceParams(reference), cancellationToken: ct);
            return reference.ToolReferenceId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<ToolReference?> GetReferenceByIdAsync(Guid referenceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_reference_id, tool_type, ref_code, technical_name, owner_plant,
       created_at_utc, created_by, updated_at_utc
FROM tool_references WHERE tool_reference_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = referenceId }, cancellationToken: ct);
            return row is null ? null : MapReference(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<ToolReference?> GetReferenceByTypeAndCodeAsync(FerramentasToolType type, string refCode, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_reference_id, tool_type, ref_code, technical_name, owner_plant,
       created_at_utc, created_by, updated_at_utc
FROM tool_references WHERE tool_type = @ToolType AND ref_code = @RefCode;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql,
                new { ToolType = FerramentasToolTypeCodec.ToStorage(type), RefCode = refCode }, cancellationToken: ct);
            return row is null ? null : MapReference(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateReferenceAsync(ToolReference reference, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tool_references SET
    technical_name = @TechnicalName,
    owner_plant = @OwnerPlant,
    updated_at_utc = @UpdatedAtUtc
WHERE tool_reference_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = reference.ToolReferenceId,
                TechnicalName = (object?)reference.TechnicalName ?? DBNull.Value,
                OwnerPlant = (object?)reference.OwnerPlant ?? DBNull.Value,
                UpdatedAtUtc = reference.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<ToolReference>> SearchReferencesAsync(
        string? reference, string? technicalName, string? lote, string? drawing,
        string? line, string? processo, string? ownerPlant, CancellationToken ct = default)
    {
        var sql = @"
SELECT r.tool_reference_id, r.tool_type, r.ref_code, r.technical_name, r.owner_plant,
       r.created_at_utc, r.created_by, r.updated_at_utc
FROM tool_references r
WHERE (@Reference IS NULL OR r.ref_code ILIKE '%'||@Reference||'%')
  AND (@TechnicalName IS NULL OR r.technical_name ILIKE '%'||@TechnicalName||'%')
  AND (@OwnerPlant IS NULL OR r.owner_plant ILIKE '%'||@OwnerPlant||'%')
  AND (@Lote IS NULL OR EXISTS (
        SELECT 1 FROM tool_lotes l WHERE l.tool_reference_id = r.tool_reference_id
        AND l.lote ILIKE '%'||@Lote||'%'))
  AND (@Drawing IS NULL OR EXISTS (
        SELECT 1 FROM tool_lotes l WHERE l.tool_reference_id = r.tool_reference_id
        AND l.drawing_code ILIKE '%'||@Drawing||'%'))
  AND (@Line IS NULL OR EXISTS (
        SELECT 1 FROM tool_lotes l WHERE l.tool_reference_id = r.tool_reference_id
        AND @Line = ANY(l.allowed_lines)))
  AND (@Processo IS NULL OR EXISTS (
        SELECT 1 FROM tool_lotes l WHERE l.tool_reference_id = r.tool_reference_id
        AND l.processo = @Processo))
ORDER BY r.tool_type, r.ref_code;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Reference = reference,
                TechnicalName = technicalName,
                Lote = lote,
                Drawing = drawing,
                Line = line,
                Processo = processo,
                OwnerPlant = ownerPlant
            }, cancellationToken: ct);
            return rows.Select<dynamic, ToolReference>(r => MapReference(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Lotes -------------------------------------------------------------

    public async Task<Guid> CreateLoteAsync(ToolLote lote, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tool_lotes
    (tool_lote_id, tool_reference_id, lote, qty, allowed_lines,
     drawing_code, drawing_revision, processo, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ReferenceId, @Lote, @Qty, @AllowedLines,
     @DrawingCode, @DrawingRevision, @Processo, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, ToLoteParams(lote), cancellationToken: ct);
            return lote.ToolLoteId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<ToolLote?> GetLoteByIdAsync(Guid loteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_lote_id, tool_reference_id, lote, qty, allowed_lines,
       drawing_code, drawing_revision, processo, created_at_utc, created_by, updated_at_utc
FROM tool_lotes WHERE tool_lote_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = loteId }, cancellationToken: ct);
            return row is null ? null : MapLote(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateLoteAsync(ToolLote lote, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tool_lotes SET
    qty = @Qty,
    allowed_lines = @AllowedLines,
    drawing_code = @DrawingCode,
    drawing_revision = @DrawingRevision,
    updated_at_utc = @UpdatedAtUtc
WHERE tool_lote_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = lote.ToolLoteId,
                Qty = (object?)lote.Qty ?? DBNull.Value,
                AllowedLines = lote.AllowedLines.ToArray(),
                DrawingCode = (object?)lote.DrawingCode ?? DBNull.Value,
                DrawingRevision = (object?)lote.DrawingRevision ?? DBNull.Value,
                UpdatedAtUtc = lote.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<ToolLote>> GetLotesByReferenceAsync(Guid referenceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_lote_id, tool_reference_id, lote, qty, allowed_lines,
       drawing_code, drawing_revision, processo, created_at_utc, created_by, updated_at_utc
FROM tool_lotes WHERE tool_reference_id = @ReferenceId ORDER BY lote;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ReferenceId = referenceId }, cancellationToken: ct);
            return rows.Select<dynamic, ToolLote>(r => MapLote(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<bool> LoteExistsInReferenceAsync(Guid referenceId, string lote, CancellationToken ct = default)
    {
        const string sql = @"
SELECT COUNT(*) FROM tool_lotes WHERE tool_reference_id = @ReferenceId AND lote = @Lote;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var count = await Db.ExecuteScalarAsync<long>(conn, sql, new { ReferenceId = referenceId, Lote = lote }, cancellationToken: ct);
            return count > 0;
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Pieces ------------------------------------------------------------

    public async Task<Guid> RegisterPieceAsync(PhysicalPiece piece, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO physical_pieces
    (physical_piece_id, tool_lote_id, sequence, number, status,
     created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @LoteId, @Sequence, @Number, @Status,
     @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            try
            {
                await Db.ExecuteAsync(conn, sql, new
                {
                    Id = piece.PhysicalPieceId,
                    LoteId = piece.ToolLoteId,
                    Sequence = piece.Sequence,
                    Number = piece.Number,
                    Status = ToolConditionCodec.ToStorage(piece.Condition),
                    CreatedAtUtc = piece.CreatedAtUtc,
                    CreatedBy = (object?)piece.CreatedBy ?? DBNull.Value,
                    UpdatedAtUtc = piece.UpdatedAtUtc
                }, cancellationToken: ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // uq_physical_pieces_lote_number — the same (lot, number) already
                // exists under concurrency (audit ON-02 / approved mapping).
                throw new PhysicalPieceDuplicateException(
                    $"O número {piece.Number} já está registado neste lote.");
            }
            return piece.PhysicalPieceId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdatePieceAsync(PhysicalPiece piece, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE physical_pieces SET
    status = @Status,
    updated_at_utc = @UpdatedAtUtc
WHERE physical_piece_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = piece.PhysicalPieceId,
                Status = ToolConditionCodec.ToStorage(piece.Condition),
                UpdatedAtUtc = piece.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PhysicalPiece>> GetPiecesByLoteAsync(Guid loteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT physical_piece_id, tool_lote_id, sequence, number, status,
       created_at_utc, created_by, updated_at_utc
FROM physical_pieces WHERE tool_lote_id = @LoteId ORDER BY sequence;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { LoteId = loteId }, cancellationToken: ct);
            return rows.Select<dynamic, PhysicalPiece>(r => MapPiece(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Check rules -------------------------------------------------------

    public async Task<Guid> AddCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tool_check_rules
    (tool_check_rule_id, tool_lote_id, rule_text, frequency, active,
     copied_from_rule_id, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @LoteId, @RuleText, @Frequency, @Active,
     @CopiedFrom, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, ToRuleParams(rule), cancellationToken: ct);
            return rule.ToolCheckRuleId;
        }
        finally { await DisposeAsync(conn); }
    }

    private static object ToRuleParams(ToolCheckRule rule) => new
    {
        Id = rule.ToolCheckRuleId,
        LoteId = rule.ToolLoteId,
        RuleText = rule.RuleText,
        Frequency = FerramentasCheckFrequencyCodec.ToStorage(rule.Frequency),
        Active = rule.Active,
        CopiedFrom = (object?)rule.CopiedFromRuleId ?? DBNull.Value,
        CreatedAtUtc = rule.CreatedAtUtc,
        CreatedBy = (object?)rule.CreatedBy ?? DBNull.Value,
        UpdatedAtUtc = rule.UpdatedAtUtc
    };

    public async Task UpdateCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tool_check_rules SET
    rule_text = @RuleText,
    frequency = @Frequency,
    updated_at_utc = @UpdatedAtUtc
WHERE tool_check_rule_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = rule.ToolCheckRuleId,
                RuleText = rule.RuleText,
                Frequency = FerramentasCheckFrequencyCodec.ToStorage(rule.Frequency),
                UpdatedAtUtc = rule.UpdatedAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task ToggleCheckRuleActiveAsync(Guid ruleId, bool active, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tool_check_rules SET active = @Active WHERE tool_check_rule_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new { Id = ruleId, Active = active }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task DeleteCheckRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE tool_check_rules SET active = FALSE WHERE tool_check_rule_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            // Soft-deactivate preserves historical occurrences (GLM-DATA-01: facts immutable).
            await Db.ExecuteAsync(conn, sql, new { Id = ruleId }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<ToolCheckRule>> GetCheckRulesByLoteAsync(Guid loteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_check_rule_id, tool_lote_id, rule_text, frequency, active,
       copied_from_rule_id, created_at_utc, created_by, updated_at_utc
FROM tool_check_rules WHERE tool_lote_id = @LoteId ORDER BY created_at_utc;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { LoteId = loteId }, cancellationToken: ct);
            return rows.Select<dynamic, ToolCheckRule>(r => MapCheckRule(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<ToolCheckRule?> GetCheckRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_check_rule_id, tool_lote_id, rule_text, frequency, active,
       copied_from_rule_id, created_at_utc, created_by, updated_at_utc
FROM tool_check_rules WHERE tool_check_rule_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = ruleId }, cancellationToken: ct);
            return row is null ? null : MapCheckRule(row);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Atomic multi-write ------------------------------------------------

    public async Task<(Guid ReferenceId, Guid LoteId)> CreateReferenceWithFirstLoteAsync(
        ToolReference reference, ToolLote lote, CancellationToken ct = default)
    {
        return await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, token) =>
        {
            const string insertReference = @"
INSERT INTO tool_references
    (tool_reference_id, tool_type, ref_code, technical_name, owner_plant,
     created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ToolType, @RefCode, @TechnicalName, @OwnerPlant,
     @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";

            const string insertLote = @"
INSERT INTO tool_lotes
    (tool_lote_id, tool_reference_id, lote, qty, allowed_lines,
     drawing_code, drawing_revision, processo, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ReferenceId, @Lote, @Qty, @AllowedLines,
     @DrawingCode, @DrawingRevision, @Processo, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";

            await Db.ExecuteAsync(conn, insertReference, ToReferenceParams(reference), transaction: tx, cancellationToken: token);
            await Db.ExecuteAsync(conn, insertLote, ToLoteParams(lote), transaction: tx, cancellationToken: token);

            return (reference.ToolReferenceId, lote.ToolLoteId);
        }, ct);
    }

    /// <summary>
    /// Duplicates a lot: the new lot header + the copied verification-rule
    /// configuration (never occurrences/checks/history — the rules carry the
    /// source rule id as copied_from_rule_id) + the "lote duplicar" audit
    /// event commit/roll back as ONE transaction (audit FA-03). No partially
    /// duplicated lot can remain on failure.
    /// </summary>
    public async Task<Guid> CreateLoteWithRulesAtomicallyAsync(
        ToolLote lote,
        IReadOnlyList<ToolCheckRule> copiedRules,
        Guid? sourceLoteId,
        string actorId,
        CancellationToken ct = default)
    {
        return await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, token) =>
        {
            const string insertLote = @"
INSERT INTO tool_lotes
    (tool_lote_id, tool_reference_id, lote, qty, allowed_lines,
     drawing_code, drawing_revision, processo, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @ReferenceId, @Lote, @Qty, @AllowedLines,
     @DrawingCode, @DrawingRevision, @Processo, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";

            const string insertRule = @"
INSERT INTO tool_check_rules
    (tool_check_rule_id, tool_lote_id, rule_text, frequency, active,
     copied_from_rule_id, created_at_utc, created_by, updated_at_utc)
VALUES
    (@Id, @LoteId, @RuleText, @Frequency, @Active,
     @CopiedFrom, @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";

            await Db.ExecuteAsync(conn, insertLote, ToLoteParams(lote), transaction: tx, cancellationToken: token);
            foreach (var rule in copiedRules ?? Array.Empty<ToolCheckRule>())
                await Db.ExecuteAsync(conn, insertRule, ToRuleParams(rule), transaction: tx, cancellationToken: token);

            const string insertAudit = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (now(), EXTRACT(YEAR FROM now()), @Actor, 'ferramentas', @Action,
        'ferramenta', @EntityId, 'succeeded', @Before, @After);";
            await Db.ExecuteAsync(conn, insertAudit, new
            {
                Actor = actorId,
                Action = "ferramentas.lote.duplicar",
                EntityId = sourceLoteId?.ToString(),
                Before = sourceLoteId?.ToString(),
                After = lote.ToolLoteId.ToString()
            }, transaction: tx, cancellationToken: token);

            return lote.ToolLoteId;
        }, ct);
    }

    // ---- Utilisation (R003, append-only per tool_lote) ----------------------

    public async Task RecordUtilisationReadingAsync(ToolUtilisationReading reading, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO tool_usage_records (tool_usage_record_id, tool_lote_id, sap_start, sap_end, percent_used,
                                value_added, value_cumulative, notes, actor_id, reading_at_utc)
VALUES (@Id, @ToolLoteId, @SapStart, @SapEnd, @PercentUsed, @ValueAdded, @ValueCumulative,
        @Notes, @ActorId, @ReadingAtUtc);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = reading.ToolUsageRecordId,
                ToolLoteId = reading.ToolLoteId,
                SapStart = (object?)reading.SapStart ?? DBNull.Value,
                SapEnd = (object?)reading.SapEnd ?? DBNull.Value,
                PercentUsed = (object?)reading.PercentUsed ?? DBNull.Value,
                ValueAdded = (object?)reading.ValueAdded ?? DBNull.Value,
                reading.ValueCumulative,
                Notes = (object?)reading.Notes ?? DBNull.Value,
                ActorId = (object?)reading.ActorId ?? DBNull.Value,
                reading.ReadingAtUtc
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<ToolUtilisationReading>> ListUtilisationReadingsAsync(Guid toolLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_usage_record_id, tool_lote_id, sap_start, sap_end, percent_used,
       value_added, value_cumulative, notes, actor_id, reading_at_utc
FROM tool_usage_records WHERE tool_lote_id = @ToolLoteId ORDER BY reading_at_utc, tool_usage_record_id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ToolLoteId = toolLoteId }, cancellationToken: ct);
            return rows.Select<dynamic, ToolUtilisationReading>(MapUtilisation).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Audit -------------------------------------------------------------

    public async Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (now(), EXTRACT(YEAR FROM now()), @Actor, 'ferramentas', @Action,
        'ferramenta', @EntityId, 'succeeded', @Before, @After);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Actor = actorId,
                Action = eventType,
                EntityId = entityId?.ToString(),
                Before = beforeSnapshot,
                After = afterSnapshot
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Mapping / parameter helpers ---------------------------------------

    private static object ToReferenceParams(ToolReference reference) => new
    {
        Id = reference.ToolReferenceId,
        ToolType = FerramentasToolTypeCodec.ToStorage(reference.ToolType),
        RefCode = reference.RefCode,
        TechnicalName = (object?)reference.TechnicalName ?? DBNull.Value,
        OwnerPlant = (object?)reference.OwnerPlant ?? DBNull.Value,
        CreatedAtUtc = reference.CreatedAtUtc,
        CreatedBy = (object?)reference.CreatedBy ?? DBNull.Value,
        UpdatedAtUtc = reference.UpdatedAtUtc
    };

    private static object ToLoteParams(ToolLote lote) => new
    {
        Id = lote.ToolLoteId,
        ReferenceId = lote.ToolReferenceId,
        Lote = lote.Lote,
        Qty = (object?)lote.Qty ?? DBNull.Value,
        AllowedLines = lote.AllowedLines.ToArray(),
        DrawingCode = (object?)lote.DrawingCode ?? DBNull.Value,
        DrawingRevision = (object?)lote.DrawingRevision ?? DBNull.Value,
        Processo = (object?)lote.Processo ?? DBNull.Value,
        CreatedAtUtc = lote.CreatedAtUtc,
        CreatedBy = (object?)lote.CreatedBy ?? DBNull.Value,
        UpdatedAtUtc = lote.UpdatedAtUtc
    };

    private static ToolReference MapReference(dynamic row) => new()
    {
        ToolReferenceId = row.tool_reference_id,
        ToolType = FerramentasToolTypeCodec.FromStorage(row.tool_type),
        RefCode = row.ref_code,
        TechnicalName = row.technical_name as string,
        OwnerPlant = row.owner_plant as string,
        CreatedAtUtc = row.created_at_utc,
        CreatedBy = row.created_by as string,
        UpdatedAtUtc = row.updated_at_utc
    };

    private static ToolLote MapLote(dynamic row) => new()
    {
        ToolLoteId = row.tool_lote_id,
        ToolReferenceId = row.tool_reference_id,
        Lote = row.lote,
        Qty = row.qty as int?,
        AllowedLines = ((row.allowed_lines as string[]) ?? Array.Empty<string>()).ToList().AsReadOnly(),
        DrawingCode = row.drawing_code as string,
        DrawingRevision = row.drawing_revision as string,
        Processo = row.processo as string,
        CreatedAtUtc = row.created_at_utc,
        CreatedBy = row.created_by as string,
        UpdatedAtUtc = row.updated_at_utc
    };

    private static PhysicalPiece MapPiece(dynamic row) => new()
    {
        PhysicalPieceId = row.physical_piece_id,
        ToolLoteId = row.tool_lote_id,
        Sequence = row.sequence,
        Number = row.number,
        Status = "operational",
        Condition = ToolConditionCodec.FromStorage(row.status),
        CreatedAtUtc = row.created_at_utc,
        CreatedBy = row.created_by as string,
        UpdatedAtUtc = row.updated_at_utc
    };

    private static ToolCheckRule MapCheckRule(dynamic row) => new()
    {
        ToolCheckRuleId = row.tool_check_rule_id,
        ToolLoteId = row.tool_lote_id,
        RuleText = row.rule_text,
        Frequency = FerramentasCheckFrequencyCodec.FromStorage(row.frequency),
        Active = row.active,
        CopiedFromRuleId = row.copied_from_rule_id as Guid?,
        CreatedAtUtc = row.created_at_utc,
        CreatedBy = row.created_by as string,
        UpdatedAtUtc = row.updated_at_utc
    };

    private static ToolUtilisationReading MapUtilisation(dynamic row) => new()
    {
        ToolUsageRecordId = row.tool_usage_record_id,
        ToolLoteId = row.tool_lote_id,
        SapStart = row.sap_start as decimal?,
        SapEnd = row.sap_end as decimal?,
        PercentUsed = row.percent_used as decimal?,
        ValueAdded = row.value_added as decimal?,
        ValueCumulative = row.value_cumulative,
        Notes = row.notes as string,
        ActorId = row.actor_id as string,
        ReadingAtUtc = (DateTimeOffset)row.reading_at_utc
    };
}