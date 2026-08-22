using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Applications;

/// <summary>
/// Catalog mirror administration (04_ACC §9 "Aplicações", GLM-CAT-02 rule 3):
/// display order and activation of KNOWN catalog modules only. Unknown
/// identifiers cannot be created; the mirror never influences authorization.
///
/// Presentation (owner decision 2026-08): the raw numeric order is never shown.
/// The UI posts "Posição no menu" ranks (1..N, dense); the mirror stores them
/// as display_order (MergeForDisplay sorts by display_order then module id, so
/// dense ranks are equivalent to any ordered value set).
/// </summary>
public class IndexModel : PageModel
{
    private readonly AdminMirrorService _mirror;

    public IndexModel(AdminMirrorService mirror)
    {
        _mirror = mirror;
    }

    public sealed class EntryLine
    {
        public string ModuleId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public bool Active { get; set; } = true;
    }

    public List<EntryLine> Entries { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync(List<MirrorEntryInput> entries)
    {
        var result = await _mirror.SaveDisplayAsync(
            entries ?? new List<MirrorEntryInput>(), HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            await LoadAsync();
            return Page();
        }

        return Redirect("/admin/applications");
    }

    private async Task LoadAsync()
    {
        var display = await _mirror.GetDisplayAsync(HttpContext.RequestAborted);
        Entries = display.IsSuccess
            ? display.Value.Select(e => new EntryLine
            {
                ModuleId = e.Module.ModuleId,
                DisplayName = e.Module.DisplayName,
                Description = CanonicalModuleCatalog.Descriptions.GetValueOrDefault(e.Module.ModuleId),
                DisplayOrder = e.DisplayOrder,
                Active = e.Active
            }).ToList()
            : new List<EntryLine>();
    }
}
