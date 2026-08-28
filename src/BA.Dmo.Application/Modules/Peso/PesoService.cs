using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Application.Modules.Peso;

// ---- request records -----------------------------------------------------

public sealed record SaveReferenceRequest(
    string MoldNumber,
    string NeckringNumber,
    string? CounterMold,
    decimal? Capacity,
    decimal? VolumeNeck,
    decimal? VolumePu,
    decimal? CaloteTp,
    string? ChangeReason);

public sealed record CreateLoteRequest(
    Guid ReferenceId,
    string Lote,
    PesoProcesso Processo,
    IReadOnlyList<string> AllowedLines,
    string ReportSubfolder,
    decimal? NominalWeight);

public sealed record CreateControlRequest(
    Guid JobOnId,
    DateTime ControlDate,
    decimal? TemperaturaC,
    string? EstadoMolde,
    string? Notas,
    IReadOnlyList<PesoLeituraInput> Leituras);

public sealed record PesoLeituraInput(string CmNumber, decimal? PesoEmAgua);

public sealed record SaveControlRequest(
    Guid ControlId,
    decimal? TemperaturaC,
    string? EstadoMolde,
    string? Notas,
    IReadOnlyList<PesoLeituraInput> Leituras,
    string? ChangeReason);

public sealed record SubmitControlRequest(Guid ControlId);

public sealed record ApproveControlRequest(Guid ControlId, bool RegisterDayApproval = true);

public sealed record RejectControlRequest(Guid ControlId, string Justification);

public sealed record ReopenControlRequest(Guid ControlId, string Reason);

public sealed record DeleteControlRequest(Guid ControlId);

public sealed record CreateComparisonRequest(
    Guid CurrentControlId,
    Guid PreviousApprovedControlId,
    string? Notas,
    IReadOnlyList<PesoComparisonPairRequest> Pairs);

public sealed record PesoComparisonPairRequest(
    string CurrentCmNumber,
    string PreviousCmNumber);

public sealed record DecideComparisonCmRequest(
    string CmNumber,
    PesoCmDecision Decision);

public sealed record ConfirmComparisonDecisionsRequest(
    Guid ControlId,
    string? Justification,
    IReadOnlyList<DecideComparisonCmRequest> Decisions);

public sealed record SaveDayApprovalRequest(string Mold, string Neckring, string Line, DateTime ApprovalDate);

public sealed record SaveSettingsRequest(string Key, string JsonValue);

/// <summary>Result of document generation: PDF bytes + deterministic filename.</summary>
public sealed record GeneratedDocument(byte[] PdfBytes, string FileName);

public sealed record GenerateDocumentRequest(Guid ControlId);

public sealed record PrepareEmailRequest(Guid ControlId);

public sealed record ControlFilterRequest(
    Guid? ReferenceId,
    string? Search,
    string? Status,
    PesoRecordType? Type,
    DateTime? From,
    DateTime? To);

/// <summary>Control row for lists (Histórico/Resp).</summary>
public sealed record PesoControlListItem(
    Guid ControlId,
    PesoRecordType Type,
    string Reference,
    string Production,
    string Machine,
    string Lote,
    decimal? Peso,
    int Revision,
    PesoControlState Status,
    DateTime ControlDate);

/// <summary>Derived calculation result returned by the live preview endpoint
/// (produced by the single C# WeightCalculator — GLM-PESO-05).</summary>
public sealed record PesoCalculationResult
{
    public decimal? Densidade { get; init; }
    public decimal? ConstanteGlassUsada { get; init; }
    public decimal? PesoMedio { get; init; }
    public decimal? CapacidadeMedia { get; init; }
    public decimal? PesoNominal { get; init; }
    public decimal? Diferenca { get; init; }
    public decimal? DiferencaPct { get; init; }
    public IReadOnlyList<PesoCalculationRow> Rows { get; init; } = Array.Empty<PesoCalculationRow>();
}

/// <summary>One per-CM calculation row of the result preview.</summary>
public sealed record PesoCalculationRow
{
    public string CmNumber { get; init; } = string.Empty;
    public decimal? PesoEmAgua { get; init; }
    public decimal? Capacidade { get; init; }
    public decimal? PesoVidro { get; init; }
}

// ---- service ------------------------------------------------------------

/// <summary>
/// U-10 — Peso use cases (modules/03, GLM-PESO-08). Every operation re-checks
/// the canonical capability server-side through the gate, executes through the
/// repository port, and records the module audit fact. Novo controlo and
/// Comparação pin the immutable <c>job_on_revision_id</c> they consumed
/// (TD-18/GLM-PESO-15). Novo controlo/Comparação inherit reference/production/
/// machine/CM/lot/process from the Job On — NO second selection.
/// </summary>
public sealed class PesoService
{
    private static readonly System.Text.Json.JsonSerializerOptions ComparisonJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly PesoAuthorizationGate _gate;
    private readonly IPesoRepository _repository;
    private readonly IJobOnRepository _jobOnRepository;
    private readonly IClock _clock;

