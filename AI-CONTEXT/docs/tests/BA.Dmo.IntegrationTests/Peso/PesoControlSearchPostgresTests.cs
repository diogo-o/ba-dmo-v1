using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
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

    [Fact]
    public async Task CreateControl_TwiceForSameIdentity_SecondFailsWithDomainError()
    {
        // uq_peso_controlos_identity (mold, neckring, production, line, lote,
        // date) is enforced by the database; the service must turn a duplicate
        // daily control into a domain error instead of leaking 23505 as a 500.
        if (SkipIfNoDatabase()) return;
        var context = await SeedServiceContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-dup-" + context.Suffix));
        var jobOnRepository = new DapperJobOnRepository(CreateFactory("peso-dup-jobon-" + context.Suffix));
        var accessor = new PesoOperadorAccessor(context.ActorId);
        var service = new PesoService(
            new PesoAuthorizationGate(accessor), repository, jobOnRepository,
            new FixedTestClock(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)));

        var first = await service.CreateControlAsync(new CreateControlRequest(
            context.JobOnId, new DateTime(2026, 9, 10), null, null, "obs",
            [new PesoLeituraInput("CM1", 152.43m)]));
        if (!first.IsSuccess)
            throw new Xunit.Sdk.XunitException(
                $"first create failed: {first.Error.Code} {first.Error.Message}");

        var duplicate = await service.CreateControlAsync(new CreateControlRequest(
            context.JobOnId, new DateTime(2026, 9, 10), null, null, "obs",
            [new PesoLeituraInput("CM1", 152.43m)]));

        Assert.True(duplicate.IsFailure);
        Assert.Equal("PESO_CONTROL_DUPLICATE", duplicate.Error.Code);

        // Leave the DB as found so re-runs do not collide with the previous
        // run's (identity-unique) control.
        await repository.DeleteControlAsync(first.Value);
    }

    [Fact]
    public async Task DeleteControl_NonApproved_RemovesTheRow()
    {
        // Regression for ba_dmo_guard_peso_approved (N25): the BEFORE DELETE
        // trigger returned NEW — which is NULL on DELETE — so every delete was
        // silently SKIPPED (reported "0 rows" with no error). The repository
        // delete must physically remove non-approved controls.
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var repository = new DapperPesoRepository(CreateFactory("peso-del-" + context.Suffix));

        await repository.DeleteControlAsync(context.ControlId);

        var gone = await repository.GetControlByIdAsync(context.ControlId);
        Assert.Null(gone);
    }

    [Fact]
    public async Task DeleteControl_Approved_StillRaisesTheGuard()
    {
        // The N25 guard must keep blocking approved-control deletion after the
        // DELETE return-value correction.
        if (SkipIfNoDatabase()) return;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var referenceId = Guid.NewGuid();
        var loteId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var controlId = Guid.NewGuid();
        var productionCode = "2026" + Guid.NewGuid().ToString("N")[..6];

        await ExecuteAsync(
            """
            INSERT INTO peso_references
                (peso_reference_id, mold_number, neckring_number, change_log)
            VALUES
                (@ReferenceId, 'APR-GUARD-' || @Suffix, 'NK-GUARD-' || @Suffix, '[]'::jsonb);

            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @ProductionCode, 'L1', 'rascunho');

            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, sections)
            VALUES
                (@RevisionId, @JobOnId, 1, '{}'::jsonb);

            INSERT INTO peso_lotes
                (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
            VALUES
                (@LoteId, @ReferenceId, 'LOTE-GUARD-' || @Suffix, 'NNPB', ARRAY['L1'], 'guard');

            INSERT INTO peso_controlos
                (peso_controlo_id, peso_reference_id, peso_lote_id, record_type,
                 mold_number, neckring_number, production_code, line, lote,
                 control_date, job_on_id, job_on_revision_id, status, approved_at_utc)
            VALUES
                (@ControlId, @ReferenceId, @LoteId, 'novo_controlo',
                 'APR-GUARD-' || @Suffix, 'NK-GUARD-' || @Suffix, @ProductionCode, 'L1', 'LOTE-GUARD-' || @Suffix,
                 DATE '2026-09-10', @JobOnId, @RevisionId, 'aprovado', now());
            """,
            new NpgsqlParameter("Suffix", suffix),
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("LoteId", loteId),
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("RevisionId", revisionId),
            new NpgsqlParameter("ControlId", controlId),
            new NpgsqlParameter("ProductionCode", productionCode));

        var repository = new DapperPesoRepository(CreateFactory("peso-del-apr-" + suffix));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            repository.DeleteControlAsync(controlId));
    }

    [Fact]
    public async Task GetControlById_RoundTripsPersistedLeitura()
    {
        // Regression: Diagram of the Peso data-entry read path. MapLeitura never
        // assigned PesoLeituraId (the record defaults to a NEW Guid per read, so
        // every detail/calculate/submit saw phantom ids) and the readings jsonb
        // parse yielded null weights, making submit report PESO_CONTROL_NO_READING
        // for a control whose readings exist in the database.
        if (SkipIfNoDatabase()) return;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var controlId = Guid.NewGuid();
        var leituraId = Guid.NewGuid();
        var loteId = Guid.NewGuid();
        var refId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var prod = "2026" + Guid.NewGuid().ToString("N")[..6];
        var readingsJson = "{ \"pesoVidro\": null, \"pesoEmAgua\": 123.45 }";

        await ExecuteAsync(
            """
            INSERT INTO peso_references
                (peso_reference_id, mold_number, neckring_number, change_log)
            VALUES
                (@RefId, 'RT-M-' || @Suffix, 'RT-N-' || @Suffix, '[]'::jsonb);

            INSERT INTO peso_lotes
                (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
            VALUES
                (@LoteId, @RefId, 'L-RT-' || @Suffix, 'NNPB', ARRAY['B1'], 'rt');

            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @Prod, 'B1', 'rascunho');

            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, sections)
            VALUES
                (@RevId, @JobOnId, 1, '{}'::jsonb);

            INSERT INTO peso_controlos
                (peso_controlo_id, peso_reference_id, peso_lote_id, record_type,
                 mold_number, neckring_number, production_code, line, lote,
                 control_date, job_on_id, job_on_revision_id, status)
            VALUES
                (@ControlId, @RefId, @LoteId, 'novo_controlo',
                 'RT-M-' || @Suffix, 'RT-N-' || @Suffix, @Prod, 'B1', 'L-RT-' || @Suffix,
                 DATE '2026-09-10', @JobOnId, @RevId, 'rascunho');

            INSERT INTO peso_leituras
                (peso_leitura_id, peso_controlo_id, cm_number, readings)
            VALUES
                (@LeituraId, @ControlId, 'CM-RT', @Readings::jsonb);
            """,
            new NpgsqlParameter("Suffix", suffix),
            new NpgsqlParameter("RefId", refId),
            new NpgsqlParameter("LoteId", loteId),
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("RevId", revId),
            new NpgsqlParameter("ControlId", controlId),
            new NpgsqlParameter("LeituraId", leituraId),
            new NpgsqlParameter("Prod", prod),
            new NpgsqlParameter("Readings", readingsJson));

        var repository = new DapperPesoRepository(CreateFactory("peso-rt-" + suffix));
        var control = await repository.GetControlByIdAsync(controlId);

        Assert.NotNull(control);
        var leitura = Assert.Single(control!.Leituras);
        Assert.Equal(leituraId, leitura.PesoLeituraId);
        Assert.Equal("CM-RT", leitura.CmNumber);
        Assert.Equal(123.45m, leitura.PesoEmAgua);
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
        // Non-pooled: the raw connection-string pool showed deterministic
        // reset-on-reuse socket failures on this host (IOException while
        // reading the batch result) when earlier tests in the class had reused
        // same-pool connections. Seeding is rare; a fresh connection per seed
        // is cheaper than chasing pool reset flakiness.
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString!) { Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Full production context for the duplicate-control proof: an actor, a
    /// Job On with a canonical reference snapshot and NNPB process revision,
    /// plus the registered Peso reference (mold 2099 / neckring SMK101) and
    /// its lote. The reference text 2099SMK101 resolves through
    /// ExtractReferenceCode + the mold/neckring split.
    /// </summary>
    private static async Task<ServiceContext> SeedServiceContextAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobOnId = Guid.NewGuid();
        var productionCode = "2026" + Guid.NewGuid().ToString("N")[..6];
        var actorId = Guid.NewGuid().ToString();

        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name)
            VALUES ('tpl-peso-' || @Suffix, 'Peso smoke ' || @Suffix);

            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                (@ActorId, @AuthUserId, 'tpl-peso-' || @Suffix, 'Peso smoke ' || @Suffix, TRUE);

            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @ProductionCode, 'B2', 'rascunho');

            INSERT INTO peso_references
                (peso_reference_id, mold_number, neckring_number, change_log)
            VALUES
                (@ReferenceId, '2099', 'SMK' || @NeckSuffix, '[]'::jsonb);

            INSERT INTO peso_lotes
                (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
            VALUES
                (@LoteId, @ReferenceId, 'L' || @Suffix, 'NNPB', ARRAY['B2'], 'SMOKE');
            """,
            new NpgsqlParameter("Suffix", suffix),
            new NpgsqlParameter("NeckSuffix", suffix.ToUpperInvariant() + "A"),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("ProductionCode", productionCode),
            new NpgsqlParameter("ReferenceId", Guid.NewGuid()),
            new NpgsqlParameter("LoteId", Guid.NewGuid()));

        // Persist the current revision through the real repository so the
        // current-revision wiring matches production.
        var revisionId = Guid.NewGuid();
        var jobOnRepository = new DapperJobOnRepository(CreateFactory("peso-dup-seed-" + suffix));
        await jobOnRepository.SaveRevisionGraphAsync(
            new JobOnRevision
            {
                JobOnRevisionId = revisionId,
                JobOnId = jobOnId,
                RevisionNumber = 2,
                ProductionSnapshot = "{\"production_code\":\"" + productionCode + "\"}",
                ReferenceSnapshot = "{\"article_reference\":\"2099SMK" + suffix.ToUpperInvariant() + "A\"}",
                MachineSnapshot = "{\"machine_code\":\"B2\"}",
                DatesSnapshot = "{\"start_at\":null,\"end_at\":null}",
                Sections = "{}",
                TypeSnapshot = null,
                StopSnapshot = null,
                ProcessSnapshot = "NNPB",
                GeneralNotes = null,
                ChangeReason = null,
                SavedBy = actorId,
                SavedAtUtc = DateTime.UtcNow
            },
            "jobon.guardar", actorId, "{}", "{}");

        return new ServiceContext(suffix, jobOnId, actorId);
    }

    private sealed record ServiceContext(string Suffix, Guid JobOnId, string ActorId);

    private sealed class PesoOperadorAccessor(string actorId) : ICurrentUserAccessor
    {
        public CurrentUser? Current => new(
            Guid.Parse(actorId), "Operador", ["peso"], Array.Empty<string>());
    }

    private sealed class FixedTestClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }

    private sealed record TestContext(string Suffix, Guid ControlId, string MoldNumber, string NeckringNumber);
}