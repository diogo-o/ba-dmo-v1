using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Boquilhas;

/// <summary>
/// U-19 — Boquilhas application service tests. Verifies the atomic create
/// (lot+trace+START), the CONFIRMED 20→25 excess-return rule end to end (full
/// return accepted + open discrepancy, never a block), movement validation rules,
/// close/reopen and lifecycle constraints. All collaborators are fakes.
/// </summary>
public class BoquilhasServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 13, 0, 0, TimeSpan.Zero);

    private static BoquilhasService BuildService(FakeBoquilhasRepository repo) => new(
        repo, new FakeBqUnitOfWorkFactory(), new BqAuthorizationGate(
            BqCurrentUser.Authorized(), new BqFakeAuthorship()), new BqFixedClock(Now));

    [Fact]
    public async Task CreateLoteWithTrace_IsOneAtomicCreation()
    {
        var repo = new FakeBoquilhasRepository();
        var service = BuildService(repo);

        var result = await service.CreateLoteWithTraceAsync(new CreateBqLoteRequest(
            "T194", "12", new[] { "b1", "B1", "C3" }, 60, 10, "obs"));

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Lotes);
        Assert.Single(repo.Traces);
        Assert.Single(repo.Movements); // the START (inicio) movement
        var lote = repo.Lotes.Single();
        Assert.Equal("T194", lote.Reference);
        // Lines are canonicalized (b1→B1, deduplicated): {B1, C3}.
        Assert.Equal(new[] { "B1", "C3" }, lote.AllowedLines);
        // One global audit fact was written.
        Assert.Contains(repo.AuditEvents, a => a.action == "boquilhas.lote.criar");
    }

    [Fact]
    public async Task DuplicateReferenceBatch_IsBlocked()
    {
        var repo = new FakeBoquilhasRepository();
        repo.SeedLote("T194", "12", "B1");
        var service = BuildService(repo);

        var result = await service.CreateLoteWithTraceAsync(new CreateBqLoteRequest(
            "T194", "12", new[] { "B1" }, 60, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("BQ_DUPLICATE_LOT", result.Error.Code);
    }

    [Fact]
    public async Task InvalidReference_IsRejected()
    {
        var service = BuildService(new FakeBoquilhasRepository());

        var result = await service.CreateLoteWithTraceAsync(new CreateBqLoteRequest(
            "194T", "12", new[] { "B1" }, 60, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(BqRules.ReferenceInvalidCode, result.Error.Code);
    }

    [Fact]
    public async Task RegisterEntrada_20To25_AcceptsFullReturnAndOpensDiscrepancy()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote("T194", "12", "B1");
        var trace = repo.SeedActiveTrace(lote, initialQty: 60);
        // Existing Saída 20 → repair 20, prod 40.
        repo.Movements.Add(new BqMovement
        {
            BqTraceId = trace.BqTraceId,
            MovementType = BqMovementType.Saida,
            Qty = 20,
            ActorId = "bq-actor",
            OccurredAtUtc = Now.AddHours(-1)
        });
        var service = BuildService(repo);

        // Return 25 > repair 20. The full 25 is accepted (no block).
        var result = await service.RegisterMovementAsync(new RegisterBqMovementRequest(
            lote.BqLoteId, trace.BqTraceId, BqMovementType.Entrada, 25, null, null, "retorno"));

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.Qty);
        Assert.Equal(5, result.Value.ExceptionalReceivedQty);
        // An open discrepancy was recorded (C27).
        var disc = repo.Discrepancies.Single();
        Assert.Equal(5, disc.ExcessQty);
        Assert.Equal(BqDiscrepancyStatus.Open, disc.Status);

        // Effective saldo: repair 0, prod 60 (20 reconciled), exceptional 5.
        var summary = await service.GetLotSummaryAsync(lote.BqLoteId);
        Assert.True(summary.IsSuccess);
        Assert.Equal(0, summary.Value.Saldo.Repair);
        Assert.Equal(60, summary.Value.Saldo.Prod);
        Assert.Equal(5, summary.Value.Saldo.ExceptionalReceived);
    }

    [Fact]
    public async Task RegisterEntrada_Exact_NoDiscrepancy()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        var trace = repo.SeedActiveTrace(lote, 60);
        repo.Movements.Add(new BqMovement
        {
            BqTraceId = trace.BqTraceId, MovementType = BqMovementType.Saida, Qty = 20,
            ActorId = "bq-actor", OccurredAtUtc = Now.AddHours(-1)
        });
        var service = BuildService(repo);

        var result = await service.RegisterMovementAsync(new RegisterBqMovementRequest(
            lote.BqLoteId, trace.BqTraceId, BqMovementType.Entrada, 20, null, null, null));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExceptionalReceivedQty);
        Assert.Empty(repo.Discrepancies);
    }

    [Fact]
    public async Task RegisterSaida_ExceedingProduction_IsBlocked()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote("T194", "12", "B1");
        var trace = repo.SeedActiveTrace(lote, initialQty: 10); // prod 10
        var service = BuildService(repo);

        var result = await service.RegisterMovementAsync(new RegisterBqMovementRequest(
            lote.BqLoteId, trace.BqTraceId, BqMovementType.Saida, 15, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(BqRules.DispatchExceedsProductionCode, result.Error.Code);
    }

    [Fact]
    public async Task Movement_OnClosedTrace_IsBlocked()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        var trace = repo.SeedActiveTrace(lote, 60);
        trace.Status = BqTraceStatus.Closed;
        var service = BuildService(repo);

        var result = await service.RegisterMovementAsync(new RegisterBqMovementRequest(
            lote.BqLoteId, trace.BqTraceId, BqMovementType.Saida, 5, null, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal(BqRules.MovementOnClosedTraceCode, result.Error.Code);
    }

    [Fact]
    public async Task CloseTrace_MarksClosed_AndAudits()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        var trace = repo.SeedActiveTrace(lote, 60);
        var service = BuildService(repo);

        var result = await service.CloseTraceAsync(new CloseBqTraceRequest(lote.BqLoteId, trace.BqTraceId, 60, 40, "fim"));

        Assert.True(result.IsSuccess);
        Assert.Equal(BqTraceStatus.Closed, trace.Status);
        Assert.Contains(repo.AuditEvents, a => a.action == "boquilhas.trace.fechar");
    }

    [Fact]
    public async Task Reopen_LastClosedTrace_Works_WhenNoActive()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        var trace = repo.SeedActiveTrace(lote, 60);
        trace.Status = BqTraceStatus.Closed;
        var service = BuildService(repo);

        var result = await service.ReopenTraceAsync(new ReopenBqTraceRequest(lote.BqLoteId, trace.BqTraceId, "reabrir"));

        Assert.True(result.IsSuccess);
        Assert.Equal(BqTraceStatus.Active, trace.Status);
    }

    [Fact]
    public async Task Lifecycle_WithActiveTrace_IsBlocked()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        repo.SeedActiveTrace(lote, 60); // active trace remains
        var service = BuildService(repo);

        var result = await service.ApplyLifecycleAsync(new BqLifecycleRequest(lote.BqLoteId, BqLifecycleEventKind.Archived, null));

        Assert.True(result.IsFailure);
        Assert.Equal(BqRules.LifecycleActiveTraceCode, result.Error.Code);
    }

    [Fact]
    public async Task Lifecycle_WithoutActiveTrace_ArchivesLot()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote();
        var service = BuildService(repo);

        var result = await service.ApplyLifecycleAsync(new BqLifecycleRequest(lote.BqLoteId, BqLifecycleEventKind.Archived, "arquivar"));

        Assert.True(result.IsSuccess);
        Assert.Equal(BqLifecycleState.Archived, repo.Lotes.Single().LifecycleState);
        Assert.Single(repo.LifecycleEvents);
    }

    [Fact]
    public async Task ListMovements_EnrichesReferenceLotRepairerAndRunningSaldo()
    {
        var repo = new FakeBoquilhasRepository();
        var lote = repo.SeedLote("T194", "12", "B1");
        var trace = repo.SeedActiveTrace(lote, 60);

        // Saída 20 (repair) with a repairer → prod 40, repair 20 at that row.
        var repairer = repo.SeedRepairer("Reparador BQ");
        repo.Repairers.Add(repairer);
        repo.Movements.Add(new BqMovement
        {
            BqTraceId = trace.BqTraceId,
            MovementType = BqMovementType.Saida,
            Qty = 20,
            RepairerId = repairer.RepairerId,
            Line = "B1",
            ActorId = "bq-actor",
            OccurredAtUtc = Now.AddHours(-1)
        });
        // Retorno 25 → matched 20 (prod 60), exceptional 5.
        repo.Movements.Add(new BqMovement
        {
            BqTraceId = trace.BqTraceId,
            MovementType = BqMovementType.Entrada,
            Qty = 25,
            RepairerId = repairer.RepairerId,
            Line = "B1",
            ActorId = "bq-actor",
            OccurredAtUtc = Now
        });

        var service = BuildService(repo);
        var result = await service.ListMovementsAsync(null,
            new BqHistoryFilter(lote.BqLoteId, null, null, null, null, null, 1, 60));

        Assert.True(result.IsSuccess);
        var byType = result.Value.ToDictionary(m => m.MovementType);
        // Every row carries the authoritative reference/lote.
        Assert.All(result.Value, m => Assert.Equal(("T194", "12"), (m.Reference, m.BatchCode)));
        // Saldo after the Saída: prod 40; after the Entrada (matched 20 + exceptional 5): prod 60.
        Assert.Equal(40, byType[BqMovementType.Saida].SaldoAfter.Prod);
        Assert.Equal(60, byType[BqMovementType.Entrada].SaldoAfter.Prod);
        Assert.Equal(5, byType[BqMovementType.Entrada].SaldoAfter.ExceptionalReceived);
        // Repairer name resolved through the canonical vocabulary (only on moves that carry one).
        Assert.Equal("Reparador BQ", byType[BqMovementType.Saida].RepairerName);
        Assert.Equal("Reparador BQ", byType[BqMovementType.Entrada].RepairerName);
        Assert.Null(byType[BqMovementType.Inicio].RepairerName);
    }

    [Fact]
    public async Task ListMovements_SearchFiltersByReferenceLotAndLine()
    {
        var repo = new FakeBoquilhasRepository();
        var loteB = repo.SeedLote("T194", "12", "B1");
        var loteC = repo.SeedLote("T195", "99", "C3");
        var sb = repo.SeedActiveTrace(loteB, 30, Now.AddHours(-3));
        var sc = repo.SeedActiveTrace(loteC, 40, Now.AddHours(-2));
        repo.Movements.Add(SaidaWithLine(sb.BqTraceId, "B1"));
        repo.Movements.Add(SaidaWithLine(sc.BqTraceId, "C3"));

        var service = BuildService(repo);

        // By reference (matches both the Inicio + Saída of loteB T194).
        var byRef = await service.ListMovementsAsync(null, new BqHistoryFilter(null, "T194", null, null, null, null, 1, 60));
        Assert.True(byRef.IsSuccess);
        Assert.Equal(2, byRef.Value.Count);
        Assert.All(byRef.Value, m => Assert.Equal("T194", m.Reference));

        // By lote (only loteC's Saída carries line C3; loteC='99', reference T195).
        var byLot = await service.ListMovementsAsync(null, new BqHistoryFilter(null, "T195", null, null, null, null, 1, 60));
        Assert.True(byLot.IsSuccess);
        Assert.Equal(2, byLot.Value.Count);
        Assert.All(byLot.Value, m => Assert.Equal("T195", m.Reference));

        // By line: only the Saída rows carry a line; B1 and C3 lanes resolve distinctly.
        var byLine = await service.ListMovementsAsync(null, new BqHistoryFilter(null, "b1", null, null, null, null, 1, 60));
        Assert.True(byLine.IsSuccess);
        Assert.Single(byLine.Value);
        Assert.Equal("B1", byLine.Value[0].Line);
    }

    private static BqMovement SaidaWithLine(Guid traceId, string line) => new()
    {
        BqTraceId = traceId,
        MovementType = BqMovementType.Saida,
        Qty = 10,
        Line = line,
        ActorId = "bq-actor",
        OccurredAtUtc = Now
    };
}