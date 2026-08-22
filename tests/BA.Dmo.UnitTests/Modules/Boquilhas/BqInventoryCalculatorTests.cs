using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Boquilhas;

/// <summary>
/// U-19 — Tests of the CONFIRMED <c>matched/unmatched/exceptionalReceived</c>
/// calculation (GLM-BQ-06, 02_DEC §3.34, UD-08/UD-09) and the BQ hard-block
/// rules. The 20→25 case is mandatory: the full return is accepted, matched is
/// min(return, repair), unmatched becomes exceptional (never auto-added to
/// production). No AllowUnmatched-style block or authorization exists.
/// </summary>
public class BqInventoryCalculatorTests
{
    [Fact]
    public void ReconcileReturn_20To25_Matches20AndUnmatched5()
    {
        var rec = BqInventoryCalculator.ReconcileReturn(returnQty: 25, expectedRepairBalance: 20);

        Assert.Equal(20, rec.MatchedQty);
        Assert.Equal(5, rec.UnmatchedQty);
    }

    [Fact]
    public void ReconcileReturn_Exact_NoUnmatched()
    {
        var rec = BqInventoryCalculator.ReconcileReturn(returnQty: 20, expectedRepairBalance: 20);

        Assert.Equal(20, rec.MatchedQty);
        Assert.Equal(0, rec.UnmatchedQty);
    }

    [Fact]
    public void ReconcileReturn_BelowExpected_NoUnmatched()
    {
        var rec = BqInventoryCalculator.ReconcileReturn(returnQty: 12, expectedRepairBalance: 20);

        Assert.Equal(12, rec.MatchedQty);
        Assert.Equal(0, rec.UnmatchedQty);
    }

    [Fact]
    public void CalculateTrace_20To25_FullLifecycle()
    {
        // START 60 → prod 60
        var state = new BqSaldos();
        var start = WeightMv(BqMovementType.Inicio, 60);
        state = Apply(state, start);
        Assert.Equal(60, state.Prod);

        // Saída 20 → prod 40, repair 20
        state = Apply(state, WeightMv(BqMovementType.Saida, 20));
        Assert.Equal(40, state.Prod);
        Assert.Equal(20, state.Repair);

        // Retorno 25 → matched 20, unmatched 5 (exceptional); never added to prod.
        state = Apply(state, WeightMv(BqMovementType.Entrada, 25));
        Assert.Equal(0, state.Repair);
        Assert.Equal(60, state.Prod);
        Assert.Equal(5, state.ExceptionalReceived);
    }

    [Fact]
    public void Dispatch_ExceedingProduction_IsBlocked()
    {
        var state = new BqSaldos { Prod = 10, Repair = 0 };

        var result = BqInventoryCalculator.Apply(state, WeightMv(BqMovementType.Saida, 15));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.DomainConflict, result.Error.Category);
        Assert.Equal(BqRules.DispatchExceedsProductionCode, result.Error.Code);
    }

    [Fact]
    public void NonRepairable_ExceedingRepair_IsBlocked()
    {
        var state = new BqSaldos { Repair = 8 };

        var result = BqInventoryCalculator.Apply(state, WeightMv(BqMovementType.Irreparavel, 10));

        Assert.True(result.IsFailure);
        Assert.Equal(BqRules.NonRepairableExceedsRepairCode, result.Error.Code);
    }

    [Fact]
    public void LineChange_DoesNotChangeBalances()
    {
        var state = new BqSaldos { Prod = 30, Repair = 5 };

        var result = BqInventoryCalculator.Apply(state, new BqMovement { MovementType = BqMovementType.Linha });

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value.Prod);
        Assert.Equal(5, result.Value.Repair);
    }

    [Fact]
    public void PhysicalInventory_IncludesExceptional()
    {
        var state = BqInventoryCalculator.Apply(
            BqInventoryCalculator.Apply(new BqSaldos { Prod = 20 }, WeightMv(BqMovementType.Saida, 20)).Value,
            WeightMv(BqMovementType.Entrada, 25)).Value;

        // prod 20, repair 20→0, exceptional 5 → physical = 20 + 0 + 0 + 5 = 25.
        Assert.Equal(20, state.Prod);
        Assert.Equal(5, state.ExceptionalReceived);
        Assert.Equal(25, state.PhysicalInventory);
    }

    private static BqSaldos Apply(BqSaldos s, BqMovement m) => BqInventoryCalculator.Apply(s, m).Value;

    private static BqMovement WeightMv(BqMovementType type, decimal qty) => new()
    {
        MovementType = type,
        Qty = qty
    };
}