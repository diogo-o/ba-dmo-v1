using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Auth;
using BA.Dmo.Infrastructure.Identity;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Web.Cli;

/// <summary>
/// One-shot privileged bootstrap of the first Admin (Plan-V3 GLM-ARCH-15,
/// GLM-ACC-13, 06_DATA §15, PV-08). CLI ONLY:
/// <code>dotnet BA.Dmo.Web.dll bootstrap-admin</code>
/// Explicit, idempotent, auditable. No HTTP setup endpoint, no HostedService,
/// no automatic startup bootstrap, no anonymous admin, no default
/// credentials. The service_role credential is used exclusively here, stays
/// server-side, and never appears in messages. Missing configuration fails
/// clearly with a non-zero exit code.
/// </summary>
public static class BootstrapAdminCommand
{
    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;
    public const int ConfigurationErrorExitCode = 2;

    public static int Run() =>
        Run(Environment.GetEnvironmentVariable, Console.Out, Console.Error);

    public static int Run(
        Func<string, string?> environment,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // 1. Explicit configuration — nothing defaulted, nothing hardcoded.
        var supabaseUrl = SupabaseSettings.ResolveUrl(environment);
        var serviceRoleKey = SupabaseSettings.ResolveServiceRoleKey(environment);
        var email = environment(SupabaseSettings.BootstrapEmailVariable);
        var password = environment(SupabaseSettings.BootstrapPasswordVariable);
        var displayName = environment(SupabaseSettings.BootstrapDisplayNameVariable);

        var missing = new List<string>();
        if (supabaseUrl is null) missing.Add(SupabaseSettings.UrlVariable);
        if (serviceRoleKey is null) missing.Add(SupabaseSettings.ServiceRoleKeyVariable);
        if (string.IsNullOrWhiteSpace(email)) missing.Add(SupabaseSettings.BootstrapEmailVariable);
        if (string.IsNullOrWhiteSpace(password)) missing.Add(SupabaseSettings.BootstrapPasswordVariable);
        if (string.IsNullOrWhiteSpace(displayName)) missing.Add(SupabaseSettings.BootstrapDisplayNameVariable);

        if (missing.Count > 0)
        {
            stderr.WriteLine(
                "BA DMO bootstrap-admin: missing required configuration: " +
                string.Join(", ", missing) +
                ". Provide explicit environment variables; nothing is defaulted. " +
                "The service-role key is used only by this privileged CLI operation.");
            return ConfigurationErrorExitCode;
        }

        // 2. Database connection (U-02 contract).
        IDbConnectionFactory connectionFactory;
        try
        {
            connectionFactory = DbConnectionFactory.FromEnvironment(environment);
        }
        catch (DatabaseConnectionException ex)
        {
            stderr.WriteLine($"BA DMO bootstrap-admin: {ex.Message}");
            return ConfigurationErrorExitCode;
        }

        // 3. One-shot operation. This CLI path constructs its OWN provisioning
        //    adapter instance; a separate instance is registered in the Web
        //    pipeline for the admin.gerir-gated user create/reset use cases
        //    (TD-16) — neither ever exposes the service-role credential.
        using var httpClient = new HttpClient();
        var provisioning = new SupabaseAdminProvisioningAdapter(
            httpClient, supabaseUrl, serviceRoleKey);
        var repository = new DapperInternalUserRepository(connectionFactory);
        var service = new BootstrapAdminService(
            provisioning, repository, SystemClock.Instance);

        try
        {
            var result = service.RunAsync(
                new BootstrapAdminOptions(email!, password!, displayName!)).GetAwaiter().GetResult();

            if (result.IsSuccess)
            {
                stdout.WriteLine(result.Value switch
                {
                    BootstrapAdminOutcome.AlreadyExists =>
                        "BA DMO bootstrap-admin: a valid Admin already exists; nothing was created (idempotent).",
                    BootstrapAdminOutcome.PreExistedRecovered =>
                        "BA DMO bootstrap-admin: first Admin created; the Auth account already existed.",
                    _ =>
                        "BA DMO bootstrap-admin: first Admin created (template + internal user + audit event)."
                });
                if (result.Value == BootstrapAdminOutcome.PreExistedRecovered)
                {
                    // D-HI4-1: the recovery link itself is never printed or
                    // logged — only the fact that one was requested, and that
                    // it was sent to the account's own email.
                    stderr.WriteLine(
                        "BA DMO bootstrap-admin: a recovery link was automatically requested " +
                        "for the pre-existing Auth account and sent to its email. Open it to " +
                        "set a known password.");
                }
                return SuccessExitCode;
            }

            stderr.WriteLine(
                $"BA DMO bootstrap-admin: FAILED — [{result.Error.Category}] {result.Error.Code}: {result.Error.Message}");
            return FailureExitCode;
        }
        catch (Exception ex)
        {
            // Never echo credentials: only the translated message.
            stderr.WriteLine($"BA DMO bootstrap-admin: FAILED — {ex.Message}");
            return FailureExitCode;
        }
    }
}
