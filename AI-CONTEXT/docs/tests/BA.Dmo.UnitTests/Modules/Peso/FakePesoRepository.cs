using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Domain.Modules.Peso;

namespace BA.Dmo.UnitTests.Modules.Peso;

/// <summary>
/// In-memory fake of the Peso persistence port (confined to tests/*). Tracks
/// references, lots, controls, day approvals, settings and audit events so
/// use-case tests can assert persistence behavior without a live DB.
/// </summary>
public sealed class FakePesoRepository : IPesoRepository
{
    public Dictionary<Guid, PesoReference> References { get; } = new();
    public Dictionary<Guid, PesoLote> Lotes { get; } = new();
    public Dictionary<Guid, PesoControl> Controls { get; } = new();
    public List<(string Mold, string Neckring, string Line, DateTime Date, string Actor)> DayApprovals { get; } = new();
    public Dictionary<string, string> Settings { get; } = new();
    public List<(Guid? EntityId, string EventType, string? Before, string? After, string Actor)> AuditEvents { get; } = new();

    public Task<Guid> CreateReferenceAsync(PesoReference reference, CancellationToken ct = default)
    {
        References[reference.PesoReferenceId] = reference;
        return Task.FromResult(reference.PesoReferenceId);
    }

    public Task<PesoReference?> GetReferenceByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(References.GetValueOrDefault(id));

    public Task<IReadOnlyList<PesoReference>> GetReferencesAsync(string? search, CancellationToken ct = default)
    {
        var rows = References.Values
            .Where(r => string.IsNullOrWhiteSpace(search) || r.MoldNumber.Contains(search) || r.NeckringNumber.Contains(search))
            .ToList();
        return Task.FromResult<IReadOnlyList<PesoReference>>(rows);
    }

    public Task<PesoReference?> GetReferenceByMoldNeckringAsync(string mold, string neckring, CancellationToken ct = default)
        => Task.FromResult(References.Values.FirstOrDefault(r => r.MoldNumber == mold && r.NeckringNumber == neckring));

    public Task UpdateReferenceAsync(PesoReference reference, CancellationToken ct = default)
    {
        if (reference is not null) References[reference.PesoReferenceId] = reference;
        return Task.CompletedTask;
    }

    public Task<Guid> CreateLoteAsync(PesoLote lote, CancellationToken ct = default)
    {
        Lotes[lote.PesoLoteId] = lote;
        return Task.FromResult(lote.PesoLoteId);
    }

    public Task<PesoLote?> GetLoteByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Lotes.GetValueOrDefault(id));

    public Task<IReadOnlyList<PesoLote>> GetLotesAsync(Guid referenceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PesoLote>>(Lotes.Values.Where(l => l.PesoReferenceId == referenceId).ToList());

    public Task<Guid> CreateControlAsync(PesoControl control, CancellationToken ct = default)
    {
        Controls[control.PesoControloId] = control;
        return Task.FromResult(control.PesoControloId);
    }

    public Task<PesoControl?> GetControlByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Controls.GetValueOrDefault(id));

    public Task<IReadOnlyList<PesoControl>> GetControlsAsync(
        Guid? referenceId, string? search, string? status, PesoRecordType? type,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var rows = Controls.Values.Where(c =>
            (referenceId is null || c.PesoReferenceId == referenceId) &&
            (string.IsNullOrWhiteSpace(search) || c.MoldNumber.Contains(search) || c.ProductionCode.Contains(search)) &&
            (status is null || PesoControlStateCodec.ToStorage(c.Status) == status) &&
            (type is null || c.RecordType == type) &&
            (from is null || c.ControlDate >= from) &&
            (to is null || c.ControlDate <= to)).ToList();
        return Task.FromResult<IReadOnlyList<PesoControl>>(rows);
    }

    public Task UpdateControlAsync(PesoControl control, CancellationToken ct = default)
    {
        FullWrites++;
        if (control is not null) Controls[control.PesoControloId] = control;
        return Task.CompletedTask;
    }

    /// <summary>N40: header-only writes (submit/approve/reject/reopen/decide).</summary>
    public Task UpdateControlHeaderAsync(PesoControl control, CancellationToken ct = default)
    {
        HeaderOnlyWrites++;
        if (control is not null) Controls[control.PesoControloId] = control;
        return Task.CompletedTask;
    }

    /// <summary>Count of full draft-rewrite writes (header + readings).</summary>
    public int FullWrites { get; private set; }

    /// <summary>Count of header-only writes (no readings DML).</summary>
    public int HeaderOnlyWrites { get; private set; }

    public Task DeleteControlAsync(Guid id, CancellationToken ct = default)
    {
        Controls.Remove(id);
        return Task.CompletedTask;
    }

    public Task SaveDayApprovalAsync(string mold, string neckring, string line, DateTime approvalDate, string approvedBy, CancellationToken ct = default)
    {
        DayApprovals.Add((mold, neckring, line, approvalDate, approvedBy));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetRecordDatesAsync(int year, int month, CancellationToken ct = default)
    {
        var dates = DayApprovals
            .Where(d => d.Date.Year == year && d.Date.Month == month)
            .Select(d => d.Date.ToString("yyyy-MM-dd"))
            .Distinct()
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(dates);
    }

    public Task SaveSettingAsync(string key, string json, string updatedBy, CancellationToken ct = default)
    {
        Settings[key] = json;
        return Task.CompletedTask;
    }

    public Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
        => Task.FromResult(Settings.GetValueOrDefault(key));

    public Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default)
    {
        AuditEvents.Add((entityId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
    }
}