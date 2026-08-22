using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// U-15 — Read-only Ferramentas-owned cross-module lookup for CM/MF PHYSICAL
/// PIECES (N04 <c>physical_pieces</c>). Reparação Externa consumes this to build
/// CM/MF repair-exit items (number + stable <c>physical_piece_id</c>) and to
/// resolve the parent <c>tool_lote_id</c> for the Armazém physical movement.
/// This port NEVER mutates Ferramentas (03_ARCH §4/§6: read-only; consumers inject
/// the owner's port, never the internal repository).
/// </summary>
public interface IFerramentasPieceLookup
{
    /// <summary>
    /// Searches CM/MF physical pieces by type + optional reference/lot/number fragments.
    /// Returns canonical piece hits with a stable <c>physical_piece_id</c> and parent lot id.
    /// </summary>
    Task<IReadOnlyList<FerramentasPieceHit>> SearchAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        string? number,
        CancellationToken ct = default);

    /// <summary>Resolves the canonical identity of a single physical piece.</summary>
    Task<FerramentasPieceHit?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default);
}

/// <summary>
/// Canonical read-only identity projection of a CM/MF physical piece, exposed by
/// the Ferramentas owner: the piece's own id + number, its parent lot id, and the
/// read-only lot/reference identity.
/// </summary>
public sealed record FerramentasPieceHit(
    Guid PhysicalPieceId,
    Guid ToolLoteId,
    Guid ToolReferenceId,
    FerramentasToolType Type,
    string Reference,
    string Lot,
    string Number,
    string? TechnicalName);