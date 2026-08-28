using System.Text.Json;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões application service (GLM-TP-01..13; TAMPOES_DESIGN_BRIEF §1–§15).
/// Aggregate quantity control by technical configuration, mobile-first. Tampões are
/// NOT tools (no Ferramentas/Armazém write) and there are no individual numbers in
/// V1. Every relevant change writes its append-only movement + global audit_events
/// row, and the atomic transfers (alterar estado / alterar configuração) update all
/// involved saldos + movement + audit in ONE <see cref="IDbUnitOfWork"/>
/// (GLM-DATA-05/07). Balances are derived from facts and never go negative.
/// </summary>
public sealed class TampaoService
{
    private readonly ITampaoRepository _repository;
    private readonly ITampoesUnitOfWorkFactory _unitOfWorkFactory;
    private readonly TampaoAuthorizationGate _gate;
    private readonly IClock _clock;

    public TampaoService(
        ITampaoRepository repository,
        ITampoesUnitOfWorkFactory unitOfWorkFactory,
        TampaoAuthorizationGate gate,
        IClock clock)
    {
        _repository = repository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _gate = gate;
        _clock = clock;
    }

    // ---- Consulta -------------------------------------------------------------

    public async Task<Result<IReadOnlyList<TampaoConfigurationDto>, DomainError>> ConsultarAsync(
        ConsultaFilter? filter, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<IReadOnlyList<TampaoConfigurationDto>, DomainError>.Failure(gate.Error);

        IReadOnlyList<TampaoConfiguration> configs;
        if (filter?.Machine is not null)
        {
            var machineResult = TampaoMachine.Validate(filter.Machine);
            if (machineResult.IsFailure)
                return Result<IReadOnlyList<TampaoConfigurationDto>, DomainError>.Failure(machineResult.Error);
            // R008: any machine matches → configuration returned once (join is on configuration).
            configs = await _repository.ListConfigurationsByMachineAsync(machineResult.Value, ct);
        }
        else if (filter?.ConfigurationId is not null)
        {
            configs = new[] { await _repository.GetConfigurationByIdAsync(filter.ConfigurationId.Value, ct) }
                .Where(c => c is not null).Select(c => c!).ToList();
        }
        else
        {
            configs = await _repository.ListConfigurationsAsync(onlyActive: true, ct);
        }

        var dtos = new List<TampaoConfigurationDto>();
        foreach (var config in configs)
        {
            var saldo = await _repository.GetSaldoByConfigurationAsync(config.TampaoConfigurationId, ct);
            var machines = await _repository.GetMachinesByConfigurationAsync(config.TampaoConfigurationId, ct);
            dtos.Add(new TampaoConfigurationDto(
                config.TampaoConfigurationId, config.Values, config.Active,
                saldo?.Enchidos ?? 0, saldo?.PorEncher ?? 0, machines));
        }
        return Result<IReadOnlyList<TampaoConfigurationDto>, DomainError>.Success(dtos.AsReadOnly());
    }

    public async Task<Result<TampaoConfigurationDto, DomainError>> GetConfigurationAsync(
        Guid configurationId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<TampaoConfigurationDto, DomainError>.Failure(gate.Error);

        var config = await _repository.GetConfigurationByIdAsync(configurationId, ct);
        if (config is null)
            return NotFound<TampaoConfigurationDto>();
        var saldo = await _repository.GetSaldoByConfigurationAsync(configurationId, ct);
        var machines = await _repository.GetMachinesByConfigurationAsync(configurationId, ct);
        return Result<TampaoConfigurationDto, DomainError>.Success(new TampaoConfigurationDto(
            config.TampaoConfigurationId, config.Values, config.Active,
            saldo?.Enchidos ?? 0, saldo?.PorEncher ?? 0, machines));
    }

    // ---- Record / detail sheet (R008) ------------------------------------------

    public async Task<Result<TampaoConfigurationDetailDto, DomainError>> GetConfigurationDetailAsync(
        Guid configurationId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure)
            return Result<TampaoConfigurationDetailDto, DomainError>.Failure(gate.Error);

