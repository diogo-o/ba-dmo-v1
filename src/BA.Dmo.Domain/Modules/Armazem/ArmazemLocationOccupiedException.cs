namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Raised by the persistence layer when a chk-then-insert (TOCTOU) race is
/// resolved: the target position is already actively occupied. Guarantees a
/// physical location never holds two DIFFERENT active tools under concurrency.
/// The application service maps this to a structured domain conflict
/// (ARMZ_POSITION_OCCUPIED) so both the fast-path pre-check and the atomic
/// write report the same clean error.
/// </summary>
public sealed class ArmazemLocationOccupiedException : Exception
{
    public ArmazemLocationOccupiedException(string message)
        : base(message)
    {
    }
}