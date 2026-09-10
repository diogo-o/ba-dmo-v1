using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL binding proof for the CONTROLO readback regression:
/// <c>controlo_sheets.submitted_at_utc</c>/<c>decided_at_utc</c> are timestamptz
/// columns that Npgsql surfaces as DateTime on dynamic rows, so the old
/// <c>as DateTimeOffset?</c> mapper casts silently returned null after the submit
/// and decide workflow steps. The explicit conversions must surface the stored
/// instants through the real domain + repository round-trip. The disposable
/// database is supplied through BA_DMO_TEST_DATABASE, following
/// RepairExitBindingTests.
/// </summary>
public sealed class ControloSheetTimestampsBindingTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task SubmitThenDecide_RoundTripSubmittedAndDecidedTimestamps()
    {
        if (SkipIfNoDatabase()) return;
        var actorId = "controlo-ts-" + Guid.NewGuid().ToString("N")[..8];
        var templateId = "controlo-ts-tpl-" + Guid.NewGuid().ToString("N")[..8];
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var productionCode = "P" + (1000 + Guid.NewGuid().GetHashCode() % 9000 + 9000) % 9000;
        var reference = "CM-" + Guid.NewGuid().ToString("N")[..6];

        await SeedContextAsync(actorId, templateId, jobOnId, revisionId, productionCode, reference);

        try
        {
            var factory = new DbConnectionFactory(ConnectionString!);
            var repository = new DapperControloSheetRepository(factory);
            var createdNow = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

            var context = new ControloFolhaProductionContext(
                jobOnId, revisionId, productionCode, reference, "M1",
                [new ControloFolhaComponent("MP_CM", null, null, reference, "TS-LOT", "TS Technical")]);
            var created = ControloFolha.Create(context, actorId, createdNow);
            Assert.True(created.IsSuccess);
            var sheet = created.Value;

            Guid sheetId;
            await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
            {
                sheetId = await repository.InsertAsync(uow, sheet);
                await uow.CommitAsync();
            }

            // Draft readback: both workflow timestamps must be null.
            var draft = await repository.GetByIdAsync(sheetId);
            Assert.NotNull(draft);
            Assert.Equal(ControloFolhaState.Rascunho, draft!.State);
            Assert.Null(draft.SubmittedAtUtc);
            Assert.Null(draft.DecidedAtUtc);
            Assert.False(draft.HasBeenSubmitted);

            // Submit writes submitted_at_utc through the domain + repo.
            var submittedNow = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
            var submitted = sheet.Submit(actorId, "Folha entregue (binding)", submittedNow);
            Assert.True(submitted.IsSuccess);
            await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
            {
                await repository.UpdateAsync(uow, sheet, sheet.Items);
                await uow.CommitAsync();
            }

            var pending = await repository.GetByIdAsync(sheetId);
            Assert.NotNull(pending);
            Assert.Equal(ControloFolhaState.Submetido, pending!.State);
            // The timestamptz readback regression: `as DateTimeOffset?` returned
            // null for submitted_at_utc; the explicit conversion must surface it.
            Assert.NotNull(pending.SubmittedAtUtc);
            Assert.Equal(submittedNow.ToUniversalTime(), pending.SubmittedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(actorId, pending.SubmittedBy);
            Assert.Null(pending.DecidedAtUtc);

            // Decide writes decided_at_utc through the domain + repo.
            var decidedNow = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
            var decided = sheet.Decide(ControloFolhaDecision.Aprovado, actorId, "Aprovado (binding)", decidedNow);
            Assert.True(decided.IsSuccess);
            await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
            {
                await repository.UpdateAsync(uow, sheet, sheet.Items);
                await uow.CommitAsync();
            }

            var final = await repository.GetByIdAsync(sheetId);
            Assert.NotNull(final);
            Assert.Equal(ControloFolhaState.Aprovado, final!.State);
            Assert.True(final.HasBeenSubmitted);
            Assert.True(final.HasBeenDecided);
            // The timestamptz readback regression: both workflow timestamps must
            // surface verbatim after the full submit+decide cycle.
            Assert.NotNull(final.SubmittedAtUtc);
            Assert.NotNull(final.DecidedAtUtc);
            Assert.Equal(submittedNow.ToUniversalTime(), final.SubmittedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(decidedNow.ToUniversalTime(), final.DecidedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(actorId, final.DecidedBy);
            Assert.Equal(ControloFolhaDecision.Aprovado, final.Decision);
        }
        finally
        {
            await CleanupAsync(actorId, templateId, jobOnId, revisionId);
        }
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] ControloSheetTimestampsBindingTests: BA_DMO_TEST_DATABASE not set - " +
            "real PostgreSQL binding assertions were not executed.");
        return true;
    }

    private static async Task SeedContextAsync(
        string actorId, string templateId, Guid jobOnId, Guid revisionId,
        string productionCode, string reference)
    {
        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, 'Controlo ts binding test', '["controlo"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                (@ActorId, @AuthUserId, @TemplateId, 'Controlo Ts Binding Test', TRUE);
            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status, created_by)
            VALUES
                (@JobOnId, @ProductionCode, 'M1', 'planeado', @ActorId);
            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, saved_by)
            VALUES
                (@RevisionId, @JobOnId, 1, @ActorId);
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("RevisionId", revisionId),
            new NpgsqlParameter("ProductionCode", productionCode),
            new NpgsqlParameter("Reference", reference));
    }

    private static async Task CleanupAsync(string actorId, string templateId, Guid jobOnId, Guid revisionId)
    {
        // Best-effort: job_on_revision is append-only (trigger P0001) and its FK
        // keeps job_on alive, which keeps the seeded actor/template alive, so the
        // job_on/revision/actor/template rows legitimately remain as unique-key
        // residue in the disposable test DB (fresh GUID keys per run, matching
        // the RepairExitBindingTests convention).
        await ExecuteAsync(
            """
            DELETE FROM controlo_sheet_items
            WHERE controlo_sheet_id IN (
                SELECT controlo_sheet_id FROM controlo_sheets WHERE created_by = @ActorId);
            DELETE FROM controlo_sheets WHERE created_by = @ActorId;
            """,
            new NpgsqlParameter("ActorId", actorId));
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