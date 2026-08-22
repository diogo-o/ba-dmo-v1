using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Peso;

/// <summary>
/// Peso Responsável route (Plan-V3 GLM-ACC-05, UD-06/UD-15; modules/03): the
/// Responsável experience belongs to holders of the peso module WITH
/// peso.aprovar. Single approval page — calendar + day list + detail; no second
/// Comparações view. Operador never accesses these routes/commands.
/// </summary>
public class ResponsavelModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly PesoService _peso;

    public ResponsavelModel(
        ICurrentUserAccessor currentUserAccessor,
        PesoService peso)
    {
        _currentUserAccessor = currentUserAccessor;
        _peso = peso;
    }

    public bool CanDecide { get; private set; }

    public string RecordDatesCsv { get; private set; } = string.Empty;

    public string MonthValue { get; private set; } = string.Empty;

    public IReadOnlyList<PesoControlListItem> Pending { get; private set; } =
        Array.Empty<PesoControlListItem>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = _currentUserAccessor.Current;
        if (user is null || !user.HasModule(CanonicalModuleCatalog.PesoModuleId))
            return Forbid();

        // Operador never accesses Responsável routes/commands (GLM-ACC-05.2).
        if (!user.HasCapability(CanonicalModuleCatalog.PesoAprovarCapabilityId))
            return Redirect("/peso");

        CanDecide = true;

        var now = DateTime.UtcNow;
        MonthValue = now.ToString("yyyy-MM");
        var records = await _peso.GetRecordDatesAsync(now.Year, now.Month, cancellationToken);
        if (records.IsSuccess)
            RecordDatesCsv = string.Join(",", records.Value);

        var pending = await _peso.SearchControlsAsync(
            new ControlFilterRequest(null, null, "pendente", null, null, null), cancellationToken);
        if (pending.IsSuccess)
            Pending = pending.Value;

        return Page();
    }
}