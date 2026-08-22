namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// U-15 — Opens an explicit <see cref="IDbUnitOfWork"/> for a coordinated
/// multi-module write (Reparação cycle + Armazém physical movement) that MUST
/// succeed or fail as ONE transaction (owner decision C; GLM-DATA-05). The
/// concrete implementation wraps the existing <see cref="DapperUnitOfWork"/>
/// (Infrastructure); Application never references the Dapper implementation.
/// Disposal without commit rolls back; nothing leaks between requests.
/// </summary>
public interface IRepairUnitOfWorkFactory
{
    Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default);
}