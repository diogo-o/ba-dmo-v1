using System.Text.Json;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Controlo;

/// <summary>
/// R010 — Folha de Controlo application service. A production-level control summary sheet
/// INSIDE the Controlo area (distinct from Peso and Pegamentos; no schema/logic merge).
///
/// Use cases: create-or-load for the selected production (using the already-selected context,
/// never asking to re-select), apply item controls (OK/NOK + observation + MCaliper),
/// submit/deliver, reopen (not a permanent lock), and responsible/chief approve-or-reject.
/// Every write is persisted with its append-only history event in ONE unit of work, so audit
/// history is never silently overwritten. The sheet pins job_on_id + the exact
/// job_on_revision_id and snapshots the components of that revision — a later Job On revision
/// never reinterprets a created sheet (immutable-revision anchor, TD-18/Peso/Pegamentos).
/// </summary>
public sealed class ControloSheetService
{
    private readonly IControloSheetRepository _repository;
    private readonly IControloProductionContextLookup _contextLookup;
    private readonly IRepairUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ControloSheetAuthorizationGate _gate;
    private readonly IClock _clock;

    public ControloSheetService(
        IControloSheetRepository repository,
        IControloProductionContextLookup contextLookup,
        IRepairUnitOfWorkFactory unitOfWorkFactory,
        ControloSheetAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _contextLookup = contextLookup;
        _unitOfWorkFactory = unitOfWorkFactory;
        _gate = gate;
        _clock = clock;
    }

