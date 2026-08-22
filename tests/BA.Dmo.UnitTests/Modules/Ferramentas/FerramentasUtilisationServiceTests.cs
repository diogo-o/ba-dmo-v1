using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Ferramentas;

/// <summary>
/// R003 — Ferramentas utilisation service tests. Verifies the append-only history,
/// the manual % use (recorded from SAP, NO formula/derivation), and that a later
/// reading never reinterprets an earlier one (SAP/% snapshot per reading).
/// </summary>
public class FerramentasUtilisationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static FerramentasService Build(FakeFerramentasRepository repo, FakeCurrentUser user) =>
        new(repo, new FakeRuleLookup(),
            new FerramentasAuthorizationGate(user, new FakeAuthorshipAccessor("ferr-actor")),
            new FixedClock(Now));

    [Fact]
    public async Task RecordReading_Appends_AndNeverOverwrites()
    {
        var repo = new FakeFerramentasRepository();
        var lot = new ToolLote { ToolLoteId = Guid.NewGuid(), Lote = "L1" };
        repo.Lotes[lot.ToolLoteId] = lot;
        var service = Build(repo, FakeCurrentUser.Authorized());

        var r1 = await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 30, 100, 30, 30, 30, null));
        var r2 = await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 30, 100, 55, 25, 55, null));

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.Equal(2, repo.UtilisationReadings.Count);
    }

    [Fact]
    public async Task GetUtilisation_ReturnsRecordedManualPercent_NoFormula()
    {
        var repo = new FakeFerramentasRepository();
        var lot = new ToolLote { ToolLoteId = Guid.NewGuid(), Lote = "L1" };
        repo.Lotes[lot.ToolLoteId] = lot;
        var service = Build(repo, FakeCurrentUser.Authorized());

        await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 20, 100, 42, 20, 60, null));
        // A LATER reading changes sap_end AND % use — this must NOT alter the earlier.
        await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 20, 90, 66, 6, 66, null));

        var status = await service.GetUtilisationAsync(lot.ToolLoteId);

        Assert.True(status.IsSuccess);
        Assert.Equal(2, status.Value.History.Count);
        Assert.Equal(66, status.Value.PercentUsed); // recorded (manual) % of the LATEST reading
        // The earlier reading keeps its own snapshot (42%, sap_end 100).
        Assert.Equal(42, status.Value.History[0].PercentUsed);
        Assert.Equal(100, status.Value.History[0].SapEnd);
    }

    [Fact]
    public async Task RecordReading_InvalidPercent_IsRejected()
    {
        var repo = new FakeFerramentasRepository();
        var lot = new ToolLote { ToolLoteId = Guid.NewGuid(), Lote = "L1" };
        repo.Lotes[lot.ToolLoteId] = lot;
        var service = Build(repo, FakeCurrentUser.Authorized());

        var result = await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 20, 100, 150, 20, 60, null));

        Assert.True(result.IsFailure);
        Assert.Equal("FERRAMENTAS_UTIL_PERCENT_RANGE", result.Error.Code);
    }

    [Fact]
    public async Task RecordReading_NoFormula_StoresNegativeCumulative_Rejected()
    {
        var repo = new FakeFerramentasRepository();
        var lot = new ToolLote { ToolLoteId = Guid.NewGuid(), Lote = "L1" };
        repo.Lotes[lot.ToolLoteId] = lot;
        var service = Build(repo, FakeCurrentUser.Authorized());

        var result = await service.RecordUtilisationReadingAsync(new RecordToolUtilisationRequest(
            lot.ToolLoteId, 20, 100, 30, 10, -5, null));

        Assert.True(result.IsFailure);
    }
}