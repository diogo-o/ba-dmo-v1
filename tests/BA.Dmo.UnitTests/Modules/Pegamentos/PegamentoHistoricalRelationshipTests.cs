using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — the five owner-required historical relationship proofs.
/// Pegamentos attach to the EXACT Job On revision; CM/BQ/MF resolve from that
/// revision; a later revision does not move/reinterpret old Pegamentos; two
/// revisions of the same production carry their own historically correct rows.
/// </summary>
public class PegamentoHistoricalRelationshipTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePegamentoRepository _repository = new();
    private readonly FakeJobOnProductionContextLookup _lookup = new();
    private readonly PegamentoService _service;

    public PegamentoHistoricalRelationshipTests()
    {
        var gate = new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized());
        _service = new PegamentoService(
            _repository, _lookup, gate, new FixedClock(Now),
            new FakeSettings("D:\\Documentos"),
            new FakeJobOnProductionFolderResolver { DefaultFolder = "5447T173" });
    }

    [Fact]
    public async Task Proof1_Creating_Persists_TheExactRevisionId()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId);

        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));

        Assert.True(created.IsSuccess);
        var stored = _repository.Controls[created.Value];
        Assert.Equal(revisionId, stored.JobOnRevisionId);
    }

    [Fact]
    public async Task Proof2_History_ResolvesOriginalCmBqMfFromThatRevision()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        var context = PegamentoContextBuilder.Complete(jobOnId, revisionId);
        _lookup.ContextByRevision[revisionId] = context;

        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var control = _repository.Controls[created.Value];
        Assert.Equal(PegamentoComponentKey.CM, control.CmSnapshot!.Key);
        Assert.Equal("5447", control.CmSnapshot.ReferenceSnapshot);
        Assert.Equal(PegamentoComponentKey.BQ, control.BqSnapshot!.Key);
        Assert.Equal("T173", control.BqSnapshot.ReferenceSnapshot);
        Assert.Equal(PegamentoComponentKey.MF, control.MfSnapshot!.Key);
        Assert.Equal(context.MachineCode, control.MachineCode);
        Assert.Equal(context.Reference, control.ReferenceSnapshot);
    }

    [Fact]
    public async Task Proof3_QueryingRevision_ReturnsItsPegamentos()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId);

        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var byRevision = await _service.ListByRevisionAsync(revisionId);

        Assert.True(byRevision.IsSuccess);
        var item = Assert.Single(byRevision.Value);
        Assert.Equal(revisionId, item.JobOnRevisionId);
    }

    [Fact]
    public async Task Proof4_LaterRevision_DoesNotMoveOldPegamentos()
    {
        var oldRevision = Guid.NewGuid();
        var newRevision = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[oldRevision] = PegamentoContextBuilder.Complete(jobOnId, oldRevision, machine: "B1");
        _lookup.ContextByRevision[newRevision] = PegamentoContextBuilder.Complete(jobOnId, newRevision, machine: "B3");

        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(oldRevision, null, null));
        Assert.True(created.IsSuccess);

        // A later revision exists; the old Pegamento must still be attributed to oldRevision only.
        var oldList = await _service.ListByRevisionAsync(oldRevision);
        var newList = await _service.ListByRevisionAsync(newRevision);

        Assert.Single(oldList.Value);
        Assert.Empty(newList.Value);
        var kept = _repository.Controls[created.Value];
        Assert.Equal(oldRevision, kept.JobOnRevisionId);
        Assert.Equal("B1", kept.MachineCode);
    }

    [Fact]
    public async Task Proof5_TwoRevisionsOfSameProduction_EachHaveOwnHistoricallyCorrectRows()
    {
        var jobOnId = Guid.NewGuid();
        var revision1 = Guid.NewGuid();
        var revision2 = Guid.NewGuid();
        _lookup.ContextByRevision[revision1] = PegamentoContextBuilder.Complete(jobOnId, revision1, machine: "B1");
        _lookup.ContextByRevision[revision2] = PegamentoContextBuilder.Complete(jobOnId, revision2, machine: "B3");

        var c1 = await _service.CreateControlAsync(new CreatePegamentoRequest(revision1, null, null));
        var c2 = await _service.CreateControlAsync(new CreatePegamentoRequest(revision2, null, null));
        Assert.True(c1.IsSuccess);
        Assert.True(c2.IsSuccess);

        var r1List = await _service.ListByRevisionAsync(revision1);
        var r2List = await _service.ListByRevisionAsync(revision2);
        var jobonList = await _service.ListByJobOnAsync(jobOnId);

        Assert.Single(r1List.Value);
        Assert.Single(r2List.Value);
        Assert.Equal(2, jobonList.Value.Count);

        var first = Assert.Single(r1List.Value);
        var second = Assert.Single(r2List.Value);
        Assert.NotEqual(first.ControloId, second.ControloId);
        Assert.Equal("B1", first.MachineCode);
        Assert.Equal("B3", second.MachineCode);
    }
}