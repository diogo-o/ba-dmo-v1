using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// The Job On families backed by the Ferramentas tool register: CM
/// (contra-molde, component family <c>MP_CM</c>), MF (molde final) and BQ
/// (boquilha). CM, MF and BQ are DISTINCT tool types with separate identities
/// and histories — the same reference code registered under a different type is
/// a different tool and never merges. PU/CS/TP are Job On production-specific
/// manual configuration (Manual 10 §6.1), NOT register-backed tool selections.
/// </summary>
public static class JobOnToolSelectionFamilies
{
    /// <summary>Maps a card/family code (CM/MF/BQ) to its component family + tool type.</summary>
    public static bool TryParse(string? family, out ComponentFamily componentFamily, out FerramentasToolType toolType)
    {
        componentFamily = default;
        toolType = default;
        if (string.IsNullOrWhiteSpace(family))
            return false;
        switch (family.Trim().ToUpperInvariant())
        {
            case "CM":
                componentFamily = ComponentFamily.MP_CM;
                toolType = FerramentasToolType.CM;
                return true;
            case "MF":
                componentFamily = ComponentFamily.MF;
                toolType = FerramentasToolType.MF;
                return true;
            case "BQ":
                componentFamily = ComponentFamily.BQ;
                toolType = FerramentasToolType.BQ;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Maps a component family to its register tool type (only for register-backed families).</summary>
    public static bool TryGetToolType(ComponentFamily family, out FerramentasToolType toolType)
    {
        toolType = default;
        switch (family)
        {
            case ComponentFamily.MP_CM:
                toolType = FerramentasToolType.CM;
                return true;
            case ComponentFamily.MF:
                toolType = FerramentasToolType.MF;
                return true;
            case ComponentFamily.BQ:
                toolType = FerramentasToolType.BQ;
                return true;
            default:
                return false;
        }
    }

    /// <summary>The tool card code of a register-backed family (CM/MF/BQ); null otherwise.</summary>
    public static string? CardCode(ComponentFamily family) => family switch
    {
        ComponentFamily.MP_CM => "CM",
        ComponentFamily.MF => "MF",
        ComponentFamily.BQ => "BQ",
        _ => null
    };
}

/// <summary>
/// Tool selection options for one Job On + family: the REAL registered
/// (tipo, referência, lote, máquina/linha) combinations valid for this Job
/// On's machine/line. Job On does not own these tools — the options are a
/// read-only projection of the Ferramentas register (N04).
/// </summary>
public sealed record JobOnToolSelection(
    Guid JobOnId,
    string Machine,
    string Family,
    IReadOnlyList<JobOnToolSelectionOption> Items);

/// <summary>One selectable registered tool lot (stable physical ids + identity tuple).</summary>
public sealed record JobOnToolSelectionOption(
    Guid ReferenceId,
    Guid LoteId,
    string Type,
    string Reference,
    string Lot,
    string? TechnicalName,
    IReadOnlyList<string> AllowedLines);
