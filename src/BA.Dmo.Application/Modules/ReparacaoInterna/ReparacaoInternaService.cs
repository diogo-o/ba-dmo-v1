using System.Text.Json;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna application service (OWNER DECISION; supersedes the earlier
/// GLM-RI-01..12 hard-block wording for this module). Registers quick in-turn repair facts
/// (CM | MF | BQ) enriched automatically with the effective production context of the line.
///
/// R009 behavior (authoritative):
/// - Production activation: most recent start date activated at 09:00 local factory,
///   line-scoped, NO end-date test; deterministic from persisted starts (GAP 1 fix).
/// - NO operational hard blocks: if the effective context cannot be resolved, the record is
///   still saved with empty/unknown context. Lot/reference/number mismatches are
///   information only, never blocks.
/// - Repeated numbers are VALID occurrences; each number persists as its own record under
///   the same context. Never deduplicated.
/// - Exact historical context (job_on_revision_id + production + reference + lot) is
///   persisted at save time so history never depends on current_revision_id (GAP 2 fix).
/// - Override/correction never modifies Job On.
///
/// Ownership is preserved: Ferramentas identity is only READ via the read-only
/// <see cref="IFerramentasPieceLookup"/>; Job On context is only READ via
/// <see cref="IJobOnActiveContextLookup"/>; no Armazém, tool or Job On write ever happens
/// here. Each record, its repair_event and the global audit_events row commit in ONE
/// <see cref="IDbUnitOfWork"/>.
/// </summary>
public sealed class ReparacaoInternaService
{
    private readonly IReparacaoInternaRepository _repository;
    private readonly IJobOnActiveContextLookup _activeContextLookup;
    private readonly IFerramentasPieceLookup _pieceLookup;
    private readonly IRepairUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ReparacaoInternaAuthorizationGate _gate;
    private readonly IClock _clock;

    public ReparacaoInternaService(
        IReparacaoInternaRepository repository,
        IJobOnActiveContextLookup activeContextLookup,
        IFerramentasPieceLookup pieceLookup,
        IRepairUnitOfWorkFactory unitOfWorkFactory,
        ReparacaoInternaAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _activeContextLookup = activeContextLookup;
        _pieceLookup = pieceLookup;
        _unitOfWorkFactory = unitOfWorkFactory;
        _gate = gate;
        _clock = clock;
    }

    // ---- Line cards (Registo tab selector) -----------------------------------

    /// <summary>
    /// Resolves the effective context of every line (B1–C3) at the current time so the
    /// full-width line-card selector can show each line's active reference or
    /// 'Sem Job On ativo'. Read-only. R009: the reference shown must be the effective
    /// production's reference (<see cref="InternalRepairLineCard.HasActiveContext"/>).
    /// </summary>
    public async Task<Result<IReadOnlyList<InternalRepairLineCard>, DomainError>> ListLineCardsAsync(
        CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<IReadOnlyList<InternalRepairLineCard>, DomainError>.Failure(gate.Error);

        var now = _clock.UtcNow;
        var cards = new List<InternalRepairLineCard>();
        foreach (var line in ReparacaoInternaModuleCatalog.Lines)
        {
            var resolution = await _activeContextLookup.ResolveActiveAsync(line, now, ct);
            if (resolution.Kind == InternalRepairResolutionKind.Single && resolution.Context is not null)
            {
                cards.Add(new InternalRepairLineCard(
                    line, resolution.Context.Reference, resolution.Context.ProductionCode, true));
            }
            else
            {
                cards.Add(new InternalRepairLineCard(line, null, null, false));
            }
        }
        return Result<IReadOnlyList<InternalRepairLineCard>, DomainError>.Success(cards.AsReadOnly());
    }

    // ---- Context resolution ---------------------------------------------------

    /// <summary>
    /// Resolves the effective production context of a line at the current time using the
    /// R009 activation rule. Returns Single (auto-prefill), None (empty/unknown context,
    /// still recordable) or Ambiguous (explicit choice). Read-only.
    /// </summary>
    public async Task<Result<InternalRepairContextDto, DomainError>> ResolveLineContextAsync(
        string line, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<InternalRepairContextDto, DomainError>.Failure(gate.Error);

        var resolution = await _activeContextLookup.ResolveActiveAsync(line, _clock.UtcNow, ct);
        var dto = new InternalRepairContextDto(
            resolution.Kind,
            resolution.Context?.JobOnId,
            resolution.Context?.JobOnRevisionId,
            resolution.Context?.ProductionCode,
            resolution.Context?.Reference,
            resolution.Context?.MachineCode,
            resolution.Context?.ActivatedFromUtc,
            resolution.Context?.ValidToUtc,
            resolution.Candidates
                .Select(c => new InternalRepairCandidateDto(
                    c.JobOnId, c.JobOnRevisionId, c.ProductionCode, c.Reference, c.MachineCode,
                    c.ValidFromUtc, c.ValidToUtc))
                .ToList()
                .AsReadOnly());
        return Result<InternalRepairContextDto, DomainError>.Success(dto);
    }

    // ---- Register --------------------------------------------------------------

    /// <summary>
    /// R009 — Registers one or more internal repair facts on the line. NO hard blocks:
    /// the effective context is auto-filled when a Single resolution exists, otherwise the
    /// facts are persisted with empty/unknown context. Each number in
    /// <paramref name="request"/>.Numbers is persisted as its OWN occurrence record sharing
    /// the same line/type/context/operator/timestamp; repeated numbers remain separate rows.
    /// Returns the list of persisted record ids (order = input order).
    /// </summary>
    public async Task<Result<IReadOnlyList<Guid>, DomainError>> RegistrarReparacoesAsync(
        RegisterReparacaoRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<IReadOnlyList<Guid>, DomainError>.Failure(gate.Error);

        var numbers = (request.Numbers ?? Array.Empty<string>())
            .Select(n => n?.Trim() ?? string.Empty);
        if (!numbers.Any())
            return Result<IReadOnlyList<Guid>, DomainError>.Failure(DomainError.Validation(
                "REPINT_NUMBER_REQUIRED", "Introduza pelo menos um número individual."));

        var now = _clock.UtcNow;

        // Auto-context is assistance: Single → prefill; None/Ambiguous → record with null context.
        InternalRepairContext? context = null;
        var resolution = await _activeContextLookup.ResolveActiveAsync(request.Line, now, ct);
        if (resolution.Kind == InternalRepairResolutionKind.Single)
            context = resolution.Context;

        var recordIds = new List<Guid>();
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            foreach (var number in numbers)
            {
                // R009: auto-context is the DEFAULT/suggested context. When the operator
                // overrides production/reference (Editar contexto), those are the confirmed
                // facts; the auto job_on/revision/lot remain the (suggested) linkage and are
                // never used to override reality or modify Job On.
                var lotId = !string.IsNullOrWhiteSpace(request.OverrideReference)
                    ? null
                    : await ResolveEffectiveLotIdAsync(request.ToolType, number, context, ct);

                var recordResult = InternalRepairRecord.Create(
                    request.Line,
                    context?.JobOnId,
                    context?.JobOnRevisionId,
                    string.IsNullOrWhiteSpace(request.OverrideProduction) ? context?.ProductionCode : request.OverrideProduction.Trim(),
                    string.IsNullOrWhiteSpace(request.OverrideReference) ? context?.Reference : request.OverrideReference.Trim(),
                    lotId,
                    request.ToolType,
                    number,
                    gate.Value.ActorId,
                    now,
                    now);
                if (recordResult.IsFailure)
                    return Result<IReadOnlyList<Guid>, DomainError>.Failure(recordResult.Error);

                var record = recordResult.Value;
                var recordId = await _repository.InsertAsync(uow, record, ct);
                await _repository.InsertRepairEventAsync(uow, recordId, number, gate.Value.ActorId, now, ct);
                await _repository.InsertAuditEventAsync(
                    uow,
                    "reparacao_interna.registrar",
                    "reparacao_interna",
                    recordId.ToString(),
                    record.JobOnId,
                    "succeeded",
                    null,
                    Serialize(record),
                    gate.Value.ActorId,
                    now,
                    ct);
                recordIds.Add(recordId);
            }

            await uow.CommitAsync(ct);
            return Result<IReadOnlyList<Guid>, DomainError>.Success(recordIds.AsReadOnly());
        }
        catch (Exception)
        {
            // Save failure: preserve input (nothing persisted), no false success.
            return Result<IReadOnlyList<Guid>, DomainError>.Failure(DomainError.Unexpected(
                "REPINT_SAVE_FAILED", "Falha ao guardar o registo; os dados introduzidos foram preservados."));
        }
    }

    /// <summary>Back-compat single-number entry point; delegates to the multi-number path.</summary>
    public Task<Result<IReadOnlyList<Guid>, DomainError>> RegisterReparacaoAsync(
        RegisterReparacaoRequest request, CancellationToken ct = default)
        => RegistrarReparacoesAsync(request, ct);

    // ---- History ----------------------------------------------------------------

    public async Task<Result<IReadOnlyList<InternalRepairHistoryRow>, DomainError>> ListHistoryAsync(
        InternalRepairFilter filter, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<IReadOnlyList<InternalRepairHistoryRow>, DomainError>.Failure(gate.Error);

        var records = await _repository.ListAsync(
            filter.From, filter.To, filter.Line, filter.JobOnId, filter.ToolType,
            filter.Number, filter.OperatorId, filter.OnlyCorrected, ct);

        // R009 GAP 2 fix: history uses the PERSISTED production/reference/lot snapshot on the
        // record and never re-resolves against current Job On data, so a later Job On revision
        // cannot reinterpret an old repair record. Legacy rows (no snapshot) show the persisted
        // core facts; the context columns are null (displayed as '—'), never fabricated.
        var rows = records
            .Select(r => new InternalRepairHistoryRow(
                r.InternalRepairRecordId,
                r.OccurredAtUtc,
                r.Line,
                r.ProductionCode,
                r.Reference,
                r.Reference,
                InternalRepairToolTypeCodec.ToStorage(r.ToolType),
                r.IndividualNumber,
                r.OperatorId,
                r.IsCorrection,
                r.CorrectionOfId ?? r.InternalRepairRecordId))
            .ToList()
            .AsReadOnly();
        return Result<IReadOnlyList<InternalRepairHistoryRow>, DomainError>.Success(rows);
    }

    // ---- Detail ------------------------------------------------------------------

    public async Task<Result<InternalRepairDetailDto, DomainError>> GetDetailAsync(
        Guid recordId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<InternalRepairDetailDto, DomainError>.Failure(gate.Error);

        var record = await _repository.GetByIdAsync(recordId, ct);
        if (record is null)
            return Result<InternalRepairDetailDto, DomainError>.Failure(DomainError.NotFound(
                "REPINT_NOT_FOUND", "Registo de Reparação Interna não encontrado."));

        var rootId = record.CorrectionOfId ?? record.InternalRepairRecordId;
        var chain = await _repository.GetChainAsync(rootId, ct);
        var chainDtos = chain.Select(r => BuildDetail(r)).ToList();
        return Result<InternalRepairDetailDto, DomainError>.Success(
            BuildDetail(record, chainDtos.AsReadOnly()));
    }

    // ---- Correction ---------------------------------------------------------------

    /// <summary>
    /// R009 — Corrects/overrides an internal repair record (capability
    /// <c>reparacao_interna.corrigir</c>). A correction is a NEW record (GLM-DATA-07); the
    /// original is never mutated and Job On is NEVER modified. R009: no operational hard
    /// block — the operator may override the auto-derived context (job_on_id/revision/
    /// production/reference/lot) to record reality, with the suggested values preserved in
    /// the audit <c>before_snapshot</c>. Original operator and occurred-at stay read-only.
    /// </summary>
    public async Task<Result<Guid, DomainError>> CorrigirReparacaoAsync(
        CorrigirReparacaoRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        var corrigir = _gate.RequireCorrigir(gate.Value.ActorId);
        if (corrigir.IsFailure)
            return Result<Guid, DomainError>.Failure(corrigir.Error);

        var original = await _repository.GetByIdAsync(request.RecordId, ct);
        if (original is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "REPINT_NOT_FOUND", "Registo de Reparação Interna não encontrado."));

        if (original.IsCorrection)
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "REPINT_CORRECTION_CHAIN",
                "Não é possível corrigir uma correção existente; corrija o registo original."));

        var correctionAtUtc = _clock.UtcNow;
        var beforeSnapshot = Serialize(original);

        // C3 / R009 — recalibrate the production context when the correction MOVES to a
        // different line. Auto-context is assistance and never a block: for each context field
        // the operator left null, the NEW line's Single active production becomes the assisted
        // default; an explicit operator override always wins; any field still null is persisted
        // as null for the new line rather than inheriting the ORIGINAL line's context. Never
        // modifies Job On.
        var lineChanged = !string.Equals(
            request.Line?.Trim(), original.Line?.Trim(), StringComparison.OrdinalIgnoreCase);
        var recalibrate = lineChanged;

        Guid? targetJobOn = request.JobOnId;
        Guid? targetRevision = request.JobOnRevisionId;
        string? targetProduction = request.ProductionCode;
        string? targetReference = request.Reference;
        Guid? targetLot = request.LotId;

        if (recalibrate)
        {
            var resolution = await _activeContextLookup.ResolveActiveAsync(request.Line, correctionAtUtc, ct);
            var resolved = resolution.Kind == InternalRepairResolutionKind.Single ? resolution.Context : null;
            if (request.JobOnId is null) targetJobOn = resolved?.JobOnId;
            if (request.JobOnRevisionId is null) targetRevision = resolved?.JobOnRevisionId;
            if (request.ProductionCode is null) targetProduction = resolved?.ProductionCode;
            if (request.Reference is null) targetReference = resolved?.Reference;
            if (request.LotId is null)
                targetLot = resolved is not null && request.ToolType == InternalRepairToolType.BQ && resolved.BqLotIds.Count == 1
                    ? resolved.BqLotIds[0]
                    : null;
        }

        var correctionResult = original.CreateCorrection(
            request.Line, request.ToolType, request.IndividualNumber,
            targetJobOn, targetRevision, targetProduction, targetReference,
            targetLot, corrigir.Value.ActorId, request.Reason, correctionAtUtc, beforeSnapshot,
            recalibrateContext: recalibrate);
        if (correctionResult.IsFailure)
            return Result<Guid, DomainError>.Failure(correctionResult.Error);

        var correction = correctionResult.Value;

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
        try
        {
            var correctionId = await _repository.InsertAsync(uow, correction, ct);
            await _repository.InsertRepairEventAsync(uow, correctionId, request.IndividualNumber, corrigir.Value.ActorId, correctionAtUtc, ct);
            await _repository.InsertAuditEventAsync(
                uow,
                "reparacao_interna.corrigir",
                "reparacao_interna",
                original.InternalRepairRecordId.ToString(),
                correction.JobOnId,
                "corrected",
                beforeSnapshot,
                Serialize(correction),
                corrigir.Value.ActorId,
                correctionAtUtc,
                ct);

            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(correctionId);
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "REPINT_SAVE_FAILED", "Falha ao guardar a correção; os dados introduzidos foram preservados."));
        }
    }

    // ---- Private helpers -----------------------------------------------------------

    /// <summary>
    /// Best-effort effective lot for the number/type: CM/MF resolve via the read-only
    /// Ferramentas piece lookup (the piece's parent lot); BQ uses the context's BQ lot when
    /// exactly one exists. NEVER blocks; returns null when not resolvable.
    /// </summary>
    private async Task<Guid?> ResolveEffectiveLotIdAsync(
        InternalRepairToolType type, string number, InternalRepairContext? context, CancellationToken ct)
    {
        if (type == InternalRepairToolType.BQ)
            return context is not null && context.BqLotIds.Count == 1 ? context.BqLotIds[0] : null;

        if (!TryMapToFerramentas(type, out var ferramentasType))
            return null;

        var hits = await _pieceLookup.SearchAsync(ferramentasType.Value, null, null, number, ct);
        var exact = hits.FirstOrDefault(h =>
            string.Equals(h.Number?.Trim(), number.Trim(), StringComparison.Ordinal));
        return exact?.ToolLoteId;
    }

    private static InternalRepairDetailDto BuildDetail(
        InternalRepairRecord record, IReadOnlyList<InternalRepairDetailDto>? chain = null) =>
        new(
            record.InternalRepairRecordId,
            record.Line,
            record.JobOnId,
            record.JobOnRevisionId,
            record.ProductionCode,
            record.Reference,
            record.Reference,
            InternalRepairToolTypeCodec.ToStorage(record.ToolType),
            record.IndividualNumber,
            record.OperatorId,
            record.OccurredAtUtc,
            record.IsCorrection,
            record.CorrectionReason,
            record.IsCorrection ? record.CreatedAtUtc : (DateTimeOffset?)null,
            record.IsCorrection ? record.CreatedBy : null,
            chain ?? Array.Empty<InternalRepairDetailDto>());

    private static bool TryMapToFerramentas(InternalRepairToolType type, out FerramentasToolType? ferramentasType)
    {
        switch (type)
        {
            case InternalRepairToolType.CM:
                ferramentasType = FerramentasToolType.CM;
                return true;
            case InternalRepairToolType.MF:
                ferramentasType = FerramentasToolType.MF;
                return true;
            default:
                ferramentasType = null;
                return false;
        }
    }

    private static string Serialize(InternalRepairRecord record) =>
        JsonSerializer.Serialize(new
        {
            record.Line,
            record.JobOnId,
            record.JobOnRevisionId,
            record.ProductionCode,
            record.Reference,
            record.LotId,
            tool_type = InternalRepairToolTypeCodec.ToStorage(record.ToolType),
            record.IndividualNumber,
            record.OperatorId,
            record.OccurredAtUtc
        });
}