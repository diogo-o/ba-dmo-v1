using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-11 — Pegamento Dapper persistence (N07/N15, GLM-PEG-08).
/// Implements IPegamentoRepository. Owns Pegamentos persistence only — does NOT read Job On tables.
/// Write flows run inside the caller-provided <see cref="IDbUnitOfWork"/>
/// (from <see cref="IPegamentoUnitOfWorkFactory"/>) so each use case is ONE
/// transaction (audit PG-04). <c>CreateAsync</c> binds <c>updated_at_utc</c>
/// falling back to <c>created_at_utc</c>, mirroring <c>UpdateAsync</c> so an
/// explicit NULL never bypasses the NOT NULL DEFAULT (audit PC-01).
/// </summary>
public sealed class DapperPegamentoRepository : IPegamentoRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPegamentoRepository(IDbConnectionFactory connectionFactory)
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

    // ---- Status codec (N07: status text DEFAULT 'aberto') --------------------

    private static string ToDbStatus(PegamentoControloStatus status) => status switch
    {
        PegamentoControloStatus.Aberto => "aberto",
        PegamentoControloStatus.Fechado => "fechado",
        _ => throw new InvalidOperationException($"Unknown PegamentoControloStatus: {status}")
    };

    private static PegamentoControloStatus FromDbStatus(string? dbValue) => dbValue?.ToLowerInvariant() switch
    {
        "aberto" => PegamentoControloStatus.Aberto,
        "fechado" => PegamentoControloStatus.Fechado,
        _ => throw new InvalidOperationException($"Unknown persisted pegamento status: {dbValue}")
    };

    // ---- Controls -----------------------------------------------------------

    public async Task<Guid> CreateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO pegamento_controlos
    (pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
     production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
     cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
     created_at_utc, created_by, updated_at_utc)
VALUES
    (@ControloId, @JobOnId, @JobOnRevisionId, @ReferenceSnapshot,
     @ProductionCode, @MachineCode, @CmSnapshot, @BqSnapshot, @MfSnapshot,
     @CmNominal, @BqNominal, @MfNominal, @Tolerance, @Status, @Notas,
     @CreatedAtUtc, @CreatedBy, @UpdatedAtUtc);";

        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            ControloId = control.PegamentoControloId,
            JobOnId = control.JobOnId,
            JobOnRevisionId = control.JobOnRevisionId,
            ReferenceSnapshot = (object?)SerializeJson(control.ReferenceSnapshot) ?? DBNull.Value,
            ProductionCode = control.ProductionCode,
            MachineCode = control.MachineCode,
            CmSnapshot = (object?)SerializeToolSnapshot(control.CmSnapshot) ?? DBNull.Value,
            BqSnapshot = (object?)SerializeToolSnapshot(control.BqSnapshot) ?? DBNull.Value,
            MfSnapshot = (object?)SerializeToolSnapshot(control.MfSnapshot) ?? DBNull.Value,
            CmNominal = (object?)control.CmNominal ?? DBNull.Value,
            BqNominal = (object?)control.BqNominal ?? DBNull.Value,
            MfNominal = (object?)control.MfNominal ?? DBNull.Value,
            Tolerance = control.Tolerance,
            Status = ToDbStatus(control.Status),
            Notas = (object?)control.Notas ?? DBNull.Value,
            CreatedAtUtc = control.CreatedAtUtc,
            CreatedBy = (object?)control.CreatedBy ?? DBNull.Value,
            // PC-01: the domain factory never sets UpdatedAtUtc; binding an explicit
            // NULL would bypass the NOT NULL DEFAULT now() → 23502. Mirror the
            // UpdateAsync fallback (control.UpdatedAtUtc ?? control.CreatedAtUtc).
            UpdatedAtUtc = control.UpdatedAtUtc ?? control.CreatedAtUtc
        }, uow.Transaction, ct);
        return control.PegamentoControloId;
    }

    public async Task<PegamentoControlo?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
       production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
       cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
       created_at_utc, created_by, updated_at_utc
