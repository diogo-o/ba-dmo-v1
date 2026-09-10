using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL round-trip proving control readback (detail + history)
/// survives a persisted measurement when the frozen nominals are present —
/// regression for the PROD cross-module smoke: HydrateControl called
/// HasValue on Npgsql dynamic decimal values (RuntimeBinderException, 500)
/// for any control with a measurement and a non-null nominal.
/// BA_DMO_TEST_DATABASE must identify an isolated test database; every row
/// uses unique identifiers (actor/template/jobon/control/measurement).
/// </summary>
public sealed class PegamentoNominalReadbackPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task ReadbackWithMeasurementAndNominals_SurfacesMeasurementAndTolerance()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N");
        var actorId = "peg-nominal-" + suffix[..10];
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        await SeedAsync(actorId, jobOnId, revisionId, suffix);

        var repository = new DapperPegamentoRepository(new DbConnectionFactory(ConnectionString!));
        var control = PegamentoControlo.Hydrate(
            controloId: Guid.NewGuid(),
            jobOnId: jobOnId,
            jobOnRevisionId: revisionId,
            productionCode: "PEG-NOM-" + suffix[..10],
            machineCode: "B1",
            referenceSnapshot: "1015T72",
            cmSnapshot: null,
            bqSnapshot: null,
            mfSnapshot: null,
            cmNominal: 42.50m,
            bqNominal: 25.00m,
            mfNominal: 38.00m,
            tolerance: 0.20m,
            status: PegamentoControloStatus.Aberto,
            notas: null,
            measurements: Array.Empty<PegamentoMedicao>(),
            createdAtUtc: DateTimeOffset.UtcNow,
            createdBy: null,
            updatedAtUtc: null);

        await using (var seed = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.CreateAsync(seed, control);
            await seed.CommitAsync();
        }

        // CM measurement within tolerance of the frozen nominal.
        var cm = new PegamentoMedicao
        {
            PegamentoControloId = control.PegamentoControloId,
            ComponentKey = PegamentoComponentKey.CM,
            ToolNumber = 1,
            Costura = 42.45m,
            ContraCostura = 41.90m,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        await using (var uow = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.AddMeasurementAsync(uow, control.PegamentoControloId, cm, actorId);
            await uow.CommitAsync();
        }

        // Readback paths that previously 500'd (RuntimeBinderException:
        // 'decimal' does not contain a definition for 'HasValue').
        var readback = await repository.GetByIdAsync(control.PegamentoControloId);
        Assert.NotNull(readback);
        Assert.Equal(42.50m, readback!.CmNominal);
        Assert.Single(readback.Measurements);
        var stored = readback.Measurements[0];
        Assert.Equal(PegamentoComponentKey.CM, stored.ComponentKey);
        Assert.Equal(42.45m, stored.Costura);
        Assert.NotNull(stored.Ovalizacao);
        Assert.NotNull(stored.Media);
        Assert.Equal(PegamentoToleranceStatus.Ok, stored.ToleranceStatus);

        var history = await repository.GetByIdAsync(control.PegamentoControloId);
        Assert.Single(history!.Measurements);
    }

    [Fact]
    public async Task ReadbackWithMeasurement_ExceededTolerance_IsMarkedNotOk()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N");
        var actorId = "peg-nominal-" + suffix[..10];
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        await SeedAsync(actorId, jobOnId, revisionId, suffix);

        var repository = new DapperPegamentoRepository(new DbConnectionFactory(ConnectionString!));
        var control = PegamentoControlo.Hydrate(
            controloId: Guid.NewGuid(),
            jobOnId: jobOnId,
            jobOnRevisionId: revisionId,
            productionCode: "PEG-NOK-" + suffix[..10],
            machineCode: "B1",
            referenceSnapshot: "1015T72",
            cmSnapshot: null,
            bqSnapshot: null,
            mfSnapshot: null,
            cmNominal: 42.50m,
            bqNominal: 25.00m,
            mfNominal: 38.00m,
            tolerance: 0.20m,
            status: PegamentoControloStatus.Aberto,
            notas: null,
            measurements: Array.Empty<PegamentoMedicao>(),
            createdAtUtc: DateTimeOffset.UtcNow,
            createdBy: null,
            updatedAtUtc: null);

        await using (var seed = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.CreateAsync(seed, control);
            await seed.CommitAsync();
        }

        var mf = new PegamentoMedicao
        {
            PegamentoControloId = control.PegamentoControloId,
            ComponentKey = PegamentoComponentKey.MF,
            ToolNumber = 1,
            Costura = 38.40m,
            ContraCostura = null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        await using (var uow = await DapperUnitOfWork.BeginAsync(new DbConnectionFactory(ConnectionString!)))
        {
            await repository.AddMeasurementAsync(uow, control.PegamentoControloId, mf, actorId);
            await uow.CommitAsync();
        }

        var readback = await repository.GetByIdAsync(control.PegamentoControloId);
        Assert.NotNull(readback);
        var stored = Assert.Single(readback!.Measurements);
        Assert.Equal(PegamentoToleranceStatus.Exceeded, stored.ToleranceStatus);
    }

    private static async Task SeedAsync(string actorId, Guid jobOnId, Guid revisionId, string suffix)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, @TemplateName, '["controlo"]'::jsonb, TRUE);
            INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc)
            VALUES (@ActorId, @AuthUserId, @TemplateId, @DisplayName, TRUE, now(), now());
            INSERT INTO job_on (job_on_id, production_code, machine_code, status, created_at_utc)
            VALUES (@JobOnId, @ProductionCode, 'B1', 'rascunho', now());
            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, production_snapshot, sections, saved_at_utc)
            VALUES (@RevisionId, @JobOnId, 1, @ProductionSnapshot::jsonb, '{}'::jsonb, now());
            """, connection);
        command.Parameters.AddWithValue("TemplateId", "tpl-peg-nom-" + suffix[..10]);
        command.Parameters.AddWithValue("TemplateName", "PEG NOMINAL " + suffix[..10]);
        command.Parameters.AddWithValue("ActorId", actorId);
        command.Parameters.AddWithValue("AuthUserId", Guid.NewGuid());
        command.Parameters.AddWithValue("DisplayName", "PEG NOMINAL " + suffix[..10]);
        command.Parameters.AddWithValue("JobOnId", jobOnId);
        command.Parameters.AddWithValue("ProductionCode", "PEG-" + suffix[..10]);
        command.Parameters.AddWithValue("RevisionId", revisionId);
        command.Parameters.AddWithValue("ProductionSnapshot", $"\"PEG-{suffix[..10]}\"");
        await command.ExecuteNonQueryAsync();
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] PegamentoNominalReadbackPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL nominal-readback assertions were not executed.");
        return true;
    }
}