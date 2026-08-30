using System.Data;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-14 — Dapper implementation of the Ferramentas read-only identity lookup
/// (IFerramentasIdentityLookup) over N04 <c>tool_references</c>/<c>tool_lotes</c>.
/// Read-only: exposes canonical reference/lot/type + technical name. Never
/// mutates Ferramentas.
/// </summary>
public sealed class DapperFerramentasIdentityLookup : IFerramentasIdentityLookup
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperFerramentasIdentityLookup(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<FerramentasIdentityHit>> SearchAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT r.tool_reference_id, r.tool_type, r.ref_code, r.technical_name,
       l.tool_lote_id, l.lote
FROM tool_references r
JOIN tool_lotes l ON l.tool_reference_id = r.tool_reference_id
WHERE r.tool_type = @Type
  AND (@Reference IS NULL OR r.ref_code ILIKE '%'||@Reference||'%')
  AND (@Lot IS NULL OR l.lote ILIKE '%'||@Lot||'%')
ORDER BY r.ref_code, l.lote;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Type = FerramentasToolTypeCodec.ToStorage(type),
                Reference = reference,
                Lot = lot
            }, cancellationToken: ct);
            return rows.Select<dynamic, FerramentasIdentityHit>(r => Map(r)).ToList().AsReadOnly();
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    public async Task<FerramentasIdentityHit?> ResolveAsync(Guid toolLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT r.tool_reference_id, r.tool_type, r.ref_code, r.technical_name,
       l.tool_lote_id, l.lote
FROM tool_lotes l
JOIN tool_references r ON r.tool_reference_id = l.tool_reference_id
WHERE l.tool_lote_id = @ToolLoteId;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { ToolLoteId = toolLoteId }, cancellationToken: ct);
            return row is null ? null : Map(row);
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    public async Task<IReadOnlyList<FerramentasToolLoteOption>> SearchToolLoteOptionsAsync(
        FerramentasToolType type,
        string? reference,
        string? lot,
        string? line,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT r.tool_reference_id, r.tool_type, r.ref_code, r.technical_name,
       l.tool_lote_id, l.lote, l.allowed_lines
FROM tool_references r
JOIN tool_lotes l ON l.tool_reference_id = r.tool_reference_id
WHERE r.tool_type = @Type
  AND (@Reference IS NULL OR r.ref_code ILIKE '%'||@Reference||'%')
  AND (@Lot IS NULL OR l.lote ILIKE '%'||@Lot||'%')
  AND (@Line IS NULL OR @Line = ANY(l.allowed_lines))
ORDER BY r.ref_code, l.lote;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Type = FerramentasToolTypeCodec.ToStorage(type),
                Reference = reference,
                Lot = lot,
                Line = line
            }, cancellationToken: ct);
            return rows.Select<dynamic, FerramentasToolLoteOption>(r => MapOption(r)).ToList().AsReadOnly();
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    public async Task<FerramentasToolLoteOption?> ResolveToolLoteOptionAsync(Guid toolLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT r.tool_reference_id, r.tool_type, r.ref_code, r.technical_name,
       l.tool_lote_id, l.lote, l.allowed_lines
FROM tool_lotes l
JOIN tool_references r ON r.tool_reference_id = l.tool_reference_id
WHERE l.tool_lote_id = @ToolLoteId;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { ToolLoteId = toolLoteId }, cancellationToken: ct);
            return row is null ? null : MapOption(row);
        }
        finally
        {
            await DisposeAsync(conn);
        }
    }

    private static FerramentasToolLoteOption MapOption(dynamic row) =>
        new(
            row.tool_reference_id,
            row.tool_lote_id,
            FerramentasToolTypeCodec.FromStorage(row.tool_type),
            (string)row.ref_code,
            (string)row.lote,
            row.technical_name as string,
            ((row.allowed_lines as string[]) ?? Array.Empty<string>()).ToList().AsReadOnly());

    private static FerramentasIdentityHit Map(dynamic row) =>
        new(
            row.tool_reference_id,
            row.tool_lote_id,
            FerramentasToolTypeCodec.FromStorage(row.tool_type),
            (string)row.ref_code,
            (string)row.lote,
            row.technical_name as string);

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}