FROM pegamento_controlos WHERE pegamento_controlo_id = @Id;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = id }, cancellationToken: ct);
            if (row is null) return null;

            // Load measurements first
            var measurements = await GetMeasurementsAsync(id, ct);

            return HydrateControl(row, measurements);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<PegamentoControlo?> GetByIdInTransactionAsync(IDbUnitOfWork uow, Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
       production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
       cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
       created_at_utc, created_by, updated_at_utc
FROM pegamento_controlos WHERE pegamento_controlo_id = @Id
FOR UPDATE;";

        dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(
            uow.Connection, sql, new { Id = id }, uow.Transaction, ct);
        if (row is null) return null;

        // Read the measurements inside the SAME transaction so the aggregate is
        // consistent with the locked header (audit PG-04).
        const string measurementsSql = @"
SELECT pegamento_medicao_id, pegamento_controlo_id, component_key, tool_number,
       costura, contra_costura, measured_at_utc, actor_id
FROM pegamento_medicoes WHERE pegamento_controlo_id = @ControloId
ORDER BY measured_at_utc ASC;";
        var measurements = await Db.QueryAsync<dynamic>(
            uow.Connection, measurementsSql, new { ControloId = id }, uow.Transaction, ct);

        return HydrateControl(row, measurements.Select(MapMeasurement).ToList().AsReadOnly());
    }

    public async Task<IReadOnlyList<PegamentoControlo>> GetByRevisionAsync(Guid jobOnRevisionId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
       production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
       cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
       created_at_utc, created_by, updated_at_utc
FROM pegamento_controlos WHERE job_on_revision_id = @JobOnRevisionId
ORDER BY created_at_utc DESC;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { JobOnRevisionId = jobOnRevisionId }, cancellationToken: ct);
            return rows.Select<dynamic, PegamentoControlo>(r => MapControlRow(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PegamentoControlo>> GetByJobOnAsync(Guid jobOnId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
       production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
       cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
       created_at_utc, created_by, updated_at_utc
FROM pegamento_controlos WHERE job_on_id = @JobOnId
ORDER BY created_at_utc DESC;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { JobOnId = jobOnId }, cancellationToken: ct);
            return rows.Select<dynamic, PegamentoControlo>(r => MapControlRow(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<PegamentoControlo>> SearchAsync(
        string? reference, string? productionCode, string? machine, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var sql = @"
SELECT pegamento_controlo_id, job_on_id, job_on_revision_id, reference_snapshot,
       production_code, machine_code, cm_snapshot, bq_snapshot, mf_snapshot,
       cm_nominal, bq_nominal, mf_nominal, tolerance, status, notas,
       created_at_utc, created_by, updated_at_utc
FROM pegamento_controlos
WHERE (@Reference IS NULL OR reference_snapshot::text ILIKE '%'||@Reference||'%')
  AND (@ProductionCode IS NULL OR production_code = @ProductionCode)
  AND (@Machine IS NULL OR machine_code = @Machine)
  AND (@From IS NULL OR created_at_utc >= @From)
  AND (@To IS NULL OR created_at_utc <= @To)
ORDER BY created_at_utc DESC;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Reference = reference,
                ProductionCode = productionCode,
                Machine = machine,
                From = from,
                To = to
            }, cancellationToken: ct);
            return rows.Select<dynamic, PegamentoControlo>(r => MapControlRow(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE pegamento_controlos SET
    tolerance = @Tolerance,
    status = @Status,
    notas = @Notas,
    updated_at_utc = @UpdatedAtUtc
WHERE pegamento_controlo_id = @ControloId;";

        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            ControloId = control.PegamentoControloId,
            Tolerance = control.Tolerance,
            Status = ToDbStatus(control.Status),
            Notas = (object?)control.Notas ?? DBNull.Value,
            UpdatedAtUtc = control.UpdatedAtUtc ?? control.CreatedAtUtc
        }, uow.Transaction, ct);
    }

    // ---- Measurements -------------------------------------------------------

    public async Task<Guid> AddMeasurementAsync(
        IDbUnitOfWork uow, Guid controloId, PegamentoMedicao medicao, string actorId, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO pegamento_medicoes
    (pegamento_medicao_id, pegamento_controlo_id, component_key, tool_number,
     costura, contra_costura, measured_at_utc, actor_id)
VALUES
    (@MedicaoId, @ControloId, @ComponentKey, @ToolNumber,
     @Costura, @ContraCostura, @MeasuredAtUtc, @ActorId);";

        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            MedicaoId = medicao.PegamentoMedicaoId,
            ControloId = controloId,
            ComponentKey = medicao.ComponentKey.ToString(),
            ToolNumber = (object?)medicao.ToolNumber ?? DBNull.Value,
            Costura = medicao.Costura,
            ContraCostura = (object?)medicao.ContraCostura ?? DBNull.Value,
            MeasuredAtUtc = medicao.CreatedAtUtc,
            ActorId = (object?)actorId ?? DBNull.Value
        }, uow.Transaction, ct);
        return medicao.PegamentoMedicaoId;
    }

    public async Task<IReadOnlyList<PegamentoMedicao>> GetMeasurementsAsync(Guid controloId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_medicao_id, pegamento_controlo_id, component_key, tool_number,
       costura, contra_costura, measured_at_utc, actor_id
FROM pegamento_medicoes WHERE pegamento_controlo_id = @ControloId
ORDER BY measured_at_utc ASC;";

        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ControloId = controloId }, cancellationToken: ct);
            return rows.Select(MapMeasurement).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Document metadata (N14) --------------------------------------------

    public async Task UpsertDocumentAsync(IDbUnitOfWork uow, PegamentoDocumento document, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO pegamento_documentos
    (pegamento_documento_id, pegamento_controlo_id, filename,
     output_root_snapshot, production_folder_snapshot,
     generated_at_utc, generated_by)
VALUES
    (@DocumentoId, @ControloId, @Filename,
     @OutputRootSnapshot, @ProductionFolderSnapshot,
     @GeneratedAtUtc, @GeneratedBy)
ON CONFLICT (pegamento_controlo_id) DO UPDATE SET
    filename = EXCLUDED.filename,
    output_root_snapshot = EXCLUDED.output_root_snapshot,
    production_folder_snapshot = EXCLUDED.production_folder_snapshot,
    generated_at_utc = EXCLUDED.generated_at_utc,
    generated_by = EXCLUDED.generated_by;";

        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            DocumentoId = document.PegamentoDocumentoId,
            ControloId = document.PegamentoControloId,
            Filename = document.Filename,
            OutputRootSnapshot = document.OutputRootSnapshot,
            ProductionFolderSnapshot = document.ProductionFolderSnapshot,
            GeneratedAtUtc = document.GeneratedAtUtc,
            GeneratedBy = (object?)document.GeneratedBy ?? DBNull.Value
        }, uow.Transaction, ct);
    }

    public async Task<PegamentoDocumento?> GetDocumentAsync(IDbUnitOfWork uow, Guid controloId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT pegamento_documento_id, pegamento_controlo_id, filename,
       output_root_snapshot, production_folder_snapshot,
       generated_at_utc, generated_by
FROM pegamento_documentos WHERE pegamento_controlo_id = @ControloId;";

        dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(
            uow.Connection, sql, new { ControloId = controloId }, uow.Transaction, ct);
        if (row is null) return null;

        return new PegamentoDocumento
        {
            PegamentoDocumentoId = row.pegamento_documento_id,
            PegamentoControloId = row.pegamento_controlo_id,
            Filename = row.filename,
            OutputRootSnapshot = row.output_root_snapshot,
            ProductionFolderSnapshot = row.production_folder_snapshot,
            GeneratedAtUtc = row.generated_at_utc,
            GeneratedBy = row.generated_by
        };
    }

    // ---- Mapping helpers ----------------------------------------------------

    /// <summary>
    /// Hydrates a full control row: reconstructs the historical
    /// ovalização/média/tolerance values from the persisted inputs and the
    /// frozen nominals, then rehydrates the aggregate. Shared by the
    /// own-connection read and the in-transaction locked read.
    /// </summary>
    private static PegamentoControlo HydrateControl(dynamic row, IReadOnlyList<PegamentoMedicao> measurements)
    {
        var tolerance = row.tolerance;
        var cmNominal = row.cm_nominal;
        var bqNominal = row.bq_nominal;
        var mfNominal = row.mf_nominal;

        foreach (var m in measurements)
        {
            m.Ovalizacao = PegamentoMeasurementCalculator.Ovalizacao(m.Costura, m.ContraCostura);
            m.Media = PegamentoMeasurementCalculator.Media(m.Costura, m.ContraCostura);

            if (m.Media.HasValue)
            {
                var nominal = m.ComponentKey switch
                {
                    PegamentoComponentKey.CM => cmNominal,
                    PegamentoComponentKey.BQ => bqNominal,
                    PegamentoComponentKey.MF => mfNominal,
                    _ => throw new InvalidOperationException($"Unknown component key: {m.ComponentKey}")
                };

                if (!nominal.HasValue)
                {
                    // Legacy N16 row with missing historical nominal — MUST NOT be Ok.
                    m.ToleranceStatus = PegamentoToleranceStatus.NotEvaluable;
                }
                else
                {
                    m.ToleranceStatus =
                        PegamentoMeasurementCalculator.CheckTolerance(
                            m.Media.Value,
                            nominal.Value,
                            tolerance);
                }
            }
        }

        return PegamentoControlo.Hydrate(
            controloId: row.pegamento_controlo_id,
            jobOnId: row.job_on_id,
            jobOnRevisionId: row.job_on_revision_id,
            productionCode: row.production_code,
            machineCode: row.machine_code,
            referenceSnapshot: DeserializeString(row.reference_snapshot) ?? string.Empty,
            cmSnapshot: DeserializeToolSnapshot(row.cm_snapshot, PegamentoComponentKey.CM),
            bqSnapshot: DeserializeToolSnapshot(row.bq_snapshot, PegamentoComponentKey.BQ),
            mfSnapshot: DeserializeToolSnapshot(row.mf_snapshot, PegamentoComponentKey.MF),
            cmNominal: cmNominal,
            bqNominal: bqNominal,
            mfNominal: mfNominal,
            tolerance: tolerance,
            status: FromDbStatus(row.status),
            notas: row.notas,
            measurements: measurements,
            createdAtUtc: row.created_at_utc,
            createdBy: row.created_by,
            updatedAtUtc: row.updated_at_utc);
    }

    /// <summary>Maps a DB row to a read-only list item (no measurements loaded).</summary>
    private static PegamentoControlo MapControlRow(dynamic row)
    {
        return PegamentoControlo.Hydrate(
            controloId: row.pegamento_controlo_id,
            jobOnId: row.job_on_id,
            jobOnRevisionId: row.job_on_revision_id,
            productionCode: row.production_code,
            machineCode: row.machine_code,
            referenceSnapshot: DeserializeString(row.reference_snapshot) ?? string.Empty,
            cmSnapshot: DeserializeToolSnapshot(row.cm_snapshot, PegamentoComponentKey.CM),
            bqSnapshot: DeserializeToolSnapshot(row.bq_snapshot, PegamentoComponentKey.BQ),
            mfSnapshot: DeserializeToolSnapshot(row.mf_snapshot, PegamentoComponentKey.MF),
            cmNominal: row.cm_nominal,
            bqNominal: row.bq_nominal,
            mfNominal: row.mf_nominal,
            tolerance: row.tolerance,
            status: FromDbStatus(row.status),
            notas: row.notas,
            measurements: Array.Empty<PegamentoMedicao>(),
            createdAtUtc: row.created_at_utc,
            createdBy: row.created_by,
            updatedAtUtc: row.updated_at_utc);
    }

    private static PegamentoMedicao MapMeasurement(dynamic row)
    {
        return new PegamentoMedicao
        {
            PegamentoMedicaoId = row.pegamento_medicao_id,
            PegamentoControloId = row.pegamento_controlo_id,
            ComponentKey = row.component_key switch
            {
                "CM" => PegamentoComponentKey.CM,
                "BQ" => PegamentoComponentKey.BQ,
                "MF" => PegamentoComponentKey.MF,
                _ => throw new InvalidOperationException($"Unknown component key: {row.component_key}")
            },
            ToolNumber = row.tool_number,
            Costura = row.costura,
            ContraCostura = row.contra_costura,
            CreatedAtUtc = row.measured_at_utc
            // Ovalizacao/Media/ToleranceStatus reconstructed during GetById hydration
        };
    }

    // ---- JSON serialization helpers -----------------------------------------

    private static string SerializeJson(string value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string? SerializeToolSnapshot(PegamentoToolSnapshot? snapshot)
    {
        if (snapshot is null) return null;
        return JsonSerializer.Serialize(new
        {
            reference = snapshot.ReferenceSnapshot,
            lot = snapshot.LotSnapshot
        }, JsonOptions);
    }

    private static PegamentoToolSnapshot? DeserializeToolSnapshot(string? json, PegamentoComponentKey key)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new PegamentoToolSnapshot(
            Key: key,
            ReferenceSnapshot: root.TryGetProperty("reference", out var refProp) ? refProp.GetString() ?? string.Empty : string.Empty,
            LotSnapshot: root.TryGetProperty("lot", out var lotProp) ? lotProp.GetString() : null);
    }

    private static string? DeserializeString(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetString();
    }
}