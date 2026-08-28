namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// R010 — Folha de Controlo module constants (OWNER DECISION: a production-level
/// control summary sheet INSIDE the Controlo functional area — not a separate top-level
/// module). The sheet is a record/workflow associated to one production via
/// job_on_id + exact job_on_revision_id. Capabilities gate create/edit/submit/review.
/// </summary>
public static class ControloSheetModuleCatalog
{
    public const string AreaId = "controlo";

    public const string ViewCapabilityId = "controlo.view";
    public const string EditCapabilityId = "controlo.edit";
    public const string SubmitCapabilityId = "controlo.submit";
    public const string ReviewCapabilityId = "controlo.review";

    /// <summary>Canonical sheet statuses (N23 <c>controlo_sheets.status</c>).</summary>
    public static readonly IReadOnlyList<string> Statuses =
        new[] { "rascunho", "submetido", "aprovado", "rejeitado" };
}