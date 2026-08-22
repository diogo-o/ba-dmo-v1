namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// R010 — One component/tool item of a Folha de Controlo (N23
/// <c>controlo_sheet_items</c>). The production/tool identity is SNAPSHOTTED from the
/// exact Job On revision at creation; later Job On changes never mutate these values.
/// Control fields per item: OK/NOK result, observation/comment, manually-entered MCaliper
/// link (no integration — the link is persisted as typed). An item's sheet association is
/// unidirectional (item → sheet); items are never shared or mutated across productions.
/// </summary>
public sealed class ControloFolhaItem
{
    public Guid ControloSheetItemId { get; set; } = Guid.NewGuid();

    public Guid ControloSheetId { get; set; }

    /// <summary>Component family (MP_CM / MF / BQ).</summary>
    public string Family { get; set; } = null!;

    public Guid? SourceToolId { get; set; }
    public Guid? SourceLotId { get; set; }

    public string? ReferenceSnapshot { get; set; }
    public string? LotSnapshot { get; set; }
    public string? TechnicalNameSnapshot { get; set; }

    /// <summary>OK / NOK (null when not yet assessed).</summary>
    public string? Result { get; set; }

    public string? Observation { get; set; }

    /// <summary>Manually-entered MCaliper link (external detailed control record).</summary>
    public string? McaliperLink { get; set; }

    /// <summary>Applies a control assessment (result/observation/link). Validation is minimal.</summary>
    public void ApplyControl(string? result, string? observation, string? mcaliperLink)
    {
        Result = NormalizeResult(result);
        Observation = observation is null ? null : observation.Trim();
        McaliperLink = string.IsNullOrWhiteSpace(mcaliperLink)
            ? null
            : mcaliperLink.Trim();
    }

    public static ControloFolhaItem SnapshotFromComponent(
        Guid sheetId, string family, Guid? sourceToolId, Guid? sourceLotId,
        string? referenceSnapshot, string? lotSnapshot, string? technicalNameSnapshot)
    {
        var item = new ControloFolhaItem
        {
            ControloSheetId = sheetId,
            Family = family,
            SourceToolId = sourceToolId,
            SourceLotId = sourceLotId,
            ReferenceSnapshot = referenceSnapshot,
            LotSnapshot = lotSnapshot,
            TechnicalNameSnapshot = technicalNameSnapshot
        };
        return item;
    }

    private static string? NormalizeResult(string? result)
    {
        if (string.IsNullOrWhiteSpace(result)) return null;
        var value = result.Trim().ToUpperInvariant();
        return value is "OK" or "NOK" ? value : null;
    }
}