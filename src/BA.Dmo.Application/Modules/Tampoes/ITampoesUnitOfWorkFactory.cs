using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Application.Modules.Tampoes;

/// <summary>
/// U-17 — Opens an explicit <see cref="IDbUnitOfWork"/> for a coordinated atomic
/// Tampões write (e.g. alterar estado / alterar configuração transfer that updates
/// BOTH origin and destination saldos + inserts a single append-only movement +
/// audit) that MUST succeed or fail as ONE transaction (GLM-DATA-05). The concrete
/// implementation wraps the existing <see cref="DapperUnitOfWork"/> (Infrastructure);
/// Application never references the Dapper implementation. Disposal without commit
/// rolls back.
/// </summary>
public interface ITampoesUnitOfWorkFactory
{
    Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}