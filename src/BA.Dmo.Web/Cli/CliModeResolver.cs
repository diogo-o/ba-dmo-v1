namespace BA.Dmo.Web.Cli;

/// <summary>
/// Resolves the execution mode from the process arguments (Plan-V3 GLM-ARCH-15):
/// <code>
/// migrate            → dotnet BA.Dmo.Web.dll migrate
/// bootstrap-admin    → dotnet BA.Dmo.Web.dll bootstrap-admin
/// (omissão)          → normal web startup
/// </code>
/// Unknown leading arguments fall back to normal web startup so that hosting parameters
/// (e.g. --urls) keep working; operational verbs are matched case-insensitively.
/// </summary>
public static class CliModeResolver
{
    public const string MigrateVerb = "migrate";
    public const string BootstrapAdminVerb = "bootstrap-admin";

    public static CliMode Resolve(string[]? args)
    {
        var verb = args is { Length: > 0 } ? args[0] : null;
        if (string.IsNullOrWhiteSpace(verb))
            return CliMode.Web;

        return string.Equals(verb, MigrateVerb, StringComparison.OrdinalIgnoreCase)
            ? CliMode.Migrate
            : string.Equals(verb, BootstrapAdminVerb, StringComparison.OrdinalIgnoreCase)
                ? CliMode.BootstrapAdmin
                : CliMode.Web;
    }
}
