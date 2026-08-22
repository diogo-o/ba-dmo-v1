using System.Security.Claims;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace BA.Dmo.Web.Pages.Auth;

/// <summary>
/// Login page (Plan-V3 GLM-ACC-01, 05_SHL §5): Supabase Auth verifies the
/// credentials through the adapter; the session cookie then carries ONLY the
/// auth user id. Internal identity/grants are resolved server-side per
/// request. Error messages are generic (never reveal whether the email
/// exists — design contract). The post-login destination is the U-04
/// first page resolved from the effective access surface: functional users
/// land on the Job On landing; an admin (no jobon.view by owner decision)
/// lands on /admin; a resolved identity without any authorized page, or a
/// backend-unavailable resolution, lands on the /no-access safe state.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ISupabaseAuthAdapter _authAdapter;
    private readonly IdentityResolutionService _resolutionService;
    private readonly ILogger<LoginModel>? _logger;

    public LoginModel(
        ISupabaseAuthAdapter authAdapter,
        IdentityResolutionService resolutionService,
        ILogger<LoginModel>? logger = null)
    {
        _authAdapter = authAdapter;
        _resolutionService = resolutionService;
        _logger = logger;
    }

    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// The submitted email, re-shown on the page after a failed attempt so
    /// the user does not have to retype it. Standard model binding: the form
    /// field <c>email</c> binds here on POST. The password is intentionally
    /// NOT bound — it is only a handler argument, never stored or rendered.
    /// </summary>
    [BindProperty]
    public string? Email { get; set; }

    public void OnGet()
    {
        // A session without access still lands here safely; no redirect loop.
    }

    public async Task<IActionResult> OnPostAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Credenciais inválidas.";
            return Page();
        }

        var signIn = await _authAdapter.SignInWithPasswordAsync(email, password, HttpContext.RequestAborted);
        if (signIn.IsFailure)
        {
            // The real provider reason (HTTP status + GoTrue error) is logged
            // server-side; the browser only ever sees one of two generic
            // messages (no email-existence disclosure — design contract).
            _logger?.LogWarning(
                "Login failed for email={Email}: [{Category}] {Code}: {Message}",
                email, signIn.Error.Category, signIn.Error.Code, signIn.Error.Message);

            // Misconfiguration/provider outage must NOT masquerade as bad
            // credentials — otherwise a missing ANON key looks like a wrong
            // password and blocks the only Admin of the system.
            ErrorMessage = signIn.Error.Category == ErrorCategory.BackendUnavailable
                ? "Autenticação temporariamente indisponível. Tente novamente em instantes."
                : "Credenciais inválidas.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(SessionClaims.AuthUserIdClaimType, signIn.Value.AuthUserId.ToString())],
            SessionClaims.AuthenticationScheme);
        await HttpContext.SignInAsync(
            SessionClaims.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true
            });

        // Authoritative post-login destination: U-04 resolution (Job On
        // landing; canonical fallback when genuinely unavailable; /no-access
        // safe state otherwise).
        var resolution = await _resolutionService.ResolveAsync(
            signIn.Value.AuthUserId, HttpContext.RequestAborted);
        if (resolution.IsSuccess &&
            resolution.Value.FirstPage.Page is not null)
        {
            return Redirect(resolution.Value.FirstPage.Page.Route);
        }

        // The Supabase credentials were accepted but the internal identity did
        // not resolve. Two distinct user-facing safe states (never any access):
        //  - BackendUnavailable (DB unreachable): a transient infrastructure
        //    failure — the user HAS a mapping, it just could not be loaded;
        //  - everything else (inactive user/template, malformed grants, no
        //    authorized first page): the identity genuinely grants nothing.
        // The concrete technical cause is logged server-side only; the
        // browser never sees connection details, SQL or stack traces.
        if (resolution.IsFailure)
        {
            _logger?.LogWarning(
                "Post-login identity resolution failed for authUserId={AuthUserId}: [{Category}] {Code}: {Message}",
                signIn.Value.AuthUserId, resolution.Error.Category, resolution.Error.Code, resolution.Error.Message);

            if (resolution.Error.Category == ErrorCategory.BackendUnavailable)
                return Redirect("/no-access?indisponivel=1");
        }

        return Redirect("/no-access");
    }
}
