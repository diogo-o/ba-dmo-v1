using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoExterna;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação External-owned abstraction over CM/MF tool-piece identity
/// (owner decisions A + Ferramentas identity adapter). The realistic layer imports
/// <see cref="IFerramentasPieceLookup"/> (Ferramentas-owned, read-only) and the
/// <see cref="IFerramentasIdentityLookup"/> (reference/lot), and projects them into
/// the U-15-native <see cref="RepairToolIdentity"/>. Read-only; never mutates
/// Ferramentas. BQ is NOT resolved here (deferred to U-19).
/// </summary>
public interface IToolPieceResolver
{
    /// <summary>Resolves CM/MF physical pieces matching type + reference/lot/number fragments.</summary>
    Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(
        RepairType type,
        string? reference,
        string? lot,
        string? number,
        CancellationToken ct = default);

    /// <summary>Resolves a single CM/MF physical piece by its stable id.</summary>
    Task<RepairToolIdentity?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default);
}

/// <summary>U-15-native projection of a CM/MF tool piece (id + parent lot + lot/reference identity).</summary>
public sealed record RepairToolIdentity(
    Guid PhysicalPieceId,
    Guid ToolLoteId,
    Guid ToolReferenceId,
    RepairType Type,
    string Reference,
    string Lot,
    string Number,
    string? TechnicalName);