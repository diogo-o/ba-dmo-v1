using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Shared;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-11 — Reads global application settings from the canonical app_settings
/// table (N11_partilhado.sql, 06_DATA §3.10). The Main Documents / Output
/// Directory is stored under the key "main_documents_output_root" as a
/// JSON string value.
/// </summary>
public sealed class DapperAppSettingsReader : IAppSettingsReader
{
    private const string OutputRootKey = "main_documents_output_root";

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperAppSettingsReader(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<string?> GetOutputRootAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT setting_value
FROM app_settings
WHERE setting_key = @SettingKey;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var raw = await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, sql, new { SettingKey = OutputRootKey }, cancellationToken: ct);

            if (raw is null)
                return null;

            var json = raw.setting_value?.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                    return doc.RootElement.GetString();

                if (doc.RootElement.TryGetProperty("value", out JsonElement valueProp) &&
                    valueProp.ValueKind == JsonValueKind.String)
                {
                    return valueProp.GetString();
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }
        finally
        {
            if (conn is IAsyncDisposable a) await a.DisposeAsync();
            else conn.Dispose();
        }
    }
}