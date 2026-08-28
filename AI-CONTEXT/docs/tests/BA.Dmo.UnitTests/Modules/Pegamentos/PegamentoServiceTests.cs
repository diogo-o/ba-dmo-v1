using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — Pegamentos use-case behavior (DS-05, GLM-PEG-06/08).
/// Creation/opening with complete Job On context, blocking on incomplete
/// context, reverse navigation Pegamento→production, list-by-revision,
/// revision immutability, and authorization gate fail-closed.
/// </summary>
public class PegamentoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePegamentoRepository _repository = new();
    private readonly FakeJobOnProductionContextLookup _lookup = new();
    private readonly FakeJobOnProductionFolderResolver _folderResolver = new() { DefaultFolder = "5447T173" };
    private readonly PegamentoAuthorizationGate _gate;
    private readonly PegamentoService _service;

    public PegamentoServiceTests()
    {
        _gate = new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Authorized());
        _service = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup, _gate, new FixedClock(Now),
            new FakeSettings("D:\\Documentos"), _folderResolver);
    }

    // ---- Creation with complete context ----

    [Fact]
    public async Task Create_WithCompleteContext_SucceedsAndDerivesJobOnId()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId);

        var result = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, "exemplo"));

        Assert.True(result.IsSuccess);
        var control = _repository.Controls[result.Value];
        Assert.Equal(jobOnId, control.JobOnId);
        Assert.Equal(revisionId, control.JobOnRevisionId);
        Assert.Equal(PegamentoControloStatus.Aberto, control.Status);
    }

    // ---- Blocked incomplete context ----

    [Fact]
    public async Task Create_WithMissingComponents_IsBlocked()
    {
        var revisionId = Guid.NewGuid();
        // Context returns null from the lookup => revision missing/incomplete.
        _lookup.ContextByRevision[revisionId] = null;

        var result = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
    }

    // ---- Reverse navigation Pegamento → production ----

    [Fact]
    public async Task GetControlDetail_ResolvesHistoricalProductionContext()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId, reference: "5447T173", machine: "B1");
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var detail = await _service.GetControlDetailAsync(created.Value);

        Assert.True(detail.IsSuccess);
        Assert.Equal("5447T173", detail.Value.Reference);
        Assert.Equal("B1", detail.Value.MachineCode);
        Assert.Equal("5447", detail.Value.CmReference);
        Assert.Equal("T173", detail.Value.BqReference);
        Assert.Equal("MF-1", detail.Value.MfReference);
    }

    // ---- List by revision ----

    [Fact]
    public async Task ListByRevision_ReturnsOnlyThatRevisionsRecords()
    {
        var jobOnId = Guid.NewGuid();
        var revision = Guid.NewGuid();
        _lookup.ContextByRevision[revision] = PegamentoContextBuilder.Complete(jobOnId, revision);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revision, null, null));
        Assert.True(created.IsSuccess);

        var list = await _service.ListByRevisionAsync(revision);

        Assert.True(list.IsSuccess);
        var item = Assert.Single(list.Value);
        Assert.Equal(revision, item.JobOnRevisionId);
    }

    // ---- Update never rewrites the revision anchor ----

    [Fact]
    public async Task Update_DoesNotRewriteRevisionAnchor()
    {
        var revisionId = Guid.NewGuid();
        var jobOnId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(jobOnId, revisionId);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, 0.25m, null));
        Assert.True(created.IsSuccess);

        var update = await _service.UpdateControlAsync(new UpdatePegamentoRequest(created.Value, 0.30m, "nova nota"));
        Assert.True(update.IsSuccess);

        var control = _repository.Controls[created.Value];
        Assert.Equal(revisionId, control.JobOnRevisionId);
        Assert.Equal(0.30m, control.Tolerance);
        Assert.Equal("nova nota", control.Notas);
    }

    // ---- Authorization gate ----

    [Fact]
    public async Task Create_WithoutAuthorizedIdentity_IsForbidden()
    {
        // Unauthorized gate resolves null actor => fails closed.
        var unauthorizedService = new PegamentoService(
            _repository, new FakePegamentoUnitOfWorkFactory(), _lookup, new PegamentoAuthorizationGate(FakeAuthorshipAccessor.Anonymous()),
            new FixedClock(Now), new FakeSettings(null), _folderResolver);

        var result = await unauthorizedService.CreateControlAsync(
            new CreatePegamentoRequest(Guid.NewGuid(), null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- Measurement entry computes server-side ----

    [Fact]
    public async Task AddMeasurement_ComputesOvalizacaoAndMediaServerSide()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var added = await _service.AddMeasurementAsync(
            new AddMeasurementRequest(created.Value, PegamentoComponentKey.CM, 42, 52.30m, 52.00m));

        Assert.True(added.IsSuccess);
        var measurement = _repository.Measurements[created.Value].Single();
        Assert.Equal(0.30m, measurement.Ovalizacao);
        Assert.Equal(52.15m, measurement.Media);
    }

    // ---- N39: one-sided CM measurement (contra_costura absent) -----------
    // The absence of contra costura must NEVER block the measurement (no
    // service/validation blocker); the calculation falls back to
    // ovalização absent + média = single value.

    [Fact]
    public async Task AddMeasurement_OneSidedCm_WithoutContraCostura_IsNonBlocking()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var added = await _service.AddMeasurementAsync(
            new AddMeasurementRequest(created.Value, PegamentoComponentKey.CM, 7, 52.30m, null));

        Assert.True(added.IsSuccess, "a one-sided measurement must never be blocked (N39)");
        var measurement = _repository.Measurements[created.Value].Single();
        Assert.Null(measurement.ContraCostura);
        Assert.Null(measurement.Ovalizacao);      // fallback: ovalização absent
        Assert.Equal(52.30m, measurement.Media);  // fallback: média = single value
    }

    // ---- PG-04: closed-control rule inside the atomic measurement flow ----

    [Fact]
    public async Task AddMeasurement_OnClosedControl_IsBlockedAndPersistsNothing()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var closed = await _service.CloseControlAsync(new CloseControlRequest(created.Value));
        Assert.True(closed.IsSuccess);

        var added = await _service.AddMeasurementAsync(
            new AddMeasurementRequest(created.Value, PegamentoComponentKey.CM, 42, 52.30m, 52.00m));

        Assert.True(added.IsFailure);
        Assert.Equal("PEGAMENTO_CONTROL_CLOSED", added.Error.Code);
        Assert.False(_repository.Measurements.ContainsKey(created.Value));
    }

    [Fact]
    public async Task UpdateControl_OnClosedControl_IsBlocked()
    {
        var revisionId = Guid.NewGuid();
        _lookup.ContextByRevision[revisionId] = PegamentoContextBuilder.Complete(Guid.NewGuid(), revisionId);
        var created = await _service.CreateControlAsync(new CreatePegamentoRequest(revisionId, null, null));
        Assert.True(created.IsSuccess);

        var closed = await _service.CloseControlAsync(new CloseControlRequest(created.Value));
        Assert.True(closed.IsSuccess);

        var updated = await _service.UpdateControlAsync(
            new UpdatePegamentoRequest(created.Value, 0.30m, "nova nota"));

        Assert.True(updated.IsFailure);
        Assert.Equal("PEGAMENTO_CONTROL_CLOSED", updated.Error.Code);
        Assert.Equal(0.20m, _repository.Controls[created.Value].Tolerance); // untouched
    }
}