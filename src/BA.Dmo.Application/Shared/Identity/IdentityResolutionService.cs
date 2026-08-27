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
/// Server-side identity resolution pipeline. The final access model is one
/// reusable template per user: template title/function + exactly one functional
/// profile + canonical module grants. Any legacy hybrid assignment fails closed
/// rather than merging Admin/Operador/Responsável access surfaces.
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

        var associatedTemplates = record.AccessTemplates is { Count: > 0 }
            ? record.AccessTemplates
            :
            [
                new InternalUserAccessTemplateRecord(
                    record.TemplateId,
                    record.TemplateName,
                    record.TemplateActive,
                    record.ModulesJson)
            ];

        var activeTemplates = associatedTemplates
            .Where(template => template.TemplateActive)
            .ToList();

        if (activeTemplates.Count == 0)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "ACCESS_TEMPLATE_INACTIVE",
                    "The access template is missing or inactive."));

        if (activeTemplates.Count != 1)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "ACCESS_TEMPLATE_AMBIGUOUS",
                    "O utilizador tem mais do que um template ativo associado. Corrija a configuração na Administração."));

        if (!FunctionalProfileNames.TryParse(record.ProfileTitle, out var profile))
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "FUNCTIONAL_PROFILE_INVALID",
                    "The internal user has no valid functional profile."));

        var effectiveTemplate = activeTemplates[0];
        var parsed = AccessTemplateGrantsParser.Parse(effectiveTemplate.ModulesJson);
        if (parsed.IsFailure)
            return Result<ResolvedIdentity, DomainError>.Failure(
                DomainError.Unauthorized(
                    "ACCESS_TEMPLATE_INACTIVE",
                    "The access grants cannot grant access."));

        var template = new AccessTemplateDefinition(
            effectiveTemplate.TemplateId,
            effectiveTemplate.TemplateName,
            active: true,
            parsed.Value);

        var access = _accessResolver.Resolve([template], profile);
        var firstPage = _accessResolver.ResolveFirstPage(access);

        var currentUser = new CurrentUser(
            record.AuthUserId,
            record.DisplayName,
            access.AuthorizedModuleIds,
            access.GrantedCapabilityIds);

        return Result<ResolvedIdentity, DomainError>.Success(new ResolvedIdentity(
            currentUser,
            record.ActorId,
            effectiveTemplate.TemplateName,
            access,
            firstPage));
    }
}
