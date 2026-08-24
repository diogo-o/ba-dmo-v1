using BA.Dmo.Application.Modules.Historia;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Historia;

/// <summary>
/// U-18 — História service tests (modules/11 GLM-HIST-07 unit: projection
/// assembly from the read contract; stable chronological ordering; grouping).
/// The service forwards the TD-24 resolved scope to the repository and keeps the
/// read pure — it never writes.
/// </summary>
public class HistoriaServiceTests
{
    [Fact]
    public async Task QueryAsync_AuthorizesAndForwardsScopeToRepository()
    {
        var current = HistoriaCurrentUser.WithModules("peso", "tampoes");
        var repos = new FakeHistoriaRepository();
        var service = new HistoriaService(
            new HistoriaAuthorizationGate(current), repos);

        var filter = new HistoriaFilter(null, null, null, null, null, null, null, null, null, 1, 20);
        var result = await service.QueryAsync(filter);

        Assert.True(result.IsSuccess);
        // TD-24: the repository received exactly the granted origin-module scope.
        Assert.Equal(new[] { "peso", "tampoes" },
            repos.LastVisibleModules);
        Assert.False(repos.LastIncludeAdmin);
    }

    [Fact]
    public async Task QueryAsync_WithAuditView_OrdersChronologicallyStableAndGroupsByEntity()
    {
        var current = HistoriaCurrentUser.WithModules("peso");
        var gate = new HistoriaAuthorizationGate(current);
        var service = new HistoriaService(gate, new FakeHistoriaRepository
        {
            Result = NewGroupedResult()
        });

        var result = await service.QueryAsync(
            new HistoriaFilter(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.True(result.IsSuccess);
        // Two entities; each group is internally chronological DESC (newest first).
        var groups = result.Value.Groups;
        Assert.Equal(2, groups.Count);
        var armazem = groups.Single(g => g.ModuleId == "armazem");
        Assert.Equal("2026-08-17 18:30", armazem.Events[0].OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"));
        Assert.Equal("2026-08-17 18:00", armazem.Events[1].OccurredAtUtc.ToString("yyyy-MM-dd HH:mm"));
    }

    [Fact]
    public async Task QueryAsync_InvalidPageSize_IsValidationError()
    {
        var service = new HistoriaService(
            new HistoriaAuthorizationGate(HistoriaCurrentUser.WithModules("peso")),
            new FakeHistoriaRepository());

        var result = await service.QueryAsync(
            new HistoriaFilter(null, null, null, null, null, null, null, null, null, 1, 25));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
    }

    [Fact]
    public async Task QueryAsync_WithoutHistoriaModule_IsForbidden()
    {
        var service = new HistoriaService(
            new HistoriaAuthorizationGate(HistoriaCurrentUser.WithoutHistoriaModule()),
            new FakeHistoriaRepository());

        var result = await service.QueryAsync(
            new HistoriaFilter(null, null, null, null, null, null, null, null, null, 1, 20));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    private static HistoriaQueryResult NewGroupedResult()
    {
        var armazem = new HistoriaGroupRow(
            "armazem|whl-1", "Lote WHL-1", "armazem", "lote", "whl-1",
            new[]
            {
                Entry("2026-08-17 18:30", "warehouse.entry.created", "armazem"),
                Entry("2026-08-17 18:00", "warehouse.entry.created", "armazem")
            });
        var peso = new HistoriaGroupRow(
            "peso|ctl-1", "Controlo CTL-1", "peso", "controlo", "ctl-1",
            new[] { Entry("2026-08-17 19:00", "weight.control.approved", "peso") });
        return new HistoriaQueryResult(
            new[] { peso, armazem }, 2, 1, 20);
    }

    private static HistoriaEntryRow Entry(string whenUtc, string action, string module) =>
        new(
            OccurredAtUtc: DateTimeOffset.Parse(whenUtc + "Z"),
            Year: 2026,
            ActorUserId: "actor-1",
            ActorNameSnapshot: "Operador A",
            ModuleId: module,
            ActionCode: action,
            EntityType: "lote",
            EntityId: "id",
            EntityLabelSnapshot: "label",
            Result: "succeeded",
            Reason: null,
            JobOnId: null,
            RevisionId: null,
            BeforeSummary: null,
            AfterSummary: null);
}

/// <summary>Fake História read port (confined to tests/*).</summary>
public sealed class FakeHistoriaRepository : IHistoriaRepository
{
    public IReadOnlyCollection<string>? LastVisibleModules { get; private set; }
    public bool LastIncludeAdmin { get; private set; }
    public HistoriaQueryResult? Result { get; init; }

    public Task<HistoriaQueryResult> QueryAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default)
    {
        LastVisibleModules = visibleModuleIds;
        LastIncludeAdmin = includeAdminWithAuditView;
        return Task.FromResult(Result ?? new HistoriaQueryResult(
            Array.Empty<HistoriaGroupRow>(), 0, filter.Page, filter.PageSize));
    }

    public Task<IReadOnlyList<HistoriaEntryRow>> QueryFlatAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default)
    {
        LastVisibleModules = visibleModuleIds;
        LastIncludeAdmin = includeAdminWithAuditView;
        return Task.FromResult<IReadOnlyList<HistoriaEntryRow>>(Array.Empty<HistoriaEntryRow>());
    }
}