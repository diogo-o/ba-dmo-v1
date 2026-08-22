using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Armazém application service (GLM-ARM-05..07). Owns physical
/// position/stock state. Tool identity is resolved ONLY through
/// Armazém's own <see cref="IToolIdentityResolver"/> (never a tool-owner repo).
/// Writes are atomic (GLM-DATA-05); actor attribution is server-derived;
/// <c>fora</c> is derived; two different references on one position is a
/// warning, never a silent normalization.
/// </summary>
public sealed class ArmazemService
{
    private readonly IArmazemRepository _repository;
    private readonly IToolIdentityResolver _toolResolver;
    private readonly ArmazemAuthorizationGate _gate;
    private readonly IClock _clock;

    public ArmazemService(
        IArmazemRepository repository,
        IToolIdentityResolver toolResolver,
        ArmazemAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _toolResolver = toolResolver;
        _gate = gate;
        _clock = clock;
    }

    /// <summary>Registra uma Entrada/Repor: posição passa a localização atual (atómico).</summary>
    public async Task<Result<Guid, DomainError>> RegistrarEntradaAsync(
        RegistrarEntradaRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var position = WarehouseLocation.NormalizePositionCode(request.PositionCode);
        if (!WarehouseLocation.IsValidPositionCode(position))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "ARMZ_LOCATION_CODE", "A posição deve ter exatamente 4 dígitos."));

        var tool = await ResolveRequiredAsync(request.ToolType, request.Reference, request.Lot, ct);
        if (tool.IsFailure) return Result<Guid, DomainError>.Failure(tool.Error);

        var locationId = await _repository.GetOrCreateLocationAsync(position, "tool", ct);
        var occupant = await _repository.GetActiveStockByLocationAsync(locationId, ct);
        if (occupant is not null && !occupant.IsActive)
            occupant = null;
        if (occupant is { IsActive: true } && occupant.ToolId != tool.Value.ToolId)
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_POSITION_OCCUPIED",
                "A posição já está ocupada por outra ferramenta."));

        var now = _clock.UtcNow;
        var stock = new WarehouseStock
        {
            WarehouseLocationId = locationId,
            ToolId = tool.Value.ToolId,
            OccupiedSinceUtc = now,
            OccupiedBy = gate.Value.ActorId
        };
        var movement = new WarehouseMovement
        {
            WarehouseStockId = null,
            Direction = WarehouseMovementDirection.In,
            Destination = request.Destination,
            ActorId = gate.Value.ActorId,
            OccurredAtUtc = now
        };

        try
        {
            var id = await _repository.RegisterEntradaAsync(stock, movement, ct);
            await _repository.InsertAuditEventAsync(
                id, "armazem.entrada", null,
                $"{position}|{tool.Value.Reference}|{tool.Value.Lot}", gate.Value.ActorId, ct);
            return Result<Guid, DomainError>.Success(id);
        }
        catch (ArmazemLocationOccupiedException)
        {
            // Atomic write detected the occupied position (race hit after the
            // fast-path pre-check). Rollback already happened in the repository;
            // map to the same clean domain conflict as the fast path.
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_POSITION_OCCUPIED",
                "A posição já está ocupada por outra ferramenta."));
        }
    }

    /// <summary>Registra uma Saída imediata (Retirar). A posição só é libertada após persistência.</summary>
    public async Task<Result<bool, DomainError>> RegistrarSaidaAsync(
        RegistrarSaidaRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var tool = await ResolveRequiredAsync(request.ToolType, request.Reference, request.Lot, ct);
        if (tool.IsFailure) return Result<bool, DomainError>.Failure(tool.Error);

        var stock = await _repository.GetActiveStockByToolIdAsync(tool.Value.ToolId, ct);
        if (stock is null || !stock.IsActive)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "ARMZ_TOOL_NOT_IN_WAREHOUSE",
                "A ferramenta não está registada como presente no Armazém."));

        var now = _clock.UtcNow;
        var movement = new WarehouseMovement
        {
            WarehouseStockId = stock.WarehouseStockId,
            Direction = WarehouseMovementDirection.Out,
            Destination = request.Destination,
            ActorId = gate.Value.ActorId,
            OccurredAtUtc = now
        };

        await _repository.RegisterSaidaAsync(
            stock.WarehouseStockId, gate.Value.ActorId, now, movement, ct);
        await _repository.InsertAuditEventAsync(
            stock.WarehouseStockId, "armazem.saida", null,
            $"{tool.Value.Reference}|{tool.Value.Lot}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>Substitui a ferramenta que ocupa uma posição (UM comando atómico).</summary>
    public async Task<Result<bool, DomainError>> SubstituirAsync(
        SubstituirRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var position = WarehouseLocation.NormalizePositionCode(request.PositionCode);
        if (!WarehouseLocation.IsValidPositionCode(position))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "ARMZ_LOCATION_CODE", "A posição deve ter exatamente 4 dígitos."));

        var newTool = await ResolveRequiredAsync(
            request.NewToolType, request.NewReference, request.NewLot, ct);
        if (newTool.IsFailure) return Result<bool, DomainError>.Failure(newTool.Error);

        var locationId = await _repository.GetOrCreateLocationAsync(position, "tool", ct);
        var current = await _repository.GetActiveStockByLocationAsync(locationId, ct);
        if (current is null || !current.IsActive)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "ARMZ_POSITION_FREE",
                "A posição não tem uma ocupação registada para substituir."));

        var now = _clock.UtcNow;
        var newStock = new WarehouseStock
        {
            WarehouseLocationId = locationId,
            ToolId = newTool.Value.ToolId,
            OccupiedSinceUtc = now,
            OccupiedBy = gate.Value.ActorId
        };
        var outMovement = new WarehouseMovement
        {
            WarehouseStockId = current.WarehouseStockId,
            Direction = WarehouseMovementDirection.Out,
            Destination = "substituicao",
            ActorId = gate.Value.ActorId,
            OccurredAtUtc = now
        };
        var inMovement = new WarehouseMovement
        {
            WarehouseStockId = null,
            Direction = WarehouseMovementDirection.In,
            ActorId = gate.Value.ActorId,
            OccurredAtUtc = now
        };

        await _repository.ReplaceOccupationAsync(
            current.WarehouseStockId, newStock, outMovement, inMovement, ct);
        await _repository.InsertAuditEventAsync(
            current.WarehouseStockId, "armazem.substituir", null,
            $"{position}|{newTool.Value.Reference}|{newTool.Value.Lot}", gate.Value.ActorId, ct);
        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>Consulta — pesquisa por tipo/referência/lote/posição com alertas.</summary>
    public async Task<Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>> ConsultarAsync(
        ConsultarRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>.Failure(gate.Error);

        if (!string.IsNullOrWhiteSpace(request.PositionCode))
            return await ConsultarPorPosicaoAsync(
                WarehouseLocation.NormalizePositionCode(request.PositionCode), ct);

        if (string.IsNullOrWhiteSpace(request.ToolType) &&
            string.IsNullOrWhiteSpace(request.Reference) &&
            string.IsNullOrWhiteSpace(request.Lot))
        {
            return Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>.Failure(
                DomainError.Validation("ARMZ_SEARCH_REQUIRED",
                    "Indique um tipo, referência, lote ou posição para pesquisar."));
        }

        var searchType = request.ToolType ?? "CM";
        var identities = await _toolResolver.SearchAsync(searchType, request.Reference, request.Lot, ct);

        var rows = new List<ArmazemConsultationRow>();
        foreach (var identity in identities.Where(i => i.Reference is not null))
        {
            var stock = await _repository.GetActiveStockByToolIdAsync(identity.ToolId, ct);
            string? position = null;
            string context = "nao_registado";
            if (stock is not null && stock.IsActive)
            {
                position = await GetLocationCodeAsync(stock.WarehouseLocationId, ct);
                context = "armazem";
            }
            else
            {
                context = "fora";
            }
            rows.Add(new ArmazemConsultationRow(
                identity.ToolId, identity.Type, identity.Reference!, identity.TechnicalName,
                identity.Lot, position, context, HasReferenceConflict: false));
        }
        return Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>.Success(rows.AsReadOnly());
    }

    private async Task<Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>> ConsultarPorPosicaoAsync(
        string positionCode, CancellationToken ct)
    {
        var location = await _repository.GetLocationByCodeAsync(positionCode, ct);
        if (location is null)
            return Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>.Success(
                new List<ArmazemConsultationRow>());

        var stocks = await _repository.GetStockByLocationAsync(location.WarehouseLocationId, ct);
        var active = stocks.Where(s => s.IsActive).ToList();

        var identities = new Dictionary<Guid, WarehouseToolIdentity>();
        foreach (var stock in active)
        {
            var identity = await _toolResolver.ResolveAsync(stock.ToolId, ct);
            if (identity is not null && identity.Reference is not null)
                identities[stock.ToolId] = identity;
        }

        var distinctReferences = identities.Values.Select(i => i.Reference).Where(r => r is not null).Distinct(StringComparer.Ordinal);
        var conflict = distinctReferences.Count() > 1;

        var rows = new List<ArmazemConsultationRow>();
        foreach (var stock in active)
        {
            if (!identities.TryGetValue(stock.ToolId, out var identity)) continue;
            rows.Add(new ArmazemConsultationRow(
                identity.ToolId, identity.Type, identity.Reference!, identity.TechnicalName,
                identity.Lot, location.Code, "armazem", conflict));
        }
        return Result<IReadOnlyList<ArmazemConsultationRow>, DomainError>.Success(rows.AsReadOnly());
    }

    /// <summary>Histórico de localização/movimentos de um lote (só localização).</summary>
    public async Task<Result<IReadOnlyList<ArmazemHistoryEntry>, DomainError>> HistoricoAsync(
        string toolType, string? reference, string? lot, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<ArmazemHistoryEntry>, DomainError>.Failure(gate.Error);

        var tool = await ResolveRequiredAsync(toolType, reference, lot, ct);
        if (tool.IsFailure) return Result<IReadOnlyList<ArmazemHistoryEntry>, DomainError>.Failure(tool.Error);

        var movements = await _repository.GetMovementHistoryAsync(tool.Value.ToolId, ct);
        var entries = movements.Select(m => new ArmazemHistoryEntry(
            WarehouseMovementDirectionCodec.ToStorage(m.Direction), null, m.Destination,
            null, m.ActorId, m.OccurredAtUtc)).ToList();
        return Result<IReadOnlyList<ArmazemHistoryEntry>, DomainError>.Success(entries.AsReadOnly());
    }

    // ---- helpers -----------------------------------------------------------

    private async Task<Result<WarehouseToolIdentity, DomainError>> ResolveRequiredAsync(
        string type, string? reference, string? lot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reference) && string.IsNullOrWhiteSpace(lot))
            return Result<WarehouseToolIdentity, DomainError>.Failure(DomainError.Validation(
                "ARMZ_TOOL_REQUIRED",
                "Indique uma referência ou um lote para identificar a ferramenta."));

        var identities = await _toolResolver.SearchAsync(type, reference, lot, ct);
        var match = identities.FirstOrDefault(i =>
            (string.IsNullOrWhiteSpace(reference) || i.Reference.Equals(reference, StringComparison.Ordinal)) &&
            (string.IsNullOrWhiteSpace(lot) || i.Lot.Equals(lot, StringComparison.Ordinal)));

        if (match is null)
            return Result<WarehouseToolIdentity, DomainError>.Failure(DomainError.NotFound(
                "ARMZ_TOOL_NOT_FOUND",
                "Ferramenta não encontrada. Verifique referência e lote."));

        return Result<WarehouseToolIdentity, DomainError>.Success(match);
    }

    private async Task<string?> GetLocationCodeAsync(Guid locationId, CancellationToken ct)
    {
        var location = await _repository.GetLocationByIdAsync(locationId, ct);
        return location?.Code;
    }
}