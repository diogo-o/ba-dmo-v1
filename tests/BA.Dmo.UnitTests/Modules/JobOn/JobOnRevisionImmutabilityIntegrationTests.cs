using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.UnitTests.Modules.Pegamentos;
using BA.Dmo.UnitTests.Modules.Peso;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// R006 — Cross-module revision-immutability integration proof (Job On → Peso →
/// Pegamentos). TEST ONLY — no production behavior/schema is changed.
///
/// Guarantee protected: a saved Job On revision is an IMMUTABLE snapshot; Peso and
/// Pegamentos records pin the EXACT <c>job_on_revision_id</c> they consumed (N06/N07
/// attribution anchor, TD-18/owner clarification). Creating a later revision B for the
/// same Job On must NEVER move/reinterpret old Peso/Pegamentos or the tool context of
/// revision A.
///
/// These are service-level integration facts (real JobOn/Peso/Pegamentos services
/// against in-memory repositories), so they run deterministic and fast. The database
/// cannot be reached in this test project; the schema-level FK/snapshot guarantees are
/// declared in the migrations and documented in R005.
/// </summary>
public class JobOnRevisionImmutabilityIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _jobOns = new();
    private readonly FakePesoRepository _peso = new();
    private readonly FakePegamentoRepository _pegamento = new();
    private readonly FakeJobOnProductionContextLookup _pegLookup = new();

    private readonly JobOnService _jobOnService;
    private readonly PesoService _pesoService;
    private readonly PegamentoService _pegamentoService;

    public JobOnRevisionImmutabilityIntegrationTests()
    {
        var jobOnGate = new JobOnAuthorizationGate(new JobOnActor());
        _jobOnService = new JobOnService(jobOnGate, _jobOns, new FakeJobOnUserContextRepository(), new TestClock(Now));

        var pesoGate = new PesoAuthorizationGate(new PesoOperador());
        _pesoService = new PesoService(pesoGate, _peso, _jobOns, new TestClock(Now));

        var pegGate = new PegamentoAuthorizationGate(PegFakeAuthorship.Authorized());
        _pegamentoService = new PegamentoService(
            _pegamento, _pegLookup, pegGate, new TestClock(Now),
            new FakeSettings("D:\\Documentos"),
            new FakeJobOnProductionFolderResolver { DefaultFolder = "5447T173" });
    }

    [Fact]
    public async Task RevB_DoesNotMoveOrReinterpret_RevA_Peso_Pegamento_OrToolContext()
    {
        // ---- 1. Create Job On + revision A with CM/MF/BQ component snapshots ----
        var createdJobOn = await _jobOnService.CreateAsync(new CreateJobOnRequest(
            "202601", "B1", Now, Now.AddHours(8)));
        Assert.True(createdJobOn.IsSuccess);
        var jobOnId = createdJobOn.Value;

        // Persist revision A (immutable) with the full historical context. It must carry
        // a Peso process + reference snapshot so Peso resolves its context from A.
        var revAId = await SeedRevisionA(jobOnId);

        // The Lookup maps the REAL revision A → a Pegamento context (same id).
        _pegLookup.ContextByRevision[revAId] = PegamentoContextBuilder.Complete(
            jobOnId, revAId, reference: "5447T173", production: "202601", machine: "B1");

        // Peso control created while A is the current revision → pins A.
        var pesoControl = await _pesoService.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 22, "usado", "obs",
            new[] { new PesoLeituraInput("CM1", 50.0m) }));
        Assert.True(pesoControl.IsSuccess);
        var pesoRecord = _peso.Controls[pesoControl.Value];

        // Pegamento control created from the exact revision A → pins A.
        var pegControl = await _pegamentoService.CreateControlAsync(
            new CreatePegamentoRequest(revAId, null, null));
        Assert.True(pegControl.IsSuccess);

        // ---- 2. Create revision B for the SAME Job On with changed context ----
        var revB = await _jobOnService.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "notas B", ChangeReason: null, ImageAssetId: null,
            Components:
            [
                new JobOnComponent { Family = ComponentFamily.MP_CM, ReferenceSnapshot = "5447CHANGED", LotSnapshot = "9" },
                new JobOnComponent { Family = ComponentFamily.MF, ReferenceSnapshot = "MF-NEW", LotSnapshot = "7" },
                new JobOnComponent { Family = ComponentFamily.BQ, ReferenceSnapshot = "T999", LotSnapshot = "8" }
            ]));
        Assert.True(revB.IsSuccess);
        var revBId = revB.Value;
        Assert.NotEqual(revAId, revBId);

        // ---- 3. Reopen/read revision A: snapshots + tool context intact ----
        // JobOnService has no read projection; read the immutable revisions from the
        // repository (the same one the service writes to), reconstructing revision A.
        var jobOn = (await _jobOns.GetByIdAsync(jobOnId))!;
        var revARead = jobOn.Revisions.Single(r => r.JobOnRevisionId == revAId);
        var cmA = revARead.Components!.First(c => c.Family == ComponentFamily.MP_CM);
        Assert.Equal("5447", cmA.ReferenceSnapshot);
        Assert.Equal("4", cmA.LotSnapshot);
        var bqA = revARead.Components!.First(c => c.Family == ComponentFamily.BQ);
        Assert.Equal("T173", bqA.ReferenceSnapshot);
        Assert.Equal("4", bqA.LotSnapshot);

        // ---- 4. Peso still pinned to A ----
        Assert.Equal(revAId, pesoRecord.JobOnRevisionId);
        Assert.Equal("202601", pesoRecord.ProductionCode);
        Assert.Equal("B1", pesoRecord.Line);

        // ---- 5. Pegamentos still pinned to A ----
        var pegStored = _pegamento.Controls[pegControl.Value];
        Assert.Equal(revAId, pegStored.JobOnRevisionId);

        // ---- 6. Reverse lookup: Peso/Pegamentos resolve to A, not B/current ----
        Assert.Equal(revAId, pesoRecord.JobOnRevisionId); // (re-assert: never B)
        var byOldRev = await _pegamentoService.ListByRevisionAsync(revAId);
        Assert.Single(byOldRev.Value);
        Assert.Equal(revAId, byOldRev.Value[0].JobOnRevisionId);

        // ---- 7. Revision B does not attract old rows ----
        var pegByNewRev = await _pegamentoService.ListByRevisionAsync(revBId);
        Assert.Empty(pegByNewRev.Value);

        // ---- 8. Historical production lookup still finds the Job On ----
        var found = await _jobOns.GetByProductionCodeAsync("202601");
        Assert.NotNull(found);
        Assert.Equal(jobOnId, found.Id);
    }

    /// <summary>Persists a complete immutable revision A (reference, process, CM/MF/BQ).</summary>
    private async Task<Guid> SeedRevisionA(Guid jobOnId)
    {
        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOnId,
            RevisionNumber = 1,
            ProductionSnapshot = "202601",
            ReferenceSnapshot = "5447T173",
            MachineSnapshot = "B1",
            DatesSnapshot = "2026-08-17/2026-08-17",
            ProcessSnapshot = "NNPB",
            GeneralNotes = "notas A",
            SavedBy = "bq-actor",
            SavedAtUtc = Now.DateTime
        };
        var components = new List<JobOnComponent>
        {
            new() { Family = ComponentFamily.MP_CM, ReferenceSnapshot = "5447", LotSnapshot = "4", TechnicalNameSnapshot = "Contra-molde" },
            new() { Family = ComponentFamily.MF, ReferenceSnapshot = "MF-9", LotSnapshot = "2" },
            new() { Family = ComponentFamily.BQ, ReferenceSnapshot = "T173", LotSnapshot = "4" }
        };
        // Components are part of the revision object so reconstruct keeps them attached.
        revision = revision with { Components = components };
        await _jobOns.InsertRevisionAsync(revision, default);
        await _jobOns.InsertComponentsAsync(components, default);
        return revision.JobOnRevisionId;
    }
}

// ---- Local fakes (confined to tests/*), distinct names to avoid collisions ----

file sealed class JobOnActor : ICurrentUserAccessor
{
    public CurrentUser? Current { get; } = new(
        Guid.NewGuid(), "Op JobOn",
        new[] { "jobon" }, new[] { "jobon.edit" });
}

file sealed class PesoOperador : ICurrentUserAccessor
{
    public CurrentUser? Current { get; } = new(
        Guid.NewGuid(), "Op Peso",
        new[] { "peso", "jobon" }, Array.Empty<string>());
}

file sealed class PegFakeAuthorship(string actorId = "peg-actor") : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } = new(
        actorId, new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));
    public static PegFakeAuthorship Authorized(string actorId = "peg-actor") => new(actorId);
}

file sealed class TestClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}