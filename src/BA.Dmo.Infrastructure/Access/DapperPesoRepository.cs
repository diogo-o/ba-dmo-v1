using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-10 — Peso Dapper persistence (N06, GLM-PESO-08). Implements IPesoRepository.
/// Every control/comparison stores job_on_id + job_on_revision_id (TD-18);
/// peso_controlos.previous_control provides the immutable previous-approved baseline
/// (TD-13/TD-30). Multi-table writes (control + readings + audit; day approval)
/// run inside one DapperUnitOfWork transaction (GLM-DATA-05).
/// </summary>
public sealed class DapperPesoRepository : IPesoRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPesoRepository(IDbConnectionFactory connectionFactory)
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

    // ---- References -------------------------------------------------------

    public async Task<Guid> CreateReferenceAsync(PesoReference r, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO peso_references
    (peso_reference_id, mold_number, neckring_number, counter_mold, capacity,
     volume_neck, volume_pu, calote_tp, change_log)
VALUES
    (@Id, @MoldNumber, @NeckringNumber, @CounterMold, @Capacity,
     @VolumeNeck, @VolumePu, @CaloteTp, @ChangeLog);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = r.PesoReferenceId,
                r.MoldNumber,
                r.NeckringNumber,
                CounterMold = (object?)r.CounterMold ?? DBNull.Value,
                Capacity = (object?)r.Capacity ?? DBNull.Value,
                VolumeNeck = (object?)r.VolumeNeck ?? DBNull.Value,
                VolumePu = (object?)r.VolumePu ?? DBNull.Value,
                CaloteTp = (object?)r.CaloteTp ?? DBNull.Value,
                ChangeLog = r.ChangeLogJson ?? "[]"
            }, cancellationToken: ct);
            return r.PesoReferenceId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<PesoReference?> GetReferenceByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT peso_reference_id, mold_number, neckring_number, counter_mold, capacity,
       volume_neck, volume_pu, calote_tp, change_log
