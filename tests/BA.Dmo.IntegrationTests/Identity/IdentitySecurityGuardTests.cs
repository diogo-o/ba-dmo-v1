using System.Reflection;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// U-05 security architecture guards (Plan-V3 PV-07, GLM-ARCH-14/18,
/// GLM-ACC-01.5). These protect material security contracts, not style:
/// privileged provisioning stays out of the web pipeline, the session cookie
/// carries no grants/roles, and no role-name identity surface exists.
/// </summary>
public class IdentitySecurityGuardTests
{
    [Fact]
    public void ProvisioningAdapter_IsNeverHeldByWebTypes_PagesAndHandlersIncluded()
    {
        // Corrected premise (LO-5/HI-4): since TD-16 the provisioning adapter
        // is NOT exclusive to the bootstrap CLI — a separate instance is
        // registered in the Web pipeline for the admin.gerir-gated user
        // create / password-reset use cases (consumed by the Application
        // layer, not by pages). The contract this guard enforces is that NO
        // type in the Web assembly (page models, handlers, middleware, CLI
        // aside) ever holds the service_role adapter directly in a
        // constructor parameter or field.
        var webAssembly = typeof(Program).Assembly;

        var offenders = webAssembly.GetTypes()
            .Where(type => type != typeof(BA.Dmo.Web.Cli.BootstrapAdminCommand))
            .Where(type =>
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == typeof(IAdminProvisioningAdapter))
                || type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Any(f => f.FieldType == typeof(IAdminProvisioningAdapter))
                || type.BaseType == typeof(PageModel) &&
                    type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                        .SelectMany(c => c.GetParameters())
                        .Any(p => p.ParameterType == typeof(IAdminProvisioningAdapter)))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "IAdminProvisioningAdapter (service_role) must never be held directly by a Web " +
            "type (pages/handlers included); only the Application-layer privileged use " +
            $"cases and the bootstrap CLI may consume it. Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void SessionCookieContract_CarriesOnlyTheAuthUserId_NoGrantsOrRoles()
    {
        // GLM-ACC-01.5: grants are never persisted in the cookie. The claim
        // contract must stay a single auth-user-id claim — no role/grant/
        // capability/module claims may be introduced.
        var claimConstants = typeof(SessionClaims)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                && f.Name.Contains("Claim", StringComparison.OrdinalIgnoreCase))
            .Select(f => (Name: f.Name, Value: (string)f.GetValue(null)!))
            .ToList();

        var single = Assert.Single(claimConstants);
        Assert.Equal(SessionClaims.AuthUserIdClaimType, single.Value);

        var forbidden = new[] { "role", "grant", "capability", "module", "admin", "template" };
        Assert.DoesNotContain(claimConstants, c =>
            forbidden.Any(marker =>
                c.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
                || c.Value.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ApplicationLayer_HasNoProviderSpecificDependencies()
    {
        // PV-06: Application depends on ports only — no Supabase SDK/HTTP
        // provider types, no Infrastructure implementations.
        var applicationAssembly =
            typeof(BA.Dmo.Application.Shared.Access.CanonicalModuleCatalog).Assembly;

        var offenders = applicationAssembly.GetReferencedAssemblies()
            .Where(name =>
                name.Name!.Contains("Supabase", StringComparison.OrdinalIgnoreCase)
                || name.Name.StartsWith("BA.Dmo.Infrastructure", StringComparison.Ordinal)
                || name.Name.StartsWith("BA.Dmo.Web", StringComparison.Ordinal))
            .Select(name => name.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Application must not reference provider/infrastructure assemblies. Offenders: {string.Join("; ", offenders)}");
    }
}
