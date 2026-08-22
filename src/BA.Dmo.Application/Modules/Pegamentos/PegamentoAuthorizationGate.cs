using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Pegamentos authorization gate (mirror of PesoAuthorizationGate).
/// Requires module pegamentos entry; fails closed (no resolved identity = Forbidden).
/// Resolves the canonical internal_users.actor_id via IPersistenceAuthorshipAccessor
/// (authenticated Supabase user → internal_users → actor_id) — never a technical
/// UUID substitution (GLM-DATA-02, GLM-ACC-01).
/// </summary>
public sealed class PegamentoAuthorizationGate
{
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public PegamentoAuthorizationGate(IPersistenceAuthorshipAccessor authorship)
    {
        _authorship = authorship;
    }

    /// <summary>
    /// Resolves the current canonical actor_id. Returns null when the user lacks
    /// access to the pegamentos module (fails closed).
    /// </summary>
    public string? ResolveActorId()
    {
        // IPersistenceAuthorshipAccessor resolves the authoritative internal_users.actor_id
        // from the authenticated session (never accepted from the client).
        return _authorship.Current.ActorId;
    }
}