using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// In-memory fake of the Pegamentos persistence port (confined to tests/*).
/// Tracks controls, measurements and document metadata so use-case tests can
/// assert persistence behavior without a live DB. Write methods accept a
/// no-op in-memory <see cref="IDbUnitOfWork"/> (atomicity is exercised by the
/// Dapper layer / real-PG tests).
/// </summary>
public sealed class FakePegamentoRepository : IPegamentoRepository
{
    public Dictionary<Guid, PegamentoControlo> Controls { get; } = new();

    public Dictionary<Guid, List<PegamentoMedicao>> Measurements { get; } = new();

    public Dictionary<Guid, PegamentoDocumento> Documents { get; } = new();

    public Task<Guid> CreateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default)
    {
        Controls[control.PegamentoControloId] = control;
        return Task.FromResult(control.PegamentoControloId);
    }

    public Task<PegamentoControlo?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(GetByIdInternal(id));

    public Task<PegamentoControlo?> GetByIdInTransactionAsync(IDbUnitOfWork uow, Guid id, CancellationToken ct = default)
        => Task.FromResult(GetByIdInternal(id));

    private PegamentoControlo? GetByIdInternal(Guid id)
    {
        if (Controls.TryGetValue(id, out var control))
        {
            // Recompute historical calculations, matching Dapper hydration.
            var measured = Measurements.GetValueOrDefault(id) ?? new List<PegamentoMedicao>();
            foreach (var m in measured)
            {
                m.Ovalizacao = PegamentoMeasurementCalculator.Ovalizacao(m.Costura, m.ContraCostura);
                m.Media = PegamentoMeasurementCalculator.Media(m.Costura, m.ContraCostura);
                if (m.Media.HasValue)
                {
                    var nominal = m.ComponentKey switch
                    {
                        PegamentoComponentKey.CM => control.CmNominal,
                        PegamentoComponentKey.BQ => control.BqNominal,
                        PegamentoComponentKey.MF => control.MfNominal,
                        _ => null
                    };
                    m.ToleranceStatus = nominal.HasValue
                        ? PegamentoMeasurementCalculator.CheckTolerance(m.Media.Value, nominal.Value, control.Tolerance)
                        : PegamentoToleranceStatus.NotEvaluable;
                }
            }
        }
        return Controls.GetValueOrDefault(id);
    }

    public Task<IReadOnlyList<PegamentoControlo>> GetByRevisionAsync(Guid jobOnRevisionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PegamentoControlo>>(
            Controls.Values.Where(c => c.JobOnRevisionId == jobOnRevisionId).ToList());

    public Task<IReadOnlyList<PegamentoControlo>> GetByJobOnAsync(Guid jobOnId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PegamentoControlo>>(
            Controls.Values.Where(c => c.JobOnId == jobOnId).ToList());

    public Task<IReadOnlyList<PegamentoControlo>> SearchAsync(
        string? reference, string? productionCode, string? machine, DateTime? from, DateTime? to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PegamentoControlo>>(Controls.Values.Where(c =>
            (string.IsNullOrWhiteSpace(reference) || c.ReferenceSnapshot.Contains(reference)) &&
            (string.IsNullOrWhiteSpace(productionCode) || c.ProductionCode == productionCode) &&
            (string.IsNullOrWhiteSpace(machine) || c.MachineCode == machine) &&
            (from is null || c.CreatedAtUtc >= from) &&
            (to is null || c.CreatedAtUtc <= to)).ToList());

    public Task UpdateAsync(IDbUnitOfWork uow, PegamentoControlo control, CancellationToken ct = default)
    {
        if (control is not null) Controls[control.PegamentoControloId] = control;
        return Task.CompletedTask;
    }

    public Task<Guid> AddMeasurementAsync(IDbUnitOfWork uow, Guid controloId, PegamentoMedicao medicao, string actorId, CancellationToken ct = default)
    {
        if (!Measurements.TryGetValue(controloId, out var list))
        {
            list = new List<PegamentoMedicao>();
            Measurements[controloId] = list;
        }
        list.Add(medicao);
        return Task.FromResult(medicao.PegamentoMedicaoId);
    }

    public Task<IReadOnlyList<PegamentoMedicao>> GetMeasurementsAsync(Guid controloId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PegamentoMedicao>>((Measurements.GetValueOrDefault(controloId) ?? new()).ToList());

    public Task UpsertDocumentAsync(IDbUnitOfWork uow, PegamentoDocumento document, CancellationToken ct = default)
    {
        Documents[document.PegamentoControloId] = document;
        return Task.CompletedTask;
    }

    public Task<PegamentoDocumento?> GetDocumentAsync(IDbUnitOfWork uow, Guid controloId, CancellationToken ct = default)
        => Task.FromResult(Documents.GetValueOrDefault(controloId));
}