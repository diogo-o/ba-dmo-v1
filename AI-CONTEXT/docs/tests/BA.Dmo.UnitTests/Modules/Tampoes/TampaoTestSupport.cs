using System.Data;
using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Tampoes;

/// <summary>Fixed UTC clock for deterministic Tampões service tests.</summary>
public sealed class TampaoFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class TampaoFakeAuthorship(string actorId = "tampoes-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>Fake current-user accessor controlling the tampoes module grant.</summary>
public sealed class TampaoCurrentUser(string? actorId = "tampoes-actor")
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user = actorId is null ? null : new CurrentUser(
        Guid.NewGuid(), "Operador Tampões",
        new[] { TampoesModuleCatalog.ModuleId }, Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static TampaoCurrentUser Authorized() => new("tampoes-actor");
    public static TampaoCurrentUser WithoutModule() => new(null);
}

/// <summary>In-memory fake of the Tampões unit-of-work factory (no DB).</summary>
public sealed class FakeTampoesUnitOfWorkFactory : ITampoesUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakeTampaoUnitOfWork());
}

/// <summary>No-op in-memory unit of work (confined to tests/*).</summary>
public sealed class FakeTampaoUnitOfWork : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// In-memory fake of <see cref="ITampaoRepository"/> (confined to tests/*). Tracks
/// field defs, values, configurations, saldos, movements and planos; supports
/// atomic-failure (via <see cref="FailTransaction"/>) to assert save-failure
/// preserves input.
/// </summary>
public sealed class FakeTampaoRepository : ITampaoRepository
{
    public List<TampaoFieldDef> FieldDefs { get; } = new();
    public List<TampaoFieldValue> FieldValues { get; } = new();
    public List<TampaoConfiguration> Configurations { get; } = new();
    public List<TampaoSaldo> Saldos { get; } = new();
    public List<TampaoMovement> Movements { get; } = new();
    public List<TampaoPlano> Planos { get; } = new();
    public List<(string action, string entityId, string result)> AuditEvents { get; } = new();
    public Dictionary<Guid, HashSet<string>> ConfigurationMachines { get; } = new();
    public List<TampaoMachineEvent> MachineEvents { get; } = new();
    public List<TampaoConfigurationNote> ConfigurationNotes { get; } = new();

    public bool FailTransaction { get; set; }

    // ---- Fields & values ----------------------------------------------------

    public Task<IReadOnlyList<TampaoFieldDef>> ListFieldDefsAsync(bool onlyActive, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoFieldDef>>(
            FieldDefs.Where(f => !onlyActive || f.Active).OrderBy(f => f.DisplayOrder).ToList());

    public Task<Guid> CreateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        FieldDefs.Add(field);
        return Task.FromResult(field.TampaoFieldDefId);
    }

