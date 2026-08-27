namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Discriminator of the tool domain behind a warehouse identity.
/// Normal CM/MF/BQ warehouse identity comes from the Ferramentas master. The
/// Boquilhas discriminator is retained for compatibility with identities from
/// the separate BQ external-repair workflow; it is not a second BQ master.
/// </summary>
public enum WarehouseToolDomain
{
    Ferramentas,
    Boquilhas
}
