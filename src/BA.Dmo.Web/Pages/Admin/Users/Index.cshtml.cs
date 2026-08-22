using BA.Dmo.Application.Modules.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>User listing (04_ACC §9: listar/pesquisar). Read-only view.</summary>
public class IndexModel : PageModel
{
    private readonly AdminUserService _users;

    public IndexModel(AdminUserService users)
    {
        _users = users;
    }

    public IReadOnlyList<AdminUserRow> Users { get; private set; } = [];

    public string? Search { get; private set; }

    public string? StateFilter { get; private set; }

    public string? Feedback { get; set; }

    /// <summary>
    /// User-safe service/configuration error (e.g. a required schema migration
    /// not applied). Rendered as a clear error state — never silently an empty
    /// user list, which would hide the backend/config failure.
    /// </summary>
    public string? ServiceErrorMessage { get; private set; }

    public async Task OnGetAsync(string? q, string? state)
    {
        Search = q;
        StateFilter = state;
        await ReloadUsersAsync();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        // HI-3: routes through the SAME service path as the Edit page
        // (AdminUserService.RequestPasswordResetAsync) — provisioning
        // generate_link + password_reset_request audit row.
        var result = await _users.RequestPasswordResetAsync(id, HttpContext.RequestAborted);
        Feedback = result.IsSuccess
            ? "Reset de palavra-passe iniciado."
            : result.Error.Message;
        if (result.IsFailure)
            ModelState.AddModelError(string.Empty, result.Error.Message);

        await ReloadUsersAsync();
        return Page();
    }

    /// <summary>
    /// Loads the full (email-enriched) set and applies the search filter
    /// case-insensitively across name, title, actor id AND email, then the
    /// optional state filter. The list is small so a single fetch is fine.
    /// </summary>
    private async Task ReloadUsersAsync()
    {
        var result = await _users.ListAsync(null, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            // Backend/configuration failure (e.g. N26 not applied). Show the
            // safe Portuguese error; DO NOT render an empty list that would
            // hide the problem.
            ServiceErrorMessage = result.Error.Message;
            Users = [];
            return;
        }
        Users = result.Value;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            Users = Users
                .Where(u =>
                    u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.ProfileTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || u.ActorId.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.AuthEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList()
                .AsReadOnly();
        }
        if (!string.IsNullOrWhiteSpace(StateFilter) && StateFilter != "all")
        {
            var isActive = StateFilter.Equals("active", StringComparison.OrdinalIgnoreCase);
            Users = Users.Where(u => u.Active == isActive).ToList().AsReadOnly();
        }
    }
}