FROM peso_references WHERE peso_reference_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = id }, cancellationToken: ct);
            return row is null ? null : MapReference(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PesoReference>> GetReferencesAsync(string? search, CancellationToken ct = default)
    {
        var sql = @"
SELECT peso_reference_id, mold_number, neckring_number, counter_mold, capacity,
       volume_neck, volume_pu, calote_tp, change_log
FROM peso_references
WHERE @Search IS NULL
   OR mold_number ILIKE '%'||@Search||'%'
   OR neckring_number ILIKE '%'||@Search||'%'
ORDER BY mold_number;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { Search = search }, cancellationToken: ct);
            return rows.Select(MapReference).ToList();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<PesoReference?> GetReferenceByMoldNeckringAsync(string mold, string neckring, CancellationToken ct = default)
    {
        const string sql = @"
SELECT peso_reference_id, mold_number, neckring_number, counter_mold, capacity,
       volume_neck, volume_pu, calote_tp, change_log
FROM peso_references WHERE mold_number = @Mold AND neckring_number = @Neck;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Mold = mold, Neck = neckring }, cancellationToken: ct);
            return row is null ? null : MapReference(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateReferenceAsync(PesoReference r, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE peso_references
SET counter_mold = @CounterMold, capacity = @Capacity, volume_neck = @VolumeNeck,
    volume_pu = @VolumePu, calote_tp = @CaloteTp, change_log = @ChangeLog,
    updated_at_utc = now()
WHERE peso_reference_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = r.PesoReferenceId,
                CounterMold = (object?)r.CounterMold ?? DBNull.Value,
                Capacity = (object?)r.Capacity ?? DBNull.Value,
                VolumeNeck = (object?)r.VolumeNeck ?? DBNull.Value,
                VolumePu = (object?)r.VolumePu ?? DBNull.Value,
                CaloteTp = (object?)r.CaloteTp ?? DBNull.Value,
                ChangeLog = r.ChangeLogJson ?? "[]"
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Lots -------------------------------------------------------------

    public async Task<Guid> CreateLoteAsync(PesoLote lote, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO peso_lotes
    (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder, nominal_weight)
VALUES
    (@Id, @ReferenceId, @Lote, @Processo, @AllowedLines, @ReportSubfolder, @NominalWeight);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Id = lote.PesoLoteId,
                ReferenceId = lote.PesoReferenceId,
                lote = lote.Lote,
                Processo = PesoProcessoCodec.ToStorage(lote.Processo),
                AllowedLines = lote.AllowedLines.ToArray(),
                ReportSubfolder = lote.ReportSubfolder,
                NominalWeight = (object?)lote.NominalWeight ?? DBNull.Value
            }, cancellationToken: ct);
            return lote.PesoLoteId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<PesoLote?> GetLoteByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder, nominal_weight
FROM peso_lotes WHERE peso_lote_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = id }, cancellationToken: ct);
            return row is null ? null : MapLote(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PesoLote>> GetLotesAsync(Guid referenceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder, nominal_weight
FROM peso_lotes WHERE peso_reference_id = @ReferenceId ORDER BY lote;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ReferenceId = referenceId }, cancellationToken: ct);
            return rows.Select(MapLote).ToList();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Controls -----------------------------------------------------------

    public async Task<Guid> CreateControlAsync(PesoControl control, CancellationToken ct = default)
    {
        return await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, token) =>
        {
            const string insertControl = @"
INSERT INTO peso_controlos
    (peso_controlo_id, peso_reference_id, peso_lote_id, record_type, mold_number,
     neckring_number, production_code, line, lote, control_date, job_on_id,
     job_on_revision_id, cm_snapshot, status, measurements_snapshot, approval_log,
     previous_control, comparison_decisions, created_by, created_at_utc)
VALUES
    (@Id, @ReferenceId, @LoteId, @RecordType, @MoldNumber,
     @NeckringNumber, @ProductionCode, @Line, @Lote, @ControlDate, @JobOnId,
     @JobOnRevisionId, @CmSnapshot, @Status, @Measurements, @ApprovalLog,
     @PreviousControl, @ComparisonDecisions, @CreatedBy, @CreatedAtUtc);";
            await Db.ExecuteAsync(conn, insertControl, new
            {
                Id = control.PesoControloId,
                ReferenceId = control.PesoReferenceId,
                LoteId = control.PesoLoteId,
                RecordType = PesoRecordTypeCodec.ToStorage(control.RecordType),
                control.MoldNumber,
                control.NeckringNumber,
                control.ProductionCode,
                control.Line,
                control.Lote,
                ControlDate = control.ControlDate,
                control.JobOnId,
                control.JobOnRevisionId,
                CmSnapshot = (object?)control.CmSnapshotJson ?? DBNull.Value,
                Status = PesoControlStateCodec.ToStorage(control.Status),
                Measurements = BuildMeasurementsSnapshot(control),
                ApprovalLog = control.ApprovalLogJson ?? "[]",
                PreviousControl = (object?)control.PreviousControlJson ?? DBNull.Value,
                ComparisonDecisions = (object?)control.ComparisonDecisionsJson ?? DBNull.Value,
                CreatedBy = control.CreatedBy,
                CreatedAtUtc = control.CreatedAtUtc.UtcDateTime
            }, tx, token);

            var leituraId = control.PesoControloId;
            foreach (var leitura in control.Leituras ?? Array.Empty<PesoLeitura>())
            {
                const string insertLeitura = @"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings, created_by)
VALUES (@Id, @ControlId, @CmNumber, @Readings, @CreatedBy);";
                await Db.ExecuteAsync(conn, insertLeitura, new
                {
                    Id = leitura.PesoLeituraId == Guid.Empty ? Guid.NewGuid() : leitura.PesoLeituraId,
                    ControlId = control.PesoControloId,
                    leitura.CmNumber,
                    Readings = JsonSerializer.Serialize(new { PesoEmAgua = leitura.PesoEmAgua, PesoVidro = leitura.PesoVidro }, JsonOptions),
                    CreatedBy = control.CreatedBy
                }, tx, token);
                _ = leituraId;
            }
            return control.PesoControloId;
        }, ct);
    }

    public async Task<PesoControl?> GetControlByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT c.*, ref.mold_number AS m_mold, ref.neckring_number AS m_neck