    public Task UpdateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default)
    {
        var existing = FieldDefs.FirstOrDefault(f => f.TampaoFieldDefId == field.TampaoFieldDefId);
        if (existing is null) return Task.CompletedTask;
        existing.FieldName = field.FieldName;
        existing.Unit = field.Unit;
        existing.PrecisionDigits = field.PrecisionDigits;
        existing.DisplayOrder = field.DisplayOrder;
        existing.Active = field.Active;
        existing.UpdatedAtUtc = field.UpdatedAtUtc;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TampaoFieldValue>> ListFieldValuesAsync(Guid fieldDefId, bool onlyActive, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoFieldValue>>(
            FieldValues.Where(v => v.TampaoFieldDefId == fieldDefId && (!onlyActive || v.Active))
                .OrderBy(v => v.ValueNumeric).ToList());

    public Task<Guid> CreateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        FieldValues.Add(value);
        return Task.FromResult(value.TampaoFieldValueId);
    }

    public Task UpdateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default)
    {
        var existing = FieldValues.FirstOrDefault(v => v.TampaoFieldValueId == value.TampaoFieldValueId);
        if (existing is null) return Task.CompletedTask;
        existing.ValueLabel = value.ValueLabel;
        existing.DisplayOrder = value.DisplayOrder;
        existing.Active = value.Active;
        existing.UpdatedAtUtc = value.UpdatedAtUtc;
        return Task.CompletedTask;
    }

    // ---- Configurations & saldos --------------------------------------------

    public Task<TampaoConfiguration?> FindConfigurationByKeyAsync(string valuesJson, CancellationToken ct = default)
        => Task.FromResult(Configurations.FirstOrDefault(c => TampaoConfigurationKey.Serialize(c.Values) == valuesJson));

    public Task<TampaoConfiguration?> GetConfigurationByIdAsync(Guid configurationId, CancellationToken ct = default)
        => Task.FromResult(Configurations.FirstOrDefault(c => c.TampaoConfigurationId == configurationId));

    public Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsAsync(bool onlyActive, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoConfiguration>>(
            Configurations.Where(c => !onlyActive || c.Active).ToList());

    public Task<TampaoSaldo?> GetSaldoByConfigurationAsync(Guid configurationId, CancellationToken ct = default)
        => Task.FromResult(Saldos.FirstOrDefault(s => s.TampaoConfigurationId == configurationId));

    public Task<Guid> CreateConfigurationAsync(IDbUnitOfWork uow, TampaoConfiguration config, string valuesJson, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Configurations.Add(config);
        return Task.FromResult(config.TampaoConfigurationId);
    }

    public Task<TampaoSaldo?> GetSaldoInTransactionAsync(IDbUnitOfWork uow, Guid configurationId, CancellationToken ct = default)
        => Task.FromResult(Saldos.FirstOrDefault(s => s.TampaoConfigurationId == configurationId));

    public Task SetSaldoAsync(IDbUnitOfWork uow, Guid configurationId, int enchidos, int porEncher, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        var saldo = Saldos.FirstOrDefault(s => s.TampaoConfigurationId == configurationId);
        if (saldo is null)
        {
            Saldos.Add(new TampaoSaldo { TampaoConfigurationId = configurationId, Enchidos = enchidos, PorEncher = porEncher });
        }
        else
        {
            saldo.Enchidos = enchidos;
            saldo.PorEncher = porEncher;
        }
        return Task.CompletedTask;
    }

    public Task InsertMovementAsync(IDbUnitOfWork uow, TampaoMovement movement, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    // ---- Movements / history ---------------------------------------------------

    public Task<IReadOnlyList<TampaoMovement>> ListMovementsAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? configurationId, TampaoMovementType? type,
        string? operatorId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoMovement>>(Movements.Where(m =>
            (from is null || m.OccurredAtUtc >= from) &&
            (to is null || m.OccurredAtUtc <= to) &&
            (configurationId is null || m.OriginConfigurationId == configurationId || m.DestinationConfigurationId == configurationId) &&
            (type is null || m.MovementType == type) &&
            (operatorId is null || m.ActorId == operatorId)).ToList());

    // ---- Planning ---------------------------------------------------------------
    public Task<Guid> CreatePlanoAsync(TampaoPlano plano, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Planos.Add(plano);
        return Task.FromResult(plano.TampaoPlanoId);
    }

    public Task<TampaoPlano?> GetPlanoByIdAsync(Guid planoId, CancellationToken ct = default)
        => Task.FromResult(Planos.FirstOrDefault(p => p.TampaoPlanoId == planoId));

    public Task CancelPlanoAsync(IDbUnitOfWork uow, Guid planoId, CancellationToken ct = default)
    {
        var p = Planos.FirstOrDefault(x => x.TampaoPlanoId == planoId);
        if (p is not null) p.Canceled = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TampaoPlano>> ListPlanosAsync(bool includeCanceled, Guid? configurationId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoPlano>>(Planos.Where(p =>
            (includeCanceled || !p.Canceled) &&
            (configurationId is null || p.TampaoConfigurationId == configurationId) &&
            (from is null || (p.PlannedForDate is not null && p.PlannedForDate >= from)) &&
            (to is null || (p.PlannedForDate is not null && p.PlannedForDate <= to))).ToList());

    // ---- Audit ------------------------------------------------------------------
    public Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        AuditEvents.Add((actionCode, entityId, result));
        return Task.CompletedTask;
    }

    // ---- Test helpers -------------------------------------------------------------
    public TampaoConfiguration SeedConfiguration(string diameter, string calote, int enchidos = 0, int porEncher = 0)
    {
        var config = new TampaoConfiguration
        {
            Values = new SortedDictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["Diâmetro"] = decimal.Parse(diameter),
                ["Profundidade/Calote"] = decimal.Parse(calote)
            }
        };
        Configurations.Add(config);
        Saldos.Add(new TampaoSaldo { TampaoConfigurationId = config.TampaoConfigurationId, Enchidos = enchidos, PorEncher = porEncher });
        return config;
    }

    // ---- Machines & notes (R008) ------------------------------------------------

    public Task<IReadOnlySet<string>> GetMachinesByConfigurationAsync(Guid configurationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(
            ConfigurationMachines.TryGetValue(configurationId, out var s)
                ? new HashSet<string>(s, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal));

    public Task ReplaceConfigurationMachinesAsync(IDbUnitOfWork uow, Guid configurationId, IEnumerable<string> machines, CancellationToken ct = default)
    {
        ConfigurationMachines[configurationId] = new HashSet<string>(machines, StringComparer.Ordinal);
        return Task.CompletedTask;
    }

    public Task InsertMachineEventAsync(IDbUnitOfWork uow, TampaoMachineEvent evt, CancellationToken ct = default)
    {
        MachineEvents.Add(evt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TampaoMachineEvent>> ListMachineEventsAsync(Guid configurationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoMachineEvent>>(
            MachineEvents.Where(e => e.TampaoConfigurationId == configurationId).ToList());

    public Task AddConfigurationNoteAsync(IDbUnitOfWork uow, TampaoConfigurationNote note, CancellationToken ct = default)
    {
        ConfigurationNotes.Add(note);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TampaoConfigurationNote>> ListConfigurationNotesAsync(Guid configurationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoConfigurationNote>>(
            ConfigurationNotes.Where(n => n.TampaoConfigurationId == configurationId).OrderBy(n => n.OccurredAtUtc).ToList());

    public Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsByMachineAsync(string machine, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TampaoConfiguration>>(
            Configurations.Where(c =>
                c.Active &&
                ConfigurationMachines.TryGetValue(c.TampaoConfigurationId, out var s) &&
                s.Contains(machine, StringComparer.Ordinal)).ToList());
}