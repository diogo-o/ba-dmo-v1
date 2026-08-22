using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// U-03 transaction model tests (Plan-V3 GLM-DATA-05, U-03 acceptance:
/// "transação ambiente funciona em teste"). Verified against lifecycle
/// doubles — no database required, no live SQL.
/// </summary>
public class DapperUnitOfWorkTests
{
    [Fact]
    public async Task BeginAsync_OpensConnection_AndBeginsTransaction()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        await using var uow = await DapperUnitOfWork.BeginAsync(factory);

        Assert.Equal(1, factory.OpenCalls);
        Assert.Same(connection, uow.Connection);
        Assert.NotNull(connection.Transaction);
        Assert.Same(connection.Transaction, uow.Transaction);
    }

    [Fact]
    public async Task RunAsync_CommitsAfterSuccess_AndReturnsResult()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        var result = await DapperUnitOfWork.RunAsync(factory, (conn, tx, ct) =>
        {
            Assert.Same(connection, conn);
            Assert.Same(connection.Transaction, tx);
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.True(connection.Transaction!.Committed);
        Assert.Equal(0, connection.Transaction.RollbackCount);
        Assert.True(connection.AsyncDisposed);
    }

    [Fact]
    public async Task RunAsync_RollsBackOnFailure_AndRethrows()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DapperUnitOfWork.RunAsync<int>(factory, (_, _, _) =>
                throw new InvalidOperationException("Simulated use-case failure.")));

        Assert.False(connection.Transaction!.Committed);
        Assert.Equal(1, connection.Transaction.RollbackCount);
        Assert.True(connection.AsyncDisposed);
    }

    [Fact]
    public async Task DisposeWithoutCommit_RollsBack_AndDisposesEverything()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        var uow = await DapperUnitOfWork.BeginAsync(factory);
        await uow.DisposeAsync();

        Assert.False(connection.Transaction!.Committed);
        Assert.Equal(1, connection.Transaction.RollbackCount);
        Assert.True(connection.Transaction.Disposed);
        Assert.True(connection.AsyncDisposed);
    }

    [Fact]
    public async Task DisposeAfterCommit_DoesNotRollback()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        var uow = await DapperUnitOfWork.BeginAsync(factory);
        await uow.CommitAsync();
        await uow.DisposeAsync();

        Assert.True(connection.Transaction!.Committed);
        Assert.Equal(0, connection.Transaction.RollbackCount);
        Assert.True(connection.AsyncDisposed);
    }

    [Fact]
    public async Task CommitTwice_IsRejected()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);

        var uow = await DapperUnitOfWork.BeginAsync(factory);
        await uow.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync());
        await uow.DisposeAsync();
    }

    [Fact]
    public async Task Cancellation_PreventsCommit_AndRollsBack()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Cancelled before the scope opens: nothing begins, nothing commits.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DapperUnitOfWork.RunAsync(factory, (_, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(1);
            }, cts.Token));

        Assert.Equal(0, factory.OpenCalls);
        Assert.Null(connection.Transaction);
    }

    [Fact]
    public async Task CancellationDuringOperation_RollsBack_WithoutCommit()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DapperUnitOfWork.RunAsync(factory, (_, _, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(1);
            }, cts.Token));

        Assert.False(connection.Transaction!.Committed);
        Assert.True(connection.Transaction.RollbackCount >= 1);
        Assert.True(connection.AsyncDisposed);
    }

    [Fact]
    public async Task BeginTransactionFailure_DisposesTheOpenedConnection()
    {
        var connection = new FakeDbConnection { BeginTransactionThrows = true };
        var factory = new FakeConnectionFactory(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DapperUnitOfWork.BeginAsync(factory));

        Assert.True(connection.AsyncDisposed);
        Assert.Null(connection.Transaction);
    }

    [Fact]
    public async Task ScopesAreIndependent_NoSharedStateBetweenRuns()
    {
        var first = new FakeDbConnection();
        var second = new FakeDbConnection();
        var queue = new Queue<FakeDbConnection>([first, second]);
        var factory = new SequentialConnectionFactory(queue);

        await DapperUnitOfWork.RunAsync(factory, (_, _, _) => Task.FromResult(0));
        await DapperUnitOfWork.RunAsync(factory, (_, _, _) => Task.FromResult(0));

        // Each scope gets its own connection/transaction: nothing leaks.
        Assert.Equal(2, factory.OpenCalls);
        Assert.True(first.Transaction!.Committed);
        Assert.True(second.Transaction!.Committed);
        Assert.NotSame(first.Transaction, second.Transaction);
    }

    private sealed class SequentialConnectionFactory(Queue<FakeDbConnection> connections)
        : BA.Dmo.Application.Shared.Persistence.IDbConnectionFactory
    {
        public int OpenCalls { get; private set; }

        public Task<System.Data.IDbConnection> OpenConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCalls++;
            return Task.FromResult<System.Data.IDbConnection>(connections.Dequeue());
        }
    }
}