FROM peso_controlos c
LEFT JOIN peso_references ref ON ref.peso_reference_id = c.peso_reference_id
WHERE c.peso_controlo_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = id }, cancellationToken: ct);
            if (row is null) return null;
            var control = MapControl(row);
            var leituras = await Db.QueryAsync<dynamic>(conn,
                @"
SELECT peso_leitura_id, peso_controlo_id, cm_number, readings
FROM peso_leituras WHERE peso_controlo_id = @ControlId ORDER BY cm_number;",
                new { ControlId = id }, cancellationToken: ct);
            control.Leituras = leituras.Select(MapLeitura).ToList();
            return control;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PesoControl>> GetControlsAsync(
        Guid? referenceId, string? search, string? status, PesoRecordType? type,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var sql = @"
SELECT c.*, ref.mold_number AS m_mold, ref.neckring_number AS m_neck
FROM peso_controlos c
LEFT JOIN peso_references ref ON ref.peso_reference_id = c.peso_reference_id
WHERE (@ReferenceId IS NULL OR c.peso_reference_id = @ReferenceId)
  AND (@Status IS NULL OR c.status = @Status)
  AND (@Type IS NULL OR c.record_type = @Type)
  AND (@From IS NULL OR c.control_date >= @From)
  AND (@To IS NULL OR c.control_date <= @To)
  AND (@Search IS NULL
       OR c.mold_number ILIKE '%'||@Search||'%'
       OR c.production_code ILIKE '%'||@Search||'%'
       OR c.line ILIKE '%'||@Search||'%'
       OR c.lote ILIKE '%'||@Search||'%')
ORDER BY c.control_date DESC;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                ReferenceId = referenceId,
                Status = status,
                Type = type.HasValue ? PesoRecordTypeCodec.ToStorage(type.Value) : (string?)null,
                From = from,
                To = to,
                Search = search
            }, cancellationToken: ct);
            var result = new List<PesoControl>();
            foreach (var row in rows)
            {
                var control = MapControl(row);
                control.Leituras = await GetLeiturasAsync(conn, control.PesoControloId, ct);
                result.Add(control);
            }
            return result;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateControlAsync(PesoControl control, CancellationToken ct = default)
    {
        await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, token) =>
        {
            await UpdateControlHeaderAsync(conn, tx, control, token);

            const string deleteLeituras = "DELETE FROM peso_leituras WHERE peso_controlo_id = @ControlId;";
            await Db.ExecuteAsync(conn, deleteLeituras, new { ControlId = control.PesoControloId }, tx, token);

            foreach (var leitura in control.Leituras ?? Array.Empty<PesoLeitura>())
            {
                const string insertLeitura = @"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings, created_by)
VALUES (@Id, @ControlId, @CmNumber, @Readings, @CreatedBy);";
                await Db.ExecuteAsync(conn, insertLeitura, new
                {
                    Id = leitura.PesoLeituraId == Guid.Empty ? Guid.NewGuid() : leitura.PesoLeituraId,
                    ControlId = control.PesoControloId,
                    leitura.CmNumber,
                    Readings = JsonSerializer.Serialize(new { PesoEmAgua = leitura.PesoEmAgua, PesoVidro = leitura.PesoVidro }, JsonOptions),
                    CreatedBy = control.CreatedBy
                }, tx, token);
            }
            return 1;
        }, ct);
    }

    /// <summary>
    /// N40 pairing: header-only update. Updates the control row and NEVER
    /// touches peso_leituras. Used by the workflow transitions
    /// (submit/approve/reject/reopen/decide) which carry no new measurement
    /// data — keeping approved readings structurally immutable at the write
    /// path level, with the N40 trigger as the DB backstop.
    /// </summary>
    public async Task UpdateControlHeaderAsync(PesoControl control, CancellationToken ct = default)
    {
        await DapperUnitOfWork.RunAsync(_connectionFactory, (conn, tx, token) =>
            UpdateControlHeaderAsync(conn, tx, control, token), ct);
    }

    private static async Task<int> UpdateControlHeaderAsync(
        IDbConnection conn, IDbTransaction tx, PesoControl control, CancellationToken token)
    {
        const string update = @"
UPDATE peso_controlos
SET record_type = @RecordType, mold_number = @MoldNumber, neckring_number = @NeckringNumber,
    production_code = @ProductionCode, line = @Line, lote = @Lote, control_date = @ControlDate,
    status = @Status, measurements_snapshot = @Measurements, approval_log = @ApprovalLog,
    comparison_decisions = @ComparisonDecisions,
    approved_by = @ApprovedBy, approved_at_utc = @ApprovedAtUtc,
    updated_at_utc = now()
WHERE peso_controlo_id = @Id;";
        return await Db.ExecuteAsync(conn, update, new
        {
            Id = control.PesoControloId,
            RecordType = PesoRecordTypeCodec.ToStorage(control.RecordType),
            control.MoldNumber,
            control.NeckringNumber,
            control.ProductionCode,
            control.Line,
            control.Lote,
            ControlDate = control.ControlDate,
            Status = PesoControlStateCodec.ToStorage(control.Status),
            Measurements = BuildMeasurementsSnapshot(control),
            ApprovalLog = control.ApprovalLogJson ?? "[]",
            ComparisonDecisions = (object?)control.ComparisonDecisionsJson ?? DBNull.Value,
            ApprovedBy = (object?)control.ApprovedBy ?? DBNull.Value,
            ApprovedAtUtc = control.ApprovedAtUtc?.UtcDateTime
        }, tx, token);
    }

    public async Task DeleteControlAsync(Guid id, CancellationToken ct = default)
    {
        await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, token) =>
        {
            var affected = await Db.ExecuteAsync(conn,
                "DELETE FROM peso_controlos WHERE peso_controlo_id = @Id;", new { Id = id }, tx, token);
            _ = affected;
            return 1;
        }, ct);
    }

    // ---- Day approvals + record dates --------------------------------------

    public async Task SaveDayApprovalAsync(
        string mold, string neckring, string line, DateTime approvalDate,
        string approvedBy, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO peso_day_approvals (mold_number, neckring_number, line, approval_date, approved_by, approved_at_utc)
VALUES (@Mold, @Neck, @Line, @ApprovalDate, @ApprovedBy, now())
ON CONFLICT (mold_number, neckring_number, line, approval_date)
DO UPDATE SET approved_by = EXCLUDED.approved_by, approved_at_utc = now();";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Mold = mold, Neck = neckring, Line = line, ApprovalDate = approvalDate, ApprovedBy = approvedBy
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<string>> GetRecordDatesAsync(int year, int month, CancellationToken ct = default)
    {
        const string sql = @"
SELECT DISTINCT to_char(control_date, 'YYYY-MM-DD') AS day
FROM peso_controlos
WHERE EXTRACT(YEAR FROM control_date) = @Year AND EXTRACT(MONTH FROM control_date) = @Month
ORDER BY day;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<string>(conn, sql, new { Year = year, Month = month }, cancellationToken: ct);
            return rows;
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Settings -----------------------------------------------------------

    public async Task SaveSettingAsync(string key, string json, string updatedBy, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO peso_settings (setting_key, setting_value, updated_by, updated_at_utc)
VALUES (@Key, @Value, @UpdatedBy, now())
ON CONFLICT (setting_key) DO UPDATE SET setting_value = EXCLUDED.setting_value, updated_by = EXCLUDED.updated_by, updated_at_utc = now();";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new { Key = key, Value = json, UpdatedBy = updatedBy }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        const string sql = "SELECT setting_value::text FROM peso_settings WHERE setting_key = @Key;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<string>(conn, sql, new { Key = key }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Audit ---------------------------------------------------------------

    public async Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (now(), EXTRACT(YEAR FROM now()), @Actor, 'peso', @Action,
        'peso_controlo', @EntityId, 'succeeded', @Before, @After);";
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

    // ---- mapping helpers ------------------------------------------------------

    private static PesoReference MapReference(dynamic row) => new()
    {
        PesoReferenceId = (Guid)row.peso_reference_id,
        MoldNumber = (string)row.mold_number,
        NeckringNumber = (string)row.neckring_number,
        CounterMold = row.counter_mold as string,
        Capacity = ToDecimal(row.capacity),
        VolumeNeck = ToDecimal(row.volume_neck),
        VolumePu = ToDecimal(row.volume_pu),
        CaloteTp = ToDecimal(row.calote_tp),
        ChangeLogJson = row.change_log?.ToString() ?? "[]"
    };

    private static PesoLote MapLote(dynamic row) => new()
    {
        PesoLoteId = (Guid)row.peso_lote_id,
        PesoReferenceId = (Guid)row.peso_reference_id,
        Lote = (string)row.lote,
        Processo = PesoProcessoCodec.Parse((string)row.processo),
        AllowedLines = ((string[])row.allowed_lines).ToList(),
        ReportSubfolder = (string)row.report_subfolder,
        NominalWeight = ToDecimal(row.nominal_weight)
    };

    private static PesoControl MapControl(dynamic row) => new()
    {
        PesoControloId = (Guid)row.peso_controlo_id,
        PesoReferenceId = (Guid)row.peso_reference_id,
        PesoLoteId = (Guid)row.peso_lote_id,
        RecordType = PesoRecordTypeCodec.Parse((string)row.record_type),
        MoldNumber = string.IsNullOrEmpty((string)row.mold_number) ? (row.m_mold as string) ?? "" : (string)row.mold_number,
        NeckringNumber = string.IsNullOrEmpty((string)row.neckring_number) ? (row.m_neck as string) ?? "" : (string)row.neckring_number,
        ProductionCode = (string)row.production_code,
        Line = (string)row.line,
        Lote = (string)row.lote,
        ControlDate = (DateTime)row.control_date,
        JobOnId = (Guid)row.job_on_id,
        JobOnRevisionId = (Guid)row.job_on_revision_id,
        CmSnapshotJson = row.cm_snapshot?.ToString(),
        Status = PesoControlStateCodec.Parse((string)row.status),
        ApprovalLogJson = row.approval_log?.ToString() ?? "[]",
        PreviousControlJson = row.previous_control?.ToString(),
        ComparisonDecisionsJson = row.comparison_decisions?.ToString(),
        ApprovedBy = row.approved_by as string,
        ApprovedAtUtc = row.approved_at_utc is null ? null : ((DateTime)row.approved_at_utc).ToUniversalTime(),
        CreatedBy = row.created_by as string,
        CreatedAtUtc = ((DateTime)row.created_at_utc).ToUniversalTime()
    };

    private static PesoLeitura MapLeitura(dynamic row) => new()
    {
        PesoLeituraId = (Guid)row.peso_leitura_id,
        PesoControloId = (Guid)row.peso_controlo_id,
        CmNumber = (string)row.cm_number,
        PesoEmAgua = DeserializeReadings(row.readings)?.PesoEmAgua,
        PesoVidro = DeserializeReadings(row.readings)?.PesoVidro
    };

    /// <summary>
    /// Deserializes the readings JSON column to extract PesoEmAgua + PesoVidro.
    /// Returns null when the column is empty or malformed.
    /// </summary>
    private static (decimal? PesoEmAgua, decimal? PesoVidro)? DeserializeReadings(object? value)
    {
        if (value is null) return null;
        try
        {
            var el = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(value.ToString());
            if (el.TryGetProperty("pesoEmAgua", out var pea) && pea.TryGetDecimal(out var peaVal))
            {
                decimal? pv = null;
                if (el.TryGetProperty("pesoVidro", out var ppv) && ppv.TryGetDecimal(out var pvVal))
                    pv = pvVal;
                return (peaVal, pv);
            }
        }
        catch { /* malformed — best effort only */ }
        return null;
    }

    private static async Task<IReadOnlyList<PesoLeitura>> GetLeiturasAsync(IDbConnection conn, Guid controlId, CancellationToken ct)
    {
        var rows = await Db.QueryAsync<dynamic>(conn,
            @"
SELECT peso_leitura_id, peso_controlo_id, cm_number, readings
FROM peso_leituras WHERE peso_controlo_id = @ControlId ORDER BY cm_number;",
            new { ControlId = controlId }, cancellationToken: ct);
        return rows.Select(MapLeitura).ToList();
    }

    private static string BuildMeasurementsSnapshot(PesoControl c)
    {
        static decimal? den(decimal? t) => t is { } tt
            ? (WeightCalculator.LookupDensity(tt).IsSuccess
                ? WeightCalculator.LookupDensity(tt).Value
                : (decimal?)null)
            : null;
        var density = den(c.TemperaturaC);

        // Compute average glass weight from readings using the same domain algorithm.
        // This ensures PdfRenderer sees correct averages even when reading from storage.
        var processo = c.Processo ?? PesoProcesso.Nnpb;
        decimal constant = c.ConstanteGlassUsada
            ?? (processo == PesoProcesso.Nnpb ? PesoModuleCatalog.ConstantNnpb : PesoModuleCatalog.ConstantPs);
        decimal? computedPesoMedio = null;
        if ((c.Leituras?.Count ?? 0) > 0)
        {
            var glassWeights = new List<decimal?>();
            foreach (var r in c.Leituras!)
            {
                if (r.PesoEmAgua.HasValue)
                    glassWeights.Add(r.PesoEmAgua.Value * constant);
            }
            var valid = glassWeights.Where(w => w.HasValue).ToList();
            if (valid.Count > 0)
                computedPesoMedio = WeightCalculator.Round2(valid.Sum(w => w!.Value) / valid.Count);
        }

        return JsonSerializer.Serialize(new
        {
            c.TemperaturaC,
            c.EstadoMolde,
            c.FimProducaoAnteriorSap,
            c.PesoMedioAnteriorSap,
            c.Notas,
            c.DataRegistoComparacao,
            c.Processo,
            c.ConstanteGlassUsada,
            Densidade = density,
            PesoMedio = computedPesoMedio,
            CapacidadeMedia = c.CapacidadeMedia,
            PesoNominal = c.PesoNominal
        }, JsonOptions);
    }

    private static decimal? ToDecimal(object value) => value switch
    {
        null => null,
        DBNull => null,
        decimal d => d,
        _ => Convert.ToDecimal(value)
    };
}
