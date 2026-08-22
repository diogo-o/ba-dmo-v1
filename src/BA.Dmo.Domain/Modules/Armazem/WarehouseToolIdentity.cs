namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Armazém-owned projection of a tool/lot identity resolved through an
/// Armazém <c>IToolIdentityResolver</c>. This is an OWN abstraction: it never
/// leaks a tool-owner type (Ferramentas/Boquilhas) into warehouse code, keeping
/// warehouse ownership decoupled so a future Boquilhas resolver can be added
/// without redesigning it.
/// </summary>
public sealed record WarehouseToolIdentity(
    Guid ToolId,
    WarehouseToolDomain Domain,
    string Type,
    string Reference,
    string Lot,
    string? TechnicalName);