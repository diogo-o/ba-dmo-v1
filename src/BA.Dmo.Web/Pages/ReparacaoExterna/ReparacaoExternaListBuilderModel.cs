namespace BA.Dmo.Web.Pages.ReparacaoExterna;

/// <summary>
/// View-model for the Reparação Externa list-builder partial (CM / MF). Carries only
/// presentation values (title + repair type); all behavior is wired through the
/// canonical API endpoints via reparacao-externa.js.
/// </summary>
public sealed record ReparacaoExternaListBuilderModel(string Title, string RepairType);