namespace BA.Dmo.Infrastructure.Auth;

/// <summary>
/// Server-side Supabase configuration contract (Plan-V3 06_DATA §6.5/§14:
/// credentials in user secrets/environment only — never in the repository,
/// never browser-visible). Plan-V3 leaves exact variable names open; these
/// are the established fresh-build names. The service-role key is ONLY ever
/// consumed by the bootstrap provisioning path (PV-07).
/// </summary>
public static class SupabaseSettings
{
    public const string UrlVariable = "BA_DMO_SUPABASE_URL";
    public const string AnonKeyVariable = "BA_DMO_SUPABASE_ANON_KEY";
    public const string ServiceRoleKeyVariable = "BA_DMO_SUPABASE_SERVICE_ROLE_KEY";
    public const string BootstrapEmailVariable = "BA_DMO_BOOTSTRAP_ADMIN_EMAIL";
    public const string BootstrapPasswordVariable = "BA_DMO_BOOTSTRAP_ADMIN_PASSWORD";
    public const string BootstrapDisplayNameVariable = "BA_DMO_BOOTSTRAP_ADMIN_NAME";

    public static string? ResolveUrl(Func<string, string?> environment) =>
        NullIfBlank(environment(UrlVariable));

    public static string? ResolveAnonKey(Func<string, string?> environment) =>
        NullIfBlank(environment(AnonKeyVariable));

    public static string? ResolveServiceRoleKey(Func<string, string?> environment) =>
        NullIfBlank(environment(ServiceRoleKeyVariable));

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
