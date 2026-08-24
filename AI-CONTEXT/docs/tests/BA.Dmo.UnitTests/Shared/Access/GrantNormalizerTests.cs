using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 grant normalization tests (Plan-V3 GLM-ACC-02 normalizeModules,
/// TD-10; roadmap U-04 "normalização: duplicados, prefixos, entradas
/// inválidas descartadas"). Discards are explicit — nothing silent.
/// </summary>
public class GrantNormalizerTests
{
    private readonly GrantNormalizer _normalizer = new(CanonicalModuleCatalog.Instance);

    [Fact]
    public void KnownModuleGrant_IsPreserved()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("boquilhas", [])
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal("boquilhas", grant.ModuleId);
        Assert.Empty(grant.Capabilities);
        Assert.Empty(result.DiscardedEntries);
    }

    [Fact]
    public void UnknownModuleId_IsDiscarded_AndReported()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("invented_module", []),
            new ModuleGrant("boquilhas", [])
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal("boquilhas", grant.ModuleId);
        var discard = Assert.Single(result.DiscardedEntries);
        Assert.Contains("invented_module", discard, StringComparison.Ordinal);
        Assert.Contains("unknown module id", discard, StringComparison.Ordinal);
    }

    [Fact]
    public void CapabilityNotOwnedByTheGrantedModule_IsDiscarded()
    {
        // Scenario 14 (GLM-ACC-07): capability assigned to the wrong module is
        // rejected by server-side validation.
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("boquilhas", new[] { "peso.aprovar" })
        });

        var grant = Assert.Single(result.Grants);
        Assert.Empty(grant.Capabilities);
        var discard = Assert.Single(result.DiscardedEntries);
        Assert.Contains("peso.aprovar", discard, StringComparison.Ordinal);
        Assert.Contains("does not belong to module 'boquilhas'", discard, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedCapability_IsPreserved()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("peso", new[] { "peso.aprovar" })
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal(["peso.aprovar"], grant.Capabilities);
        Assert.Empty(result.DiscardedEntries);
    }

    [Fact]
    public void AuditCapabilities_AreValidOnlyUnderTheAdminModule()
    {
        // GLM-CAT-03 registers audit.view/audit.export under the admin module.
        var valid = _normalizer.Normalize(new[]
        {
            new ModuleGrant("admin", new[] { "admin.gerir", "audit.view", "audit.export" })
        });
        var invalid = _normalizer.Normalize(new[]
        {
            new ModuleGrant("historia", new[] { "audit.view" })
        });

        Assert.Equal(3, Assert.Single(valid.Grants).Capabilities.Count);
        Assert.Empty(Assert.Single(invalid.Grants).Capabilities);
    }

    [Fact]
    public void DuplicateModuleEntries_FirstPrevails_AndLaterAreDiscarded()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("peso", new[] { "peso.aprovar" }),
            new ModuleGrant("peso", new string[0])
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal(["peso.aprovar"], grant.Capabilities);
        var discard = Assert.Single(result.DiscardedEntries);
        Assert.Contains("duplicate entry", discard, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateCapabilities_AreDeduplicated()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("jobon", new[] { "jobon.edit", "jobon.edit" })
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal(["jobon.edit"], grant.Capabilities);
    }

    [Fact]
    public void FunctionalArea_GrantIsDiscarded()
    {
        // Controlo has no grants of its own (GLM-CAT-01/GLM-CTR-01).
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("controlo", [])
        });

        Assert.Empty(result.Grants);
        var discard = Assert.Single(result.DiscardedEntries);
        Assert.Contains("functional area has no grants", discard, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankCapabilities_AreDiscarded()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("peso", new[] { " ", "" })
        });

        Assert.Empty(Assert.Single(result.Grants).Capabilities);
        Assert.Equal(2, result.DiscardedEntries.Count);
    }
}
