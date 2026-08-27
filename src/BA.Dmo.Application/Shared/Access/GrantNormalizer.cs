using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Server-side grant normalization (Plan-V3 GLM-ACC-02 "normalizeModules",
/// TD-10): moduleIds outside the catalog are discarded; capabilities are only
/// valid when they BELONG to the granted module as registered in the catalog
/// (capability ownership is the canonical catalog membership — GLM-CAT-03;
/// e.g. audit.view/audit.export belong to the admin module); duplicate module
/// entries are ignored (first occurrence prevails). Nonassignable technical
/// entries are discarded as direct grant targets.
/// Nothing is silently repaired: discarded entries are reported.
/// </summary>
public sealed class GrantNormalizer
{
    private readonly ModuleCatalog _catalog;

    public GrantNormalizer(ModuleCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public NormalizationResult Normalize(IEnumerable<ModuleGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        var normalized = new List<ModuleGrant>();
        var discarded = new List<string>();
        var seenModules = new HashSet<string>(StringComparer.Ordinal);

        foreach (var grant in grants)
        {
            if (grant is null)
            {
                discarded.Add("<null grant>");
                continue;
            }

            if (!_catalog.TryGetModule(grant.ModuleId, out var module))
            {
                discarded.Add($"module '{grant.ModuleId}' (unknown module id)");
                continue;
            }

            if (!module.IsAssignable)
            {
                discarded.Add($"module '{grant.ModuleId}' (module is not assignable)");
                continue;
            }

            // Duplicates ignored: first occurrence prevails (GLM-ACC-02).
            if (!seenModules.Add(module.ModuleId))
            {
                discarded.Add($"module '{grant.ModuleId}' (duplicate entry)");
                continue;
            }

            var ownedCapabilityIds = module.Capabilities
                .Select(c => c.Id)
                .ToHashSet(StringComparer.Ordinal);

            var validCapabilities = new List<string>();
            var seenCapabilities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capability in grant.Capabilities ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(capability))
                {
                    discarded.Add($"capability '<blank>' of module '{module.ModuleId}'");
                    continue;
                }

                if (!ownedCapabilityIds.Contains(capability))
                {
                    discarded.Add(
                        $"capability '{capability}' (does not belong to module '{module.ModuleId}')");
                    continue;
                }

                if (seenCapabilities.Add(capability))
                    validCapabilities.Add(capability);
            }

            normalized.Add(new ModuleGrant(module.ModuleId, validCapabilities));
        }

        return new NormalizationResult(normalized, discarded);
    }
}

/// <summary>Normalized grants plus an explicit discard report (nothing silent).</summary>
public sealed record NormalizationResult(
    IReadOnlyList<ModuleGrant> Grants,
    IReadOnlyList<string> DiscardedEntries);
