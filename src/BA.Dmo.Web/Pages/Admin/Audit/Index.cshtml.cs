using System.Text;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Audit;

/// <summary>
/// Auditoria tab (04_ACC §9, UD-17/TD-19): factual annual registry with
/// filters by year/user/module/action/result/interval and canonical pagination
/// 20/40/60. Viewing requires audit.view; the export handler requires
/// audit.export (re-checked in the use case). Read-only: no scores, no
/// rankings, no evaluation — facts only.
/// </summary>
public class IndexModel : PageModel
{
    private readonly AdminAuditService _audit;
    private readonly AdminUserService _users;

    public IndexModel(AdminAuditService audit, AdminUserService users)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    public int? Year { get; private set; }
    public string? Actor { get; private set; }
    public string? Module { get; private set; }
    public string? Action { get; private set; }
    public string? Result { get; private set; }

    /// <summary>Date inputs (yyyy-MM-dd), kept as posted for re-render.</summary>
    public string? From { get; private set; }
    public string? To { get; private set; }

    public int PageSize { get; private set; } = 20;

    public AuditQueryResult? Events { get; private set; }

    /// <summary>Options for the Utilizador filter (value = auth user id, matching actor_user_id).</summary>
    public IReadOnlyList<AdminUserRow> Users { get; private set; } = [];

    /// <summary>Options for the Módulo filter (value = module_id).</summary>
    public IReadOnlyList<ModuleDefinition> Modules => CanonicalModuleCatalog.Instance.Modules;

    public int CurrentYear => DateTime.UtcNow.Year;

    /// <summary>Years offered by the Ano select (current year back 5).</summary>
    public IReadOnlyList<int> Years =>
        Enumerable.Range(CurrentYear, 5).Select(y => CurrentYear - (y - 1)).ToList();

    public async Task OnGetAsync(int? year, string? actor, string? module,
        string? action, string? result, string? from, string? to, int pageSize = 20, int p = 1)
    {
        Year = year;
        Actor = actor;
        Module = module;
        Action = action;
        Result = result;
        From = NormalizeDate(from);
        To = NormalizeDate(to);
        PageSize = pageSize;

        await LoadUsersAsync();

        var query = await _audit.QueryAsync(
            BuildFilter(p, ParseUtcFrom(From), ParseUtcTo(To)),
            HttpContext.RequestAborted);
        if (query.IsFailure)
        {
            ModelState.AddModelError(string.Empty, query.Error.Message);
            return;
        }

        Events = query.Value;
    }

    public async Task<IActionResult> OnPostExportAsync(int? year, string? actor,
        string? module, string? action, string? result, string? from, string? to,
        int pageSize = 20, int p = 1)
    {
        var export = await _audit.ExportAsync(
            BuildFilter(p, ParseUtcFrom(NormalizeDate(from)), ParseUtcTo(NormalizeDate(to))),
            HttpContext.RequestAborted);
        if (export.IsFailure)
        {
            ModelState.AddModelError(string.Empty, export.Error.Message);
            await OnGetAsync(year, actor, module, action, result, from, to, pageSize, p);
            return Page();
        }

        return File(
            Encoding.UTF8.GetBytes(export.Value),
            "text/csv",
            $"auditoria-{Year?.ToString() ?? "tudo"}.csv");
    }

    private AuditQueryFilter BuildFilter(int page, DateTimeOffset? fromUtc, DateTimeOffset? toUtc) =>
        new(
            Year,
            string.IsNullOrWhiteSpace(Actor) ? null : Actor,
            string.IsNullOrWhiteSpace(Module) ? null : Module,
            string.IsNullOrWhiteSpace(Action) ? null : Action,
            string.IsNullOrWhiteSpace(Result) ? null : Result,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Page: page,
            PageSize: PageSize);

    private static string? NormalizeDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.Date.ToString("yyyy-MM-dd") : null;

    private static DateTimeOffset? ParseUtcFrom(string? value) =>
        DateTime.TryParse(value, out var parsed)
            ? new DateTimeOffset(parsed.Date, TimeSpan.Zero)
            : null;

    private static DateTimeOffset? ParseUtcTo(string? value) =>
        DateTime.TryParse(value, out var parsed)
            ? new DateTimeOffset(parsed.Date.AddTicks(TimeSpan.FromDays(1).Ticks - 1), TimeSpan.Zero)
            : null;

    private async Task LoadUsersAsync()
    {
        try
        {
            var result = await _users.ListAsync(null, HttpContext.RequestAborted);
            // The filter select is optional; a failure to list users must not
            // break the audit view (degrade to no options, never to an error).
            Users = result.IsSuccess ? result.Value : [];
        }
        catch
        {
            Users = [];
        }
    }
}
