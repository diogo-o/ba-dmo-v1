using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.UnitTests.Modules.Ferramentas;

/// <summary>
/// In-memory fake of the Ferramentas persistence port (confined to tests/*).
/// Tracks references, lotes, pieces, check rules and audit events.
/// </summary>
public sealed class FakeFerramentasRepository : IFerramentasRepository
{
    public Dictionary<Guid, ToolReference> References { get; } = new();

    public Dictionary<Guid, ToolLote> Lotes { get; } = new();

    public Dictionary<Guid, List<PhysicalPiece>> Pieces { get; } = new();

    public Dictionary<Guid, List<ToolCheckRule>> CheckRules { get; } = new();

    public List<(Guid? entityId, string eventType, string? before, string? after, string actor)> AuditEvents { get; } = new();

    public List<ToolUtilisationReading> UtilisationReadings { get; } = new();

    public bool FailAtomicCreate { get; set; }

    /// <summary>When true, RegisterPieceAsync throws PhysicalPieceDuplicateException (audit ON-02 mapping test).</summary>
    public bool FailPieceDuplicate { get; set; }

    public Task<Guid> CreateReferenceAsync(ToolReference reference, CancellationToken ct = default)
    {
        References[reference.ToolReferenceId] = reference;
        return Task.FromResult(reference.ToolReferenceId);
    }

    public Task<ToolReference?> GetReferenceByIdAsync(Guid referenceId, CancellationToken ct = default)
        => Task.FromResult(References.GetValueOrDefault(referenceId));

    public Task<ToolReference?> GetReferenceByTypeAndCodeAsync(FerramentasToolType type, string refCode, CancellationToken ct = default)
        => Task.FromResult(References.Values.FirstOrDefault(r => r.ToolType == type && r.RefCode == refCode));

