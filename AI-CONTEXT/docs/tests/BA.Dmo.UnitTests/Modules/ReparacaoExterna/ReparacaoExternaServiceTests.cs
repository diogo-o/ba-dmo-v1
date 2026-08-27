using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa use-case behavior (GLM-RE-01..13; owner decisions A–G):
/// CM/MF external batches only (BQ deferred to U-19); repairer snapshot per send;
/// duplicate-in-open-exit hard block; add/remove only in preparation; atomic pickup/
/// return (repair + Armazém in ONE transaction); status transitions only via
/// confirmations; deactivate-not-delete repairers; server-derived actor attribution.
/// </summary>
public class ReparacaoExternaServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeRepairRepository _repository = new();
    private readonly FakeToolPieceResolver _resolver = new();
    private readonly FakeArmazemRepairMovementPort _armazem = new();
    private readonly FakeRepairUnitOfWorkFactory _uowFactory = new();
    private readonly ReparacaoExternaService _service;

    public ReparacaoExternaServiceTests()
    {
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.Authorized(), new ReparacaoExternaFakeAuthorship());
        _service = new ReparacaoExternaService(
            _repository, _resolver, _armazem, _uowFactory, gate, new ReparacaoExternaFixedClock(Now));
    }

    private Repairer SeedRepairer(string name = "Reparador A")
    {
        var repairer = new Repairer { Name = name, Active = true, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        _repository.Repairers.Add(repairer);
        return repairer;
    }

    private RepairToolIdentity SeedPiece(string reference = "CM-100", string lot = "1", string number = "101",
        RepairType type = RepairType.CM) => _resolver.Seed(reference, lot, number, type);

    private async Task<Guid> CreateListAsync(
        RepairType type = RepairType.CM, params RepairToolIdentity[] pieces)
    {
        var result = await _service.CreateExitAsync(new CreateExitRequest(
            type,
            null,
            new DateOnly(2026, 8, 25),
            pieces.Select(p => new NewExitItemRequest(p.PhysicalPieceId, p.Number)).ToList(),
            null));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    // ---- Authorization fail-closed -----------------------------------------

    [Fact]
    public async Task CreateExit_WithoutModule_IsForbidden()
    {
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.WithoutModule(), new ReparacaoExternaFakeAuthorship());
        var svc = new ReparacaoExternaService(
            new FakeRepairRepository(), _resolver, _armazem, _uowFactory, gate, new ReparacaoExternaFixedClock(Now));
        var result = await svc.CreateExitAsync(new CreateExitRequest(RepairType.CM, null, null, Array.Empty<NewExitItemRequest>(), null));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- Create list / BQ scope / repairer snapshot -------------------------

    [Fact]
    public async Task CreateExit_WithBQType_IsRejected()
    {
        var result = await _service.CreateExitAsync(new CreateExitRequest(RepairType.BQ, null, null, Array.Empty<NewExitItemRequest>(), null));
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_TYPE_SCOPE", result.Error.Code);
    }

    [Fact]
    public async Task CreateExit_CapturesRepairerSnapshot()
    {
        var repairer = SeedRepairer();
        var piece = SeedPiece();
        var result = await _service.CreateExitAsync(new CreateExitRequest(
            RepairType.CM, repairer.RepairerId, new DateOnly(2026, 8, 25),
            new[] { new NewExitItemRequest(piece.PhysicalPieceId, piece.Number) }, null));
        Assert.True(result.IsSuccess);
        var exit = _repository.Exits.Single(x => x.RepairExitId == result.Value);
        Assert.NotNull(exit.RepairerSnapshot);
        Assert.Equal(repairer.RepairerId, exit.RepairerSnapshot!.RepairerId);
        Assert.Equal(repairer.Name, exit.RepairerSnapshot.Name);
    }

    [Fact]
    public async Task CreateExit_UsesOneUnitOfWork_ForExitItemsAndAudits_ThenCommits()
    {
        var first = SeedPiece("CM-100", "1", "101");
        var second = SeedPiece("CM-100", "1", "102");

        var result = await _service.CreateExitAsync(new CreateExitRequest(
            RepairType.CM, null, new DateOnly(2026, 8, 25),
            [
                new NewExitItemRequest(first.PhysicalPieceId, first.Number),
                new NewExitItemRequest(second.PhysicalPieceId, second.Number)
            ], null));

        Assert.True(result.IsSuccess);
        var uow = Assert.IsType<FakeUnitOfWork>(_uowFactory.Last);
        Assert.True(uow.Committed);
        Assert.True(uow.Disposed);
        Assert.Equal(6, _repository.CreateExitWrites.Count);
        Assert.All(_repository.CreateExitWrites, write => Assert.Same(uow, write.Uow));
        Assert.Equal(1, _repository.CreateExitWrites.Count(write => write.Kind == "exit"));
        Assert.Equal(2, _repository.CreateExitWrites.Count(write => write.Kind == "item"));
        Assert.Equal(3, _repository.CreateExitWrites.Count(write => write.Kind == "audit"));
        Assert.Equal(3, _repository.AuditEvents.Count);
    }

    [Fact]
    public async Task CreateExit_ItemAlreadyInOpenExit_IsHardBlocked()
    {
        var piece = SeedPiece();
        await CreateListAsync(RepairType.CM, piece);
        // Adding the SAME piece into ANOTHER open list must be blocked (hard rule).
        var result = await _service.CreateExitAsync(new CreateExitRequest(
            RepairType.CM, null, null,
            new[] { new NewExitItemRequest(piece.PhysicalPieceId, piece.Number) }, null));
        Assert.True(result.IsFailure);
        Assert.Equal(RepairExitRules.DuplicateInOpenExitCode, result.Error.Code);
    }

    // ---- Add/remove while preparing ----------------------------------------

    [Fact]
    public async Task RemoveItem_AfterDisposicionado_IsRejected()
    {
        var piece = SeedPiece();
        var exitId = await CreateListAsync(RepairType.CM, piece);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        var item = _repository.Items.Single(i => i.RepairExitId == exitId);
        var result = await _service.RemoveItemAsync(new RemoveExitItemRequest(exitId, item.RepairExitItemId));
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_LIST_NOT_EDITABLE", result.Error.Code);
    }

    // ---- Disponibilizar / pickup / return -----------------------------------

    [Fact]
    public async Task Pickup_ConfirmsOutAndReleasesArmazemAtomically()
    {
        var piece = SeedPiece();
        var exitId = await CreateListAsync(RepairType.CM, piece);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        var item = _repository.Items.Single(i => i.RepairExitId == exitId);

        var result = await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));
        Assert.True(result.IsSuccess);

        var stored = _repository.Items.Single(i => i.RepairExitItemId == item.RepairExitItemId);
        Assert.True(stored.IsPickedOut);
        var exit = _repository.Exits.Single(e => e.RepairExitId == exitId);
        Assert.Equal(RepairExitStatus.Enviado, exit.Status);

        // Both the repair write and the Armazém write participated in ONE coordinated
        // flow (owner decision C: same transaction).
        Assert.Contains(_armazem.Pickups, p => p.repairExitId == exitId && p.toolLoteId == piece.ToolLoteId);
        Assert.Contains(_repository.CoordinatedWrites, w => w.kind == "pickup" && w.exitItemId == item.RepairExitItemId);
    }

    [Fact]
    public async Task Pickup_ArmazemFailure_DoesNotCommitRepair()
    {
        var piece = SeedPiece();
        var exitId = await CreateListAsync(RepairType.CM, piece);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        var item = _repository.Items.Single(i => i.RepairExitId == exitId);
        _armazem.FailOnPickup = true;

        var result = await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));
        Assert.True(result.IsFailure);
        // The repair item must NOT have been marked picked (the transaction rolled back).
        Assert.False(_repository.Items.Single(i => i.RepairExitItemId == item.RepairExitItemId).IsPickedOut);
    }

    [Fact]
    public async Task Pickup_ToolNotInWarehouse_IsRejected()
    {
        var piece = SeedPiece();
        var exitId = await CreateListAsync(RepairType.CM, piece);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        var item = _repository.Items.Single(i => i.RepairExitId == exitId);
        _armazem.FailOnPickup = true;

        var result = await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));
        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_REPAIR_NOT_IN_WAREHOUSE", result.Error.Code);
    }

    [Fact]
    public async Task Return_Partial_TransitionsToRetornoParcial()
    {
        var a = SeedPiece("CM-100", "1", "101");
        var b = SeedPiece("CM-100", "1", "102");
        var exitId = await CreateListAsync(RepairType.CM, a, b);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        foreach (var item in _repository.Items.Where(i => i.RepairExitId == exitId).ToList())
            await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));

        var first = _repository.Items.First(i => i.RepairExitId == exitId && !i.IsReturned);
        var result = await _service.ConfirmReturnAsync(new ConfirmReturnRequest(first.RepairExitItemId, "2421"));
        Assert.True(result.IsSuccess);

        var exit = _repository.Exits.Single(e => e.RepairExitId == exitId);
        Assert.Equal(RepairExitStatus.RetornoParcial, exit.Status);
        Assert.Single(_armazem.Returns.Where(r => r.repairExitId == exitId)); // physical return recorded
    }

    [Fact]
    public async Task Return_All_TransitionsToConcluido()
    {
        var a = SeedPiece("CM-100", "1", "101");
        var b = SeedPiece("CM-100", "1", "102");
        var exitId = await CreateListAsync(RepairType.CM, a, b);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        foreach (var item in _repository.Items.Where(i => i.RepairExitId == exitId).ToList())
            await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));

        foreach (var item in _repository.Items.Where(i => i.RepairExitId == exitId).ToList())
        {
            var r = await _service.ConfirmReturnAsync(new ConfirmReturnRequest(item.RepairExitItemId, "2421"));
            Assert.True(r.IsSuccess);
        }

        var exit = _repository.Exits.Single(e => e.RepairExitId == exitId);
        Assert.Equal(RepairExitStatus.Concluido, exit.Status);
    }

    [Fact]
    public async Task Return_InvalidPosition_IsRejected()
    {
        var piece = SeedPiece();
        var exitId = await CreateListAsync(RepairType.CM, piece);
        await _service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
        var item = _repository.Items.Single(i => i.RepairExitId == exitId);
        await _service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));

        var result = await _service.ConfirmReturnAsync(new ConfirmReturnRequest(item.RepairExitItemId, "24A"));
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_POSITION_CODE", result.Error.Code);
    }

    // ---- Repairer management -------------------------------------------------

    [Fact]
    public async Task DeactivateRepairer_SetsInactiveNotDeleted()
    {
        var repairer = SeedRepairer();
        var result = await _service.DeactivateRepairerAsync(new DeactivateRepairerRequest(repairer.RepairerId));
        Assert.True(result.IsSuccess);
        var stored = _repository.Repairers.Single(r => r.RepairerId == repairer.RepairerId);
        Assert.False(stored.Active);
        Assert.Single(_repository.Repairers); // still present, not deleted
        Assert.Contains(_repository.AuditEvents, a => a.eventType == "reparacao_externa.reparador.desativar");
    }

    [Fact]
    public async Task UpsertLineDefault_WithInactiveRepairer_IsRejected()
    {
        var repairer = SeedRepairer();
        repairer.Active = false;
        var result = await _service.UpsertLineDefaultAsync(new UpsertLineDefaultRequest("B1", "CM", repairer.RepairerId));
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_REPAIRER_INACTIVE", result.Error.Code);
    }
}
