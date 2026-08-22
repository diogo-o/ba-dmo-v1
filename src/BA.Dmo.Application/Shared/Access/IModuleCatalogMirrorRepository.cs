namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// One row of module_catalog_mirror (U-02 N02). The mirror serves Admin UI
/// ordering/display ONLY — it never grants access (TD-10, GLM-ACC-03).
/// </summary>
public sealed record ModuleCatalogMirrorRow(
    string ModuleId,
    string DisplayName,
    int DisplayOrder,
    bool Active,
    DateTimeOffset SyncedAtUtc);

/// <summary>
/// Access port for the catalog mirror (implementations in Infrastructure;
/// the catalog domain/application logic stays separate from persistence).
/// </summary>
public interface IModuleCatalogMirrorRepository
{
    Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the mirror content atomically.</summary>
    Task UpsertAllAsync(
        IReadOnlyList<ModuleCatalogMirrorRow> rows,
        CancellationToken cancellationToken = default);
}
