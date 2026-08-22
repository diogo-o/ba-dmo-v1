using System.Data;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-15 — Dapper implementation of the Ferramentas read-only physical-piece lookup
/// (IFerramentasPieceLookup) over N04 <c>physical_pieces</c>/<c>tool_lotes</c>/
/// <c>tool_references</c>. Read-only: exposes the stable <c>physical_piece_id</c>,
/// its parent lot, and the lot/reference identity. Never mutates Ferramentas.
/// </summary>
public sealed class DapperFerramentasPieceLookup : IFerramentasPieceLookup
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperFerramentasPieceLookup(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<FerramentasPieceHit>> SearchAsync(
        FerramentasToolType type, string? reference, string? lot, string? number, CancellationToken ct = default)
    {
        const string sql = @"
SELECT p.physical_piece_id, p.tool_lote_id, p.number, l.tool_reference_id, l.lote,
       r.tool_type, r.ref_code, r.technical_name
FROM physical_pieces p
JOIN tool_lotes l ON l.tool_lote_id = p.tool_lote_id
JOIN tool_references r ON r.tool_reference_id = l.tool_reference_id
WHERE r.tool_type = @Type
  AND (@Reference IS NULL OR r.ref_code ILIKE '%'||@Reference||'%')
  AND (@Lot IS NULL OR l.lote ILIKE '%'||@Lot||'%')
  AND (@Number IS NULL OR p.number ILIKE '%'||@Number||'%')
ORDER BY r.ref_code, l.lote, p.number;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Type = FerramentasToolTypeCodec.ToStorage(type),
                Reference = reference,
                Lot = lot,
                Number = number
            }, cancellationToken: ct);
            return rows.Select<dynamic, FerramentasPieceHit>(r => Map(r)).ToList().AsReadOnly();
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    public async Task<FerramentasPieceHit?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT p.physical_piece_id, p.tool_lote_id, p.number, l.tool_reference_id, l.lote,
       r.tool_type, r.ref_code, r.technical_name
FROM physical_pieces p
JOIN tool_lotes l ON l.tool_lote_id = p.tool_lote_id
JOIN tool_references r ON r.tool_reference_id = l.tool_reference_id
WHERE p.physical_piece_id = @PhysicalPieceId;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { PhysicalPieceId = physicalPieceId }, cancellationToken: ct);
            return row is null ? null : Map(row);
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    private static FerramentasPieceHit Map(dynamic row) =>
        new(
            row.physical_piece_id,
            row.tool_lote_id,
            row.tool_reference_id,
            FerramentasToolTypeCodec.FromStorage(row.tool_type),
            (string)row.ref_code,
            (string)row.lote,
            (string)row.number,
            row.technical_name as string);

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}