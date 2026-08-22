namespace BA.Dmo.Application.Shared;

/// <summary>
/// Shared application settings reader — reads global configuration values
/// from the canonical app_settings table (N11_partilhado.sql).
/// </summary>
public interface IAppSettingsReader
{
    /// <summary>Gets the global Main Documents / Output Directory configuration.</summary>
    Task<string?> GetOutputRootAsync(CancellationToken ct = default);
}