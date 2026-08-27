using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// Result of a successful per-request identity resolution (GLM-ACC-01):
/// authoritative internal identity + effective access surface (U-04) +
/// first-page resolution. Grants are NEVER read from the cookie; this
/// structure is rebuilt server-side on every request.
/// </summary>
public sealed record ResolvedIdentity(
    CurrentUser User,
    string ActorId,
    string? ProfileTitle,
    EffectiveAccess Access,
    FirstPageResolution FirstPage);

/// <summary>
/// Server-side identity resolution pipeline (SCHEMA-RAT-03A, D-1/D-2). The
/// final access model is one reusable template per user resolved through the
/// canonical direct assignment:
///
///   internal_users.template_id
///      -> access_templates
///      -> access_template_profiles.functional_profile  (functional authority)
///      -> AccessResolver (template modules + profile-derived capabilities)
///
/// The N27 junction is NOT consulted and the legacy user-level profile
/// mirror column (retired in SCHEMA-RAT-03B) is NOT a functional-access
/// authority. Invalid/inconsistent data fails closed — never merged.
/// </summary>
public sealed class IdentityResolutionService
{
    private readonly IInternalUserRepository _repository;
    private readonly AccessResolver _accessResolver;
    private readonly Dictionary<Guid, Result<ResolvedIdentity, DomainError>> _requestCache = new();

    public IdentityResolutionService(
        IInternalUserRepository repository,
        AccessResolver accessResolver)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _accessResolver = accessResolver ?? throw new ArgumentNullException(nameof(accessResolver));
    }

    public async Task<Result<ResolvedIdentity, DomainError>> ResolveAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default)
    {
        if (authUserId == Guid.Empty)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "INTERNAL_USER_INACTIVE",
                    "No authenticated internal user is resolved for this session."));

        if (_requestCache.TryGetValue(authUserId, out var cached))
            return cached;

        var resolution = await ResolveUncachedAsync(authUserId, cancellationToken);
        _requestCache[authUserId] = resolution;
        return resolution;
    }

    private async Task<Result<ResolvedIdentity, DomainError>> ResolveUncachedAsync(
        Guid authUserId,
        CancellationToken cancellationToken)
    {
        InternalUserRecord? record;
        try
        {
            record = await _repository.FindByAuthUserIdAsync(authUserId, cancellationToken);
        }
        catch (AmbiguousIdentityException)
        {
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "IDENTITY_AMBIGUOUS",
                    "Conta com identidade ambígua; contacte um administrador."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.BackendUnavailable(
                    "IDENTITY_RESOLUTION_UNAVAILABLE",
                    "Internal identity could not be resolved. Try again."));
        }

        if (record is null || !record.UserActive)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "INTERNAL_USER_INACTIVE",
                    "The internal user is not registered or is inactive."));

        // D-2: the single effective template comes from the canonical direct
        // FK. No junction enumeration, no plural-template resolution, no
        // fallback that treats the junction as an equal authority. The direct
        // FK is NOT NULL + FK-constrained, so exactly one template exists.
        if (!record.TemplateActive)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "ACCESS_TEMPLATE_INACTIVE",
                    "The access template is missing or inactive."));

        // D-1: the functional profile is template-owned (access_template_profiles).
        // The legacy user-level profile mirror (retired in SCHEMA-RAT-03B) is
        // NOT the authority and is never parsed here.
        if (!FunctionalProfileNames.TryParse(record.FunctionalProfile, out var profile))
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "FUNCTIONAL_PROFILE_INVALID",
                    "The internal user has no valid functional profile."));

        var parsed = AccessTemplateGrantsParser.Parse(record.ModulesJson);
        if (parsed.IsFailure)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "ACCESS_TEMPLATE_INACTIVE",
                    "The access grants cannot grant access."));

        var template = new AccessTemplateDefinition(
            record.TemplateId,
            record.TemplateName,
            active: true,
            parsed.Value);

        var access = _accessResolver.Resolve([template], profile);
        var firstPage = _accessResolver.ResolveFirstPage(access);

        var currentUser = new CurrentUser(
            record.AuthUserId,
            record.DisplayName,
            access.AuthorizedModuleIds,
            access.GrantedCapabilityIds);

        // ProfileTitle carries the template title/function (visual/function
        // title) — the functional profile itself is profile, resolved above.
        return Result<ResolvedIdentity, DomainError>.Success(new ResolvedIdentity(
            currentUser,
            record.ActorId,
            record.TemplateName,
            access,
            firstPage));
    }
}