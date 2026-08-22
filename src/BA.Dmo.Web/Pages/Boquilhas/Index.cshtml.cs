using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Boquilhas;

/// <summary>
/// Boquilhas route surface (Plan-V3 05_SHL §5, GLM-BQ-02, U-19): guarded
/// server-side by the boquilhas module policy (module presence grants FULL
/// access — no operator/responsável split, no capability, GLM-BQ-02). The
/// canonical daily/high-frequency/quantity-based flows (Registo, Boquilhas,
/// Histórico, Definições + fixed line side panel, NO Fabrico tab) are driven from
/// the canonical /api/boquilhas/* endpoints via boquilhas.js (wiring only — no
/// business logic in JS). Reuses global design tokens only — no page-local
/// stylesheet (GLM-DSN-09), so the DesignSystemGuard allowlist is unchanged.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}