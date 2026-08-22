using System.Data;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-12 — Resolves active verification rules of a tool lote for the Job On contract
/// (modules/06 §8, GLM-FERR-08; modules/05 §7, GLM-JOB-07). Ferramentas is the
/// authoritative source of the rule configuration; inactive rules are excluded.
/// Reads ONLY tool_check_rules — no Job On table coupling.
/// </summary>
public sealed class DapperFerramentasRuleLookup : IFerramentasRuleLookup
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperFerramentasRuleLookup(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<VerificationRule>> ResolveActiveRulesAsync(
        Guid toolLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT tool_check_rule_id, rule_text, frequency
FROM tool_check_rules
WHERE tool_lote_id = @ToolLoteId AND active = TRUE
ORDER BY created_at_utc;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ToolLoteId = toolLoteId }, cancellationToken: ct);
            return rows.Select<dynamic, VerificationRule>(r => new VerificationRule(
                SourceRuleId: (Guid)r.tool_check_rule_id,
                RuleText: (string)r.rule_text,
                Frequency: MapFrequency((string)r.frequency))).ToList().AsReadOnly();
        }
        finally
        {
            if (conn is IAsyncDisposable a) await a.DisposeAsync();
            else conn.Dispose();
        }
    }

    private static VerificationFrequency MapFrequency(string storage) => storage.Trim().ToLowerInvariant() switch
    {
        "uma_vez_no_lote" => VerificationFrequency.OncePerLot,
        "por_fabrico" => VerificationFrequency.PerProduction,
        _ => VerificationFrequency.PerProduction
    };
}