using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// Ferramentas application service — use cases for creation, duplication,
/// editing, registration, verification-rule configuration and queries.
/// Enforces: atomic reference+lote; duplication copies CONFIGURATION only with a
/// read-only master identity; rules are per-lot config with future-only edits;
/// module <c>ferramentas</c> entry; <c>ferramentas.configure</c> for rule config.
/// </summary>
public sealed class FerramentasService
{
    private readonly IFerramentasRepository _repository;
    private readonly IFerramentasRuleLookup _ruleLookup;
    private readonly FerramentasAuthorizationGate _gate;
    private readonly IClock _clock;

    public FerramentasService(
        IFerramentasRepository repository,
        IFerramentasRuleLookup ruleLookup,
        FerramentasAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _ruleLookup = ruleLookup;
        _gate = gate;
        _clock = clock;
    }

    // ---- Create reference + first lot (atomic) -----------------------------

    public async Task<Result<FerramentasReferenceDetail, DomainError>> CreateReferenceWithFirstLoteAsync(
        CreateFerramentasRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<FerramentasReferenceDetail, DomainError>.Failure(gate.Error);

        var now = _clock.UtcNow;
        var referenceResult = ToolReference.Create(
            request.ToolType, request.RefCode, request.TechnicalName, request.OwnerPlant, now, gate.Value.ActorId);
        if (referenceResult.IsFailure) return Result<FerramentasReferenceDetail, DomainError>.Failure(referenceResult.Error);

        var existing = await _repository.GetReferenceByTypeAndCodeAsync(request.ToolType, request.RefCode, ct);
        if (existing is not null)
            return Result<FerramentasReferenceDetail, DomainError>.Failure(DomainError.DomainConflict(
                "FERRAMENTAS_DUPLICATE_REFERENCE",
                "Já existe uma referência com este tipo e código."));

        var loteResult = ToolLote.CreateInitial(
            referenceResult.Value.ToolReferenceId,
            request.Lote, request.Qty, request.AllowedLines,
            request.DrawingCode, request.DrawingRevision, request.Processo, now, gate.Value.ActorId);
        if (loteResult.IsFailure) return Result<FerramentasReferenceDetail, DomainError>.Failure(loteResult.Error);

        var (referenceId, loteId) = await _repository.CreateReferenceWithFirstLoteAsync(
            referenceResult.Value, loteResult.Value, ct);

        await _repository.InsertAuditEventAsync(referenceId, "ferramentas.referencia.criar",
            null, loteId.ToString(), gate.Value.ActorId, ct);

        return await BuildDetailAsync(referenceId, ct);
    }

    // ---- Edit master reference (audited, not retroactive) ------------------

