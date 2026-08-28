using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Access;

namespace BA.Dmo.IntegrationTests.Persistence;

public sealed class AuditJsonBindingTests
{
    [Fact]
    public async Task JobOnAudit_GuidScalar_IsBoundAsJsonString_ToSameColumns()
    {
        var connection = new RecordingDbConnection();
        var repository = new DapperJobOnRepository(new RecordingConnectionFactory(connection));
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");

        await repository.InsertAuditEventAsync(
            Guid.NewGuid(), null, "jobon.duplicar", null, guid.ToString(), "actor-1");

        var command = Assert.Single(connection.Commands);
        Assert.Contains(
            "INSERT INTO job_on_audit_event (job_on_id, job_on_revision_id, event_type, before_snapshot, after_snapshot, actor_id, occurred_at_utc)",
            command.Sql, StringComparison.Ordinal);
        Assert.Contains("@BeforeSnapshot::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@AfterSnapshot::jsonb", command.Sql, StringComparison.Ordinal);
        AssertJsonString(command.Parameters["AfterSnapshot"], guid.ToString());
    }

    [Fact]
    public async Task JobOnAudit_EnumNameScalar_IsBoundAsJsonString()
    {
        var connection = new RecordingDbConnection();
        var repository = new DapperJobOnRepository(new RecordingConnectionFactory(connection));

        await repository.InsertAuditEventAsync(
            Guid.NewGuid(), null, "jobon.transicao", null, "Fechado", "actor-1");

        AssertJsonString(Assert.Single(connection.Commands).Parameters["AfterSnapshot"], "Fechado");
    }

    [Fact]
    public async Task RepairAudit_CompositeScalar_IsBoundAsJsonString_ToSameColumns()
    {
        var connection = new RecordingDbConnection();
        var repository = new DapperRepairRepository(new RecordingConnectionFactory(connection));
        const string composite = "REF|LOT|NUM";

        await repository.InsertAuditEventAsync(
            Guid.NewGuid(), "reparacao_externa.lista.item", null, composite, "actor-1");

        var command = Assert.Single(connection.Commands);
        Assert.Contains(
            "INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,",
            command.Sql, StringComparison.Ordinal);
        Assert.Contains("entity_type, entity_id, result, before_summary, after_summary)", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@Before::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@After::jsonb", command.Sql, StringComparison.Ordinal);
        AssertJsonString(command.Parameters["After"], composite);
    }

    [Fact]
    public async Task ArmazemAudit_ExistingObjectAndArrayJson_RemainUnchanged_ToSameColumns()
    {
        var connection = new RecordingDbConnection();
        var repository = new DapperArmazemRepository(new RecordingConnectionFactory(connection));
        const string before = "{\"reference\":\"REF\",\"lot\":\"LOT\"}";
        const string after = "[\"REF\",\"LOT\"]";

        await repository.InsertAuditEventAsync(
            Guid.NewGuid(), "armazem.corrigir_localizacao", before, after, "actor-1");

        var command = Assert.Single(connection.Commands);
        Assert.Contains(
            "INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,",
            command.Sql, StringComparison.Ordinal);
        Assert.Contains("entity_type, entity_id, result, before_summary, after_summary)", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@Before::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@After::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Equal(before, command.Parameters["Before"]);
        Assert.Equal(after, command.Parameters["After"]);
    }

    [Fact]
    public async Task AdminAudit_NullSummaries_StayNull_AndFreeTextIsNormalizedBeforeCast()
    {
        var connection = new RecordingDbConnection();
        var repository = new DapperAdminRepository(new RecordingConnectionFactory(connection));

        // Current callers pass NULL (Manual-compliant) — stays NULL.
        var entry = new AuditEntry(
            new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero),
            "user-1", "User", "admin", "create", "internal_user", "actor-1",
            "Display", "succeeded", null, BeforeSummary: null, AfterSummary: null);
        await repository.InsertAuditEventAsync(entry);

        var command = connection.Commands[0];
        Assert.Contains("@BeforeSummary::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Contains("@AfterSummary::jsonb", command.Sql, StringComparison.Ordinal);
        Assert.Null(command.Parameters["BeforeSummary"]);
        Assert.Null(command.Parameters["AfterSummary"]);

        // Defensive hardening (audit PC-11/ADM-08): a future free-text payload is
        // normalized to a JSON string before the ::jsonb cast (never a raw 22P02).
        var freeTextEntry = entry with { BeforeSummary = "manter", AfterSummary = "{\"chave\":1}" };
        await repository.InsertAuditEventAsync(freeTextEntry);

        var second = connection.Commands[1];
        AssertJsonString(second.Parameters["BeforeSummary"], "manter");
        // Already-valid JSON passes through unchanged.
        Assert.Equal("{\"chave\":1}", second.Parameters["AfterSummary"]);
    }

    private static void AssertJsonString(object? value, string expected)
    {
        var json = Assert.IsType<string>(value);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, document.RootElement.ValueKind);
        Assert.Equal(expected, document.RootElement.GetString());
    }

    private sealed class RecordingConnectionFactory(RecordingDbConnection connection)
        : IDbConnectionFactory
    {
        public Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection.Reopen();
            return Task.FromResult<IDbConnection>(connection);
        }
    }

    private sealed record RecordedCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters);

    private sealed class RecordingDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        public List<RecordedCommand> Commands { get; } = [];

        [AllowNull]
        public override string ConnectionString { get; set; } = "Recording";
        public override string Database => "recording";
        public override string DataSource => "recording";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;

        public void Reopen() => _state = ConnectionState.Open;
        public override void Open() => Reopen();
        public override void Close() => _state = ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);

        internal void Record(string sql, DbParameterCollection parameters) => Commands.Add(new(
            sql,
            parameters.Cast<DbParameter>().ToDictionary(
                parameter => parameter.ParameterName,
                parameter => parameter.Value is DBNull ? null : parameter.Value,
                StringComparer.Ordinal)));
    }

    private sealed class RecordingDbCommand(RecordingDbConnection owner) : DbCommand
    {
        private string _commandText = string.Empty;
        private readonly RecordingParameterCollection _parameters = new();

        [AllowNull]
        public override string CommandText { get => _commandText; set => _commandText = value ?? string.Empty; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = owner;
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();
        public override object? ExecuteScalar() => null;

        public override int ExecuteNonQuery()
        {
            owner.Record(CommandText, _parameters);
            return 1;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ExecuteNonQuery());
        }
    }

    private sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values) Add(value!);
        }

        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(
            parameter => string.Equals(parameter.ParameterName, parameterName, StringComparison.Ordinal));
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0) _items.Add(value); else _items[index] = value;
        }
    }
}
