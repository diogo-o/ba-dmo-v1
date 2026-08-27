using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.UnitTests.Modules.ReparacaoExterna;

namespace BA.Dmo.UnitTests.Modules.Armazem;

public sealed class ArmazemRepairReturnServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReturnToFreeLocation_PreservesMovementRepairEventAndAuditBehavior()
    {
        var fixture = CreateFixture();
        var item = await PreparePickedItemAsync(fixture);

        var result = await fixture.Service.ConfirmReturnAsync(
            new ConfirmReturnRequest(item.RepairExitItemId, "2421"));

        Assert.True(result.IsSuccess);
        Assert.Single(fixture.Armazem.Returns);
        Assert.Contains(fixture.Repository.CoordinatedWrites,
            write => write.kind == "return" && write.exitItemId == item.RepairExitItemId);
        Assert.Contains(fixture.Repository.CoordinatedWrites,
            write => write.kind == "event" && write.exitItemId == item.RepairExitItemId);
        Assert.Contains(fixture.Repository.AuditEvents,
            audit => audit.eventType == "reparacao_externa.item.retornado");
        var uow = Assert.IsType<FakeUnitOfWork>(fixture.UnitOfWorkFactory.Last);
        Assert.True(uow.Committed);
        Assert.True(uow.Disposed);
    }

    [Fact]
    public async Task ReturnToOccupiedLocation_LeavesRepairStateUnchangedAndDoesNotCommit()
    {
        var fixture = CreateFixture();
        var item = await PreparePickedItemAsync(fixture);
        fixture.Armazem.FailOnReturn = true;
        var writesBeforeReturn = fixture.Repository.CoordinatedWrites.Count;
        var auditsBeforeReturn = fixture.Repository.AuditEvents.Count;

        var result = await fixture.Service.ConfirmReturnAsync(
            new ConfirmReturnRequest(item.RepairExitItemId, "2421"));

        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_REPAIR_POSITION_OCCUPIED", result.Error.Code);
        Assert.False(fixture.Repository.Items.Single(stored =>
            stored.RepairExitItemId == item.RepairExitItemId).IsReturned);
        Assert.Empty(fixture.Armazem.Returns);
        Assert.Equal(writesBeforeReturn, fixture.Repository.CoordinatedWrites.Count);
        Assert.Equal(auditsBeforeReturn, fixture.Repository.AuditEvents.Count);
        var uow = Assert.IsType<FakeUnitOfWork>(fixture.UnitOfWorkFactory.Last);
        Assert.False(uow.Committed);
        Assert.True(uow.Disposed);
    }

    private static async Task<RepairExitItem> PreparePickedItemAsync(Fixture fixture)
    {
        var piece = fixture.Resolver.Seed("CM-ARMAZEM-01", "LOT-01", "101");
        var created = await fixture.Service.CreateExitAsync(new CreateExitRequest(
            RepairType.CM, null, new DateOnly(2026, 8, 27),
            [new NewExitItemRequest(piece.PhysicalPieceId, piece.Number)], null));
        Assert.True(created.IsSuccess);
        var available = await fixture.Service.DisponibilizarExitAsync(
            new DisponibilizarExitRequest(created.Value));
        Assert.True(available.IsSuccess);
        var item = fixture.Repository.Items.Single(stored =>
            stored.RepairExitId == created.Value);
        var picked = await fixture.Service.ConfirmPickupAsync(
            new ConfirmPickupRequest(item.RepairExitItemId));
        Assert.True(picked.IsSuccess);
        return item;
    }

    private static Fixture CreateFixture()
    {
        var repository = new FakeRepairRepository();
        var resolver = new FakeToolPieceResolver();
        var armazem = new FakeArmazemRepairMovementPort();
        var unitOfWorkFactory = new FakeRepairUnitOfWorkFactory();
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.Authorized(), new ReparacaoExternaFakeAuthorship());
        var service = new ReparacaoExternaService(
            repository, resolver, armazem, unitOfWorkFactory, gate,
            new ReparacaoExternaFixedClock(Now));
        return new Fixture(repository, resolver, armazem, unitOfWorkFactory, service);
    }

    private sealed record Fixture(
        FakeRepairRepository Repository,
        FakeToolPieceResolver Resolver,
        FakeArmazemRepairMovementPort Armazem,
        FakeRepairUnitOfWorkFactory UnitOfWorkFactory,
        ReparacaoExternaService Service);
}
