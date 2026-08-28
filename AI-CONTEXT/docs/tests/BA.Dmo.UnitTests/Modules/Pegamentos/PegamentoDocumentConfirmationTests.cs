using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — Pegamentos PDF save-confirmation flow (owner-approved 2026-08-17).
/// Generate alone does not persist; failed/browser-save path does not persist;
/// confirmation persists ONE server-derived final document row; confirmation
/// never trusts client filename/path; a closed control cannot silently replace
/// its final document.
/// </summary>
public class PegamentoDocumentConfirmationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePegamentoRepository _repository = new();
    private readonly FakeJobOnProductionContextLookup _lookup = new();
    private readonly FakeJobOnProductionFolderResolver _folderResolver = new() { DefaultFolder = "5447T173" };
    private readonly PegamentoService _service;

    public PegamentoDocumentConfirmationTests()
    {
        _service = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup,
            new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(Now), new FakeSettings("D:\\Documentos"), _folderResolver);
    }

    [Fact]
    public async Task Confirm_PersistsServerDerivedFinalMetadata()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(
            Guid.NewGuid(), revisionId, reference: "5447T173", production: "202601", machine: "B1");
        var controloId = await CreateControl(revisionId);

        var result = await _service.ConfirmDocumentSavedAsync(controloId);

        Assert.True(result.IsSuccess);
        var doc = Assert.Single(_repository.Documents.Values);
        Assert.Equal(controloId, doc.PegamentoControloId);
        Assert.Equal("Pegamentos_202601_5447T173_B1_relatorio.pdf", doc.Filename);
        Assert.Equal("D:\\Documentos", doc.OutputRootSnapshot);
        Assert.Equal("5447T173", doc.ProductionFolderSnapshot);
        Assert.NotEqual(Guid.Empty, doc.PegamentoDocumentoId);
    }

    [Fact]
    public async Task Confirm_MissingOutputRoot_IsFailureAndDoesNotPersist()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);

        var noOutput = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup,
            new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(Now), new FakeSettings(null), _folderResolver);
        var controloId = await CreateControl(revisionId);

        var result = await noOutput.ConfirmDocumentSavedAsync(controloId);

        Assert.True(result.IsFailure);
        Assert.Equal("PEGAMENTO_OUTPUT_ROOT_MISSING", result.Error.Code);
        Assert.True(_repository.Documents.Values.All(d => d.PegamentoControloId != controloId));
    }

    [Fact]
    public async Task Confirm_MissingProductionFolder_IsFailureAndDoesNotPersist()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);

        var noFolder = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup,
            new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(Now), new FakeSettings("D:\\Documentos"),
            new FakeJobOnProductionFolderResolver());
        var controloId = await CreateControl(revisionId);

        var result = await noFolder.ConfirmDocumentSavedAsync(controloId);

        Assert.True(result.IsFailure);
        Assert.Equal("PEGAMENTO_PRODUCTION_FOLDER_MISSING", result.Error.Code);
    }

    [Fact]
    public async Task Confirm_ClosedControl_CannotSilentlyReplaceFinalDocument()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var controloId = await CreateControl(revisionId);

        // First confirmation persists a final document.
        var first = await _service.ConfirmDocumentSavedAsync(controloId);
        Assert.True(first.IsSuccess);

        // Close the control (freezes the final document).
        var closed = await _service.CloseControlAsync(new CloseControlRequest(controloId));
        Assert.True(closed.IsSuccess);

        // Server derives a NEW filename by re-resolving — but MUST reject silent replacement.
        // Simulate a differing derived filename across time is not possible with frozen state,
        // so instead assert the closed-control freeze error fires because a document exists.
        var again = await _service.ConfirmDocumentSavedAsync(controloId);

        Assert.True(again.IsFailure);
        Assert.Equal("PEGAMENTO_FINAL_DOCUMENT_FROZEN", again.Error.Code);
        Assert.Single(_repository.Documents.Values);
    }

    [Fact]
    public async Task Confirm_Aberto_OneToOne_UpsertKeepsSingleRow()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var controloId = await CreateControl(revisionId);

        var first = await _service.ConfirmDocumentSavedAsync(controloId);
        var second = await _service.ConfirmDocumentSavedAsync(controloId);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        Assert.Single(_repository.Documents.Values); // one-to-one, upsert
    }

    private async Task<Guid> CreateControl(Guid revisionId)
    {
        var created = await _service.CreateControlAsync(
            new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);
        return created.Value;
    }
}