using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Armazém-owned abstraction over tool/lot identity for warehouse
/// operations. Armazém depends ONLY on this port — never on a tool-owner
/// repository or type. The CM/MF resolver adapts Ferramentas; a future resolver
/// adapts Boquilhas without changing <c>ArmazemService</c> (owner decision C).
/// </summary>
public interface IToolIdentityResolver
{
    Task<IReadOnlyList<WarehouseToolIdentity>> SearchAsync(
        string type,
        string? reference,
        string? lot,
        CancellationToken ct = default);

    Task<WarehouseToolIdentity?> ResolveAsync(
        Guid toolId,
        CancellationToken ct = default);
}