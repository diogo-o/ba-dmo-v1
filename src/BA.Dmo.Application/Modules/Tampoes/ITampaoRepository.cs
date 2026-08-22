using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;

namespace BA.Dmo.Application.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões read/write port (N10 <c>tampao_*</c>; GLM-TP). Owns the Tampões
/// persistence ONLY (field defs, values, configurations, saldos, movements,
/// planos) plus a global <c>audit_events</c> row (module <c>tampoes</c>). The
/// atomic multi-row writes (state/configuração transfer) participate in the shared
/// <see cref="IDbUnitOfWork"/> from <see cref="ITampoesUnitOfWorkFactory"/> so they
/// commit/roll back as ONE transaction (GLM-DATA-05).
/// </summary>
public interface ITampaoRepository
{
    // ---- Fields & values ----------------------------------------------------
    Task<IReadOnlyList<TampaoFieldDef>> ListFieldDefsAsync(bool onlyActive, CancellationToken ct = default);
    Task<Guid> CreateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default);
    Task UpdateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default);
    Task<IReadOnlyList<TampaoFieldValue>> ListFieldValuesAsync(Guid fieldDefId, bool onlyActive, CancellationToken ct = default);
    Task<Guid> CreateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default);
    Task UpdateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default);

    // ---- Configurations & saldos --------------------------------------------
    Task<TampaoConfiguration?> FindConfigurationByKeyAsync(string valuesJson, CancellationToken ct = default);
    Task<TampaoConfiguration?> GetConfigurationByIdAsync(Guid configurationId, CancellationToken ct = default);
    Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsAsync(bool onlyActive, CancellationToken ct = default);
    Task<TampaoSaldo?> GetSaldoByConfigurationAsync(Guid configurationId, CancellationToken ct = default);

    /// <summary>Creates a configuration + its saldo row (both in one transaction).</summary>
    Task<Guid> CreateConfigurationAsync(IDbUnitOfWork uow, TampaoConfiguration config, string valuesJson, CancellationToken ct = default);

    /// <summary>Reads a saldo within the shared unit of work for atomic transfer.</summary>
    Task<TampaoSaldo?> GetSaldoInTransactionAsync(IDbUnitOfWork uow, Guid configurationId, CancellationToken ct = default);

    /// <summary>Upserts the saldo (insert new or update) within the shared unit of work.</summary>
    Task SetSaldoAsync(IDbUnitOfWork uow, Guid configurationId, int enchidos, int porEncher, CancellationToken ct = default);

    /// <summary>Inserts an append-only movement within the shared unit of work.</summary>
    Task InsertMovementAsync(IDbUnitOfWork uow, TampaoMovement movement, CancellationToken ct = default);

    // ---- Movements / history -------------------------------------------------
    Task<IReadOnlyList<TampaoMovement>> ListMovementsAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? configurationId, TampaoMovementType? type,
        string? operatorId, CancellationToken ct = default);

    // ---- Machines & notes (R008, multi-machine + comments) -------------------
    Task<IReadOnlySet<string>> GetMachinesByConfigurationAsync(Guid configurationId, CancellationToken ct = default);
    Task ReplaceConfigurationMachinesAsync(IDbUnitOfWork uow, Guid configurationId, IEnumerable<string> machines, CancellationToken ct = default);
    Task InsertMachineEventAsync(IDbUnitOfWork uow, TampaoMachineEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<TampaoMachineEvent>> ListMachineEventsAsync(Guid configurationId, CancellationToken ct = default);
    Task AddConfigurationNoteAsync(IDbUnitOfWork uow, TampaoConfigurationNote note, CancellationToken ct = default);
    Task<IReadOnlyList<TampaoConfigurationNote>> ListConfigurationNotesAsync(Guid configurationId, CancellationToken ct = default);

    /// <summary>Configurations whose machine set contains the given machine (filtered consultation).</summary>
    Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsByMachineAsync(string machine, CancellationToken ct = default);

    // ---- Planning -------------------------------------------------------------
    Task<Guid> CreatePlanoAsync(TampaoPlano plano, CancellationToken ct = default);
    Task<TampaoPlano?> GetPlanoByIdAsync(Guid planoId, CancellationToken ct = default);
    Task CancelPlanoAsync(IDbUnitOfWork uow, Guid planoId, CancellationToken ct = default);
    Task<IReadOnlyList<TampaoPlano>> ListPlanosAsync(bool includeCanceled, Guid? configurationId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    // ---- Audit ----------------------------------------------------------------
    Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default);
}