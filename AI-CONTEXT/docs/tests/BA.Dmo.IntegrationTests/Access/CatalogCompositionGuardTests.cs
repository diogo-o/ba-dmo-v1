using System.Reflection;
using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-04 composition guard: the canonical catalog configuration wired into
/// the application is valid (fails explicitly otherwise), and catalog
/// definitions live in Application — never inside the web UI assembly.
/// </summary>
public class CatalogCompositionGuardTests
{
    [Fact]
    public void CanonicalConfiguration_WiredAtStartup_IsValid()
    {
        // Program.cs runs the same validation at composition; a broken
        // canonical catalog must fail this test loudly.
        CatalogValidator.Validate(
            CanonicalModuleCatalog.Instance,
            CanonicalPageCatalog.Instance,
            CanonicalModuleCatalog.AreaChildren);
    }

    [Fact]
    public void CatalogDefinitions_DoNotLiveInTheWebAssembly()
    {
        var webAssembly = typeof(Program).Assembly;

        var offenders = webAssembly.GetTypes()
            .Where(type =>
                type.Name.Contains("ModuleCatalog", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("PageCatalog", StringComparison.OrdinalIgnoreCase)
                || type.Name.Contains("PageDefinition", StringComparison.OrdinalIgnoreCase))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Catalog definitions belong to Application/Domain, not Web. " +
            $"Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void LandingPolicy_IsSingleAndGlobal()
    {
        // UD-16/DS-01: exactly one landing, owned by Job On; nothing else may
        // claim landing semantics (no Boquilhas/Admin/role-based landing).
        var landings = CanonicalPageCatalog.Instance.Pages.Where(p => p.IsLanding).ToList();

        var landing = Assert.Single(landings);
        Assert.Equal(CanonicalModuleCatalog.JobonModuleId, landing.ModuleId);
        Assert.Equal("/jobon", landing.Route);
    }

    [Fact]
    public void MirrorPort_IsImplementedInInfrastructure_WithU03FactoryContract()
    {
        var repositoryType = typeof(BA.Dmo.Infrastructure.Access.DapperModuleCatalogMirrorRepository);

        Assert.True(typeof(IModuleCatalogMirrorRepository).IsAssignableFrom(repositoryType));
        Assert.Contains(
            repositoryType.GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(BA.Dmo.Application.Shared.Persistence.IDbConnectionFactory));
    }
}
