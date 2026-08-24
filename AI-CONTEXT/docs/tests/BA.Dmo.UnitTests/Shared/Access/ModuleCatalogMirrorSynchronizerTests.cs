using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 catalog mirror tests (Plan-V3 TD-10, GLM-ACC-03, GLM-CAT-02 rule 3):
/// code catalog is the source of truth; the mirror serves Admin display only;
/// unknown mirror entries are discarded explicitly; the DB never redefines
/// canonical values. DB round-trip behavior is covered by the repository port
/// contract — no live database in U-04.
/// </summary>
public class ModuleCatalogMirrorSynchronizerTests
{
    private readonly ModuleCatalogMirrorSynchronizer _synchronizer =
        new(CanonicalModuleCatalog.Instance);

    private static readonly DateTimeOffset SyncInstant =
        new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildSyncRows_MirrorsTheCanonicalCatalog_InCanonicalOrder()
    {
        var rows = _synchronizer.BuildSyncRows(SyncInstant);

        Assert.Equal(12, rows.Count);
        Assert.Equal(
            CanonicalModuleCatalog.Instance.Modules.Select(m => m.ModuleId),
            rows.Select(r => r.ModuleId));
        Assert.All(rows, r =>
        {
            Assert.True(r.Active);
            Assert.Equal(SyncInstant, r.SyncedAtUtc);
        });
        Assert.Equal(
            CanonicalModuleCatalog.Instance.Modules.Select(m => m.DisplayName),
            rows.Select(r => r.DisplayName));
    }

    [Fact]
    public void ValidateMirrorRows_DiscardsUnknownModules_WithReport()
    {
        var report = _synchronizer.ValidateMirrorRows(new[]
        {
            new ModuleCatalogMirrorRow("jobon", "Job On", 5, true, SyncInstant),
            new ModuleCatalogMirrorRow("ghost", "Fantasma", 6, true, SyncInstant)
        });

        var valid = Assert.Single(report.ValidRows);
        Assert.Equal("jobon", valid.ModuleId);
        var discarded = Assert.Single(report.DiscardedRows);
        Assert.Contains("ghost", discarded, StringComparison.Ordinal);
        Assert.Contains("unknown module id", discarded, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateMirrorRows_DiscardsDuplicateRows()
    {
        var report = _synchronizer.ValidateMirrorRows(new[]
        {
            new ModuleCatalogMirrorRow("jobon", "Job On", 5, true, SyncInstant),
            new ModuleCatalogMirrorRow("jobon", "Job On (cópia)", 6, true, SyncInstant)
        });

        Assert.Single(report.ValidRows);
        var discarded = Assert.Single(report.DiscardedRows);
        Assert.Contains("duplicate mirror row", discarded, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeForDisplay_HonorsAdminMirrorOrder_ForKnownModulesOnly()
    {
        // GLM-CAT-02 rule 3: Administration may adjust mirror order among
        // active modules; authorization is unaffected.
        var merged = _synchronizer.MergeForDisplay(new[]
        {
            new ModuleCatalogMirrorRow("tampoes", "Tampões", 1, true, SyncInstant),
            new ModuleCatalogMirrorRow("jobon", "Job On", 2, true, SyncInstant),
            new ModuleCatalogMirrorRow("ghost", "Fantasma", 0, true, SyncInstant)
        });

        // Ghost discarded; mirror order honored; remaining canonical modules
        // appended in canonical order.
        Assert.Equal("tampoes", merged[0].Module.ModuleId);
        Assert.Equal("jobon", merged[1].Module.ModuleId);
        Assert.DoesNotContain(merged, e => e.Module.ModuleId == "ghost");
        Assert.Equal(12, merged.Count);

        var appended = merged.Skip(2).Select(e => e.Module.ModuleId).ToArray();
        Assert.Equal(
            CanonicalModuleCatalog.Instance.Modules
                .Select(m => m.ModuleId)
                .Where(id => id is not ("tampoes" or "jobon" or "ghost")),
            appended);
    }

    [Fact]
    public void MergeForDisplay_EmptyMirror_YieldsFullCanonicalOrder()
    {
        var merged = _synchronizer.MergeForDisplay([]);

        Assert.Equal(
            CanonicalModuleCatalog.Instance.Modules.Select(m => m.ModuleId),
            merged.Select(e => e.Module.ModuleId));
        Assert.All(merged, e => Assert.True(e.Active));
    }

    [Fact]
    public void MergeForDisplay_PreservesMirrorActivation_ForDisplayOnly()
    {
        var merged = _synchronizer.MergeForDisplay(new[]
        {
            new ModuleCatalogMirrorRow("boquilhas", "Boquilhas", 10, Active: false, SyncInstant)
        });

        var boquilhas = merged.Single(e => e.Module.ModuleId == "boquilhas");
        Assert.False(boquilhas.Active);
    }
}
