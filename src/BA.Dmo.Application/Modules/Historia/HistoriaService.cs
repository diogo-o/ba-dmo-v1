using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Historia;

/// <summary>
/// U-18 — História transversal read service (modules/11 §3/§4, contract §13).
/// Read-only: it presents persisted append-only audit facts from the modules
/// the caller's template grants (TD-24). It never writes and never reinterprets
/// current mutable state — every row carries the actor/time recorded at
/// execution time.
/// </summary>
public sealed class HistoriaService
{
    private readonly HistoriaAuthorizationGate _gate;
    private readonly IHistoriaRepository _repository;

    public HistoriaService(HistoriaAuthorizationGate gate, IHistoriaRepository repository)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Resolves the TD-24 origin-module scope for the current request (used by
    /// the page to build the module-filter options and the visibility guarantee).
    /// Failures here mean the <c>historia</c> module is not granted (Forbidden).
    /// </summary>
    public Result<HistoriaScope, DomainError> Authorization() => _gate.Require();

    /// <summary>
    /// Authorized transversal query: results are grouped by entity and ordered
    /// by latest event (newest first) with stable ordering inside each group.
    /// </summary>
    public async Task<Result<HistoriaQueryResult, DomainError>> QueryAsync(
        HistoriaFilter filter, CancellationToken cancellationToken = default)
    {
        var scope = _gate.Require();
        if (scope.IsFailure)
            return Result<HistoriaQueryResult, DomainError>.Failure(scope.Error);

        if (!HistoriaFilter.IsValidPageSize(filter.PageSize))
            return Result<HistoriaQueryResult, DomainError>.Failure(DomainError.Validation(
                "HISTORIA_PAGE_SIZE_INVALID",
                "A paginação canónica da História é 20/40/60."));

        if (filter.Page < 1)
            return Result<HistoriaQueryResult, DomainError>.Failure(DomainError.Validation(
                "HISTORIA_PAGE_INVALID", "A página deve ser maior ou igual a 1."));

        var value = scope.Value;
        var result = await _repository.QueryAsync(
            filter, value.VisibleOriginModuleIds, value.IncludeAdminWithAuditView,
            cancellationToken);
        return Result<HistoriaQueryResult, DomainError>.Success(result);
    }

    /// <summary>
    /// Authorized flat query (no grouping) for the JSON/detail path.
    /// </summary>
    public async Task<Result<IReadOnlyList<HistoriaEntryRow>, DomainError>> QueryFlatAsync(
        HistoriaFilter filter, CancellationToken cancellationToken = default)
    {
        var scope = _gate.Require();
        if (scope.IsFailure)
            return Result<IReadOnlyList<HistoriaEntryRow>, DomainError>.Failure(scope.Error);

        if (filter.Page < 1)
            return Result<IReadOnlyList<HistoriaEntryRow>, DomainError>.Failure(
                DomainError.Validation("HISTORIA_PAGE_INVALID", "A página deve ser maior ou igual a 1."));

        var value = scope.Value;
        var rows = await _repository.QueryFlatAsync(
            filter, value.VisibleOriginModuleIds, value.IncludeAdminWithAuditView,
            cancellationToken);
        return Result<IReadOnlyList<HistoriaEntryRow>, DomainError>.Success(rows);
    }
}