using System;
using System.Collections.Generic;

namespace BA.Dmo.Web.Pages.JobOn;

/// <summary>
/// R011 — Deterministic machine/line → color mapping for the Job On Universal Landing
/// calendar + production list.
///
/// OWNER RULE (§4/§5): each of the six production lines (B1, B2, B3, C1, C2, C3) has ONE
/// stable visual color. The color identifies the MACHINE/LINE — it NEVER carries a semantic
/// status (error / NOK / warning / approval state / production problem). A same line always
/// resolves to the same key; different lines resolve to different keys; B1 never shares a
/// key with B2.
///
/// This is strictly a presentation-layer keyer. The exact hues are refined in the later
/// CSS/visual pass; the deterministic line→key mapping must not change. Keys map to the
/// <c>--dmo-line-*</c> design tokens (dmo-tokens.css), so the server-rendered calendar
/// markers and list rows share one vocabulary without inventing a second planning source.
/// </summary>
public static class JobOnLineColor
{
    /// <summary>Canonical, ordered lines of the platform (B1..C3).</summary>
    public static readonly IReadOnlyList<string> Lines = new[] { "B1", "B2", "B3", "C1", "C2", "C3" };

    /// <summary>All valid machines/lines (server-side allowed set).</summary>
    private static readonly HashSet<string> Allowed =
        new(Lines, StringComparer.Ordinal);

    /// <summary>
    /// Stable color token key for a machine/line. Never returns a semantic key. Values:
    /// B1→b1, B2→b2, B3→b3, C1→c1, C2→c2, C3→c3. Returns <c>null</c> for an unknown line.
    /// </summary>
    public static string? GetColorKey(string? machine)
    {
        if (string.IsNullOrWhiteSpace(machine))
            return null;

        var code = machine.Trim().ToUpperInvariant();
        return Allowed.Contains(code) ? code.ToLowerInvariant() : null;
    }

    /// <summary>CSS custom-property token name for a machine/line (e.g. "var(--dmo-line-b1)").</summary>
    public static string? GetColorToken(string? machine)
    {
        var key = GetColorKey(machine);
        return key is null ? null : $"var(--dmo-line-{key})";
    }

    /// <summary>Utility class applied to a marker element (e.g. "dmo-line-c1"). Null for unknown.</summary>
    public static string? GetLineClass(string? machine)
    {
        var key = GetColorKey(machine);
        return key is null ? null : $"dmo-line-{key}";
    }

    public static bool IsValid(string? machine) =>
        machine is not null && Allowed.Contains(machine.Trim().ToUpperInvariant());
}