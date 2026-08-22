namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// Minimal unit type used for rule/service results that carry no payload
/// (kept local — matches the pattern used by Reparação Interna).
/// </summary>
public readonly record struct ControloUnit
{
    public static ControloUnit Value => default;
}