using System.Data;
using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Infrastructure.Persistence;

/// <summary>
/// Explicit unit of work over ONE connection and ONE transaction
/// (Plan-V3 U-03, GLM-DATA-05: every multi-table write is transactional;
/// commit only after successful completion; rollback on failure).
/// Usage pattern:
/// <code>
/// await using var uow = await DapperUnitOfWork.BeginAsync(factory, ct);
/// // ... Dapper commands passing uow.Connection + uow.Transaction ...
/// await uow.CommitAsync(ct);
/// </code>
/// or the managed form <see cref="RunAsync"/> which commits/rolls back
/// deterministically. Disposal without commit always rolls back; nothing
/// leaks between requests and no ambient TransactionScope is used.
/// </summary>
public sealed class DapperUnitOfWork : IDbUnitOfWork
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private bool _completed;
    private bool _disposed;

    private DapperUnitOfWork(IDbConnection connection, IDbTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IDbConnection Connection => _connection;

    public IDbTransaction Transaction => _transaction;

    /// <summary>Opens a connection and begins the scope transaction.</summary>
    public static async Task<DapperUnitOfWork> BeginAsync(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);

        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var transaction = connection.BeginTransaction();
            return new DapperUnitOfWork(connection, transaction);
        }
        catch
        {
            if (connection is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Runs an operation inside a fresh scope: commits after success, rolls
    /// back and rethrows on any failure, disposes connection and transaction
    /// in all cases.
    /// </summary>
    public static async Task<TResult> RunAsync<TResult>(
        IDbConnectionFactory connectionFactory,
        Func<IDbConnection, IDbTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var unitOfWork = await BeginAsync(connectionFactory, cancellationToken);
        try
        {
            var result = await operation(
                unitOfWork.Connection, unitOfWork.Transaction, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureActive();
        _transaction.Commit();
        _completed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_completed || _disposed)
            return Task.CompletedTask;

        _transaction.Rollback();
        _completed = true;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (!_completed)
                _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            if (_connection is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                _connection.Dispose();
        }
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed)
            throw new InvalidOperationException(
                "This unit of work is already completed (committed or rolled back).");
    }
}
