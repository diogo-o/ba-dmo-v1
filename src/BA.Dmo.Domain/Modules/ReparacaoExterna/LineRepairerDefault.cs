namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Default/associated repairer per line + tool type (N08
/// <c>line_repairer_defaults</c> PK(line, tool_type); TD-15). Used to suggest a
/// repairer when creating an exit list and to filter permitted repairers by
/// type + line (REPARACAO_EXTERNA_DESIGN_BRIEF §9/§10, BOQUILHAS_INTERFACE_BEHAVIOR §9).
/// The actually-used repairer is snapshotted per exit; later association changes
/// never rewrite history.
/// </summary>
public sealed class LineRepairerDefault
{
    public string Line { get; set; } = string.Empty;

    public string ToolType { get; set; } = string.Empty;

    public Guid RepairerId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }
}