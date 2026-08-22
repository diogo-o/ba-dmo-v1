using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;

namespace BA.Dmo.Application.Modules.Controlo;

/// <summary>
/// R010 — Folha de Controlo read/write port (N23 <c>controlo_sheets</c> + items + events).
/// A sheet insert is transactional with its items; history events are append-only
/// (<c>ba_dmo_guard_append_only</c>). Reads return the sheet with its current items and
/// event history.
/// </summary>
public interface IControloSheetRepository
{
    Task<Guid> InsertAsync(IDbUnitOfWork uow, ControloFolha sheet, CancellationToken ct = default);

    Task<ControloFolha?> GetByIdAsync(Guid sheetId, CancellationToken ct = default);

    /// <summary>Loads the (latest) sheet for a production/revision, or null.</summary>
    Task<ControloFolha?> GetForProductionAsync(Guid jobOnId, Guid? jobOnRevisionId = null, CancellationToken ct = default);

    Task<IReadOnlyList<ControloFolha>> ListByProductionAsync(Guid jobOnId, CancellationToken ct = default);

    /// <summary>List sheets for a production or produced by a line (history browse).</summary>
    Task<IReadOnlyList<ControloFolha>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? machineCode, Guid? jobOnId, string? status, CancellationToken ct = default);

    /// <summary>Persists the current items AND the sheet header/status in one unit of work.</summary>
    Task UpdateAsync(IDbUnitOfWork uow, ControloFolha sheet, IReadOnlyList<ControloFolhaItem> currentItems, CancellationToken ct = default);

    /// <summary>Inserts an append-only history event.</summary>
    Task InsertEventAsync(IDbUnitOfWork uow, ControloFolhaEvent evt, CancellationToken ct = default);
}