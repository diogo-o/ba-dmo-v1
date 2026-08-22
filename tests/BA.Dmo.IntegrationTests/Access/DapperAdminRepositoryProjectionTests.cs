using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Access;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// Regression guard for the admin user-list projection defect (dmo-5051):
/// <see cref="AdminUserRow"/> carries nine constructor parameters (the last,
/// optional <c>ModulesOverrideJson</c>, and <c>AuthEmail</c>, are filled later
/// by the per-user override column and batched service-role email enrichment),
/// yet <c>DapperAdminRepository.UserColumns</c> must return a column for every
/// parameter so the real Dapper projection can materialize the row with
/// <c>AuthEmail == null</c> BEFORE service enrichment.
///
/// The test drives the REAL repository and REAL Dapper (no fakes for the
/// projection): the rows are fed through an in-memory ADO.NET reader, so the
/// exact shared <c>UserColumns</c> SQL is executed and mapped. Before the fix
/// this path throws Dapper's <c>InvalidOperationException</c> (fewer columns
/// than constructor parameters); the assertion below proves it now succeeds.
/// </summary>
public class DapperAdminRepositoryProjectionTests
{
    [Fact]
    public async Task UserColumns_MaterializesAdminUserRow_WithAuthEmailNull_BeforeEnrichment()
    {
        var authUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var updatedAt = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        // Backing result set matching the nine AdminUserRow parameters. The
        // AuthEmail and ModulesOverrideJson columns deliberately carry real
        // typed columns present in the projection yet returned as NULL (no
        // auth.users access at the runtime connection; no per-user override set):
        // Dapper must see all nine parameters.
        var table = new DataTable();
        table.Columns.Add("ActorId", typeof(string));
        table.Columns.Add("AuthUserId", typeof(Guid));
        table.Columns.Add("DisplayName", typeof(string));
        table.Columns.Add("ProfileTitle", typeof(string));
        table.Columns.Add("TemplateId", typeof(string));
        table.Columns.Add("Active", typeof(bool));
        table.Columns.Add("UpdatedAtUtc", typeof(DateTimeOffset));
        table.Columns.Add("AuthEmail", typeof(string));
        table.Columns.Add("ModulesOverrideJson", typeof(string));
        table.Rows.Add(
            "user-1", authUserId, "Utilizador Um", "Metrologia",
            "tpl-active", true, updatedAt, DBNull.Value, DBNull.Value);

        var connection = new DataReaderDbConnection(table);
        var repository = new DapperAdminRepository(new FixedReaderConnectionFactory(connection));

        var rows = await repository.ListUsersAsync(search: null);

        // The issued SQL must be the real shared projection and must include a
        // typed nullable column for the (optional) AuthEmail parameter. If the
        // projection regresses to eight columns, Dapper throws while
        // materializing AdminUserRow — and this assertion on the actually-
        // issued command text catches that regression independently.
        Assert.Contains("AS AuthEmail", connection.IssuedSql, StringComparison.Ordinal);
        Assert.Contains("NULL::text", connection.IssuedSql, StringComparison.Ordinal);
        Assert.Contains("AS ModulesOverrideJson", connection.IssuedSql, StringComparison.Ordinal);

        var row = Assert.Single(rows);
        Assert.Equal("user-1", row.ActorId);
        Assert.Equal(authUserId, row.AuthUserId);
        Assert.Equal("Utilizador Um", row.DisplayName);
        Assert.Equal("Metrologia", row.ProfileTitle);
        Assert.Equal("tpl-active", row.TemplateId);
        Assert.True(row.Active);
        Assert.Equal(updatedAt, row.UpdatedAtUtc);
        // Pre-enrichment invariant: the row materializes with a null auth email.
        Assert.Null(row.AuthEmail);
        // No per-user override set: the override column materializes as null.
        Assert.Null(row.ModulesOverrideJson);
        Assert.True(connection.WasDisposed);
    }

    /// <summary>
    /// In-memory DbConnection/DbCommand doubles backed by a real ADO.NET
    /// <c>DataTableReader</c>. Because the command derives from
    /// <see cref="System.Data.Common.DbCommand"/>, Dapper's async query path is
    /// exercised end-to-end: it issues the SQL, reads the result set through the
    /// real <c>DbDataReader</c>, and maps each column name onto the
    /// <see cref="AdminUserRow"/> constructor. No database, no fakes for the
    /// projection.
    /// </summary>
    private sealed class DataReaderDbConnection(DataTable table) : System.Data.Common.DbConnection
    {
        public bool WasDisposed { get; private set; }

