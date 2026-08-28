using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — Pegamentos server-side PDF generation + canonical filename.
/// PDF generation produces bytes + human-readable filename and does NOT
/// persist pegamento_documentos (that only happens after browser confirmation).
/// The filename carries NO database identifier (owner decision 2026-08-17).
/// </summary>
public class PegamentoPdfTests
{
    private readonly FakePegamentoRepository _repository = new();
    private readonly FakeJobOnProductionContextLookup _lookup = new();
    private readonly PegamentoPdfService _pdfService;

    public PegamentoPdfTests()
    {
        _pdfService = new PegamentoPdfService(_repository, new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()));
    }

    [Fact]
    public async Task Generate_ReturnsPdfBytesAndHumanReadableFilename()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] =
            PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId, reference: "5447T173", production: "202601", machine: "B1");
        var controloId = await CreateControl(revisionId);

        var renderer = FakePegamentoPdfRenderer.NonEmpty();
        var result = await _pdfService.GenerateAsync(renderer, controloId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pegamentos_202601_5447T173_B1_relatorio.pdf", result.Value.FileName);
        Assert.NotEmpty(result.Value.PdfBytes);
    }

    [Fact]
    public async Task Generate_DoesNotPersistDocumentRow()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] =
            PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var controloId = await CreateControl(revisionId);

        var result = await _pdfService.GenerateAsync(FakePegamentoPdfRenderer.NonEmpty(), controloId);

        Assert.True(result.IsSuccess);
        Assert.False(_repository.Documents.ContainsKey(controloId));
    }

    [Fact]
    public async Task Generate_UnknownControl_IsNotFound()
    {
        var result = await _pdfService.GenerateAsync(FakePegamentoPdfRenderer.NonEmpty(), Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
    }

    [Fact]
    public async Task Generate_Unauthorized_IsForbidden()
    {
        var unauthorized = new PegamentoPdfService(_repository, new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Anonymous()));
        var result = await unauthorized.GenerateAsync(FakePegamentoPdfRenderer.NonEmpty(), Guid.NewGuid());
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    private async Task<Guid> CreateControl(Guid revisionId)
    {
        _lookup.ContextByRevision[revisionId] ??= PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var service = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup, new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized()),
            new FixedClock(System.DateTimeOffset.MinValue), new FakeSettings("D:\\Documentos"),
            new FakeJobOnProductionFolderResolver { DefaultFolder = "5447T173" });
        var created = await service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);
        return created.Value;
    }
}