        var config = await _repository.GetConfigurationByIdAsync(configurationId, ct);
        if (config is null) return NotFound<TampaoConfigurationDetailDto>();
        var saldo = await _repository.GetSaldoByConfigurationAsync(configurationId, ct);
        var machines = await _repository.GetMachinesByConfigurationAsync(configurationId, ct);
        var notes = await _repository.ListConfigurationNotesAsync(configurationId, ct);
        var events = await _repository.ListMachineEventsAsync(configurationId, ct);

        var configDto = new TampaoConfigurationDto(
            config.TampaoConfigurationId, config.Values, config.Active,
            saldo?.Enchidos ?? 0, saldo?.PorEncher ?? 0, machines);

        var noteDtos = notes.Select(n => new TampaoConfigurationNoteDto(
            n.TampaoConfigurationNoteId, n.Note, n.ActorId, n.OccurredAtUtc)).ToList();
        var eventDtos = events.Select(e => new TampaoMachineEventDto(
            e.Machine, e.Action, e.ActorId, e.OccurredAtUtc)).ToList();

        return Result<TampaoConfigurationDetailDto, DomainError>.Success(
            new TampaoConfigurationDetailDto(configDto, noteDtos.Count > 0 ? noteDtos[^1].Note : null, noteDtos, eventDtos));
    }

    public async Task<Result<bool, DomainError>> SetConfigurationMachinesAsync(
        SetConfigurationMachinesRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var config = await _repository.GetConfigurationByIdAsync(request.ConfigurationId, ct);
        if (config is null) return NotFound<bool>();

        // Validate every machine up front (server-side allowed set; never duplicates UI).
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in request.Machines ?? Array.Empty<string>())
        {
            var r = TampaoMachine.Validate(m);
            if (r.IsFailure) return Result<bool, DomainError>.Failure(r.Error);
            normalized.Add(r.Value);
        }

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var current = await _repository.GetMachinesByConfigurationAsync(request.ConfigurationId, ct);
            await _repository.ReplaceConfigurationMachinesAsync(uow, request.ConfigurationId, normalized, ct);

            // Audit every add/remove as an append-only fact (no silent history loss).
            foreach (var m in normalized)
                if (!current.Contains(m))
                    await _repository.InsertMachineEventAsync(uow, new TampaoMachineEvent
                    {
                        TampaoConfigurationId = request.ConfigurationId,
                        Machine = m,
                        Action = "added",
                        ActorId = gate.Value.ActorId,
                        OccurredAtUtc = now
                    }, ct);
            foreach (var m in current)
                if (!normalized.Contains(m))
                    await _repository.InsertMachineEventAsync(uow, new TampaoMachineEvent
                    {
                        TampaoConfigurationId = request.ConfigurationId,
                        Machine = m,
                        Action = "removed",
                        ActorId = gate.Value.ActorId,
                        OccurredAtUtc = now
                    }, ct);

            await _repository.InsertAuditEventAsync(uow, "tampoes.configuracao.maquinas",
                "tampao_configuration", request.ConfigurationId.ToString(), "succeeded",
                string.Join(",", current), string.Join(",", normalized), gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<bool, DomainError>.Success(true);
        }
        catch (Exception)
        {
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "TAMPAO_SAVE_FAILED", "Falha ao guardar as máquinas associadas."));
        }
    }

    public async Task<Result<Guid, DomainError>> AddConfigurationNoteAsync(
        AddConfigurationNoteRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var note = (request.Note ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(note))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "TAMPAO_NOTE_REQUIRED", "A observação é obrigatória."));

        if (await _repository.GetConfigurationByIdAsync(request.ConfigurationId, ct) is null)
            return NotFound<Guid>();

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
            var noteEntity = new TampaoConfigurationNote
            {
                TampaoConfigurationId = request.ConfigurationId,
                Note = note,
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = now
            };
            await _repository.AddConfigurationNoteAsync(uow, noteEntity, ct);
            await _repository.InsertAuditEventAsync(uow, "tampoes.configuracao.observacao",
                "tampao_configuration", request.ConfigurationId.ToString(), "succeeded",
                null, note, gate.Value.ActorId, now, ct);
            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(noteEntity.TampaoConfigurationNoteId);
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "TAMPAO_SAVE_FAILED", "Falha ao guardar a observação."));
        }
    }

    // ---- Adicionar / Remover (single balance) --------------------------------

    public async Task<Result<Guid, DomainError>> AdicionarQuantidadeAsync(
        AdicionarQuantidadeRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);
        return await ApplySingleBalanceAsync(request.ConfigurationId, request.Balance, +request.Qty,
            TampaoMovementType.Adicionar, gate.Value.ActorId, "tampoes.quantidade.adicionar", ct);
    }

    public async Task<Result<Guid, DomainError>> RemoverQuantidadeAsync(
        RemoverQuantidadeRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);
        return await ApplySingleBalanceAsync(request.ConfigurationId, request.Balance, -request.Qty,
            TampaoMovementType.Remover, gate.Value.ActorId, "tampoes.quantidade.remover", ct);
    }

    private async Task<Result<Guid, DomainError>> ApplySingleBalanceAsync(
        Guid configurationId, TampaoBalanceKind balance, int delta,
        TampaoMovementType type, string actorId, string auditAction, CancellationToken ct)
    {
        var qty = Math.Abs(delta);
        var qtyResult = TampaoRules.ValidateQuantity(qty);
        if (qtyResult.IsFailure)
            return Result<Guid, DomainError>.Failure(qtyResult.Error);

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var saldo = await _repository.GetSaldoInTransactionAsync(uow, configurationId, ct)
                        ?? new TampaoSaldo { TampaoConfigurationId = configurationId };
            var current = saldo.Get(balance);

            var next = TampaoRules.ApplySingleBalanceChange(current, delta);
            if (next.IsFailure)
                return Result<Guid, DomainError>.Failure(next.Error);

            var before = SerializeBalances(saldo);
            var afterSaldo = new TampaoSaldo
            {
                TampaoConfigurationId = configurationId,
                Enchidos = balance == TampaoBalanceKind.Enchidos ? next.Value : saldo.Enchidos,
                PorEncher = balance == TampaoBalanceKind.PorEncher ? next.Value : saldo.PorEncher
            };
            var after = SerializeBalances(afterSaldo);

            await _repository.SetSaldoAsync(uow, configurationId, afterSaldo.Enchidos, afterSaldo.PorEncher, ct);

            var movement = new TampaoMovement
            {
                MovementType = type,
                OriginConfigurationId = configurationId,
                Qty = qty,
                BalancesBefore = before,
                BalancesAfter = after,
                ActorId = actorId,
                OccurredAtUtc = now
            };
            var movementId = await InsertMovementAndAuditAsync(uow, movement, auditAction, "tampao_configuration",
                configurationId.ToString(), "succeeded", before, after, actorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(movementId);
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "TAMPAO_SAVE_FAILED", "Falha ao guardar; os valores introduzidos foram preservados."));
        }
    }

    // ---- Alterar estado (single atomic transfer) ------------------------------

    public async Task<Result<Guid, DomainError>> AlterarEstadoAsync(
        AlterarEstadoRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var qtyResult = TampaoRules.ValidateQuantity(request.Qty);
        if (qtyResult.IsFailure)
            return Result<Guid, DomainError>.Failure(qtyResult.Error);

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var saldo = await _repository.GetSaldoInTransactionAsync(uow, request.ConfigurationId, ct)
                        ?? new TampaoSaldo { TampaoConfigurationId = request.ConfigurationId };
            var origin = TampaoRules.ResolveStateOrigin(saldo, request.Destination, request.Qty);
            if (origin.IsFailure)
                return Result<Guid, DomainError>.Failure(origin.Error);

            var transfer = TampaoRules.ApplyBalanceTransfer(saldo, origin.Value, request.Destination, request.Qty);
            if (transfer.IsFailure)
                return Result<Guid, DomainError>.Failure(transfer.Error);

            var before = SerializeBalances(saldo);
            var after = SerializeBalances(transfer.Value);

            await _repository.SetSaldoAsync(uow, request.ConfigurationId, transfer.Value.Enchidos, transfer.Value.PorEncher, ct);

            var movement = new TampaoMovement
            {
                MovementType = TampaoMovementType.AlterarEstado,
                OriginConfigurationId = request.ConfigurationId,
                DestinationConfigurationId = request.ConfigurationId,
                Qty = request.Qty,
                BalancesBefore = before,
                BalancesAfter = after,
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = now
            };
            var movementId = await InsertMovementAndAuditAsync(uow, movement, "tampoes.estado.alterar",
                "tampao_configuration", request.ConfigurationId.ToString(), "succeeded", before, after,
                gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(movementId);
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "TAMPAO_SAVE_FAILED", "Falha ao guardar; os valores introduzidos foram preservados."));
        }
    }

    // ---- Alterar configuração (atomic origin → destination) --------------------

    public async Task<Result<Guid, DomainError>> AlterarConfiguracaoAsync(
        AlterarConfiguracaoRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var qtyResult = TampaoRules.ValidateQuantity(request.Qty);
        if (qtyResult.IsFailure)
            return Result<Guid, DomainError>.Failure(qtyResult.Error);

        var now = _clock.UtcNow;
        try
        {
            await using var uow = await _unitOfWorkFactory.BeginAsync(ct);

            var origin = await _repository.GetConfigurationByIdAsync(request.OriginConfigurationId, ct);
            if (origin is null)
                return NotFound<Guid>();

            var originSaldo = await _repository.GetSaldoInTransactionAsync(uow, origin.TampaoConfigurationId, ct)
                              ?? new TampaoSaldo { TampaoConfigurationId = origin.TampaoConfigurationId };

            // Chosen balance is Enchidos by default for a technical transformation
            // (quantity of tampões that exist). The origin must have enough in Enchidos.
            var originBalance = originSaldo.Enchidos;
            if (originBalance < request.Qty)
                return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                    TampaoRules.InsufficientOriginCode,
                    $"Saldo de origem insuficiente: Enchidos tem {originBalance}, necessário {request.Qty}."));

            var destinationKey = TampaoConfigurationKey.Serialize(request.DestinationValues);

            // At least one characteristic must change (GLM-TP-05.3): identical
            // destination values would leave origin and destination the same config.
            var originKey = TampaoConfigurationKey.Serialize(origin.Values);
            if (string.Equals(originKey, destinationKey, StringComparison.Ordinal))
                return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                    TampaoRules.NoCharacteristicChangedCode,
                    "Nenhuma característica mudou entre a origem e o destino."));

            var destination = await _repository.FindConfigurationByKeyAsync(destinationKey, ct);
            if (destination is null)
            {
                var newConfig = new TampaoConfiguration
                {
                    Values = request.DestinationValues.ToDictionary(kv => kv.Key, kv => TampaoRules.NormalizeValue(kv.Value)),
                    Active = true,
                    CreatedBy = gate.Value.ActorId,
                    CreatedAtUtc = now
                };
                var id = await _repository.CreateConfigurationAsync(uow, newConfig, destinationKey, ct);
                destination = new TampaoConfiguration
                {
                    TampaoConfigurationId = id,
                    Values = newConfig.Values,
                    Active = true,
                    CreatedBy = gate.Value.ActorId,
                    CreatedAtUtc = now
                };
            }

            var transform = TampaoRules.ValidateConfigurationTransform(origin, destination);
            if (transform.IsFailure)
                return Result<Guid, DomainError>.Failure(transform.Error);

            // Origin Enchidos −qty; destination Enchidos +qty (same configuration transform).
            var originBefore = SerializeBalances(originSaldo);
            var destSaldo = await _repository.GetSaldoInTransactionAsync(uow, destination.TampaoConfigurationId, ct)
                            ?? new TampaoSaldo { TampaoConfigurationId = destination.TampaoConfigurationId };
            var destBefore = SerializeBalances(destSaldo);

            var newOriginEnchidos = originSaldo.Enchidos - request.Qty;
            var newDestEnchidos = destSaldo.Enchidos + request.Qty;
            if (newOriginEnchidos < 0)
                return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                    TampaoRules.NegativeBalanceCode, "Saldo de origem insuficiente."));

            await _repository.SetSaldoAsync(uow, origin.TampaoConfigurationId, newOriginEnchidos, originSaldo.PorEncher, ct);
            await _repository.SetSaldoAsync(uow, destination.TampaoConfigurationId, newDestEnchidos, destSaldo.PorEncher, ct);

            var movement = new TampaoMovement
            {
                MovementType = TampaoMovementType.AlterarConfiguracao,
                OriginConfigurationId = origin.TampaoConfigurationId,
                DestinationConfigurationId = destination.TampaoConfigurationId,
                Qty = request.Qty,
                BalancesBefore = originBefore,
                BalancesAfter = SerializeBalances(new TampaoSaldo { Enchidos = newOriginEnchidos }),
                ActorId = gate.Value.ActorId,
                OccurredAtUtc = now
            };
            var movementId = await InsertMovementAndAuditAsync(uow, movement, "tampoes.configuracao.alterar",
                "tampao_configuration", origin.TampaoConfigurationId.ToString(),
                "succeeded", originBefore, destBefore, gate.Value.ActorId, now, ct);

            await uow.CommitAsync(ct);
            return Result<Guid, DomainError>.Success(movementId);
        }
        catch (TampaoConfigurationDuplicateException)
        {
            // uq_tampao_configurations_values raced (audit TP-06): the destination
            // configuration was created concurrently by another transformation.
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "TAMPAO_CONFIGURATION_DUPLICATE",
                "Já existe uma configuração com estes valores."));
        }
        catch (Exception)
        {
            return Result<Guid, DomainError>.Failure(DomainError.Unexpected(
                "TAMPAO_SAVE_FAILED", "Falha ao guardar; os valores introduzidos foram preservados."));
        }
    }

    // ---- Planning ---------------------------------------------------------------

    public async Task<Result<Guid, DomainError>> PlanearAsync(
        PlanearRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var config = await _repository.GetConfigurationByIdAsync(request.ConfigurationId, ct);
        if (config is null) return NotFound<Guid>();

        var qtyResult = TampaoRules.ValidateQuantity(request.PlannedQty);
        if (qtyResult.IsFailure) return Result<Guid, DomainError>.Failure(qtyResult.Error);

        var plano = new TampaoPlano
        {
            TampaoConfigurationId = request.ConfigurationId,
            PlannedQty = request.PlannedQty,
            PlannedForDate = request.PlannedForDate,
            Notes = request.Notes,
            CreatedBy = gate.Value.ActorId,
            CreatedAtUtc = _clock.UtcNow
        };
        var id = await _repository.CreatePlanoAsync(plano, ct);

        // Planning is informational: no movement, no balance change. Audit records it.
        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
        await _repository.InsertAuditEventAsync(uow, "tampoes.planear", "tampao_plano", id.ToString(),
            "succeeded", null, request.ConfigurationId.ToString(), gate.Value.ActorId, plano.CreatedAtUtc, ct);
        await uow.CommitAsync(ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> CancelarPlanoAsync(
        CancelarPlanoRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var plano = await _repository.GetPlanoByIdAsync(request.PlanoId, ct);
        if (plano is null) return NotFound<bool>();
        if (plano.Canceled)
            return Result<bool, DomainError>.Success(true);

        await using var uow = await _unitOfWorkFactory.BeginAsync(ct);
        await _repository.CancelPlanoAsync(uow, request.PlanoId, ct);
        await _repository.InsertAuditEventAsync(uow, "tampoes.plano.cancelar", "tampao_plano",
            request.PlanoId.ToString(), "succeeded", null, "canceled", gate.Value.ActorId, _clock.UtcNow, ct);
        await uow.CommitAsync(ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<IReadOnlyList<TampaoPlanoDto>, DomainError>> ListPlanosAsync(
        PlanoFilter? filter, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<TampaoPlanoDto>, DomainError>.Failure(gate.Error);

        var planos = await _repository.ListPlanosAsync(
            filter?.IncludeCanceled ?? false, filter?.ConfigurationId, filter?.From, filter?.To, ct);
        var dtos = new List<TampaoPlanoDto>();
        foreach (var p in planos)
        {
            var config = await _repository.GetConfigurationByIdAsync(p.TampaoConfigurationId, ct);
            var saldo = await _repository.GetSaldoByConfigurationAsync(p.TampaoConfigurationId, ct);
            var enchidos = saldo?.Enchidos;
            // Informational difference between the need and the available Enchidos
            // (GLM-TP-05.4): planning NEVER deducts or reserves.
            var difference = enchidos is null ? (int?)null : Math.Max(0, p.PlannedQty - enchidos.Value);
            dtos.Add(new TampaoPlanoDto(
                p.TampaoPlanoId, p.TampaoConfigurationId, p.PlannedQty, p.PlannedForDate,
                p.JobOnId, p.ProductionCode, p.Notes, p.Canceled, p.CreatedAtUtc, p.CreatedBy,
                config is null ? p.TampaoConfigurationId.ToString() : BuildConfigurationLabel(config.Values),
                enchidos, difference));
        }
        return Result<IReadOnlyList<TampaoPlanoDto>, DomainError>.Success(dtos.AsReadOnly());
    }

    // ---- Histórico ---------------------------------------------------------------

    public async Task<Result<IReadOnlyList<TampaoMovimentoDto>, DomainError>> ListMovimentosAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? configurationId, TampaoMovementType? type,
        string? operatorId, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<TampaoMovimentoDto>, DomainError>.Failure(gate.Error);

        var movements = await _repository.ListMovementsAsync(from, to, configurationId, type, operatorId, ct);
        return Result<IReadOnlyList<TampaoMovimentoDto>, DomainError>.Success(movements.Select(Map).ToList().AsReadOnly());
    }

    // ---- Opções: fields & values --------------------------------------------------

    public async Task<Result<IReadOnlyList<TampaoFieldDefDto>, DomainError>> ListFieldDefsAsync(
        bool onlyActive, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<TampaoFieldDefDto>, DomainError>.Failure(gate.Error);
        var fields = await _repository.ListFieldDefsAsync(onlyActive, ct);
        return Result<IReadOnlyList<TampaoFieldDefDto>, DomainError>.Success(
            fields.Select(f => new TampaoFieldDefDto(f.TampaoFieldDefId, f.FieldName, f.Unit, f.PrecisionDigits, f.DisplayOrder, f.Active)).ToList().AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<TampaoFieldValueDto>, DomainError>> ListFieldValuesAsync(
        Guid fieldDefId, bool onlyActive, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<IReadOnlyList<TampaoFieldValueDto>, DomainError>.Failure(gate.Error);
        var values = await _repository.ListFieldValuesAsync(fieldDefId, onlyActive, ct);
        return Result<IReadOnlyList<TampaoFieldValueDto>, DomainError>.Success(
            values.Select(v => new TampaoFieldValueDto(v.TampaoFieldValueId, v.TampaoFieldDefId, v.ValueNumeric, v.ValueLabel, v.DisplayOrder, v.Active)).ToList().AsReadOnly());
    }

    public async Task<Result<Guid, DomainError>> CreateFieldDefAsync(
        CreateFieldDefRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);
        var name = request.FieldName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid, DomainError>.Failure(DomainError.Validation("TAMPAO_FIELD_NAME_REQUIRED",
                "O nome do campo é obrigatório."));

        var field = new TampaoFieldDef
        {
            FieldName = name,
            Unit = request.Unit?.Trim(),
            PrecisionDigits = request.PrecisionDigits,
            DisplayOrder = request.DisplayOrder ?? 0,
            Active = true
        };
        var id = await _repository.CreateFieldDefAsync(field, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> UpdateFieldDefAsync(
        UpdateFieldDefRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        var fields = await _repository.ListFieldDefsAsync(onlyActive: false, ct);
        var field = fields.FirstOrDefault(f => f.TampaoFieldDefId == request.FieldDefId);
        if (field is null) return NotFound<bool>();

        if (request.FieldName is not null)
        {
            var name = request.FieldName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Result<bool, DomainError>.Failure(DomainError.Validation("TAMPAO_FIELD_NAME_REQUIRED",
                    "O nome do campo é obrigatório."));
            field.FieldName = name;
        }
        if (request.Unit is not null) field.Unit = request.Unit.Trim();
        if (request.PrecisionDigits is not null) field.PrecisionDigits = request.PrecisionDigits;
        if (request.DisplayOrder is not null) field.DisplayOrder = request.DisplayOrder.Value;
        if (request.Active is not null) field.Active = request.Active.Value;
        field.UpdatedAtUtc = _clock.UtcNow;

        await _repository.UpdateFieldDefAsync(field, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<Guid, DomainError>> CreateFieldValueAsync(
        CreateFieldValueRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<Guid, DomainError>.Failure(gate.Error);

        var value = new TampaoFieldValue
        {
            TampaoFieldDefId = request.FieldDefId,
            ValueNumeric = TampaoRules.NormalizeValue(request.ValueNumeric),
            ValueLabel = string.IsNullOrWhiteSpace(request.ValueLabel) ? request.ValueNumeric.ToString() : request.ValueLabel.Trim(),
            DisplayOrder = request.DisplayOrder ?? 0,
            Active = true
        };
        var id = await _repository.CreateFieldValueAsync(value, ct);
        return Result<Guid, DomainError>.Success(id);
    }

    public async Task<Result<bool, DomainError>> UpdateFieldValueAsync(
        UpdateFieldValueRequest request, CancellationToken ct = default)
    {
        var gate = _gate.Require();
        if (gate.IsFailure) return Result<bool, DomainError>.Failure(gate.Error);

        // Locate the value across fields (read-only); deactivating never deletes history.
        var fields = await _repository.ListFieldDefsAsync(onlyActive: false, ct);
        foreach (var f in fields)
        {
            var values = await _repository.ListFieldValuesAsync(f.TampaoFieldDefId, onlyActive: false, ct);
            var v = values.FirstOrDefault(x => x.TampaoFieldValueId == request.FieldValueId);
            if (v is not null)
            {
                if (request.ValueLabel is not null) v.ValueLabel = request.ValueLabel.Trim();
                if (request.DisplayOrder is not null) v.DisplayOrder = request.DisplayOrder.Value;
                if (request.Active is not null) v.Active = request.Active.Value;
                v.UpdatedAtUtc = _clock.UtcNow;
                await _repository.UpdateFieldValueAsync(v, ct);
                return Result<bool, DomainError>.Success(true);
            }
        }
        return NotFound<bool>();
    }

    // ---- Private helpers -----------------------------------------------------------

    private async Task<Guid> InsertMovementAndAuditAsync(IDbUnitOfWork uow, TampaoMovement movement,
        string auditAction, string entityType, string entityId, string result,
        string? before, string? after, string actorId, DateTimeOffset now, CancellationToken ct)
    {
        await _repository.InsertMovementAsync(uow, movement, ct);
        await _repository.InsertAuditEventAsync(uow, auditAction, entityType, entityId, result,
            before, after, actorId, now, ct);
        return movement.TampaoMovementId;
    }

    private static TampaoMovimentoDto Map(TampaoMovement m) => new(
        m.TampaoMovementId, TampaoMovementTypeCodec.ToStorage(m.MovementType),
        m.OriginConfigurationId, m.DestinationConfigurationId, m.Qty,
        m.BalancesBefore, m.BalancesAfter, m.ActorId, m.OccurredAtUtc);

    private static string SerializeBalances(TampaoSaldo saldo) => JsonSerializer.Serialize(new
    {
        enchidos = saldo.Enchidos,
        por_encher = saldo.PorEncher
    });

    private static string BuildConfigurationLabel(IReadOnlyDictionary<string, decimal> values)
    {
        var parts = values.Select(kv => $"{kv.Key} {kv.Value.ToString("0.0##")} mm");
        return string.Join(" · ", parts);
    }

    private static Result<T, DomainError> NotFound<T>() =>
        Result<T, DomainError>.Failure(DomainError.NotFound("TAMPAO_NOT_FOUND",
            "Registo de Tampões não encontrado."));
}