    /// <summary>
    /// Creates a new draft Folha de Controlo for the selected production (controlo.edit).
    /// Uses the already-selected production/job_on context; the sheet is associated with the
    /// production and its exact current Job On revision (components snapshot).
    /// </summary>
    public async Task<Result<Guid, DomainError>> CreateAsync(
        CreateControloSheetRequest request, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.EditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        var ctxResult = await _contextLookup.ResolveAsync(request.JobOnId, ct);
        if (ctxResult.IsFailure)
            return Result<Guid, DomainError>.Failure(ctxResult.Error);

        var now = _clock.UtcNow;
        var createResult = ControloFolha.Create(ctxResult.Value, gate.Value.ActorId, now);
        if (createResult.IsFailure)
            return Result<Guid, DomainError>.Failure(createResult.Error);

        var sheet = createResult.Value;
        sheet.RecordEvent(new ControloFolhaEvent(
            Guid.NewGuid(), sheet.ControloSheetId, "criar", gate.Value.ActorId, now, null, SerializeSummary(sheet), null));

        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            var id = await _repository.InsertAsync(uow, sheet, ct);
            await _repository.InsertEventAsync(uow, sheet.Events[0], ct);
            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(id);
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "CONTROLO_SAVE_FAILED", "Falha ao gravar a folha de controlo; os dados foram preservados."));
        }
    }

    /// <summary>
    /// Loads the Folha de Controlo detail (controlo.view). Returns the sheet with its current
    /// items and full event history. Null → NotFound.
    /// </summary>
    public async Task<Result<ControloSheetDto, DomainError>> GetDetailAsync(
        Guid sheetId, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.ViewCapabilityId);
        if (gate.IsFailure)
            return Result<ControloSheetDto, DomainError>.Failure(gate.Error);

        var sheet = await _repository.GetByIdAsync(sheetId, ct);
        if (sheet is null)
            return Result<ControloSheetDto, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada."));

        return Result<ControloSheetDto, DomainError>.Success(MapToDto(sheet));
    }

    /// <summary>
    /// Loads the (latest) Folha de Controlo for a production, or creates one if none exists
    /// (controlo.edit). If none exists, creates from the selected production context.
    /// </summary>
    public async Task<Result<ControloSheetDto, DomainError>> GetForProductionAsync(
        Guid jobOnId, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.ViewCapabilityId);
        if (gate.IsFailure)
            return Result<ControloSheetDto, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetForProductionAsync(jobOnId, null, ct);
        if (existing is not null)
            return Result<ControloSheetDto, DomainError>.Success(MapToDto(existing));

        // No sheet yet → create one for the selected production.
        var created = await CreateAsync(new CreateControloSheetRequest(jobOnId), ct);
        if (created.IsFailure)
            return Result<ControloSheetDto, DomainError>.Failure(created.Error);
        var fresh = await _repository.GetByIdAsync(created.Value, ct);
        return fresh is null
            ? Result<ControloSheetDto, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada após criação."))
            : Result<ControloSheetDto, DomainError>.Success(MapToDto(fresh));
    }

    /// <summary>
    /// Loads (or creates) the Folha de Controlo for a production identified by production
    /// code + machine (the context a selected Peso production row carries), resolving the
    /// job_on internally — the user never searches/selects the production again.
    /// </summary>
    public async Task<Result<ControloSheetDto, DomainError>> GetForProductionByContextAsync(
        string productionCode, string? machineCode, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.ViewCapabilityId);
        if (gate.IsFailure)
            return Result<ControloSheetDto, DomainError>.Failure(gate.Error);

        var ctxResult = await _contextLookup.ResolveByProductionAsync(productionCode, machineCode, ct);
        if (ctxResult.IsFailure)
            return Result<ControloSheetDto, DomainError>.Failure(ctxResult.Error);

        return await GetForProductionAsync(ctxResult.Value.JobOnId, ct);
    }

    /// <summary>
    /// Applies control assessments (OK/NOK + observation + MCaliper) to the sheet items
    /// (controlo.edit). Allowed across states (submission is not a permanent lock); the change
    /// is persisted with an append-only 'editar' event.
    /// </summary>
    public async Task<Result<ControloUnit, DomainError>> UpdateItemsAsync(
        UpdateControloSheetItemsRequest request, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.EditCapabilityId);
        if (gate.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(gate.Error);

        var sheet = await _repository.GetByIdAsync(request.SheetId, ct);
        if (sheet is null)
            return Result<ControloUnit, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada."));

        var before = SerializeSummary(sheet);
        sheet.ApplyItemControls(request.Edits ?? Array.Empty<ControloFolhaItemControlEdit>(), _clock.UtcNow);
        sheet.RecordEvent(new ControloFolhaEvent(
            Guid.NewGuid(), sheet.ControloSheetId, "editar", gate.Value.ActorId, _clock.UtcNow, before, SerializeSummary(sheet), null));

        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            await _repository.UpdateAsync(uow, sheet, sheet.Items, ct);
            await _repository.InsertEventAsync(uow, sheet.Events[^1], ct);
            await uow.CommitAsync(ct);
            return Result<ControloUnit, DomainError>.Success(new ControloUnit());
        }
        catch (Exception)
        {
            return Result<ControloUnit, DomainError>.Failure(DomainError.Unexpected(
                "CONTROLO_SAVE_FAILED", "Falha ao gravar a folha de controlo; os dados foram preservados."));
        }
    }

    /// <summary>Submits/delivers the sheet (controlo.submit). Traceable; not a permanent lock.</summary>
    public async Task<Result<ControloUnit, DomainError>> SubmitAsync(
        SubmitControloSheetRequest request, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.SubmitCapabilityId);
        if (gate.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(gate.Error);

        var sheet = await _repository.GetByIdAsync(request.SheetId, ct);
        if (sheet is null)
            return Result<ControloUnit, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada."));

        var before = SerializeSummary(sheet);
        var submit = sheet.Submit(gate.Value.ActorId, request.Note, _clock.UtcNow);
        if (submit.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(submit.Error);
        sheet.RecordEvent(new ControloFolhaEvent(
            Guid.NewGuid(), sheet.ControloSheetId, "submeter", gate.Value.ActorId, _clock.UtcNow, before, SerializeSummary(sheet), request.Note));

        return await PersistEditAsync(sheet, ct);
    }

    /// <summary>Reopens a submitted/decided sheet for editing (controlo.edit). Audit traced.</summary>
    public async Task<Result<ControloUnit, DomainError>> ReopenAsync(
        ReopenControloSheetRequest request, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.EditCapabilityId);
        if (gate.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(gate.Error);

        var sheet = await _repository.GetByIdAsync(request.SheetId, ct);
        if (sheet is null)
            return Result<ControloUnit, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada."));

        var before = SerializeSummary(sheet);
        var reopen = sheet.Reopen(gate.Value.ActorId, _clock.UtcNow);
        if (reopen.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(reopen.Error);
        sheet.RecordEvent(new ControloFolhaEvent(
            Guid.NewGuid(), sheet.ControloSheetId, "reeabrir", gate.Value.ActorId, _clock.UtcNow, before, SerializeSummary(sheet), null));

        return await PersistEditAsync(sheet, ct);
    }

    /// <summary>Applies the responsible/chief review decision (controlo.review).</summary>
    public async Task<Result<ControloUnit, DomainError>> DecideAsync(
        DecideControloSheetRequest request, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.ReviewCapabilityId);
        if (gate.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(gate.Error);

        var sheet = await _repository.GetByIdAsync(request.SheetId, ct);
        if (sheet is null)
            return Result<ControloUnit, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_NOT_FOUND", "Folha de controlo não encontrada."));

        var before = SerializeSummary(sheet);
        var decide = sheet.Decide(request.Decision, gate.Value.ActorId, request.Note, _clock.UtcNow);
        if (decide.IsFailure)
            return Result<ControloUnit, DomainError>.Failure(decide.Error);
        sheet.RecordEvent(new ControloFolhaEvent(
            Guid.NewGuid(), sheet.ControloSheetId, "decidir", gate.Value.ActorId, _clock.UtcNow,
            before, SerializeSummary(sheet), $"{(request.Decision == ControloFolhaDecision.Aprovado ? "aprovado" : "rejeitado")} · {request.Note}"));

        return await PersistEditAsync(sheet, ct);
    }

    // ---- Private -----------------------------------------------------------

    /// <summary>
    /// Controlo history (free-mode consultation): lists Folha de Controlo summaries
    /// filtered by date/machine/production/status (controlo.view). No production card is
    /// required — history/search remains usable in free mode (R012 §22/§23).
    /// </summary>
    public async Task<Result<IReadOnlyList<ControloSheetDto>, DomainError>> ListSheetsAsync(
        DateTimeOffset? from = null, DateTimeOffset? to = null, string? machineCode = null,
        Guid? jobOnId = null, string? status = null, CancellationToken ct = default)
    {
        var gate = _gate.RequireCapability(ControloSheetModuleCatalog.ViewCapabilityId);
        if (gate.IsFailure)
            return Result<IReadOnlyList<ControloSheetDto>, DomainError>.Failure(gate.Error);

        var sheets = await _repository.ListAsync(from, to, machineCode, jobOnId, status, ct);
        return Result<IReadOnlyList<ControloSheetDto>, DomainError>.Success(
            sheets.Select(MapToDto).ToList().AsReadOnly());
    }

    private async Task<Result<ControloUnit, DomainError>> PersistEditAsync(ControloFolha sheet, CancellationToken ct)
    {
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            await _repository.UpdateAsync(uow, sheet, sheet.Items, ct);
            await _repository.InsertEventAsync(uow, sheet.Events[^1], ct);
            await uow.CommitAsync(ct);
            return Result<ControloUnit, DomainError>.Success(new ControloUnit());
        }
        catch (Exception)
        {
            return Result<ControloUnit, DomainError>.Failure(DomainError.Unexpected(
                "CONTROLO_SAVE_FAILED", "Falha ao gravar a folha de controlo; os dados foram preservados."));
        }
    }

    private static ControloSheetDto MapToDto(ControloFolha sheet) => new(
        sheet.ControloSheetId,
        sheet.JobOnId,
        sheet.JobOnRevisionId,
        sheet.ProductionCode,
        sheet.Reference,
        sheet.MachineCode,
        sheet.DisplayId,
        ControloFolhaStateCodec.ToStorage(sheet.State),
        sheet.CreatedBy,
        sheet.CreatedAtUtc,
        sheet.SubmittedBy,
        sheet.SubmittedAtUtc,
        sheet.SubmittedNote,
        sheet.DecidedBy,
        sheet.DecidedAtUtc,
        sheet.Decision is { } d ? ControloFolhaStateCodec.ToStorage(d) : null,
        sheet.DecisionNote,
        sheet.Items.Select(i => new ControloSheetItemDto(
            i.ControloSheetItemId, i.Family, i.SourceToolId, i.SourceLotId,
            i.ReferenceSnapshot, i.LotSnapshot, i.TechnicalNameSnapshot,
            i.Result, i.Observation, i.McaliperLink)).ToList(),
        sheet.Events.Select(e => new ControloSheetEventDto(
            e.ControloSheetEventId, e.EventType, e.ActorId, e.OccurredAtUtc, e.Note)).ToList());

    private static string? SerializeSummary(ControloFolha sheet) =>
        JsonSerializer.Serialize(new
        {
            sheetId = sheet.ControloSheetId,
            sheet.JobOnId,
            sheet.JobOnRevisionId,
            sheet.ProductionCode,
            sheet.Reference,
            sheet.MachineCode,
            status = ControloFolhaStateCodec.ToStorage(sheet.State),
            itemCount = sheet.Items.Count
        });
}