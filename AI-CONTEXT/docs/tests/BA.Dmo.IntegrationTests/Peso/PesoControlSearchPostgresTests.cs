using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Peso;

/// <summary>
/// Real-PostgreSQL regression proof for the Peso control search query
/// (DapperPesoRepository.GetControlsAsync). The Responsável Peso page issued
/// this query with a NULL date filter and crashed with Npgsql/PostgreSQL
/// 42P08 "could not determine data type of parameter $4" because a NULL
/// DateTime parameter compared against the date-typed control_date could not
/// be resolved (ambiguous date/timestamp/timestamptz promotion). The query now
/// pins the parameters with an explicit ::date cast.
///
/// BA_DMO_TEST_DATABASE must identify an isolated, already-migrated test
/// database. Every row uses a unique suffix; no existing business row is
/// selected or mutated.
/// </summary>
public sealed class PesoControlSearchPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task GetControlsAsync_NoFilters_DoesNotThrow42P08()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-search-none-" + context.Suffix));

        var controls = await repository.GetControlsAsync(
            referenceId: null, search: null, status: null, type: null, from: null, to: null);

        var control = Assert.Single(controls, c => c.MoldNumber == context.MoldNumber);
        Assert.Equal(context.NeckringNumber, control.NeckringNumber);
        Assert.Equal(PesoControlState.Pendente, control.Status);
    }

    [Fact]
    public async Task GetControlsAsync_StatusFilter_MatchingResponsavelPageCall_DoesNotThrow42P08()
    {
        // The Responsável page (/peso/responsavel) searches with status='pendente'
        // and all date parameters NULL — the exact 42P08 combination (fx).
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-search-status-" + context.Suffix));

        var controls = await repository.GetControlsAsync(
            referenceId: null, search: null, status: "pendente", type: null, from: null, to: null);

        var control = Assert.Single(controls, c => c.MoldNumber == context.MoldNumber);
        Assert.Equal(context.MoldNumber, control.MoldNumber);
        Assert.Equal(PesoControlState.Pendente, control.Status);
    }

    [Fact]
    public async Task GetControlsAsync_DateRangeFilter_ReturnsMatchingControl()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-search-dates-" + context.Suffix));

        var controls = await repository.GetControlsAsync(
            referenceId: null, search: null, status: null, type: null,
            from: new DateTime(2026, 9, 1), to: new DateTime(2026, 9, 30));

        var control = Assert.Single(controls, c => c.MoldNumber == context.MoldNumber);
        Assert.Equal(context.MoldNumber, control.MoldNumber);
    }

    [Fact]
    public async Task GetControlsAsync_OutOfRangeDates_ReturnsNothing()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-search-empty-" + context.Suffix));

        var controls = await repository.GetControlsAsync(
            referenceId: null, search: null, status: "aprovado", type: null,
            from: new DateTime(2026, 8, 1), to: new DateTime(2026, 8, 31));

        Assert.Empty(controls);
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] PesoControlSearchPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL control-search assertions were not executed.");
        return true;
    }

    private static DbConnectionFactory CreateFactory(string applicationName)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString!)
        {
            ApplicationName = applicationName
        };
        return new DbConnectionFactory(builder.ConnectionString);
    }

    private static async Task<TestContext> SeedContextAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var referenceId = Guid.NewGuid();
        var loteId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var controlId = Guid.NewGuid();
        var productionCode = "2026" + Guid.NewGuid().ToString("N")[..6];
        var mold = "SMK-TST-" + suffix;
        var neck = "SMK-TST-N-" + suffix;

        await ExecuteAsync(
            """
            INSERT INTO peso_references
                (peso_reference_id, mold_number, neckring_number, change_log)
            VALUES
                (@ReferenceId, @Mold, @Neck, '[]'::jsonb);

            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @ProductionCode, 'B2', 'rascunho');

            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, sections)
            VALUES
                (@RevisionId, @JobOnId, 1, '{}'::jsonb);

            INSERT INTO peso_lotes
                (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
            VALUES
                (@LoteId, @ReferenceId, 'L' || @Suffix, 'NNPB', ARRAY['B2'], 'SMOKE');

            INSERT INTO peso_controlos
                (peso_controlo_id, peso_reference_id, peso_lote_id, record_type,
                 mold_number, neckring_number, production_code, line, lote,
                 control_date, job_on_id, job_on_revision_id, status)
            VALUES
                (@ControlId, @ReferenceId, @LoteId, 'novo_controlo',
                 @Mold, @Neck, @ProductionCode, 'B2', 'L' || @Suffix,
                 DATE '2026-09-10', @JobOnId, @RevisionId, 'pendente');
            """,
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("LoteId", loteId),
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("RevisionId", revisionId),
            new NpgsqlParameter("ControlId", controlId),
            new NpgsqlParameter("Mold", mold),
            new NpgsqlParameter("Neck", neck),
            new NpgsqlParameter("ProductionCode", productionCode),
            new NpgsqlParameter("Suffix", suffix));

        return new TestContext(suffix, controlId, mold, neck);
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record TestContext(string Suffix, Guid ControlId, string MoldNumber, string NeckringNumber);
}