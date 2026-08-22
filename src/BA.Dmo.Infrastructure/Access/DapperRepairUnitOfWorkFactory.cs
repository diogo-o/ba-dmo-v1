using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-15 — Dapper implementation of the repair unit-of-work factory
/// (IRepairUnitOfWorkFactory). Opens an explicit <see cref="DapperUnitOfWork"/> for
/// the coordinated Reparação + Armazém write (owner decision C): commit only after
/// the whole use case succeeds; disposal without commit rolls back. This is the
/// existing DapperUnitOfWork — no new transaction framework.
/// </summary>
public sealed class DapperRepairUnitOfWorkFactory : IRepairUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperRepairUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
    {
        var uow = await DapperUnitOfWork.BeginAsync(_connectionFactory, cancellationToken);
        return uow;
    }
}