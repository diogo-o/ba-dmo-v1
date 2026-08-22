using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.ReparacaoExterna;

/// <summary>
/// Reparação Externa route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04). Server-side
/// guarded by the reparacao_externa module policy. The interactive flows
/// (Contra moldes / Moldes finais / Envios / Histórico / Definições) are driven from
/// the canonical API endpoints via reparacao-externa.js (wiring only). The Boquilhas
/// tab is present for shell parity but its functional BQ behavior is deferred to U-19
/// (owner decision A) — no fake BQ identity/persistence here.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}