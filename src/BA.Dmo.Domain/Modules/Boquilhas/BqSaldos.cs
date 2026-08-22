namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Running balance projection of a BQ trace (GLM-BQ-07, C10).
/// Two notions are NEVER collapsed:
///   <b>physical inventory</b> = <see cref="Prod"/> + <see cref="Repair"/> +
///   <see cref="Irreparable"/> + accumulated <see cref="ExceptionalReceived"/>; and
///   <b>transactional balance</b> = raw RETURN(+) / DISPATCH(−) delta that
///   accumulates −unmatched (returns above expected).
/// <see cref="ExceptionalReceived"/> is the accumulated always-recorded excess of
/// returns over the expected repair balance (UD-08/UD-09) and is displayed
/// separately from production.
/// </summary>
public sealed class BqSaldos
{
    /// <summary>Quantity physically in production (in the plant, usable on lines).</summary>
    public decimal Prod { get; set; }

    /// <summary>Quantity currently out for repair.</summary>
    public decimal Repair { get; set; }

    /// <summary>Quantity declared non-repairable.</summary>
    public decimal Irreparable { get; set; }

    /// <summary>Accumulated exceptional received quantity (returns above expected).</summary>
    public decimal ExceptionalReceived { get; set; }

    /// <summary>Transactional balance (RETURN +qty / DISPATCH −qty), excluding unmatched.</summary>
    public decimal TransactionalBalance { get; set; }

    /// <summary>Physical inventory (prod + repair + irreparable + exceptional) at this point.</summary>
    public decimal PhysicalInventory => Prod + Repair + Irreparable + ExceptionalReceived;

    public BqSaldos Clone() => new()
    {
        Prod = Prod,
        Repair = Repair,
        Irreparable = Irreparable,
        ExceptionalReceived = ExceptionalReceived,
        TransactionalBalance = TransactionalBalance
    };
}