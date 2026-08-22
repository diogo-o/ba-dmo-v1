using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-17 — Dapper implementation of the Tampões unit-of-work factory
/// (<see cref="ITampoesUnitOfWorkFactory"/>). Opens an explicit
/// <see cref="DapperUnitOfWork"/> for the atomic Tampões write (adicionar/remover,
/// alterar estado, alterar configuração): commit only after the whole use case
/// succeeds; disposal without commit rolls back. This is the existing
/// DapperUnitOfWork — no new transaction framework (GLM-DATA-05).
/// </summary>
public sealed class DapperTampoesUnitOfWorkFactory : ITampoesUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperTampoesUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => await DapperUnitOfWork.BeginAsync(_connectionFactory, cancellationToken);
}