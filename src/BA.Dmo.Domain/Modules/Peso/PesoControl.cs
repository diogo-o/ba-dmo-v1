using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// Peso control/comparison aggregate (N06 <c>peso_controlos</c>; GLM-PESO-04/06).
/// Both Novo controlo and Comparação are pinned to a stable Job On context
/// (job_on_id + job_on_revision_id mandatory — TD-18) so the Ferramenta (CM)
/// attribution always resolves through the immutable revision, never through the
/// Job On id alone (TD-26; N06 comment). Revision increments: nao_aprovado →
/// edit+submit (revision+1); aprovado/nao_aprovado → reopen(reason) → rascunho
/// (revision+1). Approved controls are immutable facts and never deleted.
/// </summary>
public sealed class PesoControl
{
    public Guid PesoControloId { get; set; } = Guid.NewGuid();

    public Guid PesoReferenceId { get; set; }

    public Guid PesoLoteId { get; set; }

    public PesoRecordType RecordType { get; set; }

    public string MoldNumber { get; set; } = string.Empty;

    public string NeckringNumber { get; set; } = string.Empty;

    public string ProductionCode { get; set; } = string.Empty;

    public string Line { get; set; } = string.Empty;

    public string Lote { get; set; } = string.Empty;

    public DateTime ControlDate { get; set; }

    /// <summary>Stable Job On production context (TD-18/GLM-PESO-15).</summary>
    public Guid JobOnId { get; set; }

    /// <summary>
    /// Immutable revision consumed by this record (TD-18). The Ferramenta
    /// attribution resolves through this revision's job_on_component rows.
    /// </summary>
    public Guid JobOnRevisionId { get; set; }

    /// <summary>Inherited, non-editable Job On context snapshot (presentation/filter).</summary>
    public string? CmSnapshotJson { get; set; }

    public PesoControlState Status { get; set; } = PesoControlState.Rascunho;

    public int Revision { get; set; } = 1;

    public string MeasurementsSnapshotJson { get; set; } = "{}";

    public string? ApprovalLogJson { get; set; } = "[]";

    public string? PreviousControlJson { get; set; }

    public string? ComparisonDecisionsJson { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public IReadOnlyList<PesoLeitura> Leituras { get; set; } = Array.Empty<PesoLeitura>();

    /// <summary>Nominal weight shown in results (from Peso lot / reference).</summary>
    public decimal? PesoNominal { get; set; }

    /// <summary>
    /// The applied process (NNPB/PS) of this control's Peso lot. Historic
    /// snapshots keep the process used at the time (GLM-PESO-11: process changes
    /// apply to new records only).
    /// </summary>
    public PesoProcesso? Processo { get; set; }

    /// <summary>
    /// The glass density (constant, e.g. NNPB/PS) ACTUALLY USED when this control
    /// was calculated/saved (OC-6). Historical integrity: once persisted, changing
    /// the configured NNPB/PS density must NOT reinterpret this value.
    /// </summary>
    public decimal? ConstanteGlassUsada { get; set; }

    /// <summary>
    /// Average estimated glass weight (diferenca_peso) across readings — the
    /// same quantity the results summary presents (GLM-PESO-05). Presentation/derived.
    /// </summary>
    public decimal? PesoMedio => WeightCalculator.GlassAverage(
        (Leituras ?? Array.Empty<PesoLeitura>()).Select(l => l.PesoVidro).ToList());

    /// <summary>
    /// Average capacity across readings (volume = weight/density) — derived for
    /// the Responsável comparison and documents. Presentation/derived.
    /// </summary>
    public decimal? CapacidadeMedia
    {
        get
        {
            if (TemperaturaC is null) return null;
            var density = WeightCalculator.LookupDensity(TemperaturaC.Value);
            if (density.IsFailure) return null;
            var capacities = (Leituras ?? Array.Empty<PesoLeitura>())
                .Select(l => WeightCalculator.VolumeFromWeight(l.PesoEmAgua, density.Value))
                .ToList();
            return WeightCalculator.GlassAverage(capacities);
        }
    }

    // ---- measurement snapshot (typed for calculation) ---------------------

    public decimal? TemperaturaC { get; set; }

    public string? EstadoMolde { get; set; }

    public DateTime? FimProducaoAnteriorSap { get; set; }

    public decimal? PesoMedioAnteriorSap { get; set; }

    public string? Notas { get; set; }

    public DateTime? DataRegistoComparacao { get; set; }

    // ---- workflow (GLM-PESO-06.6) -----------------------------------------

    /// <summary>
    /// Submits a control: rascunho → pendente. Requires at least one reading
    /// (GLM-PESO-04/10 hard block). Enviar para aprovação is never automatic.
    /// </summary>
    public Result<bool, DomainError> Submit()
    {
        if (Status != PesoControlState.Rascunho)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_CONTROL_NOT_DRAFT",
                "Apenas controlos em rascunho podem ser enviados para aprovação."));

        if (Leituras.Count == 0 || Leituras.All(l => !l.PesoEmAgua.HasValue))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_CONTROL_NO_READING",
                "É necessária pelo menos uma leitura para enviar o controlo."));

        Status = PesoControlState.Pendente;
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// Approves a control (Responsável, peso.aprovar). Records decision + a day
    /// approval fact. pendente → aprovado.
    /// </summary>
    public Result<bool, DomainError> Approve(string approvedBy, DateTimeOffset nowUtc)
    {
        if (Status != PesoControlState.Pendente)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_CONTROL_NOT_PENDING",
                "Aprovação exige um controlo pendente."));

        Status = PesoControlState.Aprovado;
        ApprovedBy = approvedBy;
        ApprovedAtUtc = nowUtc;
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// Rejects a control (Responsável). Requires a mandatory note (GLM-PESO-04/10
    /// hard block). pendente → nao_aprovado.
    /// </summary>
    public Result<bool, DomainError> Reject(string justification)
    {
        if (Status != PesoControlState.Pendente)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_CONTROL_NOT_PENDING",
                "Rejeição exige um controlo pendente."));

        if (string.IsNullOrWhiteSpace(justification))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_CONTROL_REJECT_NOTE_REQUIRED",
                "A rejeição requer uma justificação obrigatória."));

        Status = PesoControlState.NaoAprovado;
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// Reopens a control from aprovado/nao_aprovado, creating a new revision
    /// (revision+1, reopened_from_status). Justification is mandatory for
    /// approved/rejected controls (GLM-PESO-06.6/8).
    /// </summary>
    public Result<bool, DomainError> Reopen(string reason, DateTimeOffset nowUtc)
    {
        if (Status is not (PesoControlState.Aprovado or PesoControlState.NaoAprovado))
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_CONTROL_NOT_REOPENABLE",
                "Apenas controlos aprovados ou não aprovados podem ser reabertos."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_CONTROL_REOPEN_REASON",
                "Reabrir um controlo exige justificação obrigatória."));

        Status = PesoControlState.Rascunho;
        Revision += 1;
        ApprovedBy = null;
        ApprovedAtUtc = null;
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// True when the control may be physically deleted (GLM-PESO-06.7): only
    /// rascunho/nao_aprovado; pendente/aprovado never.
    /// </summary>
    public bool IsDeletable => Status is PesoControlState.Rascunho or PesoControlState.NaoAprovado;
}

/// <summary>
/// A resolved previous-approved-control fact for deltas (TD-13/TD-30;
/// <c>peso_controlos.previous_control</c>). Same mold+neckring, strictly earlier
/// production/date, CROSS-LINE. Null when none exists → deltas stay null.
/// </summary>
public sealed record PesoControloAnterior(
    Guid? PreviousPesoControloId,
    decimal? PreviousPesoMedio,
    decimal? PreviousCapacidadeMedia,
    bool Exists);
