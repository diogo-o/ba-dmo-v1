namespace BA.Dmo.Web.Cli;

/// <summary>
/// Execution mode of the BA.Dmo.Web process (Plan-V3 GLM-ARCH-15).
/// There is no separate CLI project: the web assembly distinguishes operational verbs
/// by process arguments.
/// </summary>
public enum CliMode
{
    /// <summary>Normal ASP.NET Core web startup (no operational verb supplied).</summary>
    Web,

    /// <summary>Forward-only schema migrations, CLI only (GLM-ARCH-15; runner implemented in U-02).</summary>
    Migrate,

    /// <summary>One-shot privileged bootstrap of the first Admin, CLI only (06_DATA §15; implemented in U-05).</summary>
    BootstrapAdmin
}
