using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// IDbConnection/IDbTransaction test doubles confined to tests/*
/// (GLM-ARCH-18). They record the lifecycle so the unit-of-work semantics
/// (begin/commit/rollback/dispose) are verified without any database.
/// </summary>
internal sealed class FakeDbConnection : IDbConnection, IAsyncDisposable
{
    public bool BeginTransactionThrows { get; set; }

    public FakeDbTransaction? Transaction { get; private set; }

    public bool Disposed { get; private set; }

    public bool AsyncDisposed { get; private set; }

    private string _connectionString = string.Empty;

    [AllowNull]
    public string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }

    public int ConnectionTimeout => 30;

    public string Database => "fake";

    public ConnectionState State => Disposed ? ConnectionState.Closed : ConnectionState.Open;

    public IDbTransaction BeginTransaction()
    {
        if (BeginTransactionThrows)
            throw new InvalidOperationException("Simulated BeginTransaction failure.");

        Transaction = new FakeDbTransaction(this);
        return Transaction;
    }

    public IDbTransaction BeginTransaction(IsolationLevel il) => BeginTransaction();

    public void Close()
    {
    }

    public void ChangeDatabase(string databaseName)
    {
    }

    public IDbCommand CreateCommand() => throw new NotSupportedException(
        "FakeDbConnection is only used for unit-of-work lifecycle tests.");

    public void Open()
    {
    }

    public void Dispose() => Disposed = true;

    public ValueTask DisposeAsync()
    {
        AsyncDisposed = true;
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeDbTransaction(FakeDbConnection connection) : IDbTransaction
{
    public bool Committed { get; private set; }

    public int RollbackCount { get; private set; }

    public bool Disposed { get; private set; }

    public IDbConnection Connection { get; } = connection;

    public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    public void Commit()
    {
        if (Disposed)
            throw new ObjectDisposedException(nameof(FakeDbTransaction));
        Committed = true;
    }

    public void Rollback()
    {
        if (Disposed)
            throw new ObjectDisposedException(nameof(FakeDbTransaction));
        RollbackCount++;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Factory double returning the tracked fake connection.</summary>
internal sealed class FakeConnectionFactory(FakeDbConnection connection)
    : BA.Dmo.Application.Shared.Persistence.IDbConnectionFactory
{
    public int OpenCalls { get; private set; }

    public Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenCalls++;
        return Task.FromResult<IDbConnection>(connection);
    }
}