        /// <summary>The SQL text Dapper actually issued through this connection.</summary>
        public string IssuedSql { get; internal set; } = string.Empty;

        [AllowNull]
        public override string ConnectionString { get; set; } = "InMemory";

        public override string Database => "inmemory";

        public override string DataSource => "inmemory";

        public override string ServerVersion => throw new NotSupportedException();

        public override ConnectionState State => WasDisposed
            ? ConnectionState.Closed
            : ConnectionState.Open;

        protected override System.Data.Common.DbTransaction BeginDbTransaction(
            IsolationLevel isolationLevel) =>
            throw new NotSupportedException("No transactions in the projection-only double.");

        protected override System.Data.Common.DbCommand CreateDbCommand()
        {
            WasDisposed = false;
            return new DataReaderDbCommand(this, table);
        }

        public override void Open() => WasDisposed = false;

        public override void Close() => WasDisposed = true;

        public override void ChangeDatabase(string databaseName) { }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        protected override System.Data.Common.DbProviderFactory DbProviderFactory =>
            throw new NotSupportedException();
    }

    private sealed class DataReaderDbCommand(DataReaderDbConnection owner, DataTable table)
        : System.Data.Common.DbCommand
    {
        private string _commandText = string.Empty;

        [AllowNull]
        public override string CommandText
        {
            get => _commandText;
            set
            {
                _commandText = value ?? string.Empty;
                owner.IssuedSql = _commandText;
            }
        }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override System.Data.Common.DbConnection? DbConnection { get; set; }

        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } =
            new NoParameterCollection();

        protected override System.Data.Common.DbTransaction? DbTransaction { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }

        public override void Prepare() { }

        protected override System.Data.Common.DbParameter CreateDbParameter() =>
            throw new NotSupportedException("Parameter binding is not used by the projection-only path.");

        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(
            CommandBehavior behavior) =>
            table.CreateDataReader();

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => null;

        protected override void Dispose(bool disposing) => base.Dispose(disposing);
    }

    /// <summary>
    /// Concrete empty <c>DbParameterCollection</c>: the projection SELECT binds
    /// no parameters, so this collection simply never holds any.
    /// </summary>
    private sealed class NoParameterCollection : System.Data.Common.DbParameterCollection
    {
        private readonly List<System.Data.Common.DbParameter> _items = [];

        public override int Count => _items.Count;

        public override bool IsFixedSize => false;

        public override bool IsReadOnly => false;

        public override bool IsSynchronized => false;

        public override object SyncRoot => _items;

        public override int Add(object value)
        {
            if (value is not System.Data.Common.DbParameter parameter)
                throw new ArgumentException("Not an DbParameter.", nameof(value));
            _items.Add(parameter);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains(value);

        public override bool Contains(string value) =>
            _items.Any(p => string.Equals(p.ParameterName, value, StringComparison.Ordinal));

        public override void CopyTo(Array array, int index) => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) =>
            value is System.Data.Common.DbParameter p ? _items.IndexOf(p) : -1;

        public override int IndexOf(string parameterName) => _items.FindIndex(
            p => string.Equals(p.ParameterName, parameterName, StringComparison.Ordinal));

        public override void Insert(int index, object value) => throw new NotSupportedException();

        public override void Remove(object value) => throw new NotSupportedException();

        public override void RemoveAt(int index) => throw new NotSupportedException();

        public override void RemoveAt(string parameterName) => throw new NotSupportedException();

        protected override System.Data.Common.DbParameter GetParameter(int index) =>
            _items[index];

        protected override System.Data.Common.DbParameter GetParameter(string parameterName) =>
            _items.First(p => string.Equals(
                p.ParameterName, parameterName, StringComparison.Ordinal));

        protected override void SetParameter(int index, System.Data.Common.DbParameter value) =>
            _items[index] = value;

        protected override void SetParameter(
            string parameterName, System.Data.Common.DbParameter value) =>
            throw new NotSupportedException();
    }

    private sealed class FixedReaderConnectionFactory(DataReaderDbConnection connection)
        : IDbConnectionFactory
    {
        public Task<IDbConnection> OpenConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IDbConnection>(connection);
        }
    }
}