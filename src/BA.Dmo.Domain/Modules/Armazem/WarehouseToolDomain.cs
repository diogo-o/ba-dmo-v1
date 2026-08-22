namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Discriminator of the tool domain behind a warehouse identity.
/// Ferramentas is the only supported domain in U-14 (CM/MF). Boquilhas is a
/// future domain added by another resolver without redesigning warehouse
/// ownership (owner decision C).
/// </summary>
public enum WarehouseToolDomain
{
    Ferramentas,
    Boquilhas
}