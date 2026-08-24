using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Admin;

/// <summary>
/// U-06 audit-tab + catalog-mirror tests (Plan-V3 04_ACC §9, UD-17/TD-19,
/// GLM-CAT-02 rule 3): capability separation audit.view/audit.export,
/// canonical pagination, factual export without secrets, mirror constrained
/// to canonical modules with audit of changes.
/// </summary>
public class AdminAuditAndMirrorTests
{
    private readonly FakeAdminRepository _repository = new();
    private readonly FakeMirrorRepository _mirror = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly AdminAuditService _auditService;
    private readonly AdminMirrorService _mirrorService;

    public AdminAuditAndMirrorTests()
    {
        var gate = new AdminAuthorizationGate(_identity);
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));
        _auditService = new AdminAuditService(gate, _repository);
        _mirrorService = new AdminMirrorService(
            gate, CanonicalModuleCatalog.Instance, _mirror, _repository, clock);
    }

    // ---- audit tab ----------------------------------------------------------

    [Fact]
    public async Task AuditQuery_RequiresAuditView_AndUsesCanonicalPagination()
    {
        _identity.GrantCapabilities(Array.Empty<string>()); // no audit.view

        var denied = await _auditService.QueryAsync(DefaultFilter());
        Assert.True(denied.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, denied.Error.Category);

        _identity.GrantCapabilities(new[] { "audit.view" });
        var invalidSize = await _auditService.QueryAsync(DefaultFilter() with { PageSize = 15 });
        Assert.True(invalidSize.IsFailure);
        Assert.Equal("AUDIT_PAGE_SIZE_INVALID", invalidSize.Error.Code);

        var valid = await _auditService.QueryAsync(DefaultFilter() with { PageSize = 40 });
        Assert.True(valid.IsSuccess);
        Assert.Equal(40, valid.Value.PageSize);
    }

    [Fact]
    public async Task AuditExport_RequiresAuditExport_AndNeverCarriesSecrets()
    {
        _repository.Audits.Add(new AuditEntry(
            new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
            "admin-1", "Administrador", "admin", "create",
            "internal_user", "user-9", "Novo Utilizador", "succeeded", null));

        // audit.view alone is not enough for export (distinct capability).
        _identity.GrantCapabilities(new[] { "audit.view" });
        var denied = await _auditService.ExportAsync(DefaultFilter());
        Assert.True(denied.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, denied.Error.Category);

        _identity.GrantCapabilities(new[] { "audit.view", "audit.export" });
        var export = await _auditService.ExportAsync(DefaultFilter());
        Assert.True(export.IsSuccess);
        Assert.Contains("user-9", export.Value);
    }

    [Fact]
    public async Task AuditExport_FactualContentOnly()
    {
        _repository.Audits.Add(new AuditEntry(
            new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
            "admin-1", "Administrador", "admin", "password_reset_request",
            "internal_user", "user-9", "Utilizador", "succeeded", null));
        _identity.GrantCapabilities(new[] { "audit.export" });

        var export = await _auditService.ExportAsync(DefaultFilter());

        Assert.True(export.IsSuccess);
        Assert.Contains("password_reset_request", export.Value);
        // No password/token/service-role material may exist in the export.
        Assert.DoesNotContain("password=", export.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("service_role", export.Value, StringComparison.OrdinalIgnoreCase);
    }

    // ---- catalog mirror ------------------------------------------------------

    [Fact]
    public async Task MirrorSave_UnknownModule_IsRejected_NothingPersisted()
    {
        _identity.GrantCapabilities(new[] { "admin.gerir" });

        var result = await _mirrorService.SaveDisplayAsync(new[]
        {
            new MirrorEntryInput("jobon", 1, true),
            new MirrorEntryInput("ghost_module", 2, true)
        });

        Assert.True(result.IsFailure);
        Assert.Equal("CATALOG_MIRROR_INVALID", result.Error.Code);
        Assert.Empty(_mirror.Upserted);
    }

    [Fact]
    public async Task MirrorSave_CanonicalEntries_PersistAndAudit()
    {
        _identity.GrantCapabilities(new[] { "admin.gerir" });

        var result = await _mirrorService.SaveDisplayAsync(new[]
        {
            new MirrorEntryInput("tampoes", 1, true),
            new MirrorEntryInput("jobon", 2, true)
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _mirror.Upserted.Count);
        var audit = Assert.Single(_repository.Audits);
        Assert.Equal("mirror_update", audit.ActionCode);
        // Display entries honor the saved mirror order.
        Assert.Equal("tampoes", result.Value[0].Module.ModuleId);
    }

    private static AuditQueryFilter DefaultFilter() => new(
        Year: 2026, ActorUserId: null, ModuleId: null, ActionCode: null,
        Result: null, FromUtc: null, ToUtc: null, Page: 1, PageSize: 20);

    private sealed class FakeMirrorRepository : IModuleCatalogMirrorRepository
    {
        public List<ModuleCatalogMirrorRow> Upserted { get; } = [];

        public Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleCatalogMirrorRow>>(Upserted);

        public Task UpsertAllAsync(
            IReadOnlyList<ModuleCatalogMirrorRow> rows,
            CancellationToken cancellationToken = default)
        {
            Upserted.Clear();
            Upserted.AddRange(rows);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; private set; }

        public CurrentUser? Current => User;

        public void GrantCapabilities(IEnumerable<string> capabilities) =>
            User = new CurrentUser(
                Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
                "Utilizador", new[] { "admin" }, capabilities);
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
