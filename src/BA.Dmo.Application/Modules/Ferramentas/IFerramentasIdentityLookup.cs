using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// U-14 — Read-only cross-module lookup for tool/lot identity, owned by
/// Ferramentas. Consumed by Armazém (and other tool-domain consumers) to
/// resolve/search a stable <see cref="FerramentasIdentityHit.ToolLoteId"/>. This
/// port NEVER mutates Ferramentas (03_ARCH §4/§6: lookup read-only; consumers
/// inject the owner's port, never the internal repository).
/// </summary>
public interface IFerramentasIdentityLookup
{
    /// <summary>
    /// Searches tool references (and their lots) by type + optional reference /
    /// lot fragments. Returns canonical identity hits with a stable lot id.
    /// </summary>
    Task<IReadOnlyList<FerramentasIdentityHit>> SearchAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        CancellationToken ct = default);

    /// <summary>Resolves the canonical identity of a single tool lot.</summary>
    Task<FerramentasIdentityHit?> ResolveAsync(Guid toolLoteId, CancellationToken ct = default);

    /// <summary>
    /// Searches registered tool LOTS of one type by optional reference/lot
    /// fragments and optional allowed line, returning each lot's registered
    /// <c>allowed_lines</c> (the machine/line dimension of the tool identity
    /// tuple). Read-only: returns ONLY existing registered records — consumer
    /// selection surfaces (Job On "Alterar CM/MF/BQ associado") must never
    /// present or persist combinations that are not registered here.
    /// </summary>
    Task<IReadOnlyList<FerramentasToolLoteOption>> SearchToolLoteOptionsAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        string? line,
        CancellationToken ct = default);

    /// <summary>Resolves the canonical identity + registered lines of a single tool lot.</summary>
    Task<FerramentasToolLoteOption?> ResolveToolLoteOptionAsync(Guid toolLoteId, CancellationToken ct = default);
}

/// <summary>
/// Canonical read-only identity projection of a Ferramentas tool lot
/// (reference/lot/type + technical name). Exposed by the Ferramentas owner.
/// </summary>
public sealed record FerramentasIdentityHit(
    Guid ToolReferenceId,
    Guid ToolLoteId,
    FerramentasToolType Type,
    string Reference,
    string Lot,
    string? TechnicalName);

/// <summary>
/// Canonical read-only projection of a Ferramentas tool LOT used by consumer
/// selection surfaces: the stable reference/lot ids, the (type, reference, lot)
/// master identity, the technical name and the registered allowed lines.
/// CM/MF/BQ are distinct <see cref="FerramentasToolType"/> values — the same
/// reference code registered under a different type is a DIFFERENT tool and
/// never merges with the other.
/// </summary>
public sealed record FerramentasToolLoteOption(
    Guid ToolReferenceId,
    Guid ToolLoteId,
    FerramentasToolType Type,
    string Reference,
    string Lot,
    string? TechnicalName,
    IReadOnlyList<string> AllowedLines);