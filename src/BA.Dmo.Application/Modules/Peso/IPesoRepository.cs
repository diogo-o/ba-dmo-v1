using BA.Dmo.Domain.Modules.Peso;

namespace BA.Dmo.Application.Modules.Peso;

/// <summary>
/// Peso read/write port (N06, GLM-PESO-08). All CRUD and queries go through
/// this interface; implementation uses Dapper against peso_* tables.
/// Every control/comparison stores <c>job_on_id</c> + <c>job_on_revision_id</c>
/// (TD-18). <c>peso_controlos.previous_control</c> is the immutable comparison baseline (TD-13).
/// </summary>
public interface IPesoRepository
{
    // ---- References ------------------------------------------------------
    Task<Guid> CreateReferenceAsync(PesoReference reference, CancellationToken cancellationToken = default);
    Task<PesoReference?> GetReferenceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PesoReference>> GetReferencesAsync(string? search, CancellationToken cancellationToken = default);
    Task<PesoReference?> GetReferenceByMoldNeckringAsync(string mold, string neckring, CancellationToken cancellationToken = default);
    Task UpdateReferenceAsync(PesoReference reference, CancellationToken cancellationToken = default);

    // ---- Lots ------------------------------------------------------------
    Task<Guid> CreateLoteAsync(PesoLote lote, CancellationToken cancellationToken = default);
    Task<PesoLote?> GetLoteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PesoLote>> GetLotesAsync(Guid referenceId, CancellationToken cancellationToken = default);

    // ---- Controls ---------------------------------------------------------
    Task<Guid> CreateControlAsync(PesoControl control, CancellationToken cancellationToken = default);
    Task<PesoControl?> GetControlByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PesoControl>> GetControlsAsync(
        Guid? referenceId, string? search, string? status, PesoRecordType? type,
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task UpdateControlAsync(PesoControl control, CancellationToken cancellationToken = default);

    /// <summary>
    /// Header-only update of a control (N40 pairing). Rewrites NO readings —
    /// used by the workflow transitions (submit/approve/reject/reopen/decide)
    /// so an approved baseline is never re-written through the readings table;
    /// the N40 DB guard backs this up at the store level.
    /// </summary>
    Task UpdateControlHeaderAsync(PesoControl control, CancellationToken cancellationToken = default);
    Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default);

    // ---- Day approvals -----------------------------------------------------
    Task SaveDayApprovalAsync(
        string mold, string neckring, string line, DateTime approvalDate,
        string approvedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRecordDatesAsync(
        int year, int month, CancellationToken cancellationToken = default);

    // ---- Settings ------------------------------------------------------------
    Task SaveSettingAsync(string key, string json, string updatedBy, CancellationToken cancellationToken = default);
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    // ---- Audit ---------------------------------------------------------------
    Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Peso lot administrative record (N06 <c>peso_lotes</c>; TD-17). Processo
/// NNPB/PS belongs to the LOT — inherited by Job On, Novo controlo and
/// Comparação. Allowed lines ≥1 in B1..C3; report_subfolder is a relative name.
/// </summary>
public sealed record PesoLote
{
    public Guid PesoLoteId { get; set; } = Guid.NewGuid();

    public Guid PesoReferenceId { get; set; }

    public string Lote { get; set; } = string.Empty;

    public PesoProcesso Processo { get; set; }

    public IReadOnlyList<string> AllowedLines { get; set; } = Array.Empty<string>();

    public string ReportSubfolder { get; set; } = string.Empty;

    public decimal? NominalWeight { get; set; }
}

/// <summary>Reference summary loaded for the Operador/Responsável views.</summary>
public sealed record PesoReferenceSummary
{
    public Guid PesoReferenceId { get; set; }

    public string MoldNumber { get; set; } = string.Empty;

    public string NeckringNumber { get; set; } = string.Empty;

    public string? CounterMold { get; set; }
}

/// <summary>Approved control used as the immutable comparison base (TD-29/DG-03).</summary>
public sealed record PesoApprovedBase
{
    public Guid PesoControloId { get; set; }

    public int Revision { get; set; }

    public decimal? PesoMedio { get; set; }

    public decimal? CapacidadeMedia { get; set; }
}
