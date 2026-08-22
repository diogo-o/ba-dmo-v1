namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// R010 — Folha de Controlo lifecycle state (N23 <c>controlo_sheets.status</c>).
/// Submission is NOT a permanent edit lock: a sheet can be reopened after submission
/// and edited, with the change traced in <c>controlo_sheet_events</c> (append-only).
/// </summary>
public enum ControloFolhaState
{
    /// <summary>Being prepared; editable.</summary>
    Rascunho,

    /// <summary>Submitted/delivered; pending review (may be reopened by the author).</summary>
    Submetido,

    /// <summary>Approved by the responsible/chief.</summary>
    Aprovado,

    /// <summary>Rejected by the responsible/chief.</summary>
    Rejeitado
}

/// <summary>
/// Decision value of a review (N23 <c>controlo_sheets.decision</c>).
/// </summary>
public enum ControloFolhaDecision
{
    Aprovado,
    Rejeitado
}

/// <summary>Codec between the domain enum and the N23 stored text discriminator.</summary>
public static class ControloFolhaStateCodec
{
    public static string ToStorage(ControloFolhaState state) => state switch
    {
        ControloFolhaState.Rascunho => "rascunho",
        ControloFolhaState.Submetido => "submetido",
        ControloFolhaState.Aprovado => "aprovado",
        ControloFolhaState.Rejeitado => "rejeitado",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown folha state")
    };

    public static ControloFolhaState FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "rascunho" => ControloFolhaState.Rascunho,
        "submetido" => ControloFolhaState.Submetido,
        "aprovado" => ControloFolhaState.Aprovado,
        "rejeitado" => ControloFolhaState.Rejeitado,
        _ => throw new InvalidOperationException($"Unknown persisted folha state: {value}")
    };

    public static string ToStorage(ControloFolhaDecision decision) => decision switch
    {
        ControloFolhaDecision.Aprovado => "aprovado",
        ControloFolhaDecision.Rejeitado => "rejeitado",
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision")
    };

    public static ControloFolhaDecision FromStorageDecision(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "aprovado" => ControloFolhaDecision.Aprovado,
        "rejeitado" => ControloFolhaDecision.Rejeitado,
        _ => throw new InvalidOperationException($"Unknown persisted decision: {value}")
    };
}