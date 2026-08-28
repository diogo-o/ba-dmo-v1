using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// Ferramentas read/write port (N04, GLM-FERR-08). Owns Ferramentas persistence
/// only. The stable tool identities here are consumed by Job On, Armazém and
/// Reparação — never duplicated as parallel identities.
/// </summary>
public interface IFerramentasRepository
{
    // ---- References ---------------------------------------------------------
    Task<Guid> CreateReferenceAsync(ToolReference reference, CancellationToken ct = default);
    Task<ToolReference?> GetReferenceByIdAsync(Guid referenceId, CancellationToken ct = default);
    Task<ToolReference?> GetReferenceByTypeAndCodeAsync(FerramentasToolType type, string refCode, CancellationToken ct = default);
    Task UpdateReferenceAsync(ToolReference reference, CancellationToken ct = default);
    Task<IReadOnlyList<ToolReference>> SearchReferencesAsync(
        string? reference, string? technicalName, string? lote, string? drawing,
        string? line, string? processo, string? ownerPlant, CancellationToken ct = default);

    // ---- Lotes --------------------------------------------------------------
    Task<Guid> CreateLoteAsync(ToolLote lote, CancellationToken ct = default);
    Task<ToolLote?> GetLoteByIdAsync(Guid loteId, CancellationToken ct = default);
    Task UpdateLoteAsync(ToolLote lote, CancellationToken ct = default);
    Task<IReadOnlyList<ToolLote>> GetLotesByReferenceAsync(Guid referenceId, CancellationToken ct = default);
    Task<bool> LoteExistsInReferenceAsync(Guid referenceId, string lote, CancellationToken ct = default);

    // ---- Pieces -------------------------------------------------------------
    Task<Guid> RegisterPieceAsync(PhysicalPiece piece, CancellationToken ct = default);
    Task UpdatePieceAsync(PhysicalPiece piece, CancellationToken ct = default);
    Task<IReadOnlyList<PhysicalPiece>> GetPiecesByLoteAsync(Guid loteId, CancellationToken ct = default);

    // ---- Check rules --------------------------------------------------------
    Task<Guid> AddCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default);
    Task UpdateCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default);
    Task ToggleCheckRuleActiveAsync(Guid ruleId, bool active, CancellationToken ct = default);
    Task DeleteCheckRuleAsync(Guid ruleId, CancellationToken ct = default);
    Task<IReadOnlyList<ToolCheckRule>> GetCheckRulesByLoteAsync(Guid loteId, CancellationToken ct = default);
    Task<ToolCheckRule?> GetCheckRuleByIdAsync(Guid ruleId, CancellationToken ct = default);

    // ---- Utilisation (R003: append-only, per tool_lote) ---------------------
    Task RecordUtilisationReadingAsync(ToolUtilisationReading reading, CancellationToken ct = default);
    Task<IReadOnlyList<ToolUtilisationReading>> ListUtilisationReadingsAsync(Guid toolLoteId, CancellationToken ct = default);

    // ---- Atomic multi-write -------------------------------------------------
    /// <summary>Creates a reference + its first lote in ONE transaction.</summary>
    Task<(Guid ReferenceId, Guid LoteId)> CreateReferenceWithFirstLoteAsync(ToolReference reference, ToolLote lote, CancellationToken ct = default);

    /// <summary>
    /// Creates a NEW lot and copies the given verification-rule configuration
    /// (never occurrences/checks/history) together with the "lote duplicar"
    /// audit event in ONE transaction (audit FA-03 — no partial duplicate can
    /// remain). Returns the new lot id.
    /// </summary>
    Task<Guid> CreateLoteWithRulesAtomicallyAsync(
        ToolLote lote,
        IReadOnlyList<ToolCheckRule> copiedRules,
        Guid? sourceLoteId,
        string actorId,
        CancellationToken ct = default);

    // ---- Audit --------------------------------------------------------------
    Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default);
}