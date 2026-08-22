using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Pegamento control aggregate root (N07 <c>pegamento_controlos</c>; GLM-PEG-02).
/// Pinned to an exact <c>job_on_revision_id</c> — the immutable historical anchor.
/// CM/BQ/MF inherited from that revision's component rows, never independently selectable.
/// </summary>
public sealed class PegamentoControlo
{
    public Guid PegamentoControloId { get; set; } = Guid.NewGuid();

    /// <summary>Grouping FK — derived from resolved revision context at creation.</summary>
    public Guid JobOnId { get; private set; }

    /// <summary>Immutable historical anchor — exact revision this control belongs to.</summary>
    public Guid JobOnRevisionId { get; private set; }

    /// <summary>Production code snapshot from the pinned revision.</summary>
    public string ProductionCode { get; private set; } = string.Empty;

    /// <summary>Machine code snapshot from the pinned revision.</summary>
    public string MachineCode { get; private set; } = string.Empty;

    /// <summary>Reference snapshot from the pinned revision.</summary>
    public string ReferenceSnapshot { get; private set; } = string.Empty;

    /// <summary>Inherited CM tool snapshot from the pinned revision.</summary>
    public PegamentoToolSnapshot? CmSnapshot { get; private set; }

    /// <summary>Inherited BQ tool snapshot from the pinned revision.</summary>
    public PegamentoToolSnapshot? BqSnapshot { get; private set; }

    /// <summary>Inherited MF tool snapshot from the pinned revision.</summary>
    public PegamentoToolSnapshot? MfSnapshot { get; private set; }

    /// <summary>CM nominal value frozen at creation time (historical).</summary>
    public decimal? CmNominal { get; private set; }

    /// <summary>BQ nominal value frozen at creation time (historical).</summary>
    public decimal? BqNominal { get; private set; }

    /// <summary>MF nominal value frozen at creation time (historical).</summary>
    public decimal? MfNominal { get; private set; }

    /// <summary>Tolerance corridor (default 0.20).</summary>
    public decimal Tolerance { get; private set; } = PegamentoModuleCatalog.DefaultTolerance;

    /// <summary>Control state: aberto (editable) or fechado (closed/frozen).</summary>
    public PegamentoControloStatus Status { get; private set; } = PegamentoControloStatus.Aberto;

    /// <summary>Optional notes.</summary>
    public string? Notas { get; private set; }

    /// <summary>Collection of measurements (append-only facts).</summary>
    public IReadOnlyList<PegamentoMedicao> Measurements { get; private set; } = Array.Empty<PegamentoMedicao>();

    /// <summary>Audit: created timestamp.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Audit: created by actor.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Audit: last updated timestamp.</summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    // ---- Factory / Hydration -----------------------------------------------

