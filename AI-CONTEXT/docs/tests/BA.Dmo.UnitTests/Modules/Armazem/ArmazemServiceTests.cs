using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — Armazém use-case behavior (GLM-ARM-05..07, owner decisions A/D/E):
/// Entrada occupies atomically; Saída releases only after persistence; Substituir
/// is one atomic command; Destino optional; <c>fora</c> derived; actor is
/// server-derived; two references on one position is a warning, never silent.
/// </summary>
public class ArmazemServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeArmazemRepository _repository = new();
    private readonly FakeToolIdentityResolver _resolver = new();
    private readonly ArmazemService _service;

    public ArmazemServiceTests()
    {
        var gate = new ArmazemAuthorizationGate(
            ArmazemCurrentUser.Authorized(), new ArmazemFakeAuthorship("arm-actor"));
        _service = new ArmazemService(_repository, _resolver, gate, new ArmazemFixedClock(Now));
    }

    private Guid SeedTool(string reference = "CM-100", string lot = "1")
    {
        var id = Guid.NewGuid();
        _resolver.Identities.Add(new WarehouseToolIdentity(id, WarehouseToolDomain.Ferramentas, "CM", reference, lot, "Contra-molde"));
        return id;
    }

    private Guid SeedToolAt(string position, string reference = "CM-100", string lot = "1")
    {
        var toolId = SeedTool(reference, lot);
        var locationId = _repository.GetOrCreateLocationAsync(position, "tool").Result;
        _repository.Stocks.Add(new WarehouseStock
        {
            WarehouseStockId = Guid.NewGuid(),
            WarehouseLocationId = locationId,
            ToolId = toolId,
            OccupiedSinceUtc = Now.AddHours(-1),
            OccupiedBy = "arm-actor"
        });
        return toolId;
    }

    private WarehouseStock NewStock(Guid locationId, Guid toolId) => new()
    {
        WarehouseStockId = Guid.NewGuid(),
        WarehouseLocationId = locationId,
        ToolId = toolId,
        OccupiedSinceUtc = Now,
        OccupiedBy = "arm-actor"
    };

    private static WarehouseMovement InMovement() => new()
    {
        WarehouseMovementId = Guid.NewGuid(),
        Direction = WarehouseMovementDirection.In,
        ActorId = "arm-actor",
        OccurredAtUtc = Now
    };

    // ---- Authorization fail-closed ----------------------------------------

    [Fact]
    public async Task Entrada_WithoutModule_IsForbidden()
    {
        var gate = new ArmazemAuthorizationGate(ArmazemCurrentUser.WithoutModule(), new ArmazemFakeAuthorship("arm-actor"));
        var svc = new ArmazemService(new FakeArmazemRepository(), _resolver, gate, new ArmazemFixedClock(Now));
        var result = await svc.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-100", "1", "2421", null, null));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- Entrada -----------------------------------------------------------

    [Fact]
    public async Task Entrada_InvalidPosition_IsRejected()
    {
        var result = await _service.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-100", "1", "24A", null, null));
        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_LOCATION_CODE", result.Error.Code);
    }

    [Fact]
    public async Task Entrada_OccupiedByOtherTool_IsBlocked()
    {
        SeedToolAt("2421", "CM-100", "1");
        SeedTool("CM-200", "2");
        var result = await _service.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-200", "2", "2421", null, null));
        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_POSITION_OCCUPIED", result.Error.Code);
    }

    [Fact]
    public async Task Entrada_OnFreePosition_OccupiesAndCreatesInMovement()
    {
        SeedTool("CM-100", "1");
        var result = await _service.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-100", "1", "2421", "producao", null));
        Assert.True(result.IsSuccess);
        Assert.Single(_repository.Stocks.Where(s => s.IsActive));
        Assert.Contains(_repository.Movements, m => m.Direction == WarehouseMovementDirection.In);
        Assert.Contains(_repository.AuditEvents, a => a.eventType == "armazem.entrada");
    }

    [Fact]
    public async Task Entrada_AtomicFailure_DoesNotLeaveStock()
    {
        SeedTool("CM-100", "1");
        _repository.FailAtomicWrite = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-100", "1", "2421", null, null)));
        Assert.Empty(_repository.Stocks);
    }

    // ---- Concurrency / atomic occupation guard (TOCTOU) --------------------
    // These exercise the fake's RegisterEntradaAsync directly (bypassing the
    // service fast-path pre-check) to prove the ATOMIC 1:1 invariant the same
    // way the repository's FOR UPDATE lock-guard enforces it.

    [Fact]
    public async Task Entrada_TwoDifferentToolsAtSamePosition_OnlyOneOccupiesAtomically()
    {
        var locationId = _repository.GetOrCreateLocationAsync("2421", "tool").Result;
        var toolA = SeedTool("CM-100", "1");
        var toolB = SeedTool("CM-200", "2");

        var stockA = NewStock(locationId, toolA);
        var result = await _repository.RegisterEntradaAsync(stockA, InMovement());
        Assert.Equal(stockA.WarehouseStockId, result);

        await Assert.ThrowsAsync<ArmazemLocationOccupiedException>(() =>
            _repository.RegisterEntradaAsync(NewStock(locationId, toolB), InMovement()));

        var active = Assert.Single(_repository.Stocks.Where(s =>
            s.WarehouseLocationId == locationId && s.IsActive));
        Assert.Equal(toolA, active.ToolId);
        Assert.DoesNotContain(_repository.Stocks, s => s.ToolId == toolB);
    }

    [Fact]
    public async Task Entrada_ReEntrySameToolOnOccupiedPosition_IsConflict()
    {
        var locationId = _repository.GetOrCreateLocationAsync("2421", "tool").Result;
        var toolA = SeedTool("CM-100", "1");

        await _repository.RegisterEntradaAsync(NewStock(locationId, toolA), InMovement());
        Assert.Single(_repository.Stocks.Where(s => s.IsActive));

        // Same tool already actively occupies the position: safe/default is a
        // controlled conflict, not a raw unique-index violation.
        await Assert.ThrowsAsync<ArmazemLocationOccupiedException>(() =>
            _repository.RegisterEntradaAsync(NewStock(locationId, toolA), InMovement()));
        Assert.Single(_repository.Stocks.Where(s => s.IsActive));
    }

    // ---- Saída (retirar) ---------------------------------------------------

    [Fact]
    public async Task Saida_ReleasesOnlyAfterPersistence()
    {
        var toolId = SeedToolAt("2421", "CM-100", "1");
        var result = await _service.RegistrarSaidaAsync(new RegistrarSaidaRequest("CM", "CM-100", "1", "reparacao", null));
        Assert.True(result.IsSuccess);
        Assert.All(_repository.Stocks, s => Assert.False(s.IsActive));
        Assert.Contains(_repository.Movements, m => m.Direction == WarehouseMovementDirection.Out && m.Destination == "reparacao");
    }

    [Fact]
    public async Task Saida_WhenToolNotInWarehouse_IsRejected()
    {
        SeedTool("CM-100", "1"); // never entered
        var result = await _service.RegistrarSaidaAsync(new RegistrarSaidaRequest("CM", "CM-100", "1", null, null));
        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_TOOL_NOT_IN_WAREHOUSE", result.Error.Code);
    }

    [Fact]
    public async Task Saida_AtomicFailure_KeepsPositionOccupied()
    {
        SeedToolAt("2421", "CM-100", "1");
        _repository.FailAtomicWrite = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegistrarSaidaAsync(new RegistrarSaidaRequest("CM", "CM-100", "1", null, null)));
        Assert.All(_repository.Stocks, s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task Saida_DestinationOptional_IsAllowed()
    {
        SeedToolAt("2421", "CM-100", "1");
        var result = await _service.RegistrarSaidaAsync(new RegistrarSaidaRequest("CM", "CM-100", "1", null, null));
        Assert.True(result.IsSuccess);
        Assert.Contains(_repository.Movements, m => m.Direction == WarehouseMovementDirection.Out && m.Destination == null);
    }

    // ---- Substituir (atomic) ----------------------------------------------

    [Fact]
    public async Task Substituir_ReleasesCurrentAndOccupiesReplacement()
    {
        var currentId = SeedToolAt("2421", "CM-100", "1");
        var newId = SeedTool("CM-150", "2");
        var result = await _service.SubstituirAsync(new SubstituirRequest("2421", "CM", "CM-150", "2", null));
        Assert.True(result.IsSuccess);
        var currentActive = _repository.Stocks.FirstOrDefault(s => s.ToolId == currentId);
        Assert.NotNull(currentActive);
        Assert.False(currentActive!.IsActive);
        var active = Assert.Single(_repository.Stocks.Where(s => s.IsActive));
        Assert.Equal(newId, active.ToolId);
        Assert.Equal(2, _repository.Movements.Count(m => m.WarehouseStockId.HasValue));
    }

    [Fact]
    public async Task Substituir_AtomicFailure_LeavesBothPositionsUnchanged()
    {
        SeedToolAt("2421", "CM-100", "1");
        SeedTool("CM-150", "2");
        _repository.FailAtomicWrite = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubstituirAsync(new SubstituirRequest("2421", "CM", "CM-150", "2", null)));
        Assert.All(_repository.Stocks, s => Assert.True(s.IsActive));
        Assert.Empty(_repository.Movements);
    }

    [Fact]
    public async Task Substituir_OnFreePosition_IsRejected()
    {
        SeedTool("CM-150", "2");
        var result = await _service.SubstituirAsync(new SubstituirRequest("2421", "CM", "CM-150", "2", null));
        Assert.True(result.IsFailure);
        Assert.Equal("ARMZ_POSITION_FREE", result.Error.Code);
    }

    // ---- Consulta / fora / two-ref warning --------------------------------

    [Fact]
    public async Task Consulta_ToolWithoutWarehouse_IsFora()
    {
        SeedTool("CM-200", "5");
        var result = await _service.ConsultarAsync(new ConsultarRequest("CM", "CM-200", "5", null));
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal("fora", row.LocationContext);
    }

    [Fact]
    public async Task Consulta_ToolInWarehouse_ReportsPosition()
    {
        SeedToolAt("2421", "CM-100", "1");
        var result = await _service.ConsultarAsync(new ConsultarRequest("CM", "CM-100", "1", null));
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal("armazem", row.LocationContext);
        Assert.Equal("2421", row.PositionCode);
    }

    [Fact]
    public async Task Consulta_ByPosition_TwoDifferentReferences_FlagsWarning()
    {
        // Same position occupied by two DIFFERENT references (data-quality alert).
        var locationId = _repository.GetOrCreateLocationAsync("2421", "tool").Result;
        var a = SeedTool("REF-A", "1");
        var b = SeedTool("REF-B", "2");
        _repository.Stocks.Add(new WarehouseStock { WarehouseStockId = Guid.NewGuid(), WarehouseLocationId = locationId, ToolId = a, OccupiedSinceUtc = Now, OccupiedBy = "arm-actor" });
        _repository.Stocks.Add(new WarehouseStock { WarehouseStockId = Guid.NewGuid(), WarehouseLocationId = locationId, ToolId = b, OccupiedSinceUtc = Now.AddMinutes(1), OccupiedBy = "arm-actor" });

        var result = await _service.ConsultarAsync(new ConsultarRequest(null, null, null, "2421"));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, row => Assert.True(row.HasReferenceConflict));
    }

    // ---- Repor (re-occupation) --------------------------------------------

    [Fact]
    public async Task Repor_AfterSaida_ReOccupiesSameToolAtPosition()
    {
        var toolId = SeedToolAt("2421", "CM-100", "1");
        await _service.RegistrarSaidaAsync(new RegistrarSaidaRequest("CM", "CM-100", "1", null, null));
        var result = await _service.RegistrarEntradaAsync(new RegistrarEntradaRequest("CM", "CM-100", "1", "2421", null, null));
        Assert.True(result.IsSuccess);
        var active = Assert.Single(_repository.Stocks.Where(s => s.IsActive));
        Assert.Equal(toolId, active.ToolId);
    }
}