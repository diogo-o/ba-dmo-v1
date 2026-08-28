using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — shared Job On production-folder capability (IN SCOPE for U-11).
/// Pegamentos consumes the resolved Job On production folder and cannot choose
/// another; the production folder is stable production context; historical
/// document attribution stays with PegamentoControlo + exact job_on_revision_id.
/// </summary>
public class JobOnProductionFolderResolverTests
{
    [Fact]
    public async Task Resolver_ResolvesConfiguredFolder_OrNullWhenAbsent()
    {
        var resolver = new FakeJobOnProductionFolderResolver();
        var jobOnId = Guid.NewGuid();

        Assert.Null(await resolver.ResolveAsync(jobOnId));

        resolver.FolderByJobOn[jobOnId] = "5447T173";
        Assert.Equal("5447T173", await resolver.ResolveAsync(jobOnId));
    }

    [Fact]
    public async Task Confirm_UsesResolvedJobOnFolder_AndNotAnIndependentOne()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        var lookup = new FakeJobOnProductionContextLookup();
        lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId);

        var repository = new FakePegamentoRepository();
        var folderResolver = new FakeJobOnProductionFolderResolver { DefaultFolder = "5447T173" };
        var service = new PegamentoService(
            repository, new FakePegamentoUnitOfWorkFactory(), lookup,
            new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(System.DateTimeOffset.MinValue),
            new FakeSettings("D:\\Documentos"), folderResolver);

        var created = await service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var result = await service.ConfirmDocumentSavedAsync(created.Value);
        Assert.True(result.IsSuccess);
        var doc = Assert.Single(repository.Documents.Values);
        Assert.Equal("5447T173", doc.ProductionFolderSnapshot);
    }

    [Fact]
    public async Task Confirm_LaterRevisionDoesNotReinterpretExistingPdfAttribution()
    {
        var jobOnId = Guid.NewGuid();
        var oldRevision = Guid.NewGuid();
        var newRevision = Guid.NewGuid();
        var lookup = new FakeJobOnProductionContextLookup();
        lookup.ContextByRevision[oldRevision] = PegamentoContextBuilder.Complete(jobOnId, oldRevision, machine: "B1");
        lookup.ContextByRevision[newRevision] = PegamentoContextBuilder.Complete(jobOnId, newRevision, machine: "B3");

        var repository = new FakePegamentoRepository();
        var service = new PegamentoService(
            repository, new FakePegamentoUnitOfWorkFactory(), lookup,
            new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(System.DateTimeOffset.MinValue),
            new FakeSettings("D:\\Documentos"),
            new FakeJobOnProductionFolderResolver { DefaultFolder = "5447T173" });

        var created = await service.CreateControlAsync(new CreatePegamentoRequest(oldRevision, null, null));
        Assert.True(created.IsSuccess);
        var confirm = await service.ConfirmDocumentSavedAsync(created.Value);
        Assert.True(confirm.IsSuccess);

        // A later revision exists but the document still points to oldRevision's control.
        var doc = Assert.Single(repository.Documents.Values);
        var control = repository.Controls[doc.PegamentoControloId];
        Assert.Equal(oldRevision, control.JobOnRevisionId);
        Assert.Equal("B1", control.MachineCode);
    }
}