    /// <summary>
    /// Creates a new PegamentoControlo anchored to the exact Job On revision context.
    /// The revision id is immutable by construction — no setter, no mutation method.
    /// </summary>
    public static Result<PegamentoControlo, DomainError> Create(
        PegamentoProductionContext context,
        decimal? toleranceOverride,
        string? notas,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        if (context is null)
            return Result<PegamentoControlo, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_CONTEXT_REQUIRED",
                "O contexto de produção é obrigatório para criar um controlo de pegamentos."));

        // Validate tool snapshot invariants
        if (context.CmSnapshot?.Key != PegamentoComponentKey.CM)
            return Result<PegamentoControlo, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_CM_SNAPSHOT_INVALID",
                "O snapshot CM do contexto é inválido."));
        if (context.BqSnapshot?.Key != PegamentoComponentKey.BQ)
            return Result<PegamentoControlo, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_BQ_SNAPSHOT_INVALID",
                "O snapshot BQ do contexto é inválido."));
        if (context.MfSnapshot?.Key != PegamentoComponentKey.MF)
            return Result<PegamentoControlo, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_MF_SNAPSHOT_INVALID",
                "O snapshot MF do contexto é inválido."));

        var control = new PegamentoControlo
        {
            PegamentoControloId = Guid.NewGuid(),
            JobOnId = context.JobOnId,
            JobOnRevisionId = context.JobOnRevisionId,
            ProductionCode = context.ProductionCode,
            MachineCode = context.MachineCode,
            ReferenceSnapshot = context.Reference,
            CmSnapshot = context.CmSnapshot,
            BqSnapshot = context.BqSnapshot,
            MfSnapshot = context.MfSnapshot,
            CmNominal = context.CmNominal,
            BqNominal = context.BqNominal,
            MfNominal = context.MfNominal,
            Tolerance = toleranceOverride ?? PegamentoModuleCatalog.DefaultTolerance,
            Notas = notas,
            Status = PegamentoControloStatus.Aberto,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
        };

        return Result<PegamentoControlo, DomainError>.Success(control);
    }

    /// <summary>
    /// Rehydrates an existing aggregate from persistence. This is the ONLY path
    /// that reconstructs an existing aggregate including the immutable JobOnRevisionId.
    /// </summary>
    public static PegamentoControlo Hydrate(
        Guid controloId,
        Guid jobOnId,
        Guid jobOnRevisionId,
        string productionCode,
        string machineCode,
        string referenceSnapshot,
        PegamentoToolSnapshot? cmSnapshot,
        PegamentoToolSnapshot? bqSnapshot,
        PegamentoToolSnapshot? mfSnapshot,
        decimal? cmNominal,
        decimal? bqNominal,
        decimal? mfNominal,
        decimal tolerance,
        PegamentoControloStatus status,
        string? notas,
        IReadOnlyList<PegamentoMedicao> measurements,
        DateTimeOffset createdAtUtc,
        string? createdBy,
        DateTimeOffset? updatedAtUtc)
    {
        return new PegamentoControlo
        {
            PegamentoControloId = controloId,
            JobOnId = jobOnId,
            JobOnRevisionId = jobOnRevisionId,
            ProductionCode = productionCode,
            MachineCode = machineCode,
            ReferenceSnapshot = referenceSnapshot,
            CmSnapshot = cmSnapshot,
            BqSnapshot = bqSnapshot,
            MfSnapshot = mfSnapshot,
            CmNominal = cmNominal,
            BqNominal = bqNominal,
            MfNominal = mfNominal,
            Tolerance = tolerance,
            Status = status,
            Notas = notas,
            Measurements = measurements,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    // ---- Mutation methods --------------------------------------------------

    /// <summary>
    /// Adds a measurement to the control (append-only). Computes ovalização/média
    /// via the calculation engine. Only allowed when aberto.
    /// Tool number is the number of the tool/cavity being measured (e.g. CM 42, BQ 11).
    /// </summary>
    public Result<PegamentoMedicao, DomainError> AddMeasurement(
        PegamentoComponentKey component,
        int toolNumber,
        decimal costura,
        decimal? contraCostura,
        DateTimeOffset nowUtc)
    {
        if (Status != PegamentoControloStatus.Aberto)
            return Result<PegamentoMedicao, DomainError>.Failure(DomainError.DomainConflict(
                "PEGAMENTO_CONTROL_CLOSED",
                "Apenas controlos abertos podem receber medições."));

        if (toolNumber <= 0)
            return Result<PegamentoMedicao, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_TOOL_NUMBER_REQUIRED",
                "O número da ferramenta é obrigatório."));

        var nominal = component switch
        {
            PegamentoComponentKey.CM => CmNominal,
            PegamentoComponentKey.BQ => BqNominal,
            PegamentoComponentKey.MF => MfNominal,
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        if (!nominal.HasValue)
        {
            return Result<PegamentoMedicao, DomainError>.Failure(
                DomainError.Validation(
                    "PEGAMENTO_COMPONENT_NOMINAL_REQUIRED",
                    "O valor nominal histórico do componente é obrigatório para registar a medição."));
        }

        var medicao = new PegamentoMedicao
        {
            PegamentoMedicaoId = Guid.NewGuid(),
            PegamentoControloId = PegamentoControloId,
            ComponentKey = component,
            ToolNumber = toolNumber,
            Costura = costura,
            ContraCostura = contraCostura,
            Ovalizacao = PegamentoMeasurementCalculator.Ovalizacao(costura, contraCostura),
            Media = PegamentoMeasurementCalculator.Media(costura, contraCostura),
            CreatedAtUtc = nowUtc,
        };

        if (medicao.Media.HasValue)
        {
            medicao.ToleranceStatus =
                PegamentoMeasurementCalculator.CheckTolerance(
                    medicao.Media.Value,
                    nominal.Value,
                    Tolerance);
        }

        var list = new List<PegamentoMedicao>(Measurements) { medicao };
        Measurements = list.AsReadOnly();

        return Result<PegamentoMedicao, DomainError>.Success(medicao);
    }

    /// <summary>
    /// Updates editable fields (tolerance, notes) without rewriting the revision anchor.
    /// Only allowed when aberto.
    /// </summary>
    public Result<bool, DomainError> UpdateEditableFields(
        decimal? tolerance, string? notas, DateTimeOffset nowUtc)
    {
        if (Status != PegamentoControloStatus.Aberto)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PEGAMENTO_CONTROL_CLOSED",
                "Apenas controlos abertos podem ser editados."));

        if (tolerance.HasValue)
            Tolerance = tolerance.Value;
        if (notas is not null)
            Notas = notas;
        UpdatedAtUtc = nowUtc;

        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// Closes the control. After closing, the control becomes frozen and cannot
    /// be modified without an explicit reopen workflow.
    /// Authority does not require a close reason — just state transition.
    /// </summary>
    public Result<bool, DomainError> Close(DateTimeOffset nowUtc)
    {
        if (Status != PegamentoControloStatus.Aberto)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PEGAMENTO_CONTROL_NOT_OPEN",
                "Apenas controlos abertos podem ser fechados."));

        Status = PegamentoControloStatus.Fechado;
        UpdatedAtUtc = nowUtc;

        return Result<bool, DomainError>.Success(true);
    }
}

/// <summary>Pegamento control state (aberto = editable, fechado = frozen).</summary>
public enum PegamentoControloStatus
{
    Aberto,
    Fechado
}

/// <summary>
/// A single measurement fact for a component (N07 <c>pegamento_medicoes</c>).
/// Append-only — once persisted, never modified.
/// ToolNumber is the number of the tool/cavity being measured (N.º column).
/// </summary>
public sealed class PegamentoMedicao
{
    public Guid PegamentoMedicaoId { get; set; } = Guid.NewGuid();
    public Guid PegamentoControloId { get; set; }
    public PegamentoComponentKey ComponentKey { get; set; }
    public int? ToolNumber { get; set; }
    public decimal Costura { get; set; }
    public decimal? ContraCostura { get; set; }
    public decimal? Ovalizacao { get; set; }
    public decimal? Media { get; set; }
    public PegamentoToleranceStatus ToleranceStatus { get; set; } = PegamentoToleranceStatus.Ok;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>
/// Tolerance check result for a measurement.
/// <c>NotEvaluable</c> means the historical nominal for the component is missing
/// (legacy N16 row) — the measurement MUST NOT be reported as Ok.
/// </summary>
public enum PegamentoToleranceStatus
{
    Ok,
    Warning,
    Exceeded,
    NotEvaluable
}
