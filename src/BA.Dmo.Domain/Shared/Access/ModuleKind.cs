namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// Kind of a catalog entry (Plan-V3 GLM-CAT-01 taxonomy).
/// An experience (e.g. Peso Operador/Responsável) is not a catalog entry: it is an interface
/// variant determined by a capability of its module.
/// </summary>
public enum ModuleKind
{
    /// <summary>Functional module with its own domain, data, routes and grants.</summary>
    Module,

    /// <summary>
    /// Functional area/domain (e.g. Controlo): has no grants of its own; exists when at least
    /// one authorized child exists; children are assignable separately (UD-14).
    /// </summary>
    FunctionalArea
}
