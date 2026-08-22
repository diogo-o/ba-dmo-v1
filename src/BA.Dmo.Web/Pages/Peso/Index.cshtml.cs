using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Peso;

/// <summary>
/// Peso Operador route (Plan-V3 GLM-ACC-05, UD-06/UD-15; modules/03): the
/// Operador experience belongs to holders of the peso module WITHOUT
/// peso.aprovar. The route guard enforces module entry server-side; the
/// exclusivity guard redirects peso.aprovar holders to the Responsável
/// experience. Content is the U-10 Operador surface (Novo controlo /
/// Referências / Histórico / Configurações), bound to the Peso service.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly PesoService _peso;

    public IndexModel(
        ICurrentUserAccessor currentUserAccessor,
        PesoService peso)
    {
        _currentUserAccessor = currentUserAccessor;
        _peso = peso;
    }

    public IReadOnlyList<PesoReferenceSummary> References { get; private set; } =
        Array.Empty<PesoReferenceSummary>();

    public string RecordDatesCsv { get; private set; } = string.Empty;

    public bool CanOperate { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.Current;
        if (user is null || !user.HasModule(CanonicalModuleCatalog.PesoModuleId))
            return Forbid();

        // Responsável never receives the Operador page (GLM-ACC-05.2).
        if (user.HasCapability(CanonicalModuleCatalog.PesoAprovarCapabilityId))
            return Redirect("/peso/responsavel");

        CanOperate = true;

        var references = await _peso.ListReferencesAsync(search: null, cancellationToken);
        if (references.IsSuccess)
            References = references.Value;

        return Page();
    }
}