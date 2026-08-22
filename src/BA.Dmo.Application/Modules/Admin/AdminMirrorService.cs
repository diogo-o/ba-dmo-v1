using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Administration control of the catalog mirror (Plan-V3 04_ACC §9
/// "Aplicações (catálogo)", GLM-CAT-02 rule 3, TD-10). The code catalog
/// stays canonical: the mirror only serves Admin display/order, never
/// authorization. Unknown module ids cannot be created; mirror edits never
/// redefine capability ownership, routes or business rules. Changes are
/// validated before persist and audited.
/// </summary>
public sealed class AdminMirrorService
{
    private readonly AdminAuthorizationGate _gate;
    private readonly ModuleCatalog _catalog;
    private readonly IModuleCatalogMirrorRepository _mirrorRepository;
    private readonly ModuleCatalogMirrorSynchronizer _synchronizer;
    private readonly IAdminRepository _adminRepository;
    private readonly IClock _clock;

    public AdminMirrorService(
        AdminAuthorizationGate gate,
        ModuleCatalog catalog,
        IModuleCatalogMirrorRepository mirrorRepository,
        IAdminRepository adminRepository,
        IClock clock)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _mirrorRepository = mirrorRepository
            ?? throw new ArgumentNullException(nameof(mirrorRepository));
        _adminRepository = adminRepository ?? throw new ArgumentNullException(nameof(adminRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _synchronizer = new ModuleCatalogMirrorSynchronizer(catalog);
    }

    /// <summary>Effective Admin display list (mirror order for known modules,
    /// canonical completion for the rest).</summary>
    public async Task<Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>> GetDisplayAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>.Failure(gate.Error);

        var rows = await _mirrorRepository.GetAllAsync(cancellationToken);
        return Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>.Success(
            _synchronizer.MergeForDisplay(rows));
    }

    public async Task<Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>> SaveDisplayAsync(
        IReadOnlyList<MirrorEntryInput> entries,
        CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>.Failure(gate.Error);

        ArgumentNullException.ThrowIfNull(entries);

        // Only canonical modules may appear in the mirror — creating unknown
        // identifiers is impossible without an approved code change.
        var unknown = entries
            .Where(e => !_catalog.ContainsModule(e.ModuleId))
            .Select(e => e.ModuleId)
            .Distinct()
            .ToList();
        if (unknown.Count > 0)
            return Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>.Failure(
                DomainError.Validation(
                    "CATALOG_MIRROR_INVALID",
                    "Entradas fora do catálogo canónico: " + string.Join(", ", unknown)));

        var now = _clock.UtcNow;
        var rows = entries
            .Select(e => _catalog.TryGetModule(e.ModuleId, out var module)
                ? new ModuleCatalogMirrorRow(
                    module.ModuleId, module.DisplayName, e.DisplayOrder, e.Active, now)
                : null)
            .Where(r => r is not null)
            .Cast<ModuleCatalogMirrorRow>()
            .ToList();

        await _mirrorRepository.UpsertAllAsync(rows, cancellationToken);

        await _adminRepository.InsertAuditEventAsync(new AuditEntry(
            now,
            gate.Value.ActorId,
            gate.Value.DisplayName,
            CanonicalCapabilities.AdminModuleId,
            "mirror_update",
            "module_catalog_mirror",
            "module_catalog_mirror",
            $"{rows.Count} entries",
            "succeeded",
            null), cancellationToken);

        return Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>.Success(
            _synchronizer.MergeForDisplay(rows));
    }
}
