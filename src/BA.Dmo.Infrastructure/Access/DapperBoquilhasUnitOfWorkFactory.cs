using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-19 — Dapper implementation of the Boquilhas unit-of-work factory
/// (<see cref="IBoquilhasUnitOfWorkFactory"/>). Opens an explicit
/// <see cref="DapperUnitOfWork"/> for the atomic Boquilhas write (lot+trace+START,
/// movement+discrepancy, close, lifecycle): commit only after the whole use case
/// succeeds; disposal without commit rolls back. Reuses the existing
/// DapperUnitOfWork — no new transaction framework (GLM-DATA-05).
/// </summary>
public sealed class DapperBoquilhasUnitOfWorkFactory : IBoquilhasUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperBoquilhasUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => await DapperUnitOfWork.BeginAsync(_connectionFactory, cancellationToken);
}