    public PesoService(
        PesoAuthorizationGate gate,
        IPesoRepository repository,
        IJobOnRepository jobOnRepository,
        IClock clock)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _jobOnRepository = jobOnRepository ?? throw new ArgumentNullException(nameof(jobOnRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // ---- References (Operador: peso module) -----------------------------

    public async Task<Result<Guid, DomainError>> SaveReferenceAsync(
        SaveReferenceRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var validation = PesoValidator.ValidateReference(request.MoldNumber, request.NeckringNumber);
        if (validation is not null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(validation.Code, validation.Message));

        var existing = await _repository.GetReferenceByMoldNeckringAsync(request.MoldNumber, request.NeckringNumber, ct);
        if (existing is null)
        {
            var reference = new PesoReference
            {
                PesoReferenceId = Guid.NewGuid(),
                MoldNumber = request.MoldNumber.Trim(),
                NeckringNumber = request.NeckringNumber.Trim(),
                CounterMold = request.CounterMold,
                Capacity = request.Capacity,
                VolumeNeck = request.VolumeNeck,
                VolumePu = request.VolumePu,
                CaloteTp = request.CaloteTp
            };
            var id = await _repository.CreateReferenceAsync(reference, ct);
            await _repository.InsertAuditEventAsync(id, "peso.referencia.criar", null, null, gate.Value.ActorId, ct);
            return Result<Guid, DomainError>.Success(id);
        }

        // Editing an approved/used reference requires justification, withdraws
        // approval and creates a new revision (GLM-PESO-06.8). We re-create a
        // NEW reference revision; the previous remains immutable.
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_REF_CHANGE_REASON_REQUIRED",
                "Alterar uma referência exige justificação obrigatória."));

        var updated = existing with
        {
            CounterMold = request.CounterMold,
            Capacity = request.Capacity,
            VolumeNeck = request.VolumeNeck,
            VolumePu = request.VolumePu,
            CaloteTp = request.CaloteTp,
            ChangeLogJson = AppendNote(existing.ChangeLogJson, gate.Value.ActorId, request.ChangeReason, _clock)
        };
        await _repository.UpdateReferenceAsync(updated, ct);
        await _repository.InsertAuditEventAsync(existing.PesoReferenceId, "peso.referencia.editar", existing.ChangeLogJson, updated.ChangeLogJson, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(existing.PesoReferenceId);
    }

    // ---- Lots (Operador: peso module) ------------------------------------

    public async Task<Result<Guid, DomainError>> CreateLoteAsync(
        CreateLoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var reference = await _repository.GetReferenceByIdAsync(request.ReferenceId, ct);
        if (reference is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound("PESO_REF_NOT_FOUND", "Referência não encontrada."));

        var validation = PesoValidator.ValidateLote(request.Lote, request.Processo, request.AllowedLines, request.ReportSubfolder);
        if (validation is not null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(validation.Code, validation.Message));

        var lote = new PesoLote
        {
            PesoLoteId = Guid.NewGuid(),
            PesoReferenceId = request.ReferenceId,
            Lote = request.Lote.Trim(),
            Processo = request.Processo,
            AllowedLines = request.AllowedLines.Select(l => l.Trim()).ToList(),
            ReportSubfolder = request.ReportSubfolder.Trim(),
            NominalWeight = request.NominalWeight
        };
        var id = await _repository.CreateLoteAsync(lote, ct);
        await _repository.InsertAuditEventAsync(id, "peso.lote.criar", null, null, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<Guid, DomainError>> DuplicateLoteAsync(
        Guid sourceLoteId, CreateLoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var source = await _repository.GetLoteByIdAsync(sourceLoteId, ct);
        if (source is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound("PESO_LOTE_NOT_FOUND", "Lote não encontrado."));

        var validation = PesoValidator.ValidateLote(request.Lote, request.Processo, request.AllowedLines, request.ReportSubfolder);
        if (validation is not null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(validation.Code, validation.Message));

        var lote = new PesoLote
        {
            PesoLoteId = Guid.NewGuid(),
            PesoReferenceId = source.PesoReferenceId,
            Lote = request.Lote.Trim(),
            Processo = request.Processo,
            AllowedLines = request.AllowedLines.Select(l => l.Trim()).ToList(),
            ReportSubfolder = request.ReportSubfolder.Trim(),
            NominalWeight = request.NominalWeight ?? source.NominalWeight
        };
        var id = await _repository.CreateLoteAsync(lote, ct);
        await _repository.InsertAuditEventAsync(id, "peso.lote.duplicar", source.PesoLoteId.ToString(), lote.PesoLoteId.ToString(), gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    // ---- Resolve Job On context (TD-18/GLM-PESO-06.3/15) ------------------

    private sealed record JobOnContext(
        Guid JobOnId,
        Guid RevisionId,
        string ProductionCode,
        string MachineCode,
        string ReferenceText,
        string? CmLoteText,
        PesoProcesso Processo);

    private Result<JobOnContext, DomainError> ResolveJobOnContext(JobOnEntity jobOn)
    {
        var revision = jobOn.CurrentRevision;
        if (revision is null)
            return Result<JobOnContext, DomainError>.Failure(DomainError.Validation(
                "PESO_JOBON_NO_REVISION",
                "Corrigir ferramentas no Job On — o Job On não tem uma revisão ativa válida."));

        // Ferramenta (CM) attribution resolves through the revision's MP_CM
        // component (source_tool_lot), never through the Job On id alone.
        var cm = (revision.Components ?? Array.Empty<JobOnComponent>())
            .FirstOrDefault(c => c.Family == ComponentFamily.MP_CM);

        var referenceText = !string.IsNullOrWhiteSpace(revision.ReferenceSnapshot)
            ? revision.ReferenceSnapshot
            : cm?.ReferenceSnapshot;
        if (string.IsNullOrWhiteSpace(referenceText))
            return Result<JobOnContext, DomainError>.Failure(DomainError.Validation(
                "PESO_JOBON_INVALID_REFERENCE",
                "Corrigir ferramentas no Job On — falta a Referência no contexto do Job On."));

        var process = TryParseProcess(revision.ProcessSnapshot);
        if (process is null)
            return Result<JobOnContext, DomainError>.Failure(DomainError.Validation(
                "PESO_JOBON_INVALID_PROCESS",
                "Corrigir ferramentas no Job On — não foi possível resolver o processo do lote do Peso."));

        return Result<JobOnContext, DomainError>.Success(new JobOnContext(
            jobOn.Id,
            revision.JobOnRevisionId,
            jobOn.ProductionCode,
            jobOn.MachineCode,
            referenceText,
            cm?.LotSnapshot,
            process.Value));
    }

    private static PesoProcesso? TryParseProcess(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Equals("NNPB", StringComparison.OrdinalIgnoreCase)) return PesoProcesso.Nnpb;
        if (value.Trim().Equals("PS", StringComparison.OrdinalIgnoreCase)) return PesoProcesso.Ps;
        return null;
    }

    // ---- Novo controlo ----------------------------------------------------

    public async Task<Result<Guid, DomainError>> CreateControlAsync(
        CreateControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var jobOn = await _jobOnRepository.GetByIdAsync(request.JobOnId, ct);
        if (jobOn is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound("PESO_JOBON_NOT_FOUND", "Job On não encontrado."));

        var context = ResolveJobOnContext(jobOn);
        if (context.IsFailure) return Result<Guid, DomainError>.Failure(context.Error);

        var reference = await FindReferenceByTextAsync(context.Value.ReferenceText, ct);
        var lote = reference is not null
            ? (await _repository.GetLotesAsync(reference.PesoReferenceId, ct)).FirstOrDefault()
            : null;

        var processo = lote?.Processo ?? context.Value.Processo;
        var constante = await ResolveProcessDensityAsync(processo, ct);

        var control = new PesoControl
        {
            PesoControloId = Guid.NewGuid(),
            PesoReferenceId = reference?.PesoReferenceId ?? Guid.Empty,
            PesoLoteId = lote?.PesoLoteId ?? Guid.Empty,
            RecordType = PesoRecordType.NovoControlo,
            MoldNumber = reference?.MoldNumber ?? context.Value.ReferenceText,
            NeckringNumber = reference?.NeckringNumber ?? string.Empty,
            ProductionCode = context.Value.ProductionCode,
            Line = context.Value.MachineCode,
            Lote = context.Value.CmLoteText ?? lote?.Lote ?? string.Empty,
            Processo = processo,
            ConstanteGlassUsada = constante,
            ControlDate = request.ControlDate,
            JobOnId = context.Value.JobOnId,
            JobOnRevisionId = context.Value.RevisionId,
            Status = PesoControlState.Rascunho,
            Revision = 1,
            TemperaturaC = request.TemperaturaC,
            EstadoMolde = request.EstadoMolde,
            Notas = request.Notas,
            PesoNominal = lote?.NominalWeight,
            CreatedAtUtc = _clock.UtcNow,
            CreatedBy = gate.Value.ActorId,
            Leituras = MapLeituras(request.Leituras)
        };

        await PopulateGlassWeightsAsync(control, ct);
        var id = await _repository.CreateControlAsync(control, ct);
        await _repository.InsertAuditEventAsync(id, "peso.controlo.criar", null, null, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> SaveControlAsync(
        SaveControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        var editValidation = PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(control.Status), request.ChangeReason);
        if (editValidation is not null)
            return Result<bool, DomainError>.Failure(DomainError.Validation(editValidation.Code, editValidation.Message));

        control.TemperaturaC = request.TemperaturaC;
        control.EstadoMolde = request.EstadoMolde;
        control.Notas = request.Notas;
        control.Leituras = MapLeituras(request.Leituras);
        await PopulateGlassWeightsAsync(control, ct);
        control.UpdatedAtUtc = _clock.UtcNow;
        await _repository.UpdateControlAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.guardar", null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> SubmitControlAsync(
        SubmitControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        var submit = control.Submit();
        if (submit.IsFailure) return Result<bool, DomainError>.Failure(submit.Error);
        await _repository.UpdateControlHeaderAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.submeter", null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Approval (Responsável: peso.aprovar) ------------------------------

    public async Task<Result<bool, DomainError>> ApproveControlAsync(
        ApproveControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        if (control.RecordType == PesoRecordType.Comparacao)
        {
            var decisionSnapshot = DeserializeComparisonDecisions(control.ComparisonDecisionsJson);
            if (decisionSnapshot is null || decisionSnapshot.Decisions.Count == 0 ||
                decisionSnapshot.Decisions.Any(d => d.Decision == PesoCmDecision.None))
                return Result<bool, DomainError>.Failure(DomainError.Validation(
                    "PESO_COMPARISON_UNDECIDED",
                    "Todos os CM precisam de decisão antes de confirmar."));
        }

        var approve = control.Approve(gate.Value.ActorId, _clock.UtcNow);
        if (approve.IsFailure) return Result<bool, DomainError>.Failure(approve.Error);

        await _repository.UpdateControlHeaderAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.aprovar", null, null, gate.Value.ActorId, ct);

        if (request.RegisterDayApproval)
            await _repository.SaveDayApprovalAsync(control.MoldNumber, control.NeckringNumber, control.Line, control.ControlDate, gate.Value.ActorId, ct);

        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> RejectControlAsync(
        RejectControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        var reject = control.Reject(request.Justification);
        if (reject.IsFailure) return Result<bool, DomainError>.Failure(reject.Error);

        await _repository.UpdateControlHeaderAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.nao_aprovar", null, request.Justification, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> ReopenControlAsync(
        ReopenControlRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        var reopen = control.Reopen(request.Reason, _clock.UtcNow);
        if (reopen.IsFailure) return Result<bool, DomainError>.Failure(reopen.Error);

        await _repository.UpdateControlHeaderAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.reabrir", null, request.Reason, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>Delete policy (GLM-PESO-06.7): rascunho/nao_aprovado only; the
    /// author (any module holder) OR a peso.aprovar holder; pendente/aprovado never.</summary>
    public async Task<Result<bool, DomainError>> DeleteControlAsync(
        DeleteControlRequest request, CancellationToken ct = default)
    {
        // Entry requires the module grant.
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        if (!control.IsDeletable)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_CONTROL_DELETE_STATE",
                "Apenas controlos em rascunho ou não aprovados podem ser eliminados."));

        var isAuthor = control.CreatedBy == gate.Value.ActorId;
        if (!isAuthor && !gate.Value.HasAprovarRole)
            return Result<bool, DomainError>.Failure(DomainError.Forbidden(
                "PESO_CONTROL_DELETE_UNAUTHORIZED",
                "Só o autor ou um Responsável pode eliminar este controlo."));

        await _repository.DeleteControlAsync(control.PesoControloId, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.controlo.eliminar", null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Comparison (GLM-PESO-06.4/5) ---------------------------------------

    public async Task<Result<Guid, DomainError>> CreateComparisonAsync(
        CreateComparisonRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var current = await _repository.GetControlByIdAsync(request.CurrentControlId, ct);
        if (current is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "PESO_COMPARISON_CURRENT_NOT_FOUND", "O Novo Controlo atual não foi encontrado."));
        if (current.RecordType != PesoRecordType.NovoControlo || current.Status != PesoControlState.Rascunho)
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_COMPARISON_CURRENT_NOT_DRAFT",
                "A comparação é criada dentro de um Novo Controlo ainda em rascunho."));

        var approved = await _repository.GetControlByIdAsync(request.PreviousApprovedControlId, ct);
        if (approved is null || approved.RecordType != PesoRecordType.NovoControlo ||
            approved.Status != PesoControlState.Aprovado)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_NO_APPROVED_BASE",
                "Selecione e confirme um Novo Controlo aprovado da produção anterior."));
        if (current.PesoControloId == approved.PesoControloId ||
            (current.JobOnId == approved.JobOnId && current.JobOnRevisionId == approved.JobOnRevisionId))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_SAME_PRODUCTION", "A produção anterior tem de ser diferente da produção atual."));
        if (!SameReference(current, approved))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_REFERENCE_MISMATCH", "A produção anterior tem de usar a mesma referência."));

        await PopulateGlassWeightsAsync(current, ct);
        await PopulateGlassWeightsAsync(approved, ct);

        var currentReadings = current.Leituras
            .Where(l => !string.IsNullOrWhiteSpace(l.CmNumber) && l.PesoVidro.HasValue)
            .ToDictionary(l => l.CmNumber.Trim(), StringComparer.OrdinalIgnoreCase);
        var previousReadings = approved.Leituras
            .Where(l => !string.IsNullOrWhiteSpace(l.CmNumber) && l.PesoVidro.HasValue)
            .ToDictionary(l => l.CmNumber.Trim(), StringComparer.OrdinalIgnoreCase);
        var pairs = request.Pairs ?? Array.Empty<PesoComparisonPairRequest>();
        if (currentReadings.Count == 0 || previousReadings.Count == 0 || pairs.Count == 0)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_NO_GLASS_WEIGHT",
                "Calcule o peso do vidro de ambas as produções antes de criar a comparação."));

        var normalizedPairs = pairs
            .Select(p => new PesoComparisonPairRequest(
                p.CurrentCmNumber?.Trim() ?? string.Empty,
                p.PreviousCmNumber?.Trim() ?? string.Empty))
            .ToList();
        if (normalizedPairs.Any(p => string.IsNullOrWhiteSpace(p.CurrentCmNumber) ||
                                     string.IsNullOrWhiteSpace(p.PreviousCmNumber)) ||
            normalizedPairs.Select(p => p.CurrentCmNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedPairs.Count ||
            normalizedPairs.Select(p => p.PreviousCmNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedPairs.Count ||
            normalizedPairs.Count != currentReadings.Count ||
            normalizedPairs.Any(p => !currentReadings.ContainsKey(p.CurrentCmNumber) ||
                                     !previousReadings.ContainsKey(p.PreviousCmNumber)) ||
            currentReadings.Keys.Any(cm => !normalizedPairs.Any(p => p.CurrentCmNumber.Equals(cm, StringComparison.OrdinalIgnoreCase))))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_PAIRING_INVALID",
                "Associe explicitamente cada CM atual a um único CM da produção anterior."));

        var snapshotRows = normalizedPairs.Select(pair =>
        {
            var currentWeight = currentReadings[pair.CurrentCmNumber].PesoVidro!.Value;
            var previousWeight = previousReadings[pair.PreviousCmNumber].PesoVidro!.Value;
            var (difference, percentage) = WeightCalculator.DeltaVs(currentWeight, previousWeight);
            return new PesoComparisonCmSnapshot
            {
                CurrentCmNumber = currentReadings[pair.CurrentCmNumber].CmNumber,
                PreviousCmNumber = previousReadings[pair.PreviousCmNumber].CmNumber,
                CurrentGlassWeight = currentWeight,
                PreviousGlassWeight = previousWeight,
                Difference = difference!.Value,
                DifferencePercent = percentage!.Value
            };
        }).ToList();

        var snapshot = new PesoComparisonSnapshot
        {
            CurrentControlId = current.PesoControloId,
            CurrentJobOnId = current.JobOnId,
            CurrentJobOnRevisionId = current.JobOnRevisionId,
            CurrentProductionCode = current.ProductionCode,
            CurrentLine = current.Line,
            CurrentLote = current.Lote,
            PreviousControlId = approved.PesoControloId,
            PreviousJobOnId = approved.JobOnId,
            PreviousJobOnRevisionId = approved.JobOnRevisionId,
            PreviousProductionCode = approved.ProductionCode,
            PreviousLine = approved.Line,
            PreviousLote = approved.Lote,
            CreatedAtUtc = _clock.UtcNow,
            CreatedBy = gate.Value.ActorId,
            Rows = snapshotRows
        };

        var control = new PesoControl
        {
            PesoControloId = Guid.NewGuid(),
            PesoReferenceId = approved.PesoReferenceId,
            PesoLoteId = approved.PesoLoteId,
            RecordType = PesoRecordType.Comparacao,
            MoldNumber = approved.MoldNumber,
            NeckringNumber = approved.NeckringNumber,
            ProductionCode = approved.ProductionCode,
            Line = approved.Line,
            Lote = approved.Lote,
            ControlDate = current.ControlDate,
            JobOnId = current.JobOnId,
            JobOnRevisionId = current.JobOnRevisionId,
            Status = PesoControlState.Rascunho,
            Revision = 1,
            TemperaturaC = current.TemperaturaC,
            Notas = request.Notas,
            DataRegistoComparacao = current.ControlDate,
            Processo = current.Processo,
            ConstanteGlassUsada = current.ConstanteGlassUsada,
            PesoNominal = current.PesoNominal,
            PreviousControlJson = System.Text.Json.JsonSerializer.Serialize(snapshot, ComparisonJsonOptions),
            ComparisonDecisionsJson = System.Text.Json.JsonSerializer.Serialize(
                new PesoComparisonDecisionSnapshot(), ComparisonJsonOptions),
            CreatedAtUtc = _clock.UtcNow,
            CreatedBy = gate.Value.ActorId,
            Leituras = snapshotRows.Select(row => new PesoLeitura
            {
                PesoLeituraId = Guid.NewGuid(),
                CmNumber = row.CurrentCmNumber,
                PesoEmAgua = currentReadings[row.CurrentCmNumber].PesoEmAgua,
                PesoVidro = row.CurrentGlassWeight
            }).ToList()
        };

        var id = await _repository.CreateControlAsync(control, ct);
        await _repository.InsertAuditEventAsync(
            id, "peso.comparacao.criar", approved.PesoControloId.ToString(), control.PreviousControlJson, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> ConfirmComparisonDecisionsAsync(
        ConfirmComparisonDecisionsRequest request, CancellationToken ct = default)
    {
        // Decidir CM a CM é uma operação do Responsável (GLM-PESO-02).
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));
        if (control.RecordType != PesoRecordType.Comparacao)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_NOT_COMPARISON", "Este registo não é uma Comparação."));

        var comparison = DeserializeComparisonSnapshot(control.PreviousControlJson);
        if (comparison is null || comparison.Rows.Count == 0)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "PESO_COMPARISON_SNAPSHOT_INVALID", "A comparação não contém um snapshot CM válido."));

