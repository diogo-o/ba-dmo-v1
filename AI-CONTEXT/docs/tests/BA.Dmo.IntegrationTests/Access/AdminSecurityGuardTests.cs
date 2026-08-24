using System.Reflection;
using BA.Dmo.Application.Shared.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-06 security/architecture guards (PV-07, GLM-ARCH-14/18, U-06 guard
/// list). Material contracts only: privileged provisioning isolation and
/// capability-based Admin authorization surface.
/// </summary>
public class AdminSecurityGuardTests
{
    [Fact]
    public void PrivilegedProvisioning_IsNotReachableFromAdminPages()
    {
        // The service_role adapter (IAdminProvisioningAdapter) must never be
        // a dependency of Razor pages/handlers — it lives only in the
        // bootstrap-admin CLI path and the Admin user-creation use case.
        var webAssembly = typeof(Program).Assembly;

        var offenders = webAssembly.GetTypes()
            .Where(type => typeof(PageModel).IsAssignableFrom(type))
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(ctor => ctor.GetParameters())
            .Where(p => p.ParameterType == typeof(IAdminProvisioningAdapter)
                || p.ParameterType == typeof(BA.Dmo.Infrastructure.Auth.SupabaseAdminProvisioningAdapter))
            .Select(p => $"{p.Member.DeclaringType?.FullName}.{p.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Admin pages must not depend on the privileged provisioning adapter. " +
            $"Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void AdminPages_AuthorizeViaCanonicalCapabilityPolicies_NotRoleNames()
    {
        // Every Admin page carries an [Authorize(Policy = ...)] attribute
        // (applied to the compiled Razor page class via @attribute) built on
        // canonical capabilities; no page authorizes by role name.
        var webAssembly = typeof(Program).Assembly;

        var adminPages = webAssembly.GetTypes()
            .Where(type => typeof(Microsoft.AspNetCore.Mvc.RazorPages.Page).IsAssignableFrom(type))
            .Where(type => type.Namespace?.Contains(".Pages.Admin", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(adminPages);
        foreach (var page in adminPages)
        {
            var authorize = page.GetCustomAttributes(
                    typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
                .ToList();

            var attribute = Assert.Single(authorize);
            Assert.False(string.IsNullOrEmpty(attribute.Policy),
                $"{page.FullName} must authorize via a capability policy.");
            Assert.True(attribute.Roles is null or "",
                $"{page.FullName} must not authorize by role names.");
        }
    }

    [Fact]
    public void ApplicationAdminServices_HaveNoProviderSpecificDependencies()
    {
        // Admin use cases depend on ports only (PV-06): no Supabase/HTTP
        // provider types in their constructor surface.
        var adminServices = typeof(BA.Dmo.Application.Modules.Admin.AdminUserService)
            .Assembly.GetTypes()
            .Where(type => type.Namespace == "BA.Dmo.Application.Modules.Admin")
            .Where(type => type.IsClass && type.Name.StartsWith("Admin", StringComparison.Ordinal));

        var offenders = adminServices
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(ctor => ctor.GetParameters())
            .Where(p =>
                p.ParameterType.Name.Contains("HttpClient", StringComparison.Ordinal)
                || p.ParameterType.Name.Contains("Supabase", StringComparison.OrdinalIgnoreCase)
                || p.ParameterType.Namespace?.StartsWith("BA.Dmo.Infrastructure", StringComparison.Ordinal) == true)
            .Select(p => $"{p.Member.DeclaringType?.FullName}.{p.Name}")
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Admin services must depend on ports only. Offenders: {string.Join("; ", offenders)}");
    }
}
