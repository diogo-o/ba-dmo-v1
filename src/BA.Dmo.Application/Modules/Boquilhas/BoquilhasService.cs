using System.Text.Json;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Boquilhas;

/// <summary>
/// U-19 — Boquilhas application service (GLM-BQ-01..15; 01_BOQUILHAS_SPEC).
/// Implements the canonical operational flow ONLY (U-19 D1/D2): daily,
/// high-frequency, quantity-based lot + trace management with Entrada/Saída/
/// Não reparadas/Corrigir contagem, the CONFIRMED 20→25 excess-return rule
/// (matched/unmatched/exceptionalReceived), lifecycle, repairers-per-line and a
/// read-only historical/aggregate view. It never writes Ferramentas, Armazém or
/// Reparação-Externa and never consumes a live Job On lookup (immutable snapshots
/// remain the default historical integration). Every write is transactional and
/// emits its global <c>audit_events</c> fact (UD-17/TD-19) in the same unit.
/// </summary>
public sealed class BoquilhasService
{
    private readonly IBoquilhasRepository _repository;
    private readonly IBoquilhasUnitOfWorkFactory _unitOfWorkFactory;
    private readonly BqAuthorizationGate _gate;
    private readonly IClock _clock;

    public BoquilhasService(
        IBoquilhasRepository repository,
        IBoquilhasUnitOfWorkFactory unitOfWorkFactory,
        BqAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _gate = gate;
        _clock = clock;
    }

    // ---- Create lot + first trace --------------------------------------------

