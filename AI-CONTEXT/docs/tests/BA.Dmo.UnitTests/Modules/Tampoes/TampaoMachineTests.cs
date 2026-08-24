using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Tampoes;

/// <summary>
/// R008 — Tampões multi-machine record/detail sheet (OWNER DECISION): canonical
/// list interaction (single-click select / double-click open), multi-machine
/// assignment (B1–C3, never duplicated per machine), persisted comments, machine
/// filter (ANY match returns the configuration once), actor/timestamp retained,
/// and no config duplication.
/// </summary>
public class TampaoMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTampaoRepository _repo = new();
    private readonly TampaoService _service;

    public TampaoMachineTests()
    {
        _service = new TampaoService(
            _repo, new FakeTampoesUnitOfWorkFactory(),
            new TampaoAuthorizationGate(TampaoCurrentUser.Authorized(), new TampaoFakeAuthorship()),
            new TampaoFixedClock(Now));
    }

    private TampaoConfiguration Seed() => _repo.SeedConfiguration("28,95", "4");

    [Fact]
    public async Task AssignMachineB1()
    {
        var cfg = Seed();

        var result = await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1" }));

        Assert.True(result.IsSuccess);
        var machines = await _repo.GetMachinesByConfigurationAsync(cfg.TampaoConfigurationId);
        Assert.Equal(new[] { "B1" }, machines.OrderBy(m => m, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AssignMultipleMachines_B1_B2_C1()
    {
        var cfg = Seed();

        var result = await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "B2", "C1" }));

        Assert.True(result.IsSuccess);
        var machines = await _repo.GetMachinesByConfigurationAsync(cfg.TampaoConfigurationId);
        Assert.Equal(3, machines.Count);
        Assert.Contains("B1", machines);
        Assert.Contains("B2", machines);
        Assert.Contains("C1", machines);
    }

    [Fact]
    public async Task RemoveMachineB2_KeepsOthers_AndAuditsRemoval()
    {
        var cfg = Seed();
        await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "B2", "C1" }));
        var eventsBefore = _repo.MachineEvents.Count;

        var result = await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "C1" }));

        Assert.True(result.IsSuccess);
        var machines = await _repo.GetMachinesByConfigurationAsync(cfg.TampaoConfigurationId);
        Assert.DoesNotContain("B2", machines);
        Assert.Contains("B1", machines);
        Assert.Contains("C1", machines);
        Assert.Contains(_repo.MachineEvents.Skip(eventsBefore), e => e.Machine == "B2" && e.Action == "removed");
    }

    [Fact]
    public async Task InvalidMachine_IsRejected()
    {
        var cfg = Seed();

        var result = await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "X9" }));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
    }

    [Fact]
    public async Task Comments_Persist_AndHistoryKept()
    {
        var cfg = Seed();

        var n1 = await _service.AddConfigurationNoteAsync(
            new AddConfigurationNoteRequest(cfg.TampaoConfigurationId, "primeira observação"));
        var n2 = await _service.AddConfigurationNoteAsync(
            new AddConfigurationNoteRequest(cfg.TampaoConfigurationId, "segunda observação"));

        Assert.True(n1.IsSuccess);
        Assert.True(n2.IsSuccess);
        var notes = await _repo.ListConfigurationNotesAsync(cfg.TampaoConfigurationId);
        Assert.Equal(2, notes.Count); // append-only; neither silently lost
        Assert.Equal("segunda observação", notes[^1].Note);
    }

    [Fact]
    public async Task MachineFilter_ReturnsMultiAssociatedRecord_Once()
    {
        var cfg = Seed();
        await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "B2", "C1" }));

        // Even when the config is associated with B1+B2+C1, filtering by B2 returns it ONCE.
        var list = await _service.ConsultarAsync(new ConsultaFilter(null, "B2"));

        Assert.True(list.IsSuccess);
        Assert.Single(list.Value);
        Assert.Equal(cfg.TampaoConfigurationId, list.Value[0].ConfigurationId);
    }

    [Fact]
    public async Task NoConfigurationDuplication_ForMultipleMachines()
    {
        var cfg = Seed();
        await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "B2", "C1" }));

        Assert.Single(_repo.Configurations); // one record, many machines
        Assert.Equal(3, (await _repo.GetMachinesByConfigurationAsync(cfg.TampaoConfigurationId)).Count);
    }

    [Fact]
    public async Task DetailSheet_ReturnsMachinesNotesAndEvents()
    {
        var cfg = Seed();
        await _service.SetConfigurationMachinesAsync(
            new SetConfigurationMachinesRequest(cfg.TampaoConfigurationId, new[] { "B1", "C3" }));
        await _service.AddConfigurationNoteAsync(
            new AddConfigurationNoteRequest(cfg.TampaoConfigurationId, "nota do ficha"));

        var detail = await _service.GetConfigurationDetailAsync(cfg.TampaoConfigurationId);

        Assert.True(detail.IsSuccess);
        Assert.Contains("B1", detail.Value.Configuration.Machines);
        Assert.Contains("C3", detail.Value.Configuration.Machines);
        Assert.Equal("nota do ficha", detail.Value.LatestComment);
        Assert.Single(detail.Value.Notes);
        Assert.Contains(detail.Value.MachineEvents, e => e.Action == "added");
    }

    [Fact]
    public async Task InvalidMachineFilter_IsRejected()
    {
        var result = await _service.ConsultarAsync(new ConsultaFilter(null, "X9"));

        Assert.True(result.IsFailure);
        Assert.Equal("TAMPAO_INVALID_MACHINE", result.Error.Code);
    }
}