    public async Task<Result<bool, DomainError>> EditReferenceAsync(
        EditFerramentasRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var reference = await _repository.GetReferenceByIdAsync(request.ReferenceId, ct);
        if (reference is null) return NotFound<bool>();

        var editResult = reference.EditEditableFields(request.TechnicalName, request.OwnerPlant, _clock.UtcNow, gate.Value.ActorId);
        if (editResult.IsFailure) return Result<bool, DomainError>.Failure(editResult.Error);

        await _repository.UpdateReferenceAsync(reference, ct);
        await _repository.InsertAuditEventAsync(request.ReferenceId, "ferramentas.referencia.editar",
            null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Edit lot (audited, not retroactive) -------------------------------

    public async Task<Result<bool, DomainError>> EditLoteAsync(
        EditLoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(request.LoteId, ct);
        if (lote is null) return NotFound<bool>();

        var editResult = lote.EditEditableFields(
            request.Qty, request.AllowedLines, request.DrawingCode, request.DrawingRevision, _clock.UtcNow, gate.Value.ActorId);
        if (editResult.IsFailure) return Result<bool, DomainError>.Failure(editResult.Error);

        await _repository.UpdateLoteAsync(lote, ct);
        await _repository.InsertAuditEventAsync(request.LoteId, "ferramentas.lote.editar",
            null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Duplicate lot (configuration only; master identity read-only) -----

    public async Task<Result<FerramentasLoteItem, DomainError>> CreateLoteFromBaseAsync(
        CreateLoteFromBaseRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<FerramentasLoteItem, DomainError>.Failure(gate.Error);

        var baseLote = await _repository.GetLoteByIdAsync(request.BaseLoteId, ct);
        if (baseLote is null) return NotFound<FerramentasLoteItem>();

        if (await _repository.LoteExistsInReferenceAsync(baseLote.ToolReferenceId, request.Lote, ct))
            return Result<FerramentasLoteItem, DomainError>.Failure(DomainError.DomainConflict(
                "FERRAMENTAS_DUPLICATE_LOTE",
                "Já existe um lote com este número nesta referência."));

        var newLoteResult = ToolLote.CreateFromBase(
            baseLote.ToolReferenceId, baseLote.ToolLoteId,
            request.Lote, request.Qty, request.AllowedLines,
            request.DrawingCode, request.DrawingRevision, baseLote.Processo,
            _clock.UtcNow, gate.Value.ActorId);
        if (newLoteResult.IsFailure) return Result<FerramentasLoteItem, DomainError>.Failure(newLoteResult.Error);

        var newLoteId = await _repository.CreateLoteAsync(newLoteResult.Value, ct);

        // Copy active rules as CONFIGURATION only (never occurrences/checks/history).
        var sourceRules = await _repository.GetCheckRulesByLoteAsync(baseLote.ToolLoteId, ct);
        foreach (var rule in sourceRules.Where(r => r.Active))
        {
            var copy = ToolCheckRule.Create(
                newLoteId, rule.RuleText, rule.Frequency, rule.ToolCheckRuleId, _clock.UtcNow, gate.Value.ActorId);
            if (copy.IsSuccess)
                await _repository.AddCheckRuleAsync(copy.Value, ct);
        }

        await _repository.InsertAuditEventAsync(baseLote.ToolLoteId, "ferramentas.lote.duplicar",
            baseLote.ToolLoteId.ToString(), newLoteId.ToString(), gate.Value.ActorId, ct);

        var saved = await _repository.GetLoteByIdAsync(newLoteId, ct);
        if (saved is null) return NotFound<FerramentasLoteItem>();
        return Result<FerramentasLoteItem, DomainError>.Success(MapLote(saved));
    }

    // ---- Register piece ----------------------------------------------------

    public async Task<Result<Guid, DomainError>> RegisterPieceAsync(
        RegisterPieceRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(request.LoteId, ct);
        if (lote is null) return NotFound<Guid>();

        var pieceResult = PhysicalPiece.Register(request.LoteId, request.Sequence, request.Number, _clock.UtcNow, gate.Value.ActorId);
        if (pieceResult.IsFailure) return Result<Guid, DomainError>.Failure(pieceResult.Error);

        var pieceId = await _repository.RegisterPieceAsync(pieceResult.Value, ct);
        await _repository.InsertAuditEventAsync(request.LoteId, "ferramentas.peca.registar",
            null, request.Number, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(pieceId);
    }

    // ---- Set condition (explicit fact) -------------------------------------

    public async Task<Result<bool, DomainError>> SetConditionAsync(
        SetConditionRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var pieces = await _repository.GetPiecesByLoteAsync(request.LoteId, ct);
        var piece = pieces.FirstOrDefault(p => p.Number == request.Number);
        if (piece is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "FERRAMENTAS_PIECE_NOT_FOUND", "Peça não encontrada neste lote."));

        var result = piece.SetCondition(request.Condition, request.Reason, _clock.UtcNow, gate.Value.ActorId);
        if (result.IsFailure) return Result<bool, DomainError>.Failure(result.Error);

        await _repository.UpdatePieceAsync(piece, ct);
        await _repository.InsertAuditEventAsync(request.LoteId, "ferramentas.condicao.alterar",
            null, $"{request.Number}:{ToolConditionCodec.ToStorage(request.Condition)}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Verification rules (require ferramentas.configure) ----------------

    public async Task<Result<Guid, DomainError>> AddCheckRuleAsync(
        CheckRuleRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(CanonicalModuleCatalog.FerramentasConfigureCapabilityId);
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(request.LoteId, ct);
        if (lote is null) return NotFound<Guid>();

        var ruleResult = ToolCheckRule.Create(
            request.LoteId, request.RuleText, request.Frequency, null, _clock.UtcNow, gate.Value.ActorId);
        if (ruleResult.IsFailure) return Result<Guid, DomainError>.Failure(ruleResult.Error);

        var ruleId = await _repository.AddCheckRuleAsync(ruleResult.Value, ct);
        await _repository.InsertAuditEventAsync(request.LoteId, "ferramentas.regra.criar",
            null, ruleId.ToString(), gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(ruleId);
    }

    public async Task<Result<bool, DomainError>> UpdateCheckRuleAsync(
        Guid ruleId, CheckRuleRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(CanonicalModuleCatalog.FerramentasConfigureCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var rule = await _repository.GetCheckRuleByIdAsync(ruleId, ct);
        if (rule is null) return NotFound<bool>();

        var editResult = rule.Edit(request.RuleText, request.Frequency, _clock.UtcNow, gate.Value.ActorId);
        if (editResult.IsFailure) return Result<bool, DomainError>.Failure(editResult.Error);

        await _repository.UpdateCheckRuleAsync(rule, ct);
        await _repository.InsertAuditEventAsync(ruleId, "ferramentas.regra.editar",
            null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> ToggleCheckRuleAsync(
        ToggleRuleRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require(CanonicalModuleCatalog.FerramentasConfigureCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var rule = await _repository.GetCheckRuleByIdAsync(request.RuleId, ct);
        if (rule is null) return NotFound<bool>();

        await _repository.ToggleCheckRuleActiveAsync(request.RuleId, request.Active, ct);
        await _repository.InsertAuditEventAsync(request.RuleId, "ferramentas.regra.estado",
            request.Active ? "inativo" : "ativo", request.Active ? "ativo" : "inativo", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> DeleteCheckRuleAsync(
        Guid ruleId, CancellationToken ct = default)
    {
        var gate = _gate.Require(CanonicalModuleCatalog.FerramentasConfigureCapabilityId);
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var rule = await _repository.GetCheckRuleByIdAsync(ruleId, ct);
        if (rule is null) return NotFound<bool>();

        await _repository.DeleteCheckRuleAsync(ruleId, ct);
        // Occurrences/history are preserved; only the rule configuration is removed.
        await _repository.InsertAuditEventAsync(ruleId, "ferramentas.regra.apagar",
            null, null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Queries -----------------------------------------------------------

    public async Task<Result<IReadOnlyList<FerramentasReferenceItem>, DomainError>> ListReferencesAsync(
        FerramentasSearchRequest request, CancellationToken ct = default)
    {
        var references = await _repository.SearchReferencesAsync(
            request.Reference, request.TechnicalName, request.Lote, request.Drawing,
            request.Line, request.Processo, request.OwnerPlant, ct);

        var items = new List<FerramentasReferenceItem>();
        foreach (var reference in references)
        {
            var lotes = await _repository.GetLotesByReferenceAsync(reference.ToolReferenceId, ct);
            var processo = lotes.Select(l => l.Processo).FirstOrDefault(p => p is not null);
            var lines = string.Join(", ", lotes.SelectMany(l => l.AllowedLines).Distinct(StringComparer.Ordinal));
            items.Add(new FerramentasReferenceItem(
                reference.ToolReferenceId,
                FerramentasToolTypeCodec.ToStorage(reference.ToolType),
                reference.RefCode,
                reference.TechnicalName,
                reference.OwnerPlant,
                processo,
                lines,
                lotes.Count));
        }
        return Result<IReadOnlyList<FerramentasReferenceItem>, DomainError>.Success(items.AsReadOnly());
    }

    public async Task<Result<FerramentasReferenceDetail, DomainError>> GetReferenceDetailAsync(
        Guid referenceId, CancellationToken ct = default)
        => await BuildDetailAsync(referenceId, ct);

    public async Task<Result<IReadOnlyList<FerramentasLoteItem>, DomainError>> ListLotesByReferenceAsync(
        Guid referenceId, CancellationToken ct = default)
    {
        var reference = await _repository.GetReferenceByIdAsync(referenceId, ct);
        if (reference is null) return NotFound<IReadOnlyList<FerramentasLoteItem>>();
        var lotes = await _repository.GetLotesByReferenceAsync(referenceId, ct);
        return Result<IReadOnlyList<FerramentasLoteItem>, DomainError>.Success(
            lotes.Select(MapLote).ToList().AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<FerramentasPieceItem>, DomainError>> ListPiecesByLoteAsync(
        Guid loteId, CancellationToken ct = default)
    {
        var pieces = await _repository.GetPiecesByLoteAsync(loteId, ct);
        return Result<IReadOnlyList<FerramentasPieceItem>, DomainError>.Success(
            pieces.Select(p => new FerramentasPieceItem(
                p.PhysicalPieceId, p.ToolLoteId, p.Sequence, p.Number, p.Status,
                ToolConditionCodec.ToStorage(p.Condition))).ToList().AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<FerramentasCheckRuleItem>, DomainError>> ListCheckRulesByLoteAsync(
        Guid loteId, CancellationToken ct = default)
    {
        var lote = await _repository.GetLoteByIdAsync(loteId, ct);
        if (lote is null) return NotFound<IReadOnlyList<FerramentasCheckRuleItem>>();
        var rules = await _repository.GetCheckRulesByLoteAsync(loteId, ct);
        return Result<IReadOnlyList<FerramentasCheckRuleItem>, DomainError>.Success(
            rules.Select(r => new FerramentasCheckRuleItem(
                r.ToolCheckRuleId, r.ToolLoteId, r.RuleText,
                FerramentasCheckFrequencyCodec.ToStorage(r.Frequency), r.Active, r.CopiedFromRuleId)).ToList().AsReadOnly());
    }

    // ---- Rule lookup consumed by Job On ------------------------------------

    public async Task<Result<IReadOnlyList<VerificationRule>, DomainError>> ResolveActiveRulesAsync(
        Guid toolLoteId, CancellationToken ct = default)
    {
        var rules = await _ruleLookup.ResolveActiveRulesAsync(toolLoteId, ct);
        return Result<IReadOnlyList<VerificationRule>, DomainError>.Success(rules);
    }

    // ---- Utilisation (R003, append-only per tool_lote) ----------------------

    public async Task<Result<bool, DomainError>> RecordUtilisationReadingAsync(
        RecordToolUtilisationRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        if (request.ValueCumulative < 0)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_UTIL_CUMUL_NEGATIVE", "O valor cumulativo não pode ser negativo."));

        if (request.SapStart is not null && (request.SapStart < 0 || request.SapStart > 100))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_UTIL_SAP_RANGE", "SAP start deve estar entre 0 e 100."));
        if (request.SapEnd is not null && (request.SapEnd < 0 || request.SapEnd > 100))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_UTIL_SAP_RANGE", "SAP end deve estar entre 0 e 100."));
        if (request.PercentUsed is not null && (request.PercentUsed < 0 || request.PercentUsed > 100))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_UTIL_PERCENT_RANGE", "A percentagem de utilização deve estar entre 0 e 100."));

        var lote = await _repository.GetLoteByIdAsync(request.ToolLoteId, ct);
        if (lote is null) return NotFound<bool>();

        var reading = new ToolUtilisationReading
        {
            ToolLoteId = request.ToolLoteId,
            SapStart = request.SapStart is null ? null : decimal.Round(request.SapStart.Value, 2),
            SapEnd = request.SapEnd is null ? null : decimal.Round(request.SapEnd.Value, 2),
            PercentUsed = request.PercentUsed is null ? null : decimal.Round(request.PercentUsed.Value, 1),
            ValueAdded = request.ValueAdded,
            ValueCumulative = request.ValueCumulative,
            Notes = NormalizeNull(request.Notes),
            ActorId = gate.Value.ActorId,
            ReadingAtUtc = _clock.UtcNow
        };
        await _repository.RecordUtilisationReadingAsync(reading, ct);

        await _repository.InsertAuditEventAsync(lote.ToolLoteId, "ferramentas.utilizacao.registar",
            request.SapStart?.ToString(), request.PercentUsed?.ToString(), gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<ToolUtilisationStatus, DomainError>> GetUtilisationAsync(
        Guid toolLoteId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<ToolUtilisationStatus, DomainError>.Failure(gate.Error);

        if (await _repository.GetLoteByIdAsync(toolLoteId, ct) is null)
            return NotFound<ToolUtilisationStatus>();

        var history = await _repository.ListUtilisationReadingsAsync(toolLoteId, ct);
        ToolUtilisationReading? latest = null;
        foreach (var r in history) { latest = r; } // history arrives ascending; latest = last
        // % use is the RECORDED (manual, from SAP) value of the latest reading — NO formula.
        decimal? percent = latest?.PercentUsed;
        return Result<ToolUtilisationStatus, DomainError>.Success(
            new ToolUtilisationStatus(history, latest, percent));
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task<Result<FerramentasReferenceDetail, DomainError>> BuildDetailAsync(Guid referenceId, CancellationToken ct)
    {
        var reference = await _repository.GetReferenceByIdAsync(referenceId, ct);
        if (reference is null) return NotFound<FerramentasReferenceDetail>();

        var lotes = await _repository.GetLotesByReferenceAsync(referenceId, ct);
        return Result<FerramentasReferenceDetail, DomainError>.Success(new FerramentasReferenceDetail(
            reference.ToolReferenceId,
            FerramentasToolTypeCodec.ToStorage(reference.ToolType),
            reference.RefCode,
            reference.TechnicalName,
            reference.OwnerPlant,
            lotes.Select(MapLote).ToList().AsReadOnly()));
    }

    private static FerramentasLoteItem MapLote(ToolLote lote) => new(
        lote.ToolLoteId, lote.ToolReferenceId, lote.Lote, lote.Qty, lote.AllowedLines,
        lote.DrawingCode, lote.DrawingRevision, lote.Processo, lote.CopiedFromToolLoteId);

    private static Result<T, DomainError> NotFound<T>() =>
        Result<T, DomainError>.Failure(NotFoundError());

    private static DomainError NotFoundError() =>
        DomainError.NotFound("FERRAMENTAS_NOT_FOUND", "Registo de ferramenta não encontrado.");

    private static string? NormalizeNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Search filter for the reference list (all optional, AND semantics on matching columns).</summary>
public sealed record FerramentasSearchRequest(
    string? Reference,
    string? TechnicalName,
    string? Lote,
    string? Drawing,
    string? Line,
    string? Processo,
    string? OwnerPlant);

/// <summary>R003 — records an append-only utilisation reading for a tool lot (manual % use from SAP).</summary>
public sealed record RecordToolUtilisationRequest(
    Guid ToolLoteId,
    decimal? SapStart,
    decimal? SapEnd,
    decimal? PercentUsed,
    decimal? ValueAdded,
    decimal ValueCumulative,
    string? Notes);