        var requested = request.Decisions ?? Array.Empty<DecideComparisonCmRequest>();
        if (requested.Count != comparison.Rows.Count ||
            requested.Select(d => d.CmNumber?.Trim() ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count() != requested.Count ||
            comparison.Rows.Any(row => !requested.Any(d =>
                row.CurrentCmNumber.Equals(d.CmNumber?.Trim(), StringComparison.OrdinalIgnoreCase))))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_DECISIONS_MISMATCH", "Registe uma decisão para cada CM atual da comparação."));

        var decisions = comparison.Rows.Select(row =>
        {
            var requestedDecision = requested.Single(d =>
                row.CurrentCmNumber.Equals(d.CmNumber?.Trim(), StringComparison.OrdinalIgnoreCase));
            return new PesoComparisonCmDecision
            {
                CmNumber = row.CurrentCmNumber,
                Decision = requestedDecision.Decision,
                PesoAtual = row.CurrentGlassWeight
            };
        }).ToList();

        if (decisions.Any(d => d.Decision == PesoCmDecision.None))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_UNDECIDED",
                "Todos os CM precisam de decisão."));

        if (decisions.Any(d => d.Decision == PesoCmDecision.ColocarDeParte) && string.IsNullOrWhiteSpace(request.Justification))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PESO_COMPARISON_JUSTIFICATION_REQUIRED",
                "A justificação é obrigatória quando pelo menos um CM é colocado de parte."));

        var payload = new PesoComparisonDecisionSnapshot
        {
            Decisions = decisions,
            Justification = string.IsNullOrWhiteSpace(request.Justification) ? null : request.Justification.Trim()
        };
        control.ComparisonDecisionsJson = System.Text.Json.JsonSerializer.Serialize(payload, ComparisonJsonOptions);
        control.UpdatedAtUtc = _clock.UtcNow;
        await _repository.UpdateControlHeaderAsync(control, ct);
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.comparacao.decidir", null, control.ComparisonDecisionsJson, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Day approvals + settings ------------------------------------------

    public async Task<Result<bool, DomainError>> SaveDayApprovalAsync(
        SaveDayApprovalRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        await _repository.SaveDayApprovalAsync(request.Mold, request.Neckring, request.Line, request.ApprovalDate, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> SaveSettingsAsync(
        SaveSettingsRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(PesoModuleCatalog.PesoAprovarCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.JsonValue))
            return Result<bool, DomainError>.Failure(DomainError.Validation("PESO_SETTINGS_INVALID", "Configuração inválida."));

        await _repository.SaveSettingAsync(request.Key.Trim(), request.JsonValue, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Document / email (GLM-PESO-09, DS-08) ------------------------------

    public async Task<Result<GeneratedDocument, DomainError>> GenerateDocumentAsync(
        IPdfRenderer renderer, GenerateDocumentRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<GeneratedDocument, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<GeneratedDocument, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));
        if (control.Status != PesoControlState.Aprovado)
            return Result<GeneratedDocument, DomainError>.Failure(DomainError.Validation(
                "PESO_DOC_NOT_APPROVED", "Só a revisão aprovada pode gerar a folha de produção."));

        await PopulateGlassWeightsAsync(control, ct);
        var density = control.TemperaturaC is { } tc &&
                      WeightCalculator.LookupDensity(tc) is { IsSuccess: true, Value: var densityValue }
            ? (decimal?)densityValue
            : null;
        var (dNom, dNomPct) = WeightCalculator.DeltaVs(control.PesoMedio, control.PesoNominal);

        var comparison = control.RecordType == PesoRecordType.Comparacao
            ? DeserializeComparisonSnapshot(control.PreviousControlJson)
            : null;
        var cmRows = comparison is null
            ? control.Leituras.Select(reading => new PesoCmComparisonRow
            {
                CurrentCmNumber = reading.CmNumber,
                PesoAtual = reading.PesoVidro
            }).ToList()
            : comparison.Rows.Select(row => new PesoCmComparisonRow
            {
                CurrentCmNumber = row.CurrentCmNumber,
                PreviousCmNumber = row.PreviousCmNumber,
                PesoAtual = row.CurrentGlassWeight,
                PesoAnterior = row.PreviousGlassWeight,
                DeltaPeso = row.Difference,
                DeltaPesoPct = row.DifferencePercent
            }).ToList();

        var folha = new PesoFolhaPdf
        {
            IsComparison = comparison is not null,
            MoldNumber = control.MoldNumber,
            NeckringNumber = control.NeckringNumber,
            ProductionCode = control.ProductionCode,
            Line = control.Line,
            Lote = control.Lote,
            Revision = control.Revision,
            PesoMedio = control.PesoMedio,
            CapacidadeMedia = control.CapacidadeMedia,
            PesoNominal = control.PesoNominal,
            EstadoMolde = control.EstadoMolde,
            Processo = control.Processo?.ToString() ?? "—",
            ApprovedBy = control.ApprovedBy,
            ApprovedAtUtc = control.ApprovedAtUtc,
            PreviousProductionCode = comparison is null
                ? null
                : $"{comparison.PreviousProductionCode} · Linha {comparison.PreviousLine} · Lote {comparison.PreviousLote}",
            CmRows = cmRows,
            DeltaNominal = dNom,
            DeltaNominalPct = dNomPct,
            SapPesoMedio = control.PesoMedioAnteriorSap,
            SapPeriodo = string.IsNullOrWhiteSpace(control.FimProducaoAnteriorSap?.ToString("yyyyMM")) ? null : control.FimProducaoAnteriorSap.Value.ToString("yyyyMM"),
            TemperaturaC = control.TemperaturaC,
            Densidade = density,
            ConstanteGlassUsada = control.ConstanteGlassUsada
        };

        var pdfBytes = renderer.RenderPesoFolha(folha);
        var fileName = PesoFileName.Builder(control, "Peso");
        await _repository.InsertAuditEventAsync(control.PesoControloId, "peso.documento.gerar", null, fileName, gate.Value.ActorId, ct);
        return Result<GeneratedDocument, DomainError>.Success(new GeneratedDocument(pdfBytes, fileName));
    }

    public async Task<Result<PreparedEmail, DomainError>> PrepareEmailAsync(
        PrepareEmailRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<PreparedEmail, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(request.ControlId, ct);
        if (control is null)
            return Result<PreparedEmail, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));
        if (control.Status != PesoControlState.Aprovado)
            return Result<PreparedEmail, DomainError>.Failure(DomainError.Validation(
                "PESO_EMAIL_NOT_APPROVED", "Só a revisão aprovada prepara o email de produção."));

        var lineGroup = control.Line.StartsWith("B", StringComparison.OrdinalIgnoreCase) ? "Linha B" : "Linha C";
        var recipientsSetting = await _repository.GetSettingAsync($"email_recipients_{lineGroup.Replace(" ", "").ToLowerInvariant()}", ct);
        if (string.IsNullOrWhiteSpace(recipientsSetting))
            return Result<PreparedEmail, DomainError>.Failure(DomainError.Validation(
                "PESO_EMAIL_NO_RECIPIENTS",
                "Configuração de destinatários em falta. A aprovação mantém-se válida; o envio fica bloqueado."));

        var subject = $"Controlo de Peso e Volume · {control.MoldNumber}{control.NeckringNumber} · {control.ProductionCode} · {control.Line} · Lote {control.Lote}";
        var attachment = PesoFileName.Builder(control, "Peso");
        return Result<PreparedEmail, DomainError>.Success(new PreparedEmail(
            control.Line, lineGroup, recipientsSetting, subject, attachment));
    }

    // ---- Queries ---------------------------------------------------------------

    public async Task<Result<IReadOnlyList<PesoReferenceSummary>, DomainError>> ListReferencesAsync(
        string? search = null, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<PesoReferenceSummary>, DomainError>.Failure(gate.Error);

        var references = await _repository.GetReferencesAsync(search, ct);
        var summaries = references.Select(r => new PesoReferenceSummary
        {
            PesoReferenceId = r.PesoReferenceId,
            MoldNumber = r.MoldNumber,
            NeckringNumber = r.NeckringNumber,
            CounterMold = r.CounterMold
        }).ToList();
        return Result<IReadOnlyList<PesoReferenceSummary>, DomainError>.Success(summaries);
    }

    public async Task<Result<IReadOnlyList<PesoControlListItem>, DomainError>> SearchControlsAsync(
        ControlFilterRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<PesoControlListItem>, DomainError>.Failure(gate.Error);

        var controls = await _repository.GetControlsAsync(
            request.ReferenceId, request.Search, request.Status, request.Type, request.From, request.To, ct);
        var items = controls
            .Where(c => c.Status != PesoControlState.Rascunho)
            .Select(c => new PesoControlListItem(
                c.PesoControloId, c.RecordType, $"{c.MoldNumber}{c.NeckringNumber}", c.ProductionCode,
                c.Line, c.Lote, c.PesoMedio, c.Revision, c.Status, c.ControlDate))
            .ToList();
        return Result<IReadOnlyList<PesoControlListItem>, DomainError>.Success(items);
    }

    public async Task<Result<IReadOnlyList<string>, DomainError>> GetRecordDatesAsync(
        int year, int month, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<string>, DomainError>.Failure(gate.Error);
        return Result<IReadOnlyList<string>, DomainError>.Success(
            await _repository.GetRecordDatesAsync(year, month, ct));
    }

    public async Task<Result<string?, DomainError>> GetSettingAsync(
        string key, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<string?, DomainError>.Failure(gate.Error);
        return Result<string?, DomainError>.Success(await _repository.GetSettingAsync(key, ct));
    }

    /// <summary>Full control detail for approval sheet / history view.</summary>
    public async Task<Result<PesoControl?, DomainError>> GetControlDetailAsync(
        Guid controlId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<PesoControl?, DomainError>.Failure(gate.Error);
        var control = await _repository.GetControlByIdAsync(controlId, ct);
        if (control is null)
            return Result<PesoControl?, DomainError>.Failure(DomainError.NotFound("PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));
        await PopulateGlassWeightsAsync(control, ct);
        return Result<PesoControl?, DomainError>.Success(control);
    }

    // ---- Calculation result for the live preview (single C# engine) -------

    public async Task<Result<PesoCalculationResult, DomainError>> GetControlForCalculationAsync(
        Guid controlId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<PesoCalculationResult, DomainError>.Failure(gate.Error);

        var control = await _repository.GetControlByIdAsync(controlId, ct);
        if (control is null)
            return Result<PesoCalculationResult, DomainError>.Failure(DomainError.NotFound(
                "PESO_CONTROL_NOT_FOUND", "Controlo não encontrado."));

        await PopulateGlassWeightsAsync(control, ct);

        decimal? density = null;
        if (control.TemperaturaC is { } temp)
        {
            var d = WeightCalculator.LookupDensity(temp);
            if (d.IsSuccess) density = d.Value;
        }

        // OC-6: use the density actually used for this control when it was
        // calculated/saved (historical integrity); only fall back to the current
        // configured value when the control has no saved constant yet (live
        // preview before save). Never a hardcoded constant in the calc path.
        var processo = control.Processo ?? PesoProcesso.Nnpb;
        var constant = control.ConstanteGlassUsada ?? await ResolveProcessDensityAsync(processo, ct);

        var rows = (control.Leituras ?? Array.Empty<PesoLeitura>())
            .Where(l => l.PesoEmAgua.HasValue)
            .Select(l =>
            {
                var capacidade = WeightCalculator.VolumeFromWeight(l.PesoEmAgua, density);
                return new PesoCalculationRow
                {
                    CmNumber = l.CmNumber,
                    PesoEmAgua = l.PesoEmAgua,
                    Capacidade = capacidade,
                    PesoVidro = l.PesoVidro
                };
            })
            .ToList();

        var pesoMedio = WeightCalculator.GlassAverage(rows.Select(row => row.PesoVidro).ToList());
        var capacidadeMedia = WeightCalculator.GlassAverage(rows.Select(row => row.Capacidade).ToList());
        var (dif, difPct) = WeightCalculator.DeltaVs(pesoMedio, control.PesoNominal);
        return Result<PesoCalculationResult, DomainError>.Success(new PesoCalculationResult
        {
            Densidade = density,
            ConstanteGlassUsada = constant,
            PesoMedio = pesoMedio,
            CapacidadeMedia = capacidadeMedia,
            PesoNominal = control.PesoNominal,
            Diferenca = dif,
            DiferencaPct = difPct,
            Rows = rows
        });
    }

    // ---- helpers ---------------------------------------------------------------

    private static IReadOnlyList<PesoLeitura> MapLeituras(IReadOnlyList<PesoLeituraInput> inputs) =>
        (inputs ?? Array.Empty<PesoLeituraInput>()).Select(i => new PesoLeitura
        {
            PesoLeituraId = Guid.NewGuid(),
            CmNumber = i.CmNumber,
            PesoEmAgua = i.PesoEmAgua
        }).ToList();

    private async Task PopulateGlassWeightsAsync(PesoControl control, CancellationToken ct)
    {
        PesoReference? reference = null;
        if (control.PesoReferenceId != Guid.Empty)
            reference = await _repository.GetReferenceByIdAsync(control.PesoReferenceId, ct);

        decimal? density = null;
        if (control.TemperaturaC is { } temperature)
        {
            var densityResult = WeightCalculator.LookupDensity(temperature);
            if (densityResult.IsSuccess) density = densityResult.Value;
        }

        var process = control.Processo ?? PesoProcesso.Nnpb;
        var constant = control.ConstanteGlassUsada ?? await ResolveProcessDensityAsync(process, ct);
        control.ConstanteGlassUsada ??= constant;
        control.Leituras = (control.Leituras ?? Array.Empty<PesoLeitura>())
            .Select(reading => reading with
            {
                PesoVidro = WeightCalculator.EstimateGlassWeight(
                    WeightCalculator.VolumeFromWeight(reading.PesoEmAgua, density),
                    reference?.VolumeNeck,
                    reference?.VolumePu,
                    constant)
            })
            .ToList();
    }

    private static bool SameReference(PesoControl current, PesoControl previous)
    {
        if (current.PesoReferenceId != Guid.Empty && previous.PesoReferenceId != Guid.Empty)
            return current.PesoReferenceId == previous.PesoReferenceId;
        return current.MoldNumber.Equals(previous.MoldNumber, StringComparison.OrdinalIgnoreCase) &&
               current.NeckringNumber.Equals(previous.NeckringNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static PesoComparisonSnapshot? DeserializeComparisonSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PesoComparisonSnapshot>(json, ComparisonJsonOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static PesoComparisonDecisionSnapshot? DeserializeComparisonDecisions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PesoComparisonDecisionSnapshot>(json, ComparisonJsonOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private async Task<PesoReference?> FindReferenceByTextAsync(string text, CancellationToken ct)
    {
        // The reference text (e.g. "5447T173") is a presentation key; the stable
        // link uses the Peso reference id resolved by mold+neckring when present.
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim();
        var split = 0;
        while (split < normalized.Length && char.IsDigit(normalized[split])) split++;
        if (split > 0 && split < normalized.Length)
            return await _repository.GetReferenceByMoldNeckringAsync(
                normalized[..split], normalized[split..], ct);
        return null;
    }

    private static string AppendNote(string json, string actorId, string reason, IClock clock)
    {
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json) ?? new();
            list.Add(new Dictionary<string, object?>
            {
                ["actor"] = actorId,
                ["at_utc"] = clock.UtcNow,
                ["reason"] = reason
            });
            return System.Text.Json.JsonSerializer.Serialize(list);
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// Resolves the configured glass density (constant) for a process (OC-6).
    /// Reads peso_settings constant_nnpb/constant_ps; falls back to the default
    /// when unset. Calculation always uses the configured value — never a
    /// hardcoded constant in the calc path.
    /// </summary>
    private async Task<decimal> ResolveProcessDensityAsync(PesoProcesso processo, CancellationToken ct)
    {
        var key = processo == PesoProcesso.Nnpb ? "constant_nnpb" : "constant_ps";
        var stored = await _repository.GetSettingAsync(key, ct);
        if (decimal.TryParse(stored?.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        return processo == PesoProcesso.Nnpb
            ? PesoModuleCatalog.ConstantNnpb
            : PesoModuleCatalog.ConstantPs;
    }
}
public sealed record PreparedEmail(
    string Machine,
    string LineGroup,
    string Recipients,
    string Subject,
    string AttachmentFileName);

/// <summary>
/// Deterministic Peso PDF filename convention (TD-31, GLM-PESO-09):
/// <c>{mold}{neckring}__{periodo}__{line}__L{lote}.pdf</c> — double separators,
/// <c>L</c> prefix on lot, lowercase extension. Confirmed reference
/// <c>9262T288__202604__C3__L16.pdf</c>.
/// </summary>
public static class PesoFileName
{
    public static string Builder(PesoControl control, string documentType)
    {
        var mold = string.IsNullOrWhiteSpace(control.MoldNumber) ? "n" : control.MoldNumber.Trim();
        var neck = string.IsNullOrWhiteSpace(control.NeckringNumber) ? "" : control.NeckringNumber.Trim();
        var periodo = string.IsNullOrWhiteSpace(control.ProductionCode) ? "periodo" : control.ProductionCode.Trim();
        var line = string.IsNullOrWhiteSpace(control.Line) ? "L" : control.Line.Trim();
        var lote = string.IsNullOrWhiteSpace(control.Lote) ? "" : control.Lote.Trim();
        _ = documentType; // reserved (Peso/Pegamentos differ by folder, not name here)
        return $"{mold}{neck}__{periodo}__{line}__L{lote}.pdf";
    }
}
