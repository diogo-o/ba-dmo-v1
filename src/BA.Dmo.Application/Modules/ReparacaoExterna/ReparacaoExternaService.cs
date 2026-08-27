using System.Text.Json;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa application service (GLM-RE-01..13; owner decisions A–G).
/// Owns the external repair plan/reparador/ciclo/history. Physical position state is
/// NEVER written here: it goes through the Armazém-owned
/// <see cref="IArmazemRepairMovementPort"/> within the SAME transaction as the
/// repair-cycle write (owner decisions B/C), so pickup/return either fully commit or
/// fully roll back. No physical effect is inferred — only explicit confirmations move
/// tools (owner decision D). Status transitions happen only via persisted
/// confirmations (GLM-RE-04/09). Duplicate-in-open-exit is a hard Application/domain
/// block (owner decision F). BQ is out of V1 scope (owner decision A), and the
/// functional cancel command is deferred (owner decision E).
/// </summary>
public sealed class ReparacaoExternaService
{
    private readonly IRepairRepository _repository;
    private readonly IToolPieceResolver _toolResolver;
    private readonly IArmazemRepairMovementPort _armazemRepair;
    private readonly IRepairUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ReparacaoExternaAuthorizationGate _gate;
    private readonly IClock _clock;

    public ReparacaoExternaService(
        IRepairRepository repository,
        IToolPieceResolver toolResolver,
        IArmazemRepairMovementPort armazemRepair,
        IRepairUnitOfWorkFactory unitOfWorkFactory,
        ReparacaoExternaAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _toolResolver = toolResolver;
        _armazemRepair = armazemRepair;
        _unitOfWorkFactory = unitOfWorkFactory;
        _gate = gate;
        _clock = clock;
    }

    // ---- Create exit list ---------------------------------------------------

    public async Task<Result<IReadOnlyList<RepairToolIdentity>, DomainError>> SearchToolsAsync(
        RepairType type, string? reference, string? lot, string? number, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<RepairToolIdentity>, DomainError>.Failure(gate.Error);
        if (type is not (RepairType.CM or RepairType.MF))
            return Result<IReadOnlyList<RepairToolIdentity>, DomainError>.Failure(DomainError.Validation(
                "REPEXT_TYPE_SCOPE",
                "A Reparação Externa V1 suporta apenas CM e MF (BQ pertence a U-19)."));

        var hits = await _toolResolver.SearchAsync(type, reference, lot, number, ct);
        return Result<IReadOnlyList<RepairToolIdentity>, DomainError>.Success(hits);
    }

