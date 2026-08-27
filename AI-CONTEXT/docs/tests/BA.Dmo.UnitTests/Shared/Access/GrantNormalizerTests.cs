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
    public void CapabilityOnAssignableModule_IsPreservedBySyntaxNormalization()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("jobon", new[] { "jobon.edit" })
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal(["jobon.edit"], grant.Capabilities);
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
        Assert.Empty(invalid.Grants);
        Assert.Contains(invalid.DiscardedEntries, entry =>
            entry.Contains("not assignable", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateModuleEntries_FirstPrevails_AndLaterAreDiscarded()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("jobon", new[] { "jobon.edit" }),
            new ModuleGrant("jobon", [])
        });

        var grant = Assert.Single(result.Grants);
        Assert.Equal(["jobon.edit"], grant.Capabilities);
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
    public void NonassignableTechnicalEntries_AreDiscarded()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("peso", []),
            new ModuleGrant("pegamentos", []),
            new ModuleGrant("historia", [])
        });

        Assert.Empty(result.Grants);
        Assert.Equal(3, result.DiscardedEntries.Count);
        Assert.All(result.DiscardedEntries,
            discard => Assert.Contains("module is not assignable", discard, StringComparison.Ordinal));
    }

    [Fact]
    public void BlankCapabilities_AreDiscarded()
    {
        var result = _normalizer.Normalize(new[]
        {
            new ModuleGrant("jobon", new[] { " ", "" })
        });

        Assert.Empty(Assert.Single(result.Grants).Capabilities);
        Assert.Equal(2, result.DiscardedEntries.Count);
    }
}
