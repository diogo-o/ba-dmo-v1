using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Ferramentas;

/// <summary>
/// Ferramentas reference card (U-12, brief §8): header (Tipo, Referência, Nome
/// técnico em destaque, Owner plant), lot list and per-lot Verificações tab.
/// Data and interactions are API-driven via ferramentas.js; configuration UI is
/// gated by <c>ferramentas.configure</c>. Gated by the ferramentas module policy.
/// </summary>
public class FichaModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public FichaModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>Reference id from the route.</summary>
    public Guid ReferenceId { get; private set; }

    /// <summary>Whether the user may configure verification rules.</summary>
    public bool CanConfigure { get; private set; }

    public void OnGet(Guid id)
    {
        ReferenceId = id;
        var user = _currentUserAccessor.Current;
        CanConfigure = user?.HasCapability(CanonicalModuleCatalog.FerramentasConfigureCapabilityId) == true;
    }
}