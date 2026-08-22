using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages;

/// <summary>
/// Safe state (Plan-V3 GLM-SHL-06, GLM-ACC-01.6): valid session whose
/// identity grants nothing, or whose identity could not be resolved because
/// the backend is unavailable — message, logout available, no data. Never
/// silently elevated to any module (GLM-ARCH-18).
///
/// Two distinct messages, one safe page:
///  - default: the identity resolved but no module/first page is authorized
///    (INTERNAL_USER_INACTIVE / ACCESS_TEMPLATE_INACTIVE / no page);
///  - ?indisponivel=1: the identity could NOT be resolved because the
///    database backend is unavailable (IDENTITY_RESOLUTION_UNAVAILABLE,
///    BackendUnavailable). The technical detail stays server-side (log only).
/// </summary>
[AllowAnonymous]
public class NoAccessModel : PageModel
{
    /// <summary>True when the cause is a transient backend failure, not "no modules".</summary>
    public bool BackendUnavailable { get; private set; }

    public void OnGet()
    {
        // Read the flag explicitly from the query string. Accept both the
        // boolean literals and the "1"/"0" form (bool.TryParse does NOT
        // accept "1", which is the convention used in the redirect URLs).
        var flag = false;
        if (Request.Query.TryGetValue("indisponivel", out var value))
        {
            var s = value.ToString();
            flag = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        BackendUnavailable = flag;
    }
}
