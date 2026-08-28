using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Display entry derived from the catalog + mirror for the Admin UI.
/// </summary>
public sealed record MirrorDisplayEntry(
    ModuleDefinition Module,
    int DisplayOrder,
    bool Active);

/// <summary>
/// Mirror synchronization contract (Plan-V3 TD-10, GLM-ACC-03): the catalog
/// IN CODE is the source of truth; the DB mirror serves Admin ordering/display
/// only. The DB never redefines canonical values: synchronization flows from
/// code → mirror, and mirror rows for unknown modules are discarded
/// explicitly (never silently merged into the catalog).
/// </summary>
public sealed class ModuleCatalogMirrorSynchronizer
{
    private readonly ModuleCatalog _catalog;

    public ModuleCatalogMirrorSynchronizer(ModuleCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// Validates mirror rows against the catalog: rows for unknown modules are
    /// invalid and reported (discarded), never accepted.
    /// </summary>
    public MirrorValidationReport ValidateMirrorRows(IEnumerable<ModuleCatalogMirrorRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var valid = new List<ModuleCatalogMirrorRow>();
        var discarded = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row is null)
            {
                discarded.Add("<null row>");
                continue;
            }

            if (!_catalog.ContainsModule(row.ModuleId))
            {
                discarded.Add($"module '{row.ModuleId}' (unknown module id)");
                continue;
            }

            if (!seen.Add(row.ModuleId))
            {
                discarded.Add($"module '{row.ModuleId}' (duplicate mirror row)");
                continue;
            }

            valid.Add(row);
        }

        return new MirrorValidationReport(valid, discarded);
    }

    /// <summary>
    /// Effective Admin display list: mirror order/activation is honored for
    /// modules KNOWN to the catalog (Administration may adjust order/active
    /// within the mirror — GLM-CAT-02 rule 3); unknown rows are discarded;
    /// catalog modules missing from the mirror are appended in canonical
    /// order. Authorization is never affected (mirror is display-only).
    /// </summary>
    public IReadOnlyList<MirrorDisplayEntry> MergeForDisplay(
        IEnumerable<ModuleCatalogMirrorRow> mirrorRows)
    {
        ArgumentNullException.ThrowIfNull(mirrorRows);

        var validated = ValidateMirrorRows(mirrorRows);
        var byModule = validated.ValidRows.ToDictionary(r => r.ModuleId, StringComparer.Ordinal);

        var entries = new List<MirrorDisplayEntry>();
        var included = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in validated.ValidRows.OrderBy(r => r.DisplayOrder)
                     .ThenBy(r => r.ModuleId, StringComparer.Ordinal))
        {
            if (!_catalog.TryGetModule(row.ModuleId, out var module))
                continue;

            entries.Add(new MirrorDisplayEntry(module, row.DisplayOrder, row.Active));
            included.Add(row.ModuleId);
        }

        var canonicalTailStart = entries.Count == 0
            ? 0
            : entries.Max(e => e.DisplayOrder) + 1;
        foreach (var module in _catalog.Modules.Where(m => !included.Contains(m.ModuleId)))
        {
            entries.Add(new MirrorDisplayEntry(
                module,
                Math.Max(module.CanonicalOrder, canonicalTailStart),
                Active: true));
        }

        return entries;
    }
}

/// <summary>Mirror validation outcome with an explicit discard report.</summary>
public sealed record MirrorValidationReport(
    IReadOnlyList<ModuleCatalogMirrorRow> ValidRows,
    IReadOnlyList<string> DiscardedRows);
