using System.Data;

namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// Explicit transaction scope over ONE connection (Plan-V3 U-03, GLM-DATA-05).
/// All multi-table writes of a use case run inside one unit of work:
/// commit only after successful completion; rollback on failure; deterministic
/// disposal. There is no implicit ambient TransactionScope and no transaction
/// leaking between requests.
/// </summary>
public interface IDbUnitOfWork : IAsyncDisposable
{
    /// <summary>The single connection bound to this scope.</summary>
    IDbConnection Connection { get; }

    /// <summary>The scope transaction. All commands of the use case pass it.</summary>
    IDbTransaction Transaction { get; }

    /// <summary>Commits after successful completion of the whole use case.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back explicitly; also happens automatically on disposal
    /// without commit.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