    public async Task<Result<Guid, DomainError>> CreateLoteWithTraceAsync(
        CreateBqLoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var reference = (request.Reference ?? string.Empty).Trim();
        var batchCode = (request.BatchCode ?? string.Empty).Trim();
        if (!BqRules.IsValidReference(reference))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                BqRules.ReferenceInvalidCode,
                "Referência inválida. O formato canónico é uma letra seguida de três dígitos (ex.: T194)."));

        if (string.IsNullOrWhiteSpace(batchCode))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "BQ_BATCH_REQUIRED", "O lote é obrigatório."));

        if (request.AllowedLines is null || request.AllowedLines.Count == 0)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "BQ_ALLOWED_LINES_REQUIRED", "Selecione pelo menos uma linha permitida."));

        var initialQty = BqRules.ValidateQuantity(request.InitialQuantity);
        if (initialQty.IsFailure)
            return Result<Guid, DomainError>.Failure(initialQty.Error);

        if (request.InitialUtilisation is not null)
        {
            var utilResult = BqRules.ValidateUtilisation(request.InitialUtilisation.Value);
            if (utilResult.IsFailure)
                return Result<Guid, DomainError>.Failure(utilResult.Error);
        }

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            if (await _repository.GetLoteByReferenceBatchAsync(reference, batchCode, ct) is not null)
                return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                    "BQ_DUPLICATE_LOT",
                    $"Já existe um lote {batchCode} para a referência {reference}."));

            var lote = new BqLote
            {
                Reference = reference,
                BatchCode = batchCode,
                AllowedLines = request.AllowedLines.Select(ToCanonicalLine).Distinct(StringComparer.Ordinal).ToList(),
                LifecycleState = BqLifecycleState.Available,
                CreatedBy = gate.Value.ActorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await _repository.CreateLoteAsync(uow, lote, ct);

            var trace = new BqTrace
            {
                BqTraceId = Guid.NewGuid(),
                BqLoteId = lote.BqLoteId,
                Status = BqTraceStatus.Active,
                Purpose = BqTracePurpose.Production,
                StartLine = lote.AllowedLines.Count > 0 ? lote.AllowedLines[0] : null,
                SapStart = request.InitialUtilisation is null ? null : decimal.Round(request.InitialUtilisation.Value, 2),
                CreatedBy = gate.Value.ActorId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await _repository.CreateTraceAsync(uow, trace, ct);

            var start = new BqMovement
            {
                BqMovementId = Guid.NewGuid(),
                BqTraceId = trace.BqTraceId,
                MovementType = BqMovementType.Inicio,
                Qty = request.InitialQuantity,
                Line = null,
                Notes = request.Notes,
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = now
            };
            await _repository.InsertMovementAsync(uow, start, ct);

            if (request.InitialUtilisation is not null)
            {
                await _repository.InsertUtilisationReadingAsync(uow, new BqUtilisationReading
                {
                    BqTraceId = trace.BqTraceId,
                    ReadingKind = BqUtilisationReadingKind.Initial,
                    Value = request.InitialUtilisation.Value,
                    ActorId = gate.Value.ActorId,
                    OccurredAtUtc = now
                }, ct);
            }

            await _repository.InsertAuditEventAsync(uow, "boquilhas.lote.criar", "bq_lote",
                lote.BqLoteId.ToString(), "succeeded", null, JsonSerializer.Serialize(new { lote.Reference, lote.BatchCode }),
                gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(lote.BqLoteId);
        }
        catch (BqLoteDuplicateException)
        {
            // uq_bq_lotes_reference_batch raced (audit BQ-15): the same
            // (reference, batch) was created concurrently — same clean conflict
            // as the fast-path pre-check.
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "BQ_DUPLICATE_LOT",
                $"Já existe um lote {batchCode} para a referência {reference}."));
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao criar o lote; os valores introduzidos foram preservados."));
        }
    }

    // ---- Register movement ----------------------------------------------------

    public async Task<Result<BqMovementRowDto, DomainError>> RegisterMovementAsync(
        RegisterBqMovementRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<BqMovementRowDto, DomainError>.Failure(gate.Error);

        if (request.MovementType != BqMovementType.Linha && request.Qty is null)
            return Result<BqMovementRowDto, DomainError>.Failure(DomainError.Validation(
                "BQ_QTY_REQUIRED", "A quantidade é obrigatória para este movimento."));

        if (request.MovementType == BqMovementType.Linha && request.Qty is not null)
            return Result<BqMovementRowDto, DomainError>.Failure(DomainError.Validation(
                "BQ_LINE_NO_QTY", "A mudança de linha não tem quantidade."));

        decimal? qty = null;
        if (request.Qty is not null)
        {
            var qResult = BqRules.ValidateQuantity(request.Qty.Value);
            if (qResult.IsFailure)
                return Result<BqMovementRowDto, DomainError>.Failure(qResult.Error);
            qty = qResult.Value;
        }

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var trace = await _repository.GetTraceForMovementAsync(uow, request.BqTraceId, ct);
            if (trace is null)
                return Result<BqMovementRowDto, DomainError>.Failure(DomainError.DomainConflict(
                    BqRules.MovementOnMissingTraceCode, "O trace de produção/reparação não existe."));
            if (trace.Status != BqTraceStatus.Active)
                return Result<BqMovementRowDto, DomainError>.Failure(DomainError.DomainConflict(
                    BqRules.MovementOnClosedTraceCode, "Movimentos só são permitidos num trace ativo."));

            var existing = await _repository.ListMovementsForTraceAsync(trace.BqTraceId, ct);
            var saldos = ComputeSaldos(existing, out _);
            if (saldos.IsFailure)
                return Result<BqMovementRowDto, DomainError>.Failure(saldos.Error);

            var movement = new BqMovement
            {
                BqMovementId = Guid.NewGuid(),
                BqTraceId = trace.BqTraceId,
                MovementType = request.MovementType,
                Qty = qty,
                Line = ToCanonicalLineOrNull(request.Line),
                RepairerId = request.RepairerId,
                Notes = NormalizeNull(request.Notes),
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = now
            };

            // Validate the new movement against the current effective saldos and,
            // for a return that exceeds the expected repair, compute the exceptional
            // quantity (CONFIRMED matched/unmatched, UD-08/UD-09).
            decimal? exceptional = null;
            if (request.MovementType == BqMovementType.Entrada && qty is not null)
            {
                var rec = BqInventoryCalculator.ReconcileReturn(qty.Value, saldos.Value.Repair);
                if (rec.UnmatchedQty > 0)
                    exceptional = rec.UnmatchedQty;
            }

            var validated = BqInventoryCalculator.Apply(saldos.Value, movement);
            if (validated.IsFailure)
                return Result<BqMovementRowDto, DomainError>.Failure(validated.Error);

            // Only a return may carry a repairer (repair movement association).
            if (request.MovementType is BqMovementType.Saida && request.RepairerId is not null)
                movement.RepairerId = request.RepairerId;

            movement.ExceptionalReceivedQty = exceptional;
            await _repository.InsertMovementAsync(uow, movement, ct);

            // Record the return-excess as a first-class discrepancy (C27/UD-08).
            Guid? discrepancyId = null;
            if (request.MovementType == BqMovementType.Entrada && qty is not null && exceptional is > 0)
            {
                var discrepancy = new BqDiscrepancy
                {
                    BqLoteId = trace.BqLoteId,
                    BqTraceId = trace.BqTraceId,
                    ExpectedQty = Math.Max(0, validated.Value.ExceptionalReceived - exceptional.Value),
                    ActualQty = qty.Value,
                    ExcessQty = exceptional.Value,
                    Status = BqDiscrepancyStatus.Open,
                    CreatedBy = gate.Value.ActorId,
                    CreatedAtUtc = now
                };
                discrepancyId = discrepancy.BqDiscrepancyId;
                await _repository.InsertDiscrepancyAsync(uow, discrepancy, ct);
            }

            var after = BqInventoryCalculator.Apply(saldos.Value, movement).Value;
            await _repository.InsertAuditEventAsync(uow, AuditActionFor(request.MovementType), "bq_movement",
                movement.BqMovementId.ToString(), "succeeded",
                SerializeSaldos(saldos.Value), SerializeSaldos(after), gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);

            var row = new BqMovementRowDto(
                movement.BqMovementId, movement.MovementType, movement.Qty, movement.ExceptionalReceivedQty,
                movement.Line, movement.RepairerId, null, movement.Notes, movement.ActorId,
                movement.OccurredAtUtc, after);
            return Result<BqMovementRowDto, DomainError>.Success(row);
        }
        catch (Exception)
        {
            return Result<BqMovementRowDto, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao registar o movimento; os dados introduzidos foram preservados."));
        }
    }

    // ---- Lot summary / history (read) ---------------------------------------

    public async Task<Result<BqLotSummaryDto, DomainError>> GetLotSummaryAsync(
        Guid bqLoteId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<BqLotSummaryDto, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(bqLoteId, ct);
        if (lote is null)
            return NotFound<BqLotSummaryDto>();

        var activeTrace = await _repository.GetActiveTraceForLoteAsync(bqLoteId, ct);
        var traceId = activeTrace?.BqTraceId ?? (await _repository.GetLastClosedOrActiveTraceAsync(bqLoteId, ct))?.BqTraceId;
        var movements = traceId is null
            ? Array.Empty<BqMovement>()
            : await _repository.ListMovementsForTraceAsync(traceId.Value, ct);
        var saldos = ComputeSaldos(movements, out _);
        var initialUtil = activeTrace is null ? null : await _repository.GetUtilisationReadingAsync(activeTrace.BqTraceId, BqUtilisationReadingKind.Initial, ct);
        var finalUtil = activeTrace is null ? null : await _repository.GetUtilisationReadingAsync(activeTrace.BqTraceId, BqUtilisationReadingKind.Final, ct);

        return Result<BqLotSummaryDto, DomainError>.Success(new BqLotSummaryDto(
            MapLote(lote),
            activeTrace is null ? null : new BqTraceDto(activeTrace.BqTraceId, activeTrace.Status, activeTrace.Purpose,
                activeTrace.StartLine, activeTrace.SapStart, activeTrace.SapEnd),
            MapSaldos(saldos.IsSuccess ? saldos.Value : new BqSaldos()),
            initialUtil?.Value,
            finalUtil?.Value,
            movements.Count));
    }

    public async Task<Result<IReadOnlyList<BqMovementRowDto>, DomainError>> ListMovementsAsync(
        Guid? bqLoteId, BqHistoryFilter filter, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<BqMovementRowDto>, DomainError>.Failure(gate.Error);

        var rows = await _repository.ListMovementsAsync(filter, ct);
        return Result<IReadOnlyList<BqMovementRowDto>, DomainError>.Success(
            await EnrichMovementRowsAsync(rows, ct));
    }

    /// <summary>
    /// Enriches raw movement facts with the read/Registo + Histórico view fields:
    /// reference + lote (resolved via the trace's lot), repairer name, and the
    /// AUTHORITATIVE running <c>prod</c> balance after each movement (computed by
    /// replaying the trace's movements chronologically with the canonical
    /// <see cref="BqInventoryCalculator"/>). The listed saldo is never a derived
    /// formula and never a separate per-row column — it is the true running balance
    /// of the respecting trace (BOQUILHAS_INTERFACE_BEHAVIOR §5/§8).
    /// </summary>
    private async Task<IReadOnlyList<BqMovementRowDto>> EnrichMovementRowsAsync(
        IReadOnlyList<BqMovement> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return Array.Empty<BqMovementRowDto>();

        var repairerNames = new Dictionary<Guid, string>();
        foreach (var r in await _repository.ListRepairersAsync(onlyActive: false, ct))
            repairerNames[r.RepairerId] = r.Name;

        // Resolve each distinct trace's lot (reference/lote) and precompute the running
        // saldo-after map for every movement id of that trace.
        var traceIds = rows.Select(m => m.BqTraceId).Distinct().ToList();
        var saldoAfterById = new Dictionary<Guid, BqSaldos>();
        var referenceByTrace = new Dictionary<Guid, (string Reference, string BatchCode)>();
        foreach (var traceId in traceIds)
        {
            var traceMovements = await _repository.ListMovementsForTraceAsync(traceId, ct);
            var ordered = traceMovements.OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.BqMovementId).ToList();
            var state = new BqSaldos();
            foreach (var m in ordered)
            {
                if (BqInventoryCalculator.Apply(state, m).IsSuccess)
                    state = BqInventoryCalculator.Apply(state, m).Value;
                saldoAfterById[m.BqMovementId] = state.Clone();
            }

            // Reference/lote come from the trace's lot (parent bq_lotes).
            var trace = await _repository.GetTraceByIdAsync(traceId, ct);
            if (trace is null) continue;
            var lote = await _repository.GetLoteByIdAsync(trace.BqLoteId, ct);
            if (lote is not null)
                referenceByTrace[traceId] = (lote.Reference, lote.BatchCode);
        }

        var result = new List<BqMovementRowDto>(rows.Count);
        foreach (var r in rows)
        {
            var (reference, batchCode) = referenceByTrace.TryGetValue(r.BqTraceId, out var rc) ? rc : (default(string), default(string));
            var saldoAfter = saldoAfterById.TryGetValue(r.BqMovementId, out var s) ? s : new BqSaldos();
            result.Add(new BqMovementRowDto(
                r.BqMovementId, r.MovementType, r.Qty, r.ExceptionalReceivedQty, r.Line, r.RepairerId,
                r.RepairerId is not null && repairerNames.TryGetValue(r.RepairerId.Value, out var rn) ? rn : null,
                r.Notes, r.ActorId, r.OccurredAtUtc, saldoAfter,
                reference, batchCode));
        }
        return result.AsReadOnly();
    }

    public async Task<Result<IReadOnlyList<BqLoteDto>, DomainError>> ListLotesAsync(
        BqLoteFilter filter, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<BqLoteDto>, DomainError>.Failure(gate.Error);

        var lotes = await _repository.ListLotesAsync(filter, ct);
        return Result<IReadOnlyList<BqLoteDto>, DomainError>.Success(lotes.Select(MapLote).ToList().AsReadOnly());
    }

    // ---- Close trace ----------------------------------------------------------

    public async Task<Result<bool, DomainError>> CloseTraceAsync(
        CloseBqTraceRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var trace = await _repository.GetTraceByIdAsync(request.BqTraceId, ct);
            if (trace is null) return NotFound<bool>();
            if (trace.Status != BqTraceStatus.Active)
                return Result<bool, DomainError>.Failure(DomainError.Validation(
                    "BQ_TRACE_NOT_ACTIVE", "Apenas um trace ativo pode ser fechado."));

            if (request.FinalUtilisation is not null)
            {
                var uResult = BqRules.ValidateUtilisation(request.FinalUtilisation.Value);
                if (uResult.IsFailure)
                    return Result<bool, DomainError>.Failure(uResult.Error);
                await _repository.InsertUtilisationReadingAsync(uow, new BqUtilisationReading
                {
                    BqTraceId = trace.BqTraceId,
                    ReadingKind = BqUtilisationReadingKind.Final,
                    Value = request.FinalUtilisation.Value,
                    ActorId = gate.Value.ActorId,
                    OccurredAtUtc = now
                }, ct);
            }

            await _repository.CloseTraceAsync(uow, trace.BqTraceId, ct);
            await _repository.InsertAuditEventAsync(uow, "boquilhas.trace.fechar", "bq_trace",
                trace.BqTraceId.ToString(), "succeeded", null, request.Reason, gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao fechar o trace."));
        }
    }

    // ---- Reopen (last closed only) -------------------------------------------

    public async Task<Result<bool, DomainError>> ReopenTraceAsync(
        ReopenBqTraceRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        if (await _repository.GetActiveTraceForLoteAsync(request.BqLoteId, ct) is not null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                BqRules.ReopenHasActiveTraceCode,
                "Não pode reabrir: já existe um trace ativo para este lote."));

        var last = await _repository.GetLastClosedOrActiveTraceAsync(request.BqLoteId, ct);
        if (last is null || last.BqTraceId != request.BqTraceId || last.Status != BqTraceStatus.Closed)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                BqRules.ReopenNotLastCode,
                "Só o último trace fechado pode ser reaberto."));

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            await _repository.ReopenTraceAsync(uow, request.BqTraceId, ct);
            await _repository.AppendReopenHistoryAsync(uow, request.BqTraceId, gate.Value.ActorId, now, ct);
            await _repository.InsertAuditEventAsync(uow, "boquilhas.trace.reabrir", "bq_trace",
                request.BqTraceId.ToString(), "succeeded", null, request.Reason, gate.Value.ActorId, now, ct);
            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao reabrir o trace."));
        }
    }

    // ---- Edit lot --------------------------------------------------------------

    public async Task<Result<bool, DomainError>> EditLoteAsync(
        EditBqLoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(request.BqLoteId, ct);
        if (lote is null) return NotFound<bool>();

        if (request.Reference is not null && !BqRules.IsValidReference(request.Reference.Trim()))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                BqRules.ReferenceInvalidCode, "Referência inválida."));

        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            if (request.Reference is not null) lote.Reference = request.Reference.Trim();
            if (request.BatchCode is not null) lote.BatchCode = request.BatchCode.Trim();
            if (request.AllowedLines is not null)
            {
                var lines = request.AllowedLines.Select(ToCanonicalLine).Distinct(StringComparer.Ordinal).ToList();
                if (lines.Count == 0)
                    return Result<bool, DomainError>.Failure(DomainError.Validation(
                        "BQ_ALLOWED_LINES_REQUIRED", "Selecione pelo menos uma linha permitida."));
                lote.AllowedLines = lines;
            }
            lote.UpdatedAtUtc = _clock.UtcNow;
            await _repository.UpdateLoteAsync(uow, lote, ct);

            var before = JsonSerializer.Serialize(new { request.BqLoteId });
            await _repository.InsertAuditEventAsync(uow, "boquilhas.lote.editar", "bq_lote",
                lote.BqLoteId.ToString(), "succeeded", before, request.ChangeNote, gate.Value.ActorId, _clock.UtcNow, ct);
            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao editar o lote."));
        }
    }

    // ---- Lifecycle -------------------------------------------------------------

    public async Task<Result<bool, DomainError>> ApplyLifecycleAsync(
        BqLifecycleRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var lote = await _repository.GetLoteByIdAsync(request.BqLoteId, ct);
        if (lote is null) return NotFound<bool>();

        if (await _repository.GetActiveTraceForLoteAsync(request.BqLoteId, ct) is not null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                BqRules.LifecycleActiveTraceCode,
                "Não pode arquivar/sucatar: o lote tem um trace ativo."));

        var target = request.Kind switch
        {
            BqLifecycleEventKind.Archived or BqLifecycleEventKind.Retired => BqLifecycleState.Archived,
            BqLifecycleEventKind.Scrapped => BqLifecycleState.Scrapped,
            BqLifecycleEventKind.Restored => BqLifecycleState.Available,
            _ => throw new ArgumentOutOfRangeException(nameof(request.Kind))
        };

        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            await _repository.UpdateLifecycleStateAsync(uow, request.BqLoteId, target, ct);
            await _repository.InsertLifecycleEventAsync(uow, new BqLifecycleEvent
            {
                BqLoteId = request.BqLoteId,
                Kind = request.Kind,
                Reason = NormalizeNull(request.Reason),
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = _clock.UtcNow
            }, ct);
            await _repository.InsertAuditEventAsync(uow, $"boquilhas.lote.{BqLifecycleEventKindCodec.ToStorage(request.Kind)}",
                "bq_lote", request.BqLoteId.ToString(), "succeeded", null, request.Reason, gate.Value.ActorId, _clock.UtcNow, ct);
            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao aplicar o estado de ciclo de vida."));
        }
    }

    // ---- Discrepancies -----------------------------------------------------------

    public async Task<Result<IReadOnlyList<BqDiscrepancyDto>, DomainError>> ListDiscrepanciesAsync(
        Guid? bqLoteId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<BqDiscrepancyDto>, DomainError>.Failure(gate.Error);
        var rows = await _repository.ListDiscrepanciesAsync(bqLoteId, ct);
        return Result<IReadOnlyList<BqDiscrepancyDto>, DomainError>.Success(rows.Select(d =>
            new BqDiscrepancyDto(d.BqDiscrepancyId, d.BqLoteId, d.BqTraceId, d.ExpectedQty, d.ActualQty,
                d.ExcessQty, d.Status, d.ResolutionNote, d.CreatedAtUtc)).ToList().AsReadOnly());
    }

    public async Task<Result<bool, DomainError>> ResolveDiscrepancyAsync(
        ResolveBqDiscrepancyRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var disc = (await _repository.ListDiscrepanciesAsync(null, ct)).FirstOrDefault(d => d.BqDiscrepancyId == request.BqDiscrepancyId);
        if (disc is null) return NotFound<bool>();

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "BQ_RESOLUTION_NOTE_REQUIRED", "A nota de resolução é obrigatória."));

        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            var target = new BqDiscrepancy
            {
                BqDiscrepancyId = disc.BqDiscrepancyId,
                BqLoteId = disc.BqLoteId,
                BqTraceId = disc.BqTraceId,
                ExpectedQty = disc.ExpectedQty,
                ActualQty = disc.ActualQty,
                ExcessQty = disc.ExcessQty,
                Status = BqDiscrepancyStatus.Resolved,
                ResolutionNote = request.ResolutionNote.Trim(),
                CreatedAtUtc = disc.CreatedAtUtc
            };
            await _repository.UpdateDiscrepancyAsync(uow, target, ct);
            await _repository.InsertAuditEventAsync(uow, "boquilhas.discrepancia.resolver", "bq_discrepancy",
                disc.BqDiscrepancyId.ToString(), "succeeded", null, null, gate.Value.ActorId, _clock.UtcNow, ct);
            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "BQ_SAVE_FAILED", "Falha ao resolver a discrepância."));
        }
    }

    // ---- Repairers ---------------------------------------------------------------

    public async Task<Result<IReadOnlyList<BqRepairerDto>, DomainError>> ListRepairersAsync(
        bool onlyActive, string? type, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<BqRepairerDto>, DomainError>.Failure(gate.Error);
        var rows = await _repository.ListRepairersAsync(onlyActive, ct);

        // TD-15 — Filter by capability: BQ flow shows only BQ-capable repairers,
        // CM flow shows only CM-capable, MF flow shows only MF-capable.
        // A repairer may support multiple types and appear in several flows.
        var filtered = type != null && type.ToUpperInvariant() is "CM" or "MF" or "BQ"
            ? rows.Where(r => r.SupportedTypes.Contains(type.ToUpperInvariant())).ToList()
            : rows.ToList();

        return Result<IReadOnlyList<BqRepairerDto>, DomainError>.Success(
            filtered.Select(r => new BqRepairerDto(r.RepairerId, r.Name, r.Active, r.SupportedTypes)).ToList().AsReadOnly());
    }

    public async Task<Result<Guid, DomainError>> CreateRepairerAsync(
        CreateBqRepairerRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "BQ_REPAIRER_NAME_REQUIRED", "O nome do reparador é obrigatório."));
        var repairer = new BqRepairer
        {
            Name = name,
            Active = true,
            CreatedBy = gate.Value.ActorId,
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow
        };
        return Result<Guid, DomainError>.Success(await _repository.CreateRepairerAsync(repairer, ct));
    }

    public async Task<Result<bool, DomainError>> UpdateRepairerAsync(
        UpdateBqRepairerRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);
        var repairer = await _repository.GetRepairerByIdAsync(request.RepairerId, ct);
        if (repairer is null) return NotFound<bool>();
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Result<bool, DomainError>.Failure(DomainError.Validation(
                    "BQ_REPAIRER_NAME_REQUIRED", "O nome do reparador é obrigatório."));
            repairer.Name = name;
        }
        if (request.Active is not null) repairer.Active = request.Active.Value;
        repairer.UpdatedAtUtc = _clock.UtcNow;
        await _repository.UpdateRepairerAsync(repairer, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> SetLineRepairerDefaultAsync(
        SetLineRepairerDefaultRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);
        if (!BoquilhasModuleCatalog.Lines.Contains(request.Line, StringComparer.Ordinal))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "BQ_LINE_INVALID", "Linha inválida."));

        // If the default is deactivated, the line requires a new association (brief §9).
        if (request.DefaultRepairerId is not null)
        {
            var def = await _repository.GetRepairerByIdAsync(request.DefaultRepairerId.Value, ct);
            if (def is null || !def.Active)
                return Result<bool, DomainError>.Failure(DomainError.Validation(
                    "BQ_DEFAULT_REPAIRER_INACTIVE",
                    "O reparador predefinido está inativo; associe outro predefinido."));
        }

        await _repository.SetLineRepairerDefaultAsync(new BqLineRepairerDefault
        {
            Line = request.Line,
            DefaultRepairerId = request.DefaultRepairerId,
            AllowedRepairerIds = request.AllowedRepairerIds ?? Array.Empty<Guid>()
        }, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Private helpers ---------------------------------------------------------

    private static Result<BqSaldos, DomainError> ComputeSaldos(
        IEnumerable<BqMovement> movements, out IReadOnlyList<BqMovement> effective)
    {
        var ordered = movements
            .OrderBy(m => m.OccurredAtUtc)
            .ThenBy(m => m.BqMovementId)
            .ToList();
        effective = ordered;
        var state = new BqSaldos();
        foreach (var m in ordered)
        {
            var applied = BqInventoryCalculator.Apply(state, m);
            if (applied.IsFailure)
                return Result<BqSaldos, DomainError>.Failure(applied.Error);
            state = applied.Value;
        }
        return Result<BqSaldos, DomainError>.Success(state);
    }

    private static BqLoteDto MapLote(BqLote l) => new(
        l.BqLoteId, l.Reference, l.BatchCode, l.AllowedLines, l.LifecycleState, l.CreatedBy ?? string.Empty);

    private static BqSaldosDto MapSaldos(BqSaldos s) => new(
        s.Prod, s.Repair, s.Irreparable, s.ExceptionalReceived, s.TransactionalBalance, s.PhysicalInventory);

    private static string SerializeSaldos(BqSaldos s) => JsonSerializer.Serialize(new
    {
        prod = s.Prod,
        repair = s.Repair,
        irreparable = s.Irreparable,
        exceptional = s.ExceptionalReceived,
        transactional = s.TransactionalBalance
    });

    private static string ToCanonicalLine(string line) => (line ?? string.Empty).Trim().ToUpperInvariant();

    private static string? ToCanonicalLineOrNull(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        return line.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AuditActionFor(BqMovementType type) => type switch
    {
        BqMovementType.Saida => "boquilhas.movimento.saida",
        BqMovementType.Entrada => "boquilhas.movimento.entrada",
        BqMovementType.Irreparavel => "boquilhas.movimento.irreparavel",
        BqMovementType.Linha => "boquilhas.movimento.linha",
        BqMovementType.Contagem => "boquilhas.movimento.contagem",
        _ => "boquilhas.movimento"
    };

    private static Result<T, DomainError> NotFound<T>() =>
        Result<T, DomainError>.Failure(DomainError.NotFound("BQ_NOT_FOUND",
            "Registo de Boquilhas não encontrado."));
}