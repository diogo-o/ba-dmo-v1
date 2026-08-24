using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna service use cases: register persists each supplied number as its
/// own occurrence fact under the same context (repeated numbers preserved, no dedupe); NO
/// hard blocks (none/ambiguous context and lot/number mismatches still record); the exact
/// production context (job_on/revision/production/reference/lot) is persisted and history
/// reads it (never re-derives from current Job On); BQ is REJECTED as an internal repair type
/// (CM/MF-only); correction/override creates a NEW record and never modifies Job On.
/// All collaborators are in-memory fakes.
/// </summary>
public class ReparacaoInternaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static (ReparacaoInternaService service, FakeReparacaoInternaRepository repo,
        FakeJobOnActiveContextLookup ctx, FakeFerramentasPieceLookup pieces)
        Build(bool grantCorrigir = false)
    {
        var repo = new FakeReparacaoInternaRepository();
        var ctx = new FakeJobOnActiveContextLookup();
        var pieces = new FakeFerramentasPieceLookup();
        var user = grantCorrigir ? ReparacaoInternaCurrentUser.Authorized(true)
            : ReparacaoInternaCurrentUser.Authorized(false);
        var service = new ReparacaoInternaService(
            repo, ctx, pieces, new FakeReparacaoInternaUowFactory(),
            new ReparacaoInternaAuthorizationGate(user, new ReparacaoInternaFakeAuthorship()),
            new ReparacaoInternaFixedClock(Now));
        return (service, repo, ctx, pieces);
    }

    private static Guid SeedSingleContext(FakeJobOnActiveContextLookup ctx, string line = "B1")
    {
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var cmLot = Guid.NewGuid();
        ctx.SeedSingle(line, FakeJobOnActiveContextLookup.Context(
            line, jobOnId, "REF-1", "202608", new[] { cmLot }, new List<Guid>(), revisionId: revisionId));
        return cmLot;
    }

    private static RegisterReparacaoRequest Req(string line, InternalRepairToolType type, params string[] numbers)
        => new(line, type, numbers);

    // ---- Register: happy path (single active context) --------------------------

    [Fact]
    public async Task Register_WithSingleContext_SendsEachNumberAndPersistsContext()
    {
        var (service, repo, ctx, pieces) = Build();
        var cmLot = SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", cmLot, FerramentasToolType.CM);

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(repo.Records);
        Assert.Equal("B1", record.Line);
        Assert.Equal(InternalRepairToolType.CM, record.ToolType);
        Assert.Equal("1234", record.IndividualNumber);
        Assert.Equal(Now, record.OccurredAtUtc);
        Assert.Equal("repan-actor", record.OperatorId);
        Assert.Equal("REF-1", record.Reference);
        Assert.Equal("202608", record.ProductionCode);
        Assert.NotNull(record.JobOnRevisionId); // historical revision pin persisted (GAP 2)
        Assert.Single(repo.RepairEvents);
        Assert.Contains(repo.AuditEvents, a => a.action == "reparacao_interna.registrar" && a.result == "succeeded");
    }

    [Fact]
    public async Task Register_WithRepeatedNumbers_PersistsEachOccurrence()
    {
        var (service, repo, ctx, pieces) = Build();
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "5", Guid.NewGuid(), FerramentasToolType.MF);
        pieces.Seed("REF-1", "7", Guid.NewGuid(), FerramentasToolType.MF);

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.MF, "5", "5", "7"));

        Assert.True(result.IsSuccess);
        // 5,5,7 = 3 occurrences (no dedupe, no DISTINCT).
        Assert.Equal(3, repo.Records.Count);
        Assert.Equal(new[] { "5", "5", "7" }, repo.Records.Select(r => r.IndividualNumber).ToArray());
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(3, repo.RepairEvents.Count); // one event per occurrence
    }

    [Fact]
    public async Task Register_WithNoActiveContext_StillRecords_NoHardBlock()
    {
        var (service, repo, ctx, pieces) = Build();
        ctx.SeedNone("B1");

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsSuccess); // R009: absence of auto-context does NOT block.
        var record = Assert.Single(repo.Records);
        Assert.Null(record.JobOnId);
        Assert.Null(record.Reference); // no invented auto-context
    }

    [Fact]
    public async Task Register_WithAmbiguousContext_StillRecords_NoHardBlock()
    {
        var (service, repo, ctx, _) = Build();
        var revId = Guid.NewGuid();
        ctx.SeedAmbiguous("B1",
            new InternalRepairContextCandidate(Guid.NewGuid(), revId, "B1", "202608", "REF-1", "B1", Now, null),
            new InternalRepairContextCandidate(Guid.NewGuid(), revId, "B1", "202609", "REF-2", "B1", Now, null));

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsSuccess); // ambiguity never blocks (R009).
        Assert.Single(repo.Records);
    }

    [Fact]
    public async Task Register_BQ_IsRejectedAsRepairType_CM_MF_Only()
    {
        var (service, repo, ctx, pieces) = Build();
        SeedSingleContext(ctx, "C1");

        // BQ is not an internal repair type (owner decision CM/MF-only). With BQ removed
        // from the enum, the recordable boundary rejects any non-CM/MF value (the same path
        // that rejects "BQ" at the request boundary).
        var result = await service.RegistrarReparacoesAsync(Req("C1", (InternalRepairToolType)99, "77"));

        Assert.True(result.IsFailure);
        Assert.Equal("REPINT_INVALID_TYPE", result.Error.Code);
        Assert.Empty(repo.Records); // nothing persisted
    }

    [Fact]
    public async Task Register_FullReference_KeepsContextOnlySuffix()
    {
        var (service, repo, ctx, pieces) = Build();
        // A full reference like 5447T173 carries a context-only suffix (T173). The internal
        // repair record preserves the complete reference string; only the recordable TOOL TYPE
        // is constrained to CM/MF, never the reference context.
        var jobOnId = Guid.NewGuid();
        ctx.SeedSingle("C1", FakeJobOnActiveContextLookup.Context(
            "C1", jobOnId, "5447T173", "202608", new List<Guid>(), new List<Guid>(), revisionId: Guid.NewGuid()));
        pieces.Seed("5447T173", "1234", Guid.NewGuid(), FerramentasToolType.CM);

        var result = await service.RegistrarReparacoesAsync(Req("C1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(repo.Records);
        Assert.Equal("5447T173", record.Reference); // full reference preserved verbatim
    }

    [Fact]
    public async Task Register_WithNumberOutsideLotScope_StillRecords_NoHardBlock()
    {
        var (service, repo, ctx, pieces) = Build();
        var cmLot = SeedSingleContext(ctx, "B1");
        // A piece exists but in a DIFFERENT lot than the resolved context lot.
        pieces.Seed("REF-1", "9999", Guid.NewGuid(), FerramentasToolType.CM);

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "9999"));

        Assert.True(result.IsSuccess); // lot mismatch is information only, never a block.
        var record = Assert.Single(repo.Records);
        Assert.Equal("9999", record.IndividualNumber);
        // Effective lot for an unmatched number is not invented.
        Assert.NotEqual(cmLot, record.LotId);
    }

    [Fact]
    public async Task Register_SaveFailure_PreservesInputAndNoSuccess()
    {
        var (service, repo, ctx, pieces) = Build();
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        repo.FailInsert = true;

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsFailure);
        Assert.Empty(repo.Records);            // nothing persisted on failure
        Assert.DoesNotContain(repo.Records, r => r.IndividualNumber == "1234");
    }

    [Fact]
    public async Task Register_WithoutModule_FailsClosed()
    {
        var repo = new FakeReparacaoInternaRepository();
        var ctx = new FakeJobOnActiveContextLookup();
        var pieces = new FakeFerramentasPieceLookup();
        var service = new ReparacaoInternaService(
            repo, ctx, pieces, new FakeReparacaoInternaUowFactory(),
            new ReparacaoInternaAuthorizationGate(ReparacaoInternaCurrentUser.WithoutModule(),
                new ReparacaoInternaFakeAuthorship()),
            new ReparacaoInternaFixedClock(Now));

        var result = await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(repo.Records);
    }

    // ---- Line cards ------------------------------------------------------------

    [Fact]
    public async Task ListLineCards_ShowsActiveReferenceOrNone()
    {
        var (service, repo, ctx, _) = Build();
        var jobOnId = Guid.NewGuid();
        ctx.SeedSingle("B1", FakeJobOnActiveContextLookup.Context("B1", jobOnId, "REF-1", "202608"));
        // B2..C3 resolve to None by default.

        var result = await service.ListLineCardsAsync();

        Assert.True(result.IsSuccess);
        var cards = result.Value;
        Assert.Equal(6, cards.Count);
        var b1 = cards.Single(c => c.Line == "B1");
        Assert.True(b1.HasActiveContext);
        Assert.Equal("REF-1", b1.Reference);
        var b2 = cards.Single(c => c.Line == "B2");
        Assert.False(b2.HasActiveContext);
    }

    // ---- Correction -------------------------------------------------------------

    [Fact]
    public async Task Corrigir_WithCapability_PreservesOriginalAndCreatesNewRow()
    {
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        pieces.Seed("REF-1", "5678", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];

        var result = await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "B1", InternalRepairToolType.CM, "5678",
                null, null, "202608", "REF-1", null, "dígito errado"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, repo.Records.Count);
        var original = repo.Records.Single(r => r.InternalRepairRecordId == originalId);
        var correction = repo.Records.Single(r => r.IsCorrection);
        Assert.Equal("1234", original.IndividualNumber); // original untouched
        Assert.Equal("5678", correction.IndividualNumber);
        Assert.Equal(originalId, correction.CorrectionOfId);
        Assert.NotNull(correction.BeforeSnapshot);
        Assert.Equal("dígito errado", correction.CorrectionReason);
        Assert.Equal("repan-actor", correction.CreatedBy); // correction author
        Assert.Contains(repo.AuditEvents, a => a.action == "reparacao_interna.corrigir" && a.result == "corrected");
    }

    [Fact]
    public async Task Corrigir_WithoutCapability_IsForbidden()
    {
        var (service, repo, ctx, pieces) = Build(grantCorrigir: false);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];

        var result = await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "B1", InternalRepairToolType.CM, "5678",
                null, null, null, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("REPINT_CORRIGIR_FORBIDDEN", result.Error.Code);
        Assert.Single(repo.Records); // no correction row persisted
    }

    [Fact]
    public async Task Corrigir_OverridingContext_DoesNotBlockAndMovesToNewLineContext()
    {
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];
        // C2 has NO active context at the original occurred-at. R009: no block — the operator
        // can override the context and record reality (Job On untouched).
        ctx.SeedNone("C2");

        var result = await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "C2", InternalRepairToolType.CM, "5678",
                null, null, "999901", "REF-9", null, "produção mudou"));

        Assert.True(result.IsSuccess);
        var correction = repo.Records.Single(r => r.IsCorrection);
        Assert.Equal("C2", correction.Line);
        Assert.Equal("999901", correction.ProductionCode);
        Assert.Equal("REF-9", correction.Reference);
        Assert.Single(repo.Records, r => !r.IsCorrection); // original preserved
    }

    [Fact]
    public async Task Corrigir_LineChanged_AutoRecalibratesContextToNewActiveProduction()
    {
        // C1/C2 — the machine/line is editable AFTER save via correction.
        // C3 — when the correction MOVES to a line that HAS a Single active production, the
        // production context auto-recalibrates to that NEW line (assisted default), without any
        // explicit operator override and never touching Job On. Original stays a separate row.
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1"); // B1 → REF-1 / 202608
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];

        // C2 has its OWN active production (REF-2 / 202609) with a distinct revision/lot.
        Guid overrideJobOn = Guid.NewGuid();
        Guid overrideRevision = Guid.NewGuid();
        Guid overrideLot = Guid.NewGuid();
        ctx.SeedSingle("C2", FakeJobOnActiveContextLookup.Context(
            "C2", overrideJobOn, "REF-2", "202609", new[] { overrideLot }, new List<Guid>(),
            revisionId: overrideRevision));

        var result = await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "C2", InternalRepairToolType.CM, "5678",
                null, null, null, null, null, "mudou de linha"));

        Assert.True(result.IsSuccess);
        var correction = repo.Records.Single(r => r.IsCorrection);
        Assert.Equal("C2", correction.Line);
        // Context recalibrated to the NEW line's active production (not inherited from B1).
        Assert.Equal(overrideJobOn, correction.JobOnId);
        Assert.Equal(overrideRevision, correction.JobOnRevisionId);
        Assert.Equal("202609", correction.ProductionCode);
        Assert.Equal("REF-2", correction.Reference);
        // Original B1 record untouched (append-only).
        var original = repo.Records.Single(r => !r.IsCorrection);
        Assert.Equal("B1", original.Line);
        Assert.Equal("REF-1", original.Reference);
        Assert.Equal("202608", original.ProductionCode);
    }

    [Fact]
    public async Task Corrigir_LineChanged_ToNoProduction_PersistsCleanNullContext()
    {
        // C3 + R009 no-block: when the correction moves to a line with NO active production,
        // the correction persists a clean null context for the new line — it must NOT inherit
        // the original line's production context.
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];
        ctx.SeedNone("C2");

        var result = await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "C2", InternalRepairToolType.CM, "5678",
                null, null, null, null, null, "linha sem produção"));

        Assert.True(result.IsSuccess); // no-production NEVER blocks (R009).
        var correction = repo.Records.Single(r => r.IsCorrection);
        Assert.Equal("C2", correction.Line);
        Assert.Null(correction.JobOnId);
        Assert.Null(correction.JobOnRevisionId);
        Assert.Null(correction.ProductionCode);
        Assert.Null(correction.Reference);
        // The original keeps its B1 context untouched.
        var original = repo.Records.Single(r => !r.IsCorrection);
        Assert.Equal("REF-1", original.Reference);
    }

    // ---- History ---------------------------------------------------------------

    [Fact]
    public async Task ListHistory_ReturnsLatestValidPerChain_AndFiltersOnlyCorrected()
    {
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        pieces.Seed("REF-1", "5678", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];

        var correctionId = (await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "B1", InternalRepairToolType.CM, "5678",
                null, null, null, null, null, null))).Value;

        var all = (await service.ListHistoryAsync(new InternalRepairFilter(null, null, null, null, null, null, null, false), default)).Value;
        var corrected = (await service.ListHistoryAsync(new InternalRepairFilter(null, null, null, null, null, null, null, true), default)).Value;

        Assert.Single(all); // one chain root → one latest-valid row
        Assert.Equal(correctionId, all[0].RecordId);
        Assert.True(all[0].IsCorrected);
        Assert.Single(corrected);
        Assert.Equal(correctionId, corrected[0].RecordId);
    }

    [Fact]
    public async Task ListHistory_UsesPersistedContext_NotLiveReResolution()
    {
        // R009 GAP 2: the historical record carries its own production/reference snapshot, so
        // history does not depend on replaying the active-context lookup (which the fakes would
        // return empty for after the seed is removed).
        var (service, repo, ctx, pieces) = Build();
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        await service.RegistrarReparacoesAsync(Req("B1", InternalRepairToolType.CM, "1234"));

        // Remove the active context source — history must still show the persisted fact.
        ctx.ByLine.Remove("B1");

        var rows = (await service.ListHistoryAsync(new InternalRepairFilter(null, null, null, null, null, null, null, false), default)).Value;
        var row = Assert.Single(rows);
        Assert.Equal("202608", row.ProductionCode);
        Assert.Equal("REF-1", row.Reference);
    }

    [Fact]
    public async Task GetDetail_ReturnsChain()
    {
        var (service, repo, ctx, pieces) = Build(grantCorrigir: true);
        SeedSingleContext(ctx, "B1");
        pieces.Seed("REF-1", "1234", Guid.NewGuid(), FerramentasToolType.CM);
        pieces.Seed("REF-1", "5678", Guid.NewGuid(), FerramentasToolType.CM);
        var originalId = (await service.RegistrarReparacoesAsync(
            Req("B1", InternalRepairToolType.CM, "1234"))).Value[0];
        await service.CorrigirReparacaoAsync(
            new CorrigirReparacaoRequest(originalId, "B1", InternalRepairToolType.CM, "5678",
                null, null, null, null, null, null));

        var detail = (await service.GetDetailAsync(originalId)).Value;
        Assert.Equal(2, detail.CorrectionChain.Count);
    }
}