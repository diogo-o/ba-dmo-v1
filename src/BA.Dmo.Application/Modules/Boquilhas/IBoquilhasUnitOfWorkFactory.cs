using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Application.Modules.Boquilhas;

/// <summary>
/// U-19 — Opens an explicit <see cref="IDbUnitOfWork"/> for a coordinated atomic
/// Boquilhas write (lot+trace+START, movement+audit, return+discrepancy, close,
/// lifecycle) that MUST succeed or fail as ONE transaction (GLM-DATA-05). The
/// concrete implementation wraps the existing <see cref="DapperUnitOfWork"/>
/// (Infrastructure); Application never references the Dapper implementation.
/// Disposal without commit rolls back.
/// </summary>
public interface IBoquilhasUnitOfWorkFactory
{
    Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}