    public async Task<Result<Guid, DomainError>> CreateExitAsync(
        CreateExitRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        if (request.RepairType is not (RepairType.CM or RepairType.MF))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "REPEXT_TYPE_SCOPE",
                "A Reparação Externa V1 suporta apenas CM e MF (BQ pertence a U-19)."));

        var repairer = request.RepairerId is null
            ? null
            : await _repository.GetRepairerByIdAsync(request.RepairerId.Value, ct);
        var snapshot = repairer is null ? null : new RepairerSnapshot(repairer.RepairerId, repairer.Name, repairer.Active);

        var exit = RepairExit.Create(request.RepairType, snapshot, request.PlannedDate, _clock.UtcNow, gate.Value.ActorId);
        if (exit.IsFailure) return Result<Guid, DomainError>.Failure(exit.Error);

        var preparedItems = new List<(RepairExitItem Item, string AuditAfter)>();
        var requestPieceIds = new HashSet<Guid>();
        foreach (var item in request.Items)
        {
            var prepared = await PrepareItemAsync(exit.Value, item.PhysicalPieceId, item.Number, requestPieceIds, ct);
            if (prepared.IsFailure) return Result<Guid, DomainError>.Failure(prepared.Error);
            preparedItems.Add(prepared.Value);
        }

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
        var exitId = await _repository.CreateExitAsync(uow, exit.Value, snapshot, Serialize(snapshot), ct);
        foreach (var prepared in preparedItems)
        {
            await _repository.AddItemAsync(uow, prepared.Item, ct);
            await _repository.InsertAuditEventAsync(uow, exitId, "reparacao_externa.lista.item",
                null, prepared.AuditAfter, gate.Value.ActorId, ct);
        }

        await _repository.InsertAuditEventAsync(uow, exitId, "reparacao_externa.lista.criar",
            null, $"{RepairTypeCodec.ToStorage(request.RepairType)}|{request.RepairerId?.ToString()}", gate.Value.ActorId, ct);
        await uow.CommitAsync(ct);
        return Result<Guid, DomainError>.Success(exitId);
    }

    // ---- Add / remove items (only while preparing) --------------------------

    public async Task<Result<Guid, DomainError>> AddItemAsync(
        AddExitItemRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);
        return await AddItemCoreAsync(request.RepairExitId, request.PhysicalPieceId, request.Number, gate.Value.ActorId, ct);
    }

    private async Task<Result<Guid, DomainError>> AddItemCoreAsync(
        Guid exitId, Guid physicalPieceId, string number, string actorId, CancellationToken ct)
    {
        var exit = await _repository.GetExitByIdAsync(exitId, ct);
        if (exit is null) return NotFound<Guid>();
        if (!exit.IsPreparing)
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_LIST_NOT_EDITABLE",
                "A lista já não está em preparação; não é possível adicionar itens."));

        var prepared = await PrepareItemAsync(exit, physicalPieceId, number, requestPieceIds: null, ct);
        if (prepared.IsFailure) return Result<Guid, DomainError>.Failure(prepared.Error);

        var itemId = await _repository.AddItemAsync(prepared.Value.Item, ct);
        await _repository.InsertAuditEventAsync(exitId, "reparacao_externa.lista.item",
            null, prepared.Value.AuditAfter, actorId, ct);
        return Result<Guid, DomainError>.Success(itemId);
    }

    private async Task<Result<(RepairExitItem Item, string AuditAfter), DomainError>> PrepareItemAsync(
        RepairExit exit,
        Guid physicalPieceId,
        string number,
        HashSet<Guid>? requestPieceIds,
        CancellationToken ct)
    {
        var piece = await _toolResolver.ResolveAsync(physicalPieceId, ct);
        if (piece is null)
            return Result<(RepairExitItem, string), DomainError>.Failure(DomainError.NotFound(
                "REPEXT_PIECE_NOT_FOUND", "A ferramenta CM/MF escolhida não foi encontrada."));

        if (!string.Equals(piece.Number, number?.Trim(), StringComparison.Ordinal))
            return Result<(RepairExitItem, string), DomainError>.Failure(DomainError.Validation(
                "REPEXT_PIECE_NUMBER_MISMATCH",
                "O número individual não corresponde à ferramenta escolhida."));

        // Hard block (GLM-RE-09 / owner decision F): the item must not already be
        // in another open exit.
        if ((requestPieceIds is not null && !requestPieceIds.Add(physicalPieceId))
            || await _repository.ExistsItemInOpenExitAsync(physicalPieceId, ct))
            return Result<(RepairExitItem, string), DomainError>.Failure(DomainError.DomainConflict(
                RepairExitRules.DuplicateInOpenExitCode,
                "Esta ferramenta já está incluída numa saída programada aberta."));

        // The list type governs the item kind (CM or MF).
        var itemResult = RepairExitItem.CreateCmMf(exit.RepairExitId, physicalPieceId, number, exit.RepairType);
        if (itemResult.IsFailure)
            return Result<(RepairExitItem, string), DomainError>.Failure(itemResult.Error);

        return Result<(RepairExitItem, string), DomainError>.Success(
            (itemResult.Value, $"{piece.Reference}|{piece.Lot}|{piece.Number}"));
    }

    public async Task<Result<bool, DomainError>> RemoveItemAsync(
        RemoveExitItemRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var exit = await _repository.GetExitByIdAsync(request.RepairExitId, ct);
        if (exit is null) return NotFound<bool>();
        if (!exit.IsPreparing)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_LIST_NOT_EDITABLE",
                "A lista já não está em preparação; não é possível remover itens."));

        var items = await _repository.GetExitItemsAsync(request.RepairExitId, ct);
        var item = items.FirstOrDefault(i => i.RepairExitItemId == request.RepairExitItemId && i.RepairExitId == request.RepairExitId);
        if (item is null) return NotFound<bool>();
        if (item.IsPickedOut || item.IsReturned)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_ITEM_MOVED",
                "O item já foi recolhido ou devolvido; não pode ser removido."));

        await _repository.DeleteItemAsync(request.RepairExitItemId, ct);
        await _repository.InsertAuditEventAsync(request.RepairExitId, "reparacao_externa.lista.item.remover",
            request.RepairExitItemId.ToString(), null, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Disponibilizar (Preparação → A retirar) ----------------------------

    public async Task<Result<bool, DomainError>> DisponibilizarExitAsync(
        DisponibilizarExitRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

        var exit = await _repository.GetExitByIdAsync(request.RepairExitId, ct);
        if (exit is null) return NotFound<bool>();

        if (exit.Status != RepairExitStatus.Preparacao)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_NOT_PREPARING",
                "A lista não está em preparação; não pode ser disponibilizada."));

        var items = await _repository.GetExitItemsAsync(request.RepairExitId, ct);
        if (items.Count == 0)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "REPEXT_EMPTY_LIST",
                "A lista está vazia; adicione itens antes de disponibilizar."));

        await _repository.UpdateExitStatusAsync(uow, request.RepairExitId, RepairExitStatusCodec.ToStorage(RepairExitStatus.ARetirar), ct);
        await uow.CommitAsync(ct);

        await _repository.InsertAuditEventAsync(request.RepairExitId, "reparacao_externa.lista.disponibilizar",
            "preparacao", "a_retirar", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Confirm pickup (one transaction) -----------------------------------

    public async Task<Result<bool, DomainError>> ConfirmPickupAsync(
        ConfirmPickupRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

        var item = await _repository.GetItemByIdAsync(request.RepairExitItemId, ct);
        if (item is null) return NotFound<bool>();

        var exit = await _repository.GetExitByIdAsync(item.RepairExitId, ct);
        if (exit is null) return NotFound<bool>();

        // Recompute list status from the items BEFORE this confirmation, then
        // hard-block-cycle rules.
        var itemsBefore = await _repository.GetExitItemsAsync(item.RepairExitId, ct);
        var machineResult = RepairExitStatusMachine.ConfirmPickup(
            exit.Status, itemsBefore, item);
        if (machineResult.IsFailure) return Result<bool, DomainError>.Failure(machineResult.Error);

        if (item.IsReturned)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_ITEM_ALREADY_RETURNED",
                "Este item já foi devolvido; não é possível confirmar a recolha."));

        if (item.IsPickedOut)
        {
            // Idempotent pickup: already-recorded out fact; no movement duplication.
            return Result<bool, DomainError>.Success(true);
        }

        var confirmOut = item.ConfirmPickedOut(_clock.UtcNow, gate.Value.ActorId);
        if (confirmOut.IsFailure) return Result<bool, DomainError>.Failure(confirmOut.Error);

        // Resolve the parent lot for the Armazém physical movement.
        var piece = await _toolResolver.ResolveAsync(item.PhysicalPieceId!.Value, ct);
        if (piece is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "REPEXT_PIECE_NOT_FOUND", "A ferramenta CM/MF deste item não foi encontrada."));

        // Physical release (Armazém-owned) in the SAME transaction as the item update.
        var armazem = await _armazemRepair.ConfirmPickupAsync(
            uow, item.RepairExitId, piece.ToolLoteId, gate.Value.ActorId, _clock.UtcNow, ct);
        if (armazem.IsFailure)
            return Result<bool, DomainError>.Failure(armazem.Error);

        await _repository.ConfirmItemPickedAsync(uow, item, ct);
        await _repository.UpdateExitStatusAsync(uow, item.RepairExitId, RepairExitStatusCodec.ToStorage(machineResult.Value), ct);
        await _repository.InsertRepairEventAsync(uow, item.RepairExitItemId, "recolha_externa", gate.Value.ActorId, _clock.UtcNow, ct);

        await uow.CommitAsync(ct);

        await _repository.InsertAuditEventAsync(item.RepairExitId, "reparacao_externa.item.recolhido",
            null, $"{item.IndividualNumber}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Confirm return (one transaction) -----------------------------------

    public async Task<Result<bool, DomainError>> ConfirmReturnAsync(
        ConfirmReturnRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

        var item = await _repository.GetItemByIdAsync(request.RepairExitItemId, ct);
        if (item is null) return NotFound<bool>();

        var exit = await _repository.GetExitByIdAsync(item.RepairExitId, ct);
        if (exit is null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                RepairExitRules.ReturnWithoutExitCode,
                "Não existe uma saída programada correspondente para este retorno."));

        if (exit.Status == RepairExitStatus.Preparacao)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                RepairExitRules.ReturnWithoutExitCode,
                "A lista ainda não foi enviada; não existe ciclo de retorno para este item."));

        if (!string.IsNullOrWhiteSpace(request.PositionCode) &&
            !System.Text.RegularExpressions.Regex.IsMatch(request.PositionCode.Trim(), @"^\d{4}$"))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "REPEXT_POSITION_CODE", "A posição de retorno deve ter exatamente 4 dígitos."));

        if (item.IsReturned)
        {
            // Idempotent return: already-recorded in fact; no movement duplication.
            return Result<bool, DomainError>.Success(true);
        }

        var confirmIn = item.ConfirmReturned(_clock.UtcNow, gate.Value.ActorId);
        if (confirmIn.IsFailure) return Result<bool, DomainError>.Failure(confirmIn.Error);

        var piece = await _toolResolver.ResolveAsync(item.PhysicalPieceId!.Value, ct);
        if (piece is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "REPEXT_PIECE_NOT_FOUND", "A ferramenta CM/MF deste item não foi encontrada."));

        var armazem = await _armazemRepair.ConfirmReturnAsync(
            uow, item.RepairExitId, piece.ToolLoteId, request.PositionCode, gate.Value.ActorId, _clock.UtcNow, ct);
        if (armazem.IsFailure)
            return Result<bool, DomainError>.Failure(armazem.Error);

        await _repository.ConfirmItemReturnedAsync(uow, item, ct);

        var itemsAfter = await _repository.GetExitItemsAsync(item.RepairExitId, ct);
        var machineResult = RepairExitStatusMachine.ConfirmReturn(exit.Status, itemsAfter);
        if (machineResult.IsFailure) return Result<bool, DomainError>.Failure(machineResult.Error);
        await _repository.UpdateExitStatusAsync(uow, item.RepairExitId, RepairExitStatusCodec.ToStorage(machineResult.Value), ct);
        await _repository.InsertRepairEventAsync(uow, item.RepairExitItemId, "retorno_externa", gate.Value.ActorId, _clock.UtcNow, ct);

        await uow.CommitAsync(ct);

        await _repository.InsertAuditEventAsync(item.RepairExitId, "reparacao_externa.item.retornado",
            null, $"{item.IndividualNumber}|{request.PositionCode}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Queries ------------------------------------------------------------

    public async Task<Result<IReadOnlyList<RepairExitDto>, DomainError>> ListExitsAsync(
        RepairType? type, RepairExitStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var exits = await _repository.ListExitsAsync(type, status, from, to, ct);
        var dtos = new List<RepairExitDto>();
        foreach (var exit in exits)
            dtos.Add(await BuildExitDtoAsync(exit, ct));
        return Result<IReadOnlyList<RepairExitDto>, DomainError>.Success(dtos.AsReadOnly());
    }

    public async Task<Result<RepairExitDto, DomainError>> GetExitAsync(Guid repairExitId, CancellationToken ct = default)
    {
        var exit = await _repository.GetExitByIdAsync(repairExitId, ct);
        if (exit is null) return NotFound<RepairExitDto>();
        return Result<RepairExitDto, DomainError>.Success(await BuildExitDtoAsync(exit, ct));
    }

    public async Task<Result<IReadOnlyList<RepairerDto>, DomainError>> ListRepairersAsync(CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<RepairerDto>, DomainError>.Failure(gate.Error);

        var list = await _repository.ListRepairersAsync(ct);
        var dtos = new List<RepairerDto>();
        foreach (var r in list)
        {
            var types = await _repository.ListRepairerRepairTypesAsync(r.RepairerId, ct);
            dtos.Add(new RepairerDto(r.RepairerId, r.Name, r.Active, types));
        }
        return Result<IReadOnlyList<RepairerDto>, DomainError>.Success(dtos.AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<LineRepairerDefaultDto>, DomainError>> ListLineDefaultsAsync(CancellationToken ct = default)
    {
        var list = await _repository.ListLineDefaultsAsync(ct);
        return Result<IReadOnlyList<LineRepairerDefaultDto>, DomainError>.Success(
            list.Select(d => new LineRepairerDefaultDto(d.Line, d.ToolType, d.RepairerId)).ToList().AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<RepairHistoryRow>, DomainError>> GetHistoryAsync(
        CancellationToken ct = default)
    {
        var exits = await _repository.ListExitsAsync(null, null, null, null, ct);
        var rows = new List<RepairHistoryRow>();
        foreach (var exit in exits)
        {
            foreach (var item in await _repository.GetExitItemsAsync(exit.RepairExitId, ct))
            {
                var piece = item.PhysicalPieceId is null
                    ? null
                    : await _toolResolver.ResolveAsync(item.PhysicalPieceId.Value, ct);
                rows.Add(new RepairHistoryRow(
                    exit.RepairExitId.ToString(),
                    RepairTypeCodec.ToStorage(exit.RepairType),
                    piece?.Reference,
                    piece?.Lot,
                    item.Qty,
                    item.IndividualNumber,
                    exit.RepairerSnapshot?.Name,
                    item.OutAtUtc,
                    item.OutOperatorId,
                    item.InAtUtc,
                    item.InOperatorId,
                    ExitItemState(item)));
            }
        }
        return Result<IReadOnlyList<RepairHistoryRow>, DomainError>.Success(rows.AsReadOnly());
    }

    // ---- Repairer management (Definições) -----------------------------------

    public async Task<Result<Guid, DomainError>> CreateRepairerAsync(
        CreateRepairerRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "REPEXT_REPAIRER_NAME_REQUIRED", "O nome do reparador é obrigatório."));

        var typesResult = NormalizeSupportedTypes(request.SupportedTypes);
        if (typesResult.IsFailure) return Result<Guid, DomainError>.Failure(typesResult.Error);

        var repairer = new Repairer { Name = name, Active = true, CreatedAtUtc = _clock.UtcNow, UpdatedAtUtc = _clock.UtcNow };
        var id = await _repository.CreateRepairerAsync(repairer, ct);
        await _repository.SetRepairerRepairTypesAsync(id, typesResult.Value, ct);
        await _repository.InsertAuditEventAsync(id, "reparacao_externa.reparador.criar", null, name, gate.Value.ActorId, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> UpdateRepairerAsync(
        UpdateRepairerRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var repairer = await _repository.GetRepairerByIdAsync(request.RepairerId, ct);
        if (repairer is null) return NotFound<bool>();

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "REPEXT_REPAIRER_NAME_REQUIRED", "O nome do reparador é obrigatório."));

        repairer.Name = name;
        repairer.UpdatedAtUtc = _clock.UtcNow;
        await _repository.UpdateRepairerAsync(repairer, ct);

        if (request.SupportedTypes is not null)
        {
            var typesResult = NormalizeSupportedTypes(request.SupportedTypes);
            if (typesResult.IsFailure) return Result<bool, DomainError>.Failure(typesResult.Error);
            await _repository.SetRepairerRepairTypesAsync(request.RepairerId, typesResult.Value, ct);
        }

        await _repository.InsertAuditEventAsync(request.RepairerId, "reparacao_externa.reparador.editar", null, name, gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> DeactivateRepairerAsync(
        DeactivateRepairerRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var repairer = await _repository.GetRepairerByIdAsync(request.RepairerId, ct);
        if (repairer is null) return NotFound<bool>();

        await _repository.DeactivateRepairerAsync(request.RepairerId, ct);
        await _repository.InsertAuditEventAsync(request.RepairerId, "reparacao_externa.reparador.desativar",
            "ativo", "inativo", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> UpsertLineDefaultAsync(
        UpsertLineDefaultRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var repairer = await _repository.GetRepairerByIdAsync(request.RepairerId, ct);
        if (repairer is null) return NotFound<bool>();
        if (!repairer.Active)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_REPAIRER_INACTIVE", "Não é possível associar um reparador desativado."));

        var lineDefault = new LineRepairerDefault
        {
            Line = request.Line,
            ToolType = request.ToolType,
            RepairerId = request.RepairerId,
            UpdatedAtUtc = _clock.UtcNow,
            UpdatedBy = gate.Value.ActorId
        };
        await _repository.UpsertLineDefaultAsync(lineDefault, ct);
        await _repository.InsertAuditEventAsync(request.RepairerId, "reparacao_externa.linha.defeito",
            null, $"{request.Line}|{request.ToolType}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- DTO building / helpers ---------------------------------------------

    /// <summary>
    /// R004 — Normalizes the many-to-many supported repair types (CM/MF/BQ). Empty/null
    /// is allowed (capability undefined); every entry must be one of the authoritative
    /// types. Returns the canonical uppercase set.
    /// </summary>
    private static Result<IReadOnlySet<string>, DomainError> NormalizeSupportedTypes(IEnumerable<string>? types)
    {
        if (types is null) return Result<IReadOnlySet<string>, DomainError>.Success(new HashSet<string>(StringComparer.Ordinal));
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in types)
        {
            var canonical = t?.Trim().ToUpperInvariant();
            if (canonical is not ("CM" or "MF" or "BQ"))
                return Result<IReadOnlySet<string>, DomainError>.Failure(DomainError.Validation(
                    "REPEXT_REPAIRER_TYPE_INVALID",
                    "Tipo de reparação inválido. Autoritário: CM, MF, BQ."));
            result.Add(canonical);
        }
        return Result<IReadOnlySet<string>, DomainError>.Success(result);
    }

    private async Task<RepairExitDto> BuildExitDtoAsync(RepairExit exit, CancellationToken ct)
    {
        var items = await _repository.GetExitItemsAsync(exit.RepairExitId, ct);
        var itemDtos = new List<RepairExitItemDto>();
        foreach (var item in items)
        {
            var piece = item.PhysicalPieceId is null
                ? null
                : await _toolResolver.ResolveAsync(item.PhysicalPieceId.Value, ct);

            // Resolve physical location context (Armazém) via a resolver that returns
            // the current position. Reuses the tool piece projection for identity; the
            // location itself is NOT owned by U-15 — this view only mirrors it.
            string? position = null;
            string context = "nao_registado";
            // Position is provided by the warehouse at confirmation time; for the list
            // view we surface unknown-location as a warning only when not yet recorded.
            if (item.IsPickedOut) context = "em_reparacao";

            itemDtos.Add(new RepairExitItemDto(
                item.RepairExitItemId,
                item.BqLoteId,
                item.PhysicalPieceId,
                piece?.Reference,
                piece?.Lot,
                item.Qty,
                item.IndividualNumber,
                item.Picked,
                item.OutAtUtc,
                item.OutOperatorId,
                item.InAtUtc,
                item.InOperatorId,
                item.Status,
                position,
                context));
        }
        return new RepairExitDto(
            exit.RepairExitId,
            RepairTypeCodec.ToStorage(exit.RepairType),
            exit.RepairerId,
            exit.RepairerSnapshot?.Name,
            exit.PlannedDate,
            RepairExitStatusCodec.ToStorage(exit.Status),
            exit.CreatedBy,
            exit.CreatedAtUtc,
            itemDtos.AsReadOnly());
    }

    private static string ExitItemState(RepairExitItem item) => item switch
    {
        _ when item.IsReturned => RepairExitStatusCodec.ToStorage(RepairExitStatus.Concluido),
        _ when item.IsPickedOut => RepairExitStatusCodec.ToStorage(RepairExitStatus.Enviado),
        _ => RepairExitStatusCodec.ToStorage(RepairExitStatus.Preparacao)
    };

    private static string? Serialize(RepairerSnapshot? snapshot) =>
        snapshot is null ? null : JsonSerializer.Serialize(snapshot);

    private static Result<T, DomainError> NotFound<T>() =>
        Result<T, DomainError>.Failure(NotFoundError());

    private static DomainError NotFoundError() =>
        DomainError.NotFound("REPEXT_NOT_FOUND", "Registo de Reparação Externa não encontrado.");
}