    public Task UpdateReferenceAsync(ToolReference reference, CancellationToken ct = default)
    {
        if (reference is not null) References[reference.ToolReferenceId] = reference;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ToolReference>> SearchReferencesAsync(
        string? reference, string? technicalName, string? lote, string? drawing,
        string? line, string? processo, string? ownerPlant, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ToolReference>>(References.Values
            .Where(r =>
                (string.IsNullOrWhiteSpace(reference) || r.RefCode.Contains(reference)) &&
                (string.IsNullOrWhiteSpace(technicalName) || (r.TechnicalName ?? string.Empty).Contains(technicalName)) &&
                (string.IsNullOrWhiteSpace(ownerPlant) || (r.OwnerPlant ?? string.Empty).Contains(ownerPlant)))
            .ToList());

    public Task<Guid> CreateLoteAsync(ToolLote lote, CancellationToken ct = default)
    {
        Lotes[lote.ToolLoteId] = lote;
        return Task.FromResult(lote.ToolLoteId);
    }

    public Task<ToolLote?> GetLoteByIdAsync(Guid loteId, CancellationToken ct = default)
        => Task.FromResult(Lotes.GetValueOrDefault(loteId));

    public Task UpdateLoteAsync(ToolLote lote, CancellationToken ct = default)
    {
        if (lote is not null) Lotes[lote.ToolLoteId] = lote;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ToolLote>> GetLotesByReferenceAsync(Guid referenceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ToolLote>>(Lotes.Values.Where(l => l.ToolReferenceId == referenceId).ToList());

    public Task<bool> LoteExistsInReferenceAsync(Guid referenceId, string lote, CancellationToken ct = default)
        => Task.FromResult(Lotes.Values.Any(l => l.ToolReferenceId == referenceId && l.Lote == lote));

    public Task<Guid> RegisterPieceAsync(PhysicalPiece piece, CancellationToken ct = default)
    {
        if (FailPieceDuplicate)
            throw new PhysicalPieceDuplicateException(
                $"O número {piece.Number} já está registado neste lote.");
        if (!Pieces.TryGetValue(piece.ToolLoteId, out var list))
        {
            list = new List<PhysicalPiece>();
            Pieces[piece.ToolLoteId] = list;
        }
        list.Add(piece);
        return Task.FromResult(piece.PhysicalPieceId);
    }

    public Task UpdatePieceAsync(PhysicalPiece piece, CancellationToken ct = default)
    {
        if (piece is not null && Pieces.TryGetValue(piece.ToolLoteId, out var list))
        {
            var idx = list.FindIndex(p => p.PhysicalPieceId == piece.PhysicalPieceId);
            if (idx >= 0) list[idx] = piece;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PhysicalPiece>> GetPiecesByLoteAsync(Guid loteId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PhysicalPiece>>((Pieces.GetValueOrDefault(loteId) ?? new()).ToList());

    public Task<Guid> AddCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default)
    {
        if (!CheckRules.TryGetValue(rule.ToolLoteId, out var list))
        {
            list = new List<ToolCheckRule>();
            CheckRules[rule.ToolLoteId] = list;
        }
        list.Add(rule);
        return Task.FromResult(rule.ToolCheckRuleId);
    }

    public Task UpdateCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ToggleCheckRuleActiveAsync(Guid ruleId, bool active, CancellationToken ct = default)
    {
        var rule = CheckRules.Values.SelectMany(l => l).FirstOrDefault(r => r.ToolCheckRuleId == ruleId);
        if (rule is not null) rule.Active = active;
        return Task.CompletedTask;
    }

    public Task DeleteCheckRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var rule = CheckRules.Values.SelectMany(l => l).FirstOrDefault(r => r.ToolCheckRuleId == ruleId);
        if (rule is not null) rule.Active = false;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ToolCheckRule>> GetCheckRulesByLoteAsync(Guid loteId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ToolCheckRule>>((CheckRules.GetValueOrDefault(loteId) ?? new()).ToList());

    public Task<ToolCheckRule?> GetCheckRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
        => Task.FromResult(CheckRules.Values.SelectMany(l => l).FirstOrDefault(r => r.ToolCheckRuleId == ruleId));

    public Task<(Guid ReferenceId, Guid LoteId)> CreateReferenceWithFirstLoteAsync(ToolReference reference, ToolLote lote, CancellationToken ct = default)
    {
        if (FailAtomicCreate)
            throw new InvalidOperationException("simulated atomic failure");
        References[reference.ToolReferenceId] = reference;
        Lotes[lote.ToolLoteId] = lote;
        return Task.FromResult((reference.ToolReferenceId, lote.ToolLoteId));
    }

    public Task<Guid> CreateLoteWithRulesAtomicallyAsync(
        ToolLote lote, IReadOnlyList<ToolCheckRule> copiedRules, Guid? sourceLoteId, string actorId, CancellationToken ct = default)
    {
        if (FailAtomicCreate)
            throw new InvalidOperationException("simulated atomic failure");
        Lotes[lote.ToolLoteId] = lote;
        foreach (var rule in copiedRules)
        {
            if (!CheckRules.TryGetValue(rule.ToolLoteId, out var list))
            {
                list = new List<ToolCheckRule>();
                CheckRules[rule.ToolLoteId] = list;
            }
            list.Add(rule);
        }
        AuditEvents.Add((sourceLoteId, "ferramentas.lote.duplicar", sourceLoteId?.ToString(), lote.ToolLoteId.ToString(), actorId));
        return Task.FromResult(lote.ToolLoteId);
    }

    public Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default)
    {
        AuditEvents.Add((entityId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
    }

    public Task RecordUtilisationReadingAsync(ToolUtilisationReading reading, CancellationToken ct = default)
    {
        UtilisationReadings.Add(reading);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ToolUtilisationReading>> ListUtilisationReadingsAsync(Guid toolLoteId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ToolUtilisationReading>>(
            UtilisationReadings.Where(r => r.ToolLoteId == toolLoteId).OrderBy(r => r.ReadingAtUtc).ToList());
}