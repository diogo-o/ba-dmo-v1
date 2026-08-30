using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// In-memory fake of the Ferramentas-owned read-only identity lookup
/// (confined to tests/*). Simulates the N04 tool register
/// (<c>tool_references</c> + <c>tool_lotes</c> with <c>allowed_lines</c>) as a
/// seeded set of tool lots. The same reference code registered under a
/// different tool type (or for different lines) is a DIFFERENT tool here —
/// exactly the register identity the Job On picker must respect. Read-only:
/// the Job On flow never mutates it (tests assert the register is unchanged).
/// </summary>
public sealed class FakeFerramentasToolLookup : IFerramentasIdentityLookup
{
    /// <summary>One registered tool lot (a real (type, reference, lot, lines) combination).</summary>
    public sealed record RegisteredLot(
        Guid ToolReferenceId,
        Guid ToolLoteId,
        FerramentasToolType Type,
        string Reference,
        string Lot,
        string? TechnicalName,
        IReadOnlyList<string> AllowedLines);

    public List<RegisteredLot> Lots { get; } = new();

    /// <summary>Number of resolution calls (asserts the flow only READS the register).</summary>
    public int ResolveCalls { get; private set; }

    /// <summary>Number of search calls (asserts the flow only READS the register).</summary>
    public int SearchCalls { get; private set; }

    public void Register(
        Guid toolReferenceId,
        Guid toolLoteId,
        FerramentasToolType type,
        string reference,
        string lot,
        string? technicalName = null,
        params string[] allowedLines)
        => Lots.Add(new RegisteredLot(
            toolReferenceId, toolLoteId, type, reference, lot, technicalName,
            allowedLines.AsReadOnly()));

    public Task<IReadOnlyList<FerramentasIdentityHit>> SearchAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        CancellationToken ct = default)
    {
        var result = Lots.Where(l =>
                l.Type == type &&
                (string.IsNullOrWhiteSpace(reference) || l.Reference.Contains(reference)) &&
                (string.IsNullOrWhiteSpace(lot) || l.Lot.Contains(lot)))
            .Select(l => new FerramentasIdentityHit(
                l.ToolReferenceId, l.ToolLoteId, l.Type, l.Reference, l.Lot, l.TechnicalName))
            .ToList();
        return Task.FromResult<IReadOnlyList<FerramentasIdentityHit>>(result);
    }

    public Task<FerramentasIdentityHit?> ResolveAsync(Guid toolLoteId, CancellationToken ct = default)
    {
        var lot = Lots.FirstOrDefault(l => l.ToolLoteId == toolLoteId);
        return Task.FromResult(lot is null
            ? null
            : new FerramentasIdentityHit(
                lot.ToolReferenceId, lot.ToolLoteId, lot.Type, lot.Reference, lot.Lot, lot.TechnicalName));
    }

    public Task<IReadOnlyList<FerramentasToolLoteOption>> SearchToolLoteOptionsAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        string? line,
        CancellationToken ct = default)
    {
        SearchCalls++;
        var result = Lots.Where(l =>
                l.Type == type &&
                (string.IsNullOrWhiteSpace(reference) || l.Reference.Contains(reference)) &&
                (string.IsNullOrWhiteSpace(lot) || l.Lot.Contains(lot)) &&
                (line is null || l.AllowedLines.Contains(line)))
            .Select(l => new FerramentasToolLoteOption(
                l.ToolReferenceId, l.ToolLoteId, l.Type, l.Reference, l.Lot, l.TechnicalName, l.AllowedLines))
            .ToList();
        return Task.FromResult<IReadOnlyList<FerramentasToolLoteOption>>(result);
    }

    public Task<FerramentasToolLoteOption?> ResolveToolLoteOptionAsync(Guid toolLoteId, CancellationToken ct = default)
    {
        ResolveCalls++;
        var lot = Lots.FirstOrDefault(l => l.ToolLoteId == toolLoteId);
        return Task.FromResult(lot is null
            ? null
            : new FerramentasToolLoteOption(
                lot.ToolReferenceId, lot.ToolLoteId, lot.Type, lot.Reference, lot.Lot, lot.TechnicalName,
                lot.AllowedLines));
    }
}
