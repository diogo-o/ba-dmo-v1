using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Pegamento read/write port (N07/N15, GLM-PEG-08). All CRUD and queries go through
/// this interface; implementation uses Dapper against pegamento_* tables.
/// Owns Pegamentos persistence only — does NOT read Job On tables.
/// WRITE methods participate in the caller-provided <see cref="IDbUnitOfWork"/>
/// (from <see cref="IPegamentoUnitOfWorkFactory"/>) so each use case — create,
/// measurement, update/close, document confirm — commits or rolls back as ONE
/// transaction (audit PG-04; GLM-DATA-05).
/// </summary>
public interface IPegamentoRepository
{
    // ---- Controls -----------------------------------------------------------
    Task<Guid> CreateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default);
    Task<PegamentoControlo?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// In-transaction control read with FOR UPDATE: serializes the read→write
    /// use cases (measurement on a just-closed control, document confirm
    /// freeze) against concurrent close/update of the same control.
    /// </summary>
    Task<PegamentoControlo?> GetByIdInTransactionAsync(IDbUnitOfWork uow, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> GetByRevisionAsync(Guid jobOnRevisionId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> GetByJobOnAsync(Guid jobOnId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> SearchAsync(
        string? reference, string? productionCode, string? machine, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task UpdateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default);

    // ---- Measurements -------------------------------------------------------
    Task<Guid> AddMeasurementAsync(IDbUnitOfWork uow, Guid controloId, PegamentoMedicao medicao, string actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoMedicao>> GetMeasurementsAsync(Guid controloId, CancellationToken ct = default);

    // ---- Document metadata (N14) --------------------------------------------
    Task UpsertDocumentAsync(IDbUnitOfWork uow, PegamentoDocumento document, CancellationToken ct = default);
    Task<PegamentoDocumento?> GetDocumentAsync(IDbUnitOfWork uow, Guid controloId, CancellationToken ct = default);
}