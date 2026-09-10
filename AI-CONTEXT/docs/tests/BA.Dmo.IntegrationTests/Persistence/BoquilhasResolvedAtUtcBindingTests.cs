using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL binding proof for the BOQUILHAS readback regression:
/// <c>bq_discrepancies.resolved_at_utc</c> is a timestamptz that Npgsql surfaces
/// as DateTime on dynamic rows, so the old <c>as DateTimeOffset?</c> mapper cast
/// silently returned null after a discrepancy resolution. The explicit conversion
/// must surface the stored instant (and keep the null path null for open
/// discrepancies). The disposable database is supplied through
/// BA_DMO_TEST_DATABASE, following RepairExitBindingTests.
/// </summary>
public sealed class BoquilhasResolvedAtUtcBindingTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task ListDiscrepanciesAsync_SurfacesResolvedAtUtcAndKeepsOpenNull()
    {
        if (SkipIfNoDatabase()) return;
        var actorId = "boquilhas-ts-" + Guid.NewGuid().ToString("N")[..8];
        var templateId = "boquilhas-ts-tpl-" + Guid.NewGuid().ToString("N")[..8];
        var lotId = Guid.NewGuid();
        var traceId = Guid.NewGuid();
        var openDiscrepancyId = Guid.NewGuid();
        var resolvedDiscrepancyId = Guid.NewGuid();
        var reference = "T" + (100 + Guid.NewGuid().GetHashCode() % 900 + 900) % 900;
        var batch = "TS-BATCH-" + Guid.NewGuid().ToString("N")[..6];

        await SeedAsync(actorId, templateId, lotId, traceId, openDiscrepancyId,
            resolvedDiscrepancyId, reference, batch);

        try
        {
            var repository = new DapperBoquilhasRepository(new DbConnectionFactory(ConnectionString!));
            var resolvedAt = new DateTimeOffset(2026, 9, 11, 16, 45, 0, TimeSpan.Zero);

            var discrepancies = await repository.ListDiscrepanciesAsync(lotId);
            Assert.Equal(2, discrepancies.Count);

            // The timestamptz readback regression: `as DateTimeOffset?` returned
            // null for the resolved row; the explicit conversion must surface it.
            var resolved = Assert.Single(discrepancies, d => d.BqDiscrepancyId == resolvedDiscrepancyId);
            Assert.NotNull(resolved.ResolvedAtUtc);
            Assert.Equal(resolvedAt.ToUniversalTime(), resolved.ResolvedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(actorId, resolved.ResolvedBy);
            Assert.Equal("Excesso devolvido (binding)", resolved.ResolutionNote);

            // The null path must stay null: an open discrepancy is unresolved.
            var open = Assert.Single(discrepancies, d => d.BqDiscrepancyId == openDiscrepancyId);
            Assert.Null(open.ResolvedAtUtc);
            Assert.Null(open.ResolvedBy);
            Assert.Equal("open", BqDiscrepancyStatusCodec.ToStorage(open.Status));
        }
        finally
        {
            await CleanupAsync(actorId, templateId, lotId, traceId, openDiscrepancyId, resolvedDiscrepancyId);
        }
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] BoquilhasResolvedAtUtcBindingTests: BA_DMO_TEST_DATABASE not set - " +
            "real PostgreSQL binding assertions were not executed.");
        return true;
    }

    private static async Task SeedAsync(
        string actorId, string templateId, Guid lotId, Guid traceId,
        Guid openDiscrepancyId, Guid resolvedDiscrepancyId, string reference, string batch)
    {
        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, 'Boquilhas ts binding test', '["boquilhas"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                (@ActorId, @AuthUserId, @TemplateId, 'Boquilhas Ts Binding Test', TRUE);
            INSERT INTO bq_lotes (bq_lote_id, reference, batch_code, allowed_lines, lifecycle_state, created_by)
            VALUES (@LotId, @Reference, @Batch, ARRAY['B1']::text[], 'available', @ActorId);
            INSERT INTO bq_traces (bq_trace_id, bq_lote_id, status, purpose, start_line, created_by)
            VALUES (@TraceId, @LotId, 'active', 'production', 'B1', @ActorId);
            INSERT INTO bq_discrepancies
                (bq_discrepancy_id, bq_lote_id, bq_trace_id, expected_qty, actual_qty, excess_qty,
                 status, resolution_note, resolved_by, resolved_at_utc, created_by)
            VALUES
                (@OpenId, @LotId, @TraceId, 20, 24, 4, 'open', NULL, NULL, NULL, @ActorId),
                (@ResolvedId, @LotId, @TraceId, 20, 24, 4, 'resolved',
                 'Excesso devolvido (binding)', @ActorId, '2026-09-11T16:45:00Z'::timestamptz, @ActorId);
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("TraceId", traceId),
            new NpgsqlParameter("OpenId", openDiscrepancyId),
            new NpgsqlParameter("ResolvedId", resolvedDiscrepancyId),
            new NpgsqlParameter("Reference", reference),
            new NpgsqlParameter("Batch", batch));
    }

    private static async Task CleanupAsync(
        string actorId, string templateId, Guid lotId, Guid traceId,
        Guid openDiscrepancyId, Guid resolvedDiscrepancyId)
    {
        await ExecuteAsync(
            """
            DELETE FROM bq_discrepancies
            WHERE bq_discrepancy_id IN (@OpenId, @ResolvedId);
            DELETE FROM bq_traces WHERE bq_trace_id = @TraceId;
            DELETE FROM bq_lotes WHERE bq_lote_id = @LotId;
            DELETE FROM internal_users WHERE actor_id = @ActorId;
            DELETE FROM access_templates WHERE template_id = @TemplateId;
            """,
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("TraceId", traceId),
            new NpgsqlParameter("OpenId", openDiscrepancyId),
            new NpgsqlParameter("ResolvedId", resolvedDiscrepancyId));
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }
}