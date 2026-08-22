using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Pegamento read/write port (N07/N15, GLM-PEG-08). All CRUD and queries go through
/// this interface; implementation uses Dapper against pegamento_* tables.
/// Owns Pegamentos persistence only — does NOT read Job On tables.
/// </summary>
public interface IPegamentoRepository
{
    // ---- Controls -----------------------------------------------------------
    Task<Guid> CreateAsync(PegamentoControlo control, CancellationToken ct = default);
    Task<PegamentoControlo?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> GetByRevisionAsync(Guid jobOnRevisionId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> GetByJobOnAsync(Guid jobOnId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoControlo>> SearchAsync(
        string? reference, string? productionCode, string? machine, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task UpdateAsync(PegamentoControlo control, CancellationToken ct = default);

    // ---- Measurements -------------------------------------------------------
    Task<Guid> AddMeasurementAsync(Guid controloId, PegamentoMedicao medicao, string actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PegamentoMedicao>> GetMeasurementsAsync(Guid controloId, CancellationToken ct = default);

    // ---- Document metadata (N14) --------------------------------------------
    Task UpsertDocumentAsync(PegamentoDocumento document, CancellationToken ct = default);
    Task<PegamentoDocumento?> GetDocumentAsync(Guid controloId, CancellationToken ct = default);
}