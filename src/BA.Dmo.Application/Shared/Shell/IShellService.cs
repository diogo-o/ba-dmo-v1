using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.Application.Shared.Shell;

/// <summary>
/// Per-request shell state (Plan-V3 GLM-SHL-01/02): the identity presentation
/// (display name + profile_title — never a permission source, UD-02) and the
/// navigation derived from the server-side resolved grants (GLM-SHL-03).
/// </summary>
public sealed record ShellState(
    string DisplayName,
    string? ProfileTitle,
    ShellNavigation Navigation);

/// <summary>
/// Shell state port consumed by the single application shell (05_SHL §1–2).
/// Resolved per request on the server; null = no resolved internal identity
/// (fail-closed safe state — no data, no tabs, no role fallback).
/// The implementation lives in the web layer (request-scoped), mirroring the
/// ICurrentUserAccessor pattern.
/// </summary>
public interface IShellService
{
    ShellState? Current { get; }
}
