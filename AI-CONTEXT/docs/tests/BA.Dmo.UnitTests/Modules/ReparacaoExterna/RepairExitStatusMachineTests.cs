using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Pure status machine behavior (GLM-RE-04, GLM-RE-09): transitions
/// happen only via persisted confirmations; partial return → Retorno parcial;
/// all items returned → Concluído; closed cycle cannot accept pickups.
/// </summary>
public class RepairExitStatusMachineTests
{
    private static RepairExitItem Item(DateTimeOffset? inAtUtc = null, bool picked = false)
        => new()
        {
            RepairExitItemId = Guid.NewGuid(),
            RepairExitId = Guid.NewGuid(),
            PhysicalPieceId = Guid.NewGuid(),
            IndividualNumber = "1",
            Picked = picked,
            InAtUtc = inAtUtc
        };

    [Fact]
    public void Pickup_OnClosedCycle_IsRejected()
    {
        var items = new[] { Item(picked: true) };
        var result = RepairExitStatusMachine.ConfirmPickup(RepairExitStatus.Concluido, items, items[0]);
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_CYCLE_CLOSED", result.Error.Code);
    }

    [Fact]
    public void Pickup_FirstConfirmation_TransitionsToARetirar()
    {
        var items = new[] { Item(), Item() }; // none picked yet
        var result = RepairExitStatusMachine.ConfirmPickup(RepairExitStatus.Preparacao, items, items[0]);
        Assert.True(result.IsSuccess);
        Assert.Equal(RepairExitStatus.ARetirar, result.Value);
    }

    [Fact]
    public void Pickup_AllItemsPicked_TransitionsToEnviado()
    {
        var items = new[] { Item(picked: true), Item() };
        var result = RepairExitStatusMachine.ConfirmPickup(RepairExitStatus.ARetirar, items, items[1]);
        Assert.True(result.IsSuccess);
        Assert.Equal(RepairExitStatus.Enviado, result.Value);
    }

    [Fact]
    public void Pickup_AfterReturnStarted_IsRejected()
    {
        var returned = Item(inAtUtc: new DateTimeOffset(2026, 8, 18, 14, 0, 0, TimeSpan.Zero));
        var pending = Item();
        var items = new[] { returned, pending };
        var result = RepairExitStatusMachine.ConfirmPickup(RepairExitStatus.RetornoParcial, items, pending);
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_CYCLE_PARTIAL", result.Error.Code);
    }

    [Fact]
    public void Return_Partial_TransitionsToRetornoParcial()
    {
        var items = new[] { Item(inAtUtc: new DateTimeOffset(2026, 8, 18, 14, 0, 0, TimeSpan.Zero)), Item() };
        var result = RepairExitStatusMachine.ConfirmReturn(RepairExitStatus.RetornoParcial, items);
        Assert.True(result.IsSuccess);
        Assert.Equal(RepairExitStatus.RetornoParcial, result.Value);
    }

    [Fact]
    public void Return_AllItems_TransitionsToConcluido()
    {
        var items = new[] { Item(inAtUtc: new DateTimeOffset(2026, 8, 18, 14, 0, 0, TimeSpan.Zero)), Item(inAtUtc: new DateTimeOffset(2026, 8, 18, 15, 0, 0, TimeSpan.Zero)) };
        var result = RepairExitStatusMachine.ConfirmReturn(RepairExitStatus.RetornoParcial, items);
        Assert.True(result.IsSuccess);
        Assert.Equal(RepairExitStatus.Concluido, result.Value);
    }

    [Fact]
    public void Return_OnCancelled_IsRejected()
    {
        var items = new[] { Item() };
        var result = RepairExitStatusMachine.ConfirmReturn(RepairExitStatus.Cancelado, items);
        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_CYCLE_CANCELED", result.Error.Code);
    }
}