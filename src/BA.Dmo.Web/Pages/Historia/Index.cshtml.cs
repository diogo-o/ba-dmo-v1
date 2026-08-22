using BA.Dmo.Application.Modules.Historia;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Historia;

/// <summary>
/// U-18 — História transversal read page (modules/11 GLM-HIST-03, contract §13
/// History Entry). Guarded server-side by the <c>historia</c> module policy and,
/// per TD-24, only returns events of the modules the identity's active template
/// grants (resolved by the authorization gate). Read-only: no write path; every
/// row preserves the actor/time recorded at execution time.
/// </summary>
public class IndexModel : PageModel
{
    private readonly HistoriaService _service;

    public IndexModel(HistoriaService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public string? Query { get; private set; }
    public string? Module { get; private set; }
    public string? Action { get; private set; }
    public string? Actor { get; private set; }
    public string? Result { get; private set; }
    public DateTimeOffset? FromUtc { get; private set; }
    public DateTimeOffset? ToUtc { get; private set; }
    public int PageSize { get; private set; } = 20;

    public HistoriaQueryResult? Histories { get; private set; }
    public IReadOnlyCollection<string> VisibleModuleIds { get; private set; } = Array.Empty<string>();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(
        string? query, string? module, string? action, string? actor,
        string? result, DateTime? from, DateTime? to,
        int pageSize = 20, int page = 1)
    {
        Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        Module = string.IsNullOrWhiteSpace(module) ? null : module.Trim();
        Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
        Actor = string.IsNullOrWhiteSpace(actor) ? null : actor.Trim();
        Result = string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        FromUtc = from.HasValue ? new DateTimeOffset(from.Value, TimeSpan.Zero) : null;
        ToUtc = to.HasValue ? new DateTimeOffset(to.Value, TimeSpan.Zero) : null;
        PageSize = pageSize is > 0 ? pageSize : 20;

        // Resolve and expose the TD-24 origin-module scope for the module filter.
        var scope = _service.Authorization();
        if (scope.IsFailure)
        {
            ErrorMessage = scope.Error.Message;
            return;
        }
        VisibleModuleIds = scope.Value.VisibleOriginModuleIds;

        var filter = new HistoriaFilter(
            Query, EntityType: null, EntityId: null, Module, Action, Actor, Result,
            FromUtc, ToUtc, page < 1 ? 1 : page, PageSize);

        var resultQuery = await _service.QueryAsync(filter, HttpContext.RequestAborted);
        if (resultQuery.IsFailure)
        {
            ErrorMessage = resultQuery.Error.Message;
            return;
        }

        Histories = resultQuery.Value;
    }
}