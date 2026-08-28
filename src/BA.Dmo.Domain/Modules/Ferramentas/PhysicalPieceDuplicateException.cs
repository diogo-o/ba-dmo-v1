namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Raised by the persistence layer when a physical piece registration hits
/// <c>uq_physical_pieces_lote_number</c> (N04): the same (tool_lote_id,
/// number) already exists under concurrency. The service maps this to a
/// structured domain conflict (FERRAMENTAS_PIECE_DUPLICATE) instead of a raw
/// 23505 (audit ON-02 / approved unique-violation mapping).
/// </summary>
public sealed class PhysicalPieceDuplicateException : Exception
{
    public PhysicalPieceDuplicateException(string message)
        : base(message)
    {
    }
}