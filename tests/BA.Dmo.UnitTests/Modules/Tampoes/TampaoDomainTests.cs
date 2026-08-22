using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões domain invariants (GLM-TP-04/08): value normalization (4 ≡ 4.0
/// ≡ 4.00 → one canonical key), configuration equality/target-reuse, non-negative
/// balances, destination-equal-origin blocked, single atomic movement transfer.
/// </summary>
public class TampaoDomainTests
{
    [Fact]
    public void ConfigurationKey_NormalizesEquivalentValues()
    {
        var k4 = MakeKey(4m);
        var k40 = MakeKey(4.0m);
        var k400 = MakeKey(4.0000m);
        Assert.Equal(k4, k40);
        Assert.Equal(k4, k400);
    }

    [Fact]
    public void ConfigurationKey_IsStableIgnoringInsertionOrder()
    {
        var values1 = new SortedDictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Diâmetro"] = 28.95m,
            ["Profundidade/Calote"] = 4m
        };
        var a = TampaoConfigurationKey.Serialize(values1);
        var b = TampaoConfigurationKey.Serialize(values1.Reverse().ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.Equal(a, b);
    }

    [Fact]
    public void NormalizeValue_CollapsesLegacyVariants()
    {
        Assert.Equal(4m, TampaoRules.NormalizeValue(4m));
        Assert.Equal(4m, TampaoRules.NormalizeValue(4.0m));
        Assert.Equal(4m, TampaoRules.NormalizeValue(4.0000m));
    }

    [Fact]
    public void ValidateQuantity_PositiveIntegerOnly()
    {
        Assert.True(TampaoRules.ValidateQuantity(1).IsSuccess);
        Assert.True(TampaoRules.ValidateQuantity(25).IsSuccess);
        Assert.False(TampaoRules.ValidateQuantity(0).IsSuccess);
        Assert.False(TampaoRules.ValidateQuantity(-3).IsSuccess);
    }

    [Fact]
    public void ApplySingleBalanceChange_NeverNegative()
    {
        var ok = TampaoRules.ApplySingleBalanceChange(5, -3);
        Assert.True(ok.IsSuccess);
        Assert.Equal(2, ok.Value);

        var neg = TampaoRules.ApplySingleBalanceChange(2, -5);
        Assert.True(neg.IsFailure);
        Assert.Equal("TAMPAO_NEGATIVE_BALANCE", neg.Error.Code);
    }

    [Fact]
    public void ResolveStateOrigin_OriginIsOpposite_AndBlocksInsufficient()
    {
        var saldo = new TampaoSaldo { Enchidos = 0, PorEncher = 10 };
        // Selecting "Enchidos" as destination draws from Por encher.
        var origin = TampaoRules.ResolveStateOrigin(saldo, TampaoBalanceKind.Enchidos, 5);
        Assert.True(origin.IsSuccess);
        Assert.Equal(TampaoBalanceKind.PorEncher, origin.Value);

        var insufficient = TampaoRules.ResolveStateOrigin(saldo, TampaoBalanceKind.Enchidos, 100);
        Assert.True(insufficient.IsFailure);
        Assert.Equal("TAMPAO_INSUFFICIENT_ORIGIN", insufficient.Error.Code);
    }

    [Fact]
    public void ApplyBalanceTransfer_BlocksDestinationEqualsOrigin()
    {
        var saldo = new TampaoSaldo { Enchidos = 10, PorEncher = 5 };
        var result = TampaoRules.ApplyBalanceTransfer(saldo, TampaoBalanceKind.Enchidos, TampaoBalanceKind.Enchidos, 3);
        Assert.True(result.IsFailure);
        Assert.Equal("TAMPAO_DESTINATION_EQUALS_ORIGIN", result.Error.Code);
    }

    [Fact]
    public void ValidateConfigurationTransform_RequiresDifferentIdAndAChangedCharacteristic()
    {
        var origin = new TampaoConfiguration { Values = MakeValues(4m) };
        var same = new TampaoConfiguration { TampaoConfigurationId = origin.TampaoConfigurationId, Values = MakeValues(4m) };
        var sameResult = TampaoRules.ValidateConfigurationTransform(origin, same);
        Assert.True(sameResult.IsFailure);
        Assert.Equal("TAMPAO_DESTINATION_EQUALS_ORIGIN", sameResult.Error.Code);

        var identicalValues = new TampaoConfiguration { Values = MakeValues(4m) };
        var noChange = TampaoRules.ValidateConfigurationTransform(origin, identicalValues);
        Assert.True(noChange.IsFailure);
        Assert.Equal("TAMPAO_NO_CHARACTERISTIC_CHANGED", noChange.Error.Code);

        var changed = new TampaoConfiguration { Values = MakeValues(7m) };
        Assert.True(TampaoRules.ValidateConfigurationTransform(origin, changed).IsSuccess);
    }

    private static string MakeKey(decimal calote) =>
        TampaoConfigurationKey.Serialize(new SortedDictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Profundidade/Calote"] = calote
        });

    private static IReadOnlyDictionary<string, decimal> MakeValues(decimal calote) =>
        new SortedDictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Profundidade/Calote"] = calote
        };
}