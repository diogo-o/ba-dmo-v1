using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões service use cases (GLM-TP-05..09; roadmap U-17 tests):
/// balances never negative; state/configuração transforms are atomic single
/// movements; existing destination configuration is reused; planning does not
/// reserve/alter stock; save-failure preserves inputs; authorization fail-closed.
/// All collaborators are in-memory fakes (no live DB).
/// </summary>
public class TampaoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static (TampaoService service, FakeTampaoRepository repo) Build()
    {
        var repo = new FakeTampaoRepository();
        var service = new TampaoService(
            repo, new FakeTampoesUnitOfWorkFactory(),
            new TampaoAuthorizationGate(TampaoCurrentUser.Authorized(), new TampaoFakeAuthorship()),
            new TampaoFixedClock(Now));
        return (service, repo);
    }

    // ---- Adicionar / Remover ---------------------------------------------------

    [Fact]
    public async Task Adicionar_IncrementsOnlyChosenBalance_AndCreatesMovement()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 10, porEncher: 5);

        var movementId = (await service.AdicionarQuantidadeAsync(
            new AdicionarQuantidadeRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 3))).Value;

        var saldo = repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId);
        Assert.Equal(13, saldo.Enchidos);
        Assert.Equal(5, saldo.PorEncher); // untouched
        Assert.Single(repo.Movements);
        Assert.Equal(TampaoMovementType.Adicionar, repo.Movements[0].MovementType);
        Assert.Equal(3, repo.Movements[0].Qty);
        Assert.True(movementId != Guid.Empty);
        Assert.Contains(repo.AuditEvents, a => a.action == "tampoes.quantidade.adicionar" && a.result == "succeeded");
    }

    [Fact]
    public async Task Remover_DecrementsOnlyChosenBalance_AndBlocksNegative()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 4, porEncher: 5);

        var ok = await service.RemoverQuantidadeAsync(
            new RemoverQuantidadeRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 2));
        Assert.True(ok.IsSuccess);
        Assert.Equal(2, repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId).Enchidos);

        var negative = await service.RemoverQuantidadeAsync(
            new RemoverQuantidadeRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 5));
        Assert.True(negative.IsFailure);
        Assert.Equal(TampaoRules.NegativeBalanceCode, negative.Error.Code);
        Assert.Single(repo.Movements); // only the successful removal recorded
    }

    [Fact]
    public async Task Adicionar_SaveFailure_PreservesInputAndNoSuccess()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 10);
        repo.FailTransaction = true;

        var result = await service.AdicionarQuantidadeAsync(
            new AdicionarQuantidadeRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 3));

        Assert.True(result.IsFailure);
        Assert.Equal("TAMPAO_SAVE_FAILED", result.Error.Code);
        Assert.Equal(10, repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId).Enchidos);
        Assert.Empty(repo.Movements);
    }

    // ---- Alterar estado (atomic single movement) --------------------------------

    [Fact]
    public async Task AlterarEstado_TransfersBetweenBalances_AsSingleMovement()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 0, porEncher: 10);

        var movementId = (await service.AlterarEstadoAsync(
            new AlterarEstadoRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 5))).Value;

        var saldo = repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId);
        Assert.Equal(5, saldo.Enchidos);
        Assert.Equal(5, saldo.PorEncher);
        // Exactly ONE movement for the whole transfer (never two independent moves).
        Assert.Single(repo.Movements);
        Assert.Equal(TampaoMovementType.AlterarEstado, repo.Movements[0].MovementType);
        Assert.NotNull(repo.Movements[0].BalancesBefore);
        Assert.NotNull(repo.Movements[0].BalancesAfter);
        Assert.Contains(repo.AuditEvents, a => a.action == "tampoes.estado.alterar");
    }

    [Fact]
    public async Task AlterarEstado_InsufficientOrigin_IsBlocked()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 0, porEncher: 3);

        var result = await service.AlterarEstadoAsync(
            new AlterarEstadoRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 5));
        Assert.True(result.IsFailure);
        Assert.Equal(TampaoRules.InsufficientOriginCode, result.Error.Code);
        Assert.Empty(repo.Movements);
    }

    // ---- Alterar configuração (atomic origin → destination) -----------------------

    [Fact]
    public async Task AlterarConfiguracao_TransformsQuantity_AndCreatesDestination()
    {
        var (service, repo) = Build();
        var origin = repo.SeedConfiguration("28,95", "4", enchidos: 25, porEncher: 0);

        var movementId = (await service.AlterarConfiguracaoAsync(new AlterarConfiguracaoRequest(
            origin.TampaoConfigurationId,
            new Dictionary<string, decimal> { ["Diâmetro"] = 28.95m, ["Profundidade/Calote"] = 7m },
            25))).Value;

        // Origin Enchidos reduced; a new destination configuration created with +25.
        Assert.Equal(0, repo.Saldos.Single(s => s.TampaoConfigurationId == origin.TampaoConfigurationId).Enchidos);
        var dest = repo.Configurations.Single(c => c.TampaoConfigurationId != origin.TampaoConfigurationId);
        Assert.Equal(7m, dest.Values["Profundidade/Calote"]);
        Assert.Equal(25, repo.Saldos.Single(s => s.TampaoConfigurationId == dest.TampaoConfigurationId).Enchidos);
        Assert.Single(repo.Movements);
        Assert.Equal(TampaoMovementType.AlterarConfiguracao, repo.Movements[0].MovementType);
        Assert.Equal(movementId, repo.Movements[0].TampaoMovementId);
        Assert.Contains(repo.AuditEvents, a => a.action == "tampoes.configuracao.alterar");
    }

    [Fact]
    public async Task AlterarConfiguracao_ReusesExistingDestination()
    {
        var (service, repo) = Build();
        var origin = repo.SeedConfiguration("28,95", "4", enchidos: 25);
        var existing = repo.SeedConfiguration("28,95", "7", enchidos: 10);
        var countBefore = repo.Configurations.Count;

        await service.AlterarConfiguracaoAsync(new AlterarConfiguracaoRequest(
            origin.TampaoConfigurationId,
            new Dictionary<string, decimal> { ["Diâmetro"] = 28.95m, ["Profundidade/Calote"] = 7m },
            25));

        // No new row: the existing 7mm configuration id is reused (GLM-TP-05.3).
        Assert.Equal(countBefore, repo.Configurations.Count);
        Assert.Equal(35, repo.Saldos.Single(s => s.TampaoConfigurationId == existing.TampaoConfigurationId).Enchidos);
        Assert.Equal(0, repo.Saldos.Single(s => s.TampaoConfigurationId == origin.TampaoConfigurationId).Enchidos);
    }

    [Fact]
    public async Task AlterarConfiguracao_DestinationEqualsValues_IsBlocked()
    {
        var (service, repo) = Build();
        var origin = repo.SeedConfiguration("28,95", "4", enchidos: 25);

        // Destination = origin values → no characteristic changed → blocked.
        var result = await service.AlterarConfiguracaoAsync(new AlterarConfiguracaoRequest(
            origin.TampaoConfigurationId,
            new Dictionary<string, decimal> { ["Diâmetro"] = 28.95m, ["Profundidade/Calote"] = 4m },
            25));
        Assert.True(result.IsFailure);
        Assert.Equal("TAMPAO_NO_CHARACTERISTIC_CHANGED", result.Error.Code);
        Assert.Empty(repo.Movements);
    }

    [Fact]
    public async Task AlterarConfiguracao_InsufficientOrigin_IsBlocked()
    {
        var (service, repo) = Build();
        var origin = repo.SeedConfiguration("28,95", "4", enchidos: 5);

        var result = await service.AlterarConfiguracaoAsync(new AlterarConfiguracaoRequest(
            origin.TampaoConfigurationId,
            new Dictionary<string, decimal> { ["Diâmetro"] = 28.95m, ["Profundidade/Calote"] = 7m },
            25));
        Assert.True(result.IsFailure);
        Assert.Equal(TampaoRules.InsufficientOriginCode, result.Error.Code);
        Assert.Empty(repo.Movements);
    }

    [Fact]
    public async Task AlterarConfiguracao_DestinationDuplicate_Raw23505_MapsToCleanDomainConflict()
    {
        var (service, repo) = Build();
        var origin = repo.SeedConfiguration("28,95", "4", enchidos: 25);
        repo.FailConfigurationDuplicate = true; // concurrent uq_tampao_configurations_values (audit TP-06)

        var result = await service.AlterarConfiguracaoAsync(new AlterarConfiguracaoRequest(
            origin.TampaoConfigurationId,
            new Dictionary<string, decimal> { ["Diâmetro"] = 28.95m, ["Profundidade/Calote"] = 7m },
            25));

        Assert.True(result.IsFailure);
        Assert.Equal("TAMPAO_CONFIGURATION_DUPLICATE", result.Error.Code);
        // Nothing persisted: no movement, no balance change, no new configuration.
        Assert.Empty(repo.Movements);
        Assert.Equal(25, repo.Saldos.Single(s => s.TampaoConfigurationId == origin.TampaoConfigurationId).Enchidos);
        Assert.Single(repo.Configurations);
    }

    // ---- Planeamento (planear ≠ reservar) ------------------------------------------

    [Fact]
    public async Task Planear_DoesNotAlterOrReserveStock()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 28, porEncher: 5);
        var enchidosBefore = repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId).Enchidos;
        var porEncherBefore = repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId).PorEncher;

        var planoId = (await service.PlanearAsync(
            new PlanearRequest(cfg.TampaoConfigurationId, 100, new DateOnly(2026, 9, 1), null))).Value;

        Assert.True(planoId != Guid.Empty);
        Assert.Single(repo.Planos);
        Assert.Empty(repo.Movements); // planning creates NO physical movement
        var saldo = repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId);
        Assert.Equal(enchidosBefore, saldo.Enchidos);
        Assert.Equal(porEncherBefore, saldo.PorEncher);
    }

    [Fact]
    public async Task CancelarPlano_PreservesBalances()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 28);
        var planoId = (await service.PlanearAsync(
            new PlanearRequest(cfg.TampaoConfigurationId, 50, null, null))).Value;

        await service.CancelarPlanoAsync(new CancelarPlanoRequest(planoId));

        var plano = repo.Planos.Single(p => p.TampaoPlanoId == planoId);
        Assert.True(plano.Canceled);
        Assert.Empty(repo.Movements); // cancel never touches balances
        Assert.Equal(28, repo.Saldos.Single(s => s.TampaoConfigurationId == cfg.TampaoConfigurationId).Enchidos);
    }

    // ---- Opções -------------------------------------------------------------------

    [Fact]
    public async Task Opcoes_DeactivateValue_DoesNotDeleteConfigurationsOrHistory()
    {
        var (service, repo) = Build();
        var field = new Domain.Modules.Tampoes.TampaoFieldDef { FieldName = "Diâmetro", Unit = "mm" };
        await repo.CreateFieldDefAsync(field);
        var value = new Domain.Modules.Tampoes.TampaoFieldValue { TampaoFieldDefId = field.TampaoFieldDefId, ValueNumeric = 4m, ValueLabel = "4" };
        await repo.CreateFieldValueAsync(value);
        var cfg = repo.SeedConfiguration("28,95", "4");
        var configCount = repo.Configurations.Count;

        await service.UpdateFieldValueAsync(new UpdateFieldValueRequest(value.TampaoFieldValueId, "4", 1, false));

        // Value rows persist (deactivated, not deleted); configurations/history intact.
        Assert.Single(repo.FieldValues);
        Assert.False(repo.FieldValues[0].Active);
        Assert.Equal(configCount, repo.Configurations.Count);
    }

    // ---- Authorization -------------------------------------------------------------

    [Fact]
    public async Task Consulta_WithoutModule_FailsClosed()
    {
        var repo = new FakeTampaoRepository();
        var service = new TampaoService(
            repo, new FakeTampoesUnitOfWorkFactory(),
            new TampaoAuthorizationGate(TampaoCurrentUser.WithoutModule(), new TampaoFakeAuthorship()),
            new TampaoFixedClock(Now));

        var result = await service.ConsultarAsync(null);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- Histórico -----------------------------------------------------------------

    [Fact]
    public async Task ListMovimentos_FiltersByType()
    {
        var (service, repo) = Build();
        var cfg = repo.SeedConfiguration("28,95", "4", enchidos: 10);
        await service.AdicionarQuantidadeAsync(new AdicionarQuantidadeRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 3));
        await service.AlterarEstadoAsync(new AlterarEstadoRequest(cfg.TampaoConfigurationId, TampaoBalanceKind.Enchidos, 2));

        var adds = (await service.ListMovimentosAsync(null, null, null, TampaoMovementType.Adicionar, null)).Value;
        Assert.Single(adds);
        Assert.Equal("adicionar", adds[0].MovementType);
    }
}