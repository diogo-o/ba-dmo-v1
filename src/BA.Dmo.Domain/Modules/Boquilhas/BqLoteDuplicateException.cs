namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// Raised by the persistence layer when a Boquilhas lot create hits
/// <c>uq_bq_lotes_reference_batch</c> (N03): a concurrent create already
/// inserted the same (reference, batch_code). The service maps this to the
/// same structured domain conflict as the pre-check (BQ_DUPLICATE_LOT) so both
/// the fast path and the race path report the same clean error (audit BQ-15).
/// </summary>
public sealed class BqLoteDuplicateException : Exception
{
    public BqLoteDuplicateException(string message)
        : base(message)
    {
    }
}