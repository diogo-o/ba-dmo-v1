namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Record holding the inherited tool identity for a Pegamento component.
/// Populated from the pinned revision's job_on_component rows only.
/// Historical data: reference + lot + nominal. No TechnicalName required.
/// </summary>
public sealed record PegamentoToolSnapshot(
    PegamentoComponentKey Key,
    string ReferenceSnapshot,
    string? LotSnapshot);