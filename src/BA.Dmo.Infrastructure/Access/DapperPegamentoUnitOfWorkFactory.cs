using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Dapper implementation of the Pegamentos unit-of-work factory
/// (<see cref="IPegamentoUnitOfWorkFactory"/>). Opens an explicit
/// <see cref="DapperUnitOfWork"/> for the atomic Pegamentos write flows
/// (create control, add measurement, update/close, confirm-document):
/// commit only after the whole use case succeeds; disposal without commit
/// rolls back. This is the existing DapperUnitOfWork — no new transaction
/// framework (GLM-DATA-05; audit PG-04).
/// </summary>
public sealed class DapperPegamentoUnitOfWorkFactory : IPegamentoUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPegamentoUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => await DapperUnitOfWork.BeginAsync(_connectionFactory, cancellationToken);
}