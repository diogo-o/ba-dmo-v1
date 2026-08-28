using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Opens an explicit <see cref="IDbUnitOfWork"/> for a coordinated atomic
/// Pegamentos write (create control, add measurement, update/close,
/// confirm-document) that MUST succeed or fail as ONE transaction
/// (GLM-DATA-05; audit PG-04). The concrete implementation wraps the existing
/// <see cref="DapperUnitOfWork"/> (Infrastructure); Application never references
/// the Dapper implementation. Disposal without commit rolls back.
/// </summary>
public interface IPegamentoUnitOfWorkFactory
{
    Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}