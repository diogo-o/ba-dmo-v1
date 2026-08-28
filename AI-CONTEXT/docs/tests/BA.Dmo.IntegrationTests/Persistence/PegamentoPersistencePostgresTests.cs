using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL proofs for the Pegamentos Queue-A hardening:
///   * PC-01 — create binds updated_at_utc (fallback to created_at_utc) so the
///     NOT NULL column never receives an explicit NULL (23502 would otherwise
///     break control creation on a migration-compliant DB);
///   * PG-04 — the create/measurement/UoW write paths execute against the real
///     schema inside a single transaction.
/// BA_DMO_TEST_DATABASE must identify an isolated test database. Every row uses
/// a unique jobon/pegamento identifier; no existing row is selected or mutated.
/// </summary>
public sealed class PegamentoPersistencePostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task CreateControlAsync_PersistsUpdatedAtUtc_NeverNull()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = new DapperPegamentoRepository(new DbConnectionFactory(ConnectionString!));
        var control = NewControl(context.JobOnId, context.JobOnRevisionId);

        await using var uow = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!));
        await repository.CreateAsync(uow, control);
        await uow.CommitAsync();

        var (createdAt, updatedAt) = await ReadTimestampsAsync(control.PegamentoControloId);
        Assert.NotNull(updatedAt);
        Assert.Equal(createdAt, updatedAt); // PC-01 fallback: updated_at_utc = created_at_utc
    }

    [Fact]
    public async Task AddMeasurement_WithinUoW_ReadsLockedControlAndPersistsMeasurement()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = new DapperPegamentoRepository(new DbConnectionFactory(ConnectionString!));
        var control = NewControl(context.JobOnId, context.JobOnRevisionId);

        await using (var seed = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.CreateAsync(seed, control);
            await seed.CommitAsync();
        }

        await using var uow = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!));
        var locked = await repository.GetByIdInTransactionAsync(uow, control.PegamentoControloId);
        Assert.NotNull(locked);
        Assert.Equal(PegamentoControloStatus.Aberto, locked!.Status);

        var medicaoId = await repository.AddMeasurementAsync(
            uow, control.PegamentoControloId, NewMedicao(control.PegamentoControloId), "peg-actor");
        await uow.CommitAsync();

        var count = await CountMeasurementsAsync(control.PegamentoControloId);
        Assert.Equal(1, count);
        Assert.NotEqual(Guid.Empty, medicaoId);
    }

    // ---- N39: one-sided measurement (contra_costura absent) --------------
    // The N39 DROP NOT NULL makes a NULL contra_costura persist cleanly —
    // the domain/calculator/service already handle the one-sided case; the
    // database must never be the blocker. Self-skipping when the test
    // database has NOT applied N39 (column still NOT NULL).

    [Fact]
    public async Task AddMeasurement_OneSidedCm_ContraCosturaNull_IsPersisted()
    {
        if (SkipIfNoDatabase()) return;
        if (await ContraCosturaStillNotNullAsync()) return; // N39 not applied
        var context = await SeedAsync();
        var repository = new DapperPegamentoRepository(new DbConnectionFactory(ConnectionString!));
        var control = NewControl(context.JobOnId, context.JobOnRevisionId);

        await using (var seed = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.CreateAsync(seed, control);
            await seed.CommitAsync();
        }

        var oneSided = NewMedicao(control.PegamentoControloId);
        oneSided.Costura = 52.30m;
        oneSided.ContraCostura = null;

        await using var uow = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!));
        var medicaoId = await repository.AddMeasurementAsync(
            uow, control.PegamentoControloId, oneSided, "peg-actor");
        await uow.CommitAsync();

        Assert.NotEqual(Guid.Empty, medicaoId);
        var stored = await ReadContraCosturaAsync(medicaoId);
        Assert.Null(stored); // NULL persisted — no 23502 (N39)
        Assert.Equal(1, await CountMeasurementsAsync(control.PegamentoControloId));
    }

    private static async Task<bool> ContraCosturaStillNotNullAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'pegamento_medicoes'
              AND column_name = 'contra_costura'
              AND is_nullable = 'NO';
            """, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) >= 1;
    }

    private static async Task<decimal?> ReadContraCosturaAsync(Guid medicaoId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT contra_costura FROM pegamento_medicoes WHERE pegamento_medicao_id = @Id;", connection);
        command.Parameters.AddWithValue("Id", medicaoId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToDecimal(value);
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] PegamentoPersistencePostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL PC-01/PG-04 assertions were not executed.");
        return true;
    }

    private static PegamentoControlo NewControl(Guid jobOnId, Guid jobOnRevisionId)
    {
        return PegamentoControlo.Hydrate(
            controloId: Guid.NewGuid(),
            jobOnId: jobOnId,
            jobOnRevisionId: jobOnRevisionId,
            productionCode: "PEG-PG-" + Guid.NewGuid().ToString("N"),
            machineCode: "B1",
            referenceSnapshot: "T123",
            cmSnapshot: null,
            bqSnapshot: null,
            mfSnapshot: null,
            cmNominal: 52.00m,
            bqNominal: null,
            mfNominal: null,
            tolerance: 0.20m,
            status: PegamentoControloStatus.Aberto,
            notas: null,
            measurements: Array.Empty<PegamentoMedicao>(),
            createdAtUtc: DateTimeOffset.UtcNow,
            createdBy: null,
            updatedAtUtc: null);
    }

    private static PegamentoMedicao NewMedicao(Guid controloId) => new()
    {
        PegamentoControloId = controloId,
        ComponentKey = PegamentoComponentKey.CM,
        ToolNumber = 1,
        Costura = 52.30m,
        ContraCostura = 52.00m,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task<TestContext> SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var productionCode = "PEGON-" + suffix;

        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO job_on (job_on_id, production_code, machine_code, status, created_at_utc)
            VALUES (@JobOnId, @ProductionCode, 'B1', 'rascunho', now());
            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, production_snapshot, sections, saved_at_utc)
            VALUES (@RevisionId, @JobOnId, 1, @ProductionSnapshot::jsonb, '{}'::jsonb, now());
            """, connection);
        command.Parameters.AddWithValue("JobOnId", jobOnId);
        command.Parameters.AddWithValue("RevisionId", revisionId);
        command.Parameters.AddWithValue("ProductionCode", productionCode);
        command.Parameters.AddWithValue("ProductionSnapshot", $"\"{productionCode}\"");
        await command.ExecuteNonQueryAsync();
        return new TestContext(jobOnId, revisionId);
    }

    private static async Task<(DateTime? CreatedAt, DateTime? UpdatedAt)> ReadTimestampsAsync(Guid controloId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT created_at_utc, updated_at_utc
            FROM pegamento_controlos WHERE pegamento_controlo_id = @Id;
            """, connection);
        command.Parameters.AddWithValue("Id", controloId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDateTime(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
    }

    private static async Task<int> CountMeasurementsAsync(Guid controloId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pegamento_medicoes WHERE pegamento_controlo_id = @Id;", connection);
        command.Parameters.AddWithValue("Id", controloId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record TestContext(Guid JobOnId, Guid JobOnRevisionId);
}