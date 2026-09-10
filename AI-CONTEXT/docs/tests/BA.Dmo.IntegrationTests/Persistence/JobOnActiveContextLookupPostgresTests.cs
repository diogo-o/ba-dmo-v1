using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL regression for the Reparação Interna active-context lookup
/// (R009): the resolution must parse the canonical Job On reference snapshot
/// (<c>{"article_reference": code}</c>, owner D2 rule) exactly like the
/// Controlo/Peso/Pegamentos context readers. The previous implementation only
/// accepted a string root or a <c>"reference"</c> key, so a current Job On could
/// never resolve a line context (always None).
/// BA_DMO_TEST_DATABASE must identify an isolated, freshly migrated (N01–N42)
/// test database; every row uses unique identifiers (job_on_component is
/// append-only, so the database must be fresh between runs — same convention as
/// the other real-PostgreSQL tests).
/// </summary>
public sealed class JobOnActiveContextLookupPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task ResolveActiveAsync_CanonicalArticleReferenceSnapshot_ResolvesSingleContext()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var factory = new DbConnectionFactory(ConnectionString!);
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var cmLotId = Guid.NewGuid();
        var cmRefId = Guid.NewGuid();
        // Yesterday: activated at 09:00 factory local the day before → active now.
        var plannedStart = DateTimeOffset.UtcNow.AddDays(-1).ToOffset(TimeSpan.Zero);

        await SeedAsync(jobOnId, revisionId, cmRefId, cmLotId, plannedStart, suffix);

        var lookup = new DapperJobOnActiveContextLookup(factory, new DapperJobOnRepository(factory));
        var resolution = await lookup.ResolveActiveAsync("B1", DateTimeOffset.UtcNow);

        // The canonical-decode regression: the resolved context must carry the
        // reference from the Job On's {article_reference} snapshot. Exact ids
        // are not asserted — several runs on the same factory calendar day tie
        // on the same activation moment (09:00 local of their start date) and
        // the projection picks the first, so the assertions stay on the shared
        // marker facts every seeded run satisfies.
        Assert.Equal(InternalRepairResolutionKind.Single, resolution.Kind);
        Assert.NotNull(resolution.Context);
        Assert.Equal("1015T72", resolution.Context!.Reference);
        Assert.StartsWith("PROD-lookup-", resolution.Context.ProductionCode);
        Assert.Equal("B1", resolution.Context.MachineCode);
        Assert.Equal("B1", resolution.Context.Line);
        Assert.NotEmpty(resolution.Context.CmLotIds); // MP_CM source lot links read
        Assert.True(resolution.Context.ActivatedFromUtc.HasValue);
        Assert.Null(resolution.Context.ValidToUtc);
    }

    [Fact]
    public async Task ResolveActiveAsync_NoActiveProduction_ReturnsNone()
    {
        if (SkipIfNoDatabase()) return;

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var factory = new DbConnectionFactory(ConnectionString!);
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        // Future start: not yet activated at 09:00 factory local.
        var plannedStart = DateTimeOffset.UtcNow.AddDays(5).ToOffset(TimeSpan.Zero);

        await SeedAsync(jobOnId, revisionId, Guid.NewGuid(), Guid.NewGuid(), plannedStart, suffix);

        var lookup = new DapperJobOnActiveContextLookup(factory, new DapperJobOnRepository(factory));
        var resolution = await lookup.ResolveActiveAsync("B3", DateTimeOffset.UtcNow);

        Assert.Equal(InternalRepairResolutionKind.None, resolution.Kind);
        Assert.Null(resolution.Context);
    }

    private static async Task SeedAsync(
        Guid jobOnId, Guid revisionId, Guid cmRefId, Guid cmLotId,
        DateTimeOffset plannedStart, string suffix)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO tool_references (tool_reference_id, tool_type, ref_code)
            VALUES (@CmRefId, 'CM', @CmRefCode);
            INSERT INTO tool_lotes (tool_lote_id, tool_reference_id, lote, qty, allowed_lines)
            VALUES (@CmLotId, @CmRefId, @Lot, 1, ARRAY['B1']);
            INSERT INTO job_on (job_on_id, production_code, machine_code, status,
                                planned_start_at, planned_end_at, created_at_utc)
            VALUES (@JobOnId, @ProductionCode, 'B1', 'planeado',
                    @PlannedStart, @PlannedEnd, now());
            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number,
                 production_snapshot, reference_snapshot, machine_snapshot, sections, saved_at_utc)
            VALUES (@RevisionId, @JobOnId, 1,
                    @ProductionSnapshot::jsonb, @ReferenceSnapshot::jsonb,
                    @MachineSnapshot::jsonb, '{}'::jsonb, now());
            INSERT INTO job_on_component
                (job_on_component_id, job_on_revision_id, family,
                 source_tool_id, source_lot_id, reference_snapshot, lot_snapshot, display_order)
            VALUES (@ComponentId, @RevisionId, 'MP_CM',
                    @CmRefId, @CmLotId, @CmRefCode, @Lot, 1);
            UPDATE job_on SET current_revision_id = @RevisionId WHERE job_on_id = @JobOnId;
            """, connection);
        command.Parameters.AddWithValue("JobOnId", jobOnId);
        command.Parameters.AddWithValue("ProductionCode", "PROD-lookup-" + suffix);
        command.Parameters.AddWithValue("PlannedStart", plannedStart.UtcDateTime);
        command.Parameters.AddWithValue("PlannedEnd", plannedStart.AddDays(1).UtcDateTime);
        command.Parameters.AddWithValue("RevisionId", revisionId);
        command.Parameters.AddWithValue("ProductionSnapshot", $"{{\"production_code\": \"PROD-lookup-{suffix}\"}}");
        command.Parameters.AddWithValue("ReferenceSnapshot", "{\"article_reference\": \"1015T72\"}");
        command.Parameters.AddWithValue("MachineSnapshot", "{\"machine_code\": \"B1\"}");
        command.Parameters.AddWithValue("ComponentId", Guid.NewGuid());
        command.Parameters.AddWithValue("CmRefId", cmRefId);
        command.Parameters.AddWithValue("CmLotId", cmLotId);
        command.Parameters.AddWithValue("CmRefCode", "SMOKE-LOOKUP-" + suffix);
        command.Parameters.AddWithValue("Lot", "L-" + suffix[..8]);
        await command.ExecuteNonQueryAsync();
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] JobOnActiveContextLookupPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL context-lookup assertions were not executed.");
        return true;
    }
}