using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna aggregate root (N08/N22 <c>internal_repair_records</c>;
/// GLM-RI-07 + OWNER DECISION). A record captures a quick in-turn repair fact on a line:
/// Linha + Tipo CM/MF/BQ + número individual, enriched automatically with the effective
/// production context when resolvable. The record NEVER rewrites other domains (position,
/// useful-life, technical state, Job On or master data) (GLM-RI-01).
///
/// R009 changes over the earlier model:
/// - Tool type is CM | MF | BQ.
/// - NO operational hard blocks: context is assistance. If the effective production
///   context cannot be resolved, the record is still saved (context empty/unknown).
/// - The exact historical production context is persisted on the record
///   (job_on_revision_id + production_code + reference + effective lot) so history never
///   depends on current_revision_id (GAP 2). These fields are NULL-able and legacy rows
///   remain readable without fabricated certainty.
/// - Repeated individual numbers are valid; each occurrence is a separate record
///   (5,5,7 → three records with the same context). Never deduplicate.
///
/// Corrections are NEW rows (GLM-DATA-07): a correction creates a fresh
/// <see cref="InternalRepairRecord"/> referencing the original via <c>correction_of_id</c>
/// and storing <c>before_snapshot</c> + reason. The original is never mutated or deleted.
/// </summary>
public sealed class InternalRepairRecord
{
    /// <summary>Primary key.</summary>
    public Guid InternalRepairRecordId { get; set; } = Guid.NewGuid();

    /// <summary>Line (B1–C3).</summary>
    public string Line { get; set; } = null!;

    /// <summary>Effective production Job On (logical link; may be null R009).</summary>
    public Guid? JobOnId { get; set; }

    /// <summary>Exact immutable revision anchor (R009 GAP 2 fix). Null when unresolved/legacy.</summary>
    public Guid? JobOnRevisionId { get; set; }

    /// <summary>Effective production code snapshot (R009). Null when unresolved/legacy.</summary>
    public string? ProductionCode { get; set; }

    /// <summary>Effective reference snapshot (R009). Null when unresolved/legacy.</summary>
    public string? Reference { get; set; }

    /// <summary>Effective lot for the tool type (logical, enrichment only; null when unresolved).</summary>
    public Guid? LotId { get; set; }

    /// <summary>Tool type CM/MF/BQ.</summary>
    public InternalRepairToolType ToolType { get; set; }

    /// <summary>Individual number (one occurrence; repeated numbers yield repeated rows).</summary>
    public string IndividualNumber { get; set; } = null!;

    /// <summary>Operator captured server-side at register.</summary>
    public string? OperatorId { get; set; }

    /// <summary>When the repair happened (server clock, captured at register).</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Original record this is a correction of (null for a primary record).</summary>
    public Guid? CorrectionOfId { get; set; }

    /// <summary>JSON snapshot of the original values before a correction (null on primary).</summary>
    public string? BeforeSnapshot { get; set; }

    /// <summary>Reason for the correction (optional per N08; UNRESOLVED in GLM-RI-12).</summary>
    public string? CorrectionReason { get; set; }

    /// <summary>Who created the record (primary register or correction).</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Creation timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>True when this is a correction of another record.</summary>
    public bool IsCorrection => CorrectionOfId is not null;

    /// <summary>
    /// Creates a primary internal-repair record (R009 — NO hard blocks). Validates only the
    /// structurally-minimal facts (known line, one non-empty individual number, a type, the
    /// operator and the occurred-at). The production context is nullable assistance: if the
    /// effective context is resolved it is persisted (job_on_id + revision + production +
    /// reference + effective lot); otherwise it is left null and the record is still saved.
    /// </summary>
    public static Result<InternalRepairRecord, DomainError> Create(
        string line,
        Guid? jobOnId,
        Guid? jobOnRevisionId,
        string? productionCode,
        string? reference,
        Guid? lotId,
        InternalRepairToolType toolType,
        string individualNumber,
        string operatorId,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset createdAtUtc)
    {
        var normalizedLine = line?.Trim() ?? string.Empty;
        if (!ReparacaoInternaModuleCatalog.Lines.Contains(normalizedLine))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Validation(
                "REPINT_LINE_UNKNOWN", "A Linha escolhida não é reconhecida."));

        var number = individualNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(number))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Validation(
                "REPINT_NUMBER_REQUIRED",
                "O número individual da ferramenta é obrigatório."));

        if (string.IsNullOrWhiteSpace(operatorId))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_OPERATOR_REQUIRED",
                "Não foi possível resolver o operador autenticado."));

        return Result<InternalRepairRecord, DomainError>.Success(new InternalRepairRecord
        {
            InternalRepairRecordId = Guid.NewGuid(),
            Line = normalizedLine,
            JobOnId = jobOnId,
            JobOnRevisionId = jobOnRevisionId,
            ProductionCode = productionCode,
            Reference = reference,
            LotId = lotId,
            ToolType = toolType,
            IndividualNumber = number,
            OperatorId = operatorId,
            OccurredAtUtc = occurredAtUtc,
            CreatedBy = operatorId,
            CreatedAtUtc = createdAtUtc
        });
    }

    /// <summary>
    /// Creates a CORRECTION as a NEW record (GLM-DATA-07). The original instance is never
    /// mutated. R009: like registration, the correction carries no operational hard block —
    /// the (re-)resolved context is assistance and may be null. Original operator and
    /// occurred-at stay read-only (REPARACAO_INTERNA_DESIGN_BRIEF §9). Correcting/overriding
    /// a context never modifies Job On (R009 §13).
    ///
    /// <paramref name="recalibrateContext"/> mirrors the registration auto-context behaviour
    /// for a correction that MOVED to a different line (R009/C3): when true, the context fields
    /// are taken exactly from the resolved values passed in — including an explicit null when
    /// the new line has no produced context — instead of inheriting the ORIGINAL line's context.
    /// When false (same-line correction, operator did not re-specify context) the original
    /// context is preserved via the <c>??</c> fallback.
    /// </summary>
    public Result<InternalRepairRecord, DomainError> CreateCorrection(
        string line,
        InternalRepairToolType toolType,
        string individualNumber,
        Guid? jobOnId,
        Guid? jobOnRevisionId,
        string? productionCode,
        string? reference,
        Guid? lotId,
        string correctionAuthor,
        string? reason,
        DateTimeOffset correctionAtUtc,
        string beforeSnapshotJson,
        bool recalibrateContext = false)
    {
        if (!IsValidForCorrection(this))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.DomainConflict(
                "REPINT_CORRECTION_CHAIN",
                "Não é possível corrigir uma correção existente; corrija o registo original."));

        var normalizedLine = line?.Trim() ?? string.Empty;
        if (!ReparacaoInternaModuleCatalog.Lines.Contains(normalizedLine))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Validation(
                "REPINT_LINE_UNKNOWN", "A Linha escolhida não é reconhecida."));

        var number = individualNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(number))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Validation(
                "REPINT_NUMBER_REQUIRED",
                "O número individual da ferramenta é obrigatório."));

        if (string.IsNullOrWhiteSpace(correctionAuthor))
            return Result<InternalRepairRecord, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_CORRECTOR_REQUIRED",
                "Não foi possível resolver o utilizador autorizado a corrigir."));

        return Result<InternalRepairRecord, DomainError>.Success(new InternalRepairRecord
        {
            InternalRepairRecordId = Guid.NewGuid(),
            Line = normalizedLine,
            JobOnId = recalibrateContext ? jobOnId : (jobOnId ?? JobOnId),
            JobOnRevisionId = recalibrateContext ? jobOnRevisionId : (jobOnRevisionId ?? JobOnRevisionId),
            ProductionCode = recalibrateContext ? productionCode : (productionCode ?? ProductionCode),
            Reference = recalibrateContext ? reference : (reference ?? Reference),
            LotId = recalibrateContext ? lotId : (lotId ?? LotId),
            ToolType = toolType,
            IndividualNumber = number,
            OperatorId = OperatorId,
            OccurredAtUtc = OccurredAtUtc,
            CorrectionOfId = InternalRepairRecordId,
            BeforeSnapshot = beforeSnapshotJson,
            CorrectionReason = reason,
            CreatedBy = correctionAuthor,
            CreatedAtUtc = correctionAtUtc
        });
    }

    private static bool IsValidForCorrection(InternalRepairRecord record) =>
        record.CorrectionOfId is null;
}