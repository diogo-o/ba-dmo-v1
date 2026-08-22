using System.Text.Json;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Administration use cases for access templates (Plan-V3 04_ACC §9, U-06).
/// Every write is validated server-side against the canonical catalog
/// (GLM-ACC-03): module ids must exist, capabilities must belong to their
/// module, functional areas take no grants (GLM-CAT-01), and invalid entries
/// are REJECTED — never silently granted or silently discarded. The U-04
/// catalog/normalizer is the single model; no second template model exists.
/// Self-lockout: GLM-ACC-10. Concurrency: GLM-ACC-12. Templates are
/// deactivated, never deleted (UD-10).
/// </summary>
public sealed class AdminTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AdminAuthorizationGate _gate;
    private readonly IAdminRepository _repository;
    private readonly GrantNormalizer _normalizer;
    private readonly IClock _clock;

    public AdminTemplateService(
        AdminAuthorizationGate gate,
        IAdminRepository repository,
        GrantNormalizer normalizer,
        IClock clock)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<IReadOnlyList<AdminTemplateRow>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Task.FromResult<IReadOnlyList<AdminTemplateRow>>(Array.Empty<AdminTemplateRow>());

        return _repository.ListTemplatesAsync(cancellationToken);
    }

    public async Task<Result<AdminTemplateRow, DomainError>> GetAsync(
        string templateId, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminTemplateRow, DomainError>.Failure(gate.Error);

        var template = await _repository.GetTemplateAsync(templateId, cancellationToken);
        return template is null
            ? Result<AdminTemplateRow, DomainError>.Failure(DomainError.NotFound(
                "ACCESS_TEMPLATE_NOT_FOUND", "Template de acesso não encontrado."))
            : Result<AdminTemplateRow, DomainError>.Success(template);
    }

    public async Task<Result<AdminTemplateRow, DomainError>> CreateAsync(
        CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminTemplateRow, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.TemplateId)
            || string.IsNullOrWhiteSpace(request.Name))
            return Result<AdminTemplateRow, DomainError>.Failure(DomainError.Validation(
                "ACCESS_TEMPLATE_INVALID",
                "O identificador e o nome do template são obrigatórios."));

        if (await _repository.GetTemplateAsync(request.TemplateId, cancellationToken) is not null)
            return Result<AdminTemplateRow, DomainError>.Failure(DomainError.DomainConflict(
                "ACCESS_TEMPLATE_EXISTS", "Já existe um template com este identificador."));

        var grants = ValidateGrants(request.Grants);
        if (grants.IsFailure)
            return Result<AdminTemplateRow, DomainError>.Failure(grants.Error);

        var now = _clock.UtcNow;
        await _repository.CreateTemplateAsync(
            request.TemplateId.Trim(), request.Name.Trim(), grants.Value, now, cancellationToken);

        await AuditAsync(gate.Value, "create", request.TemplateId.Trim(),
            request.Name.Trim(), "succeeded", null, now, cancellationToken);

        return Result<AdminTemplateRow, DomainError>.Success(new AdminTemplateRow(
            request.TemplateId.Trim(), request.Name.Trim(), grants.Value, Active: true, now));
    }

    public async Task<Result<AdminTemplateRow, DomainError>> UpdateAsync(
        UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminTemplateRow, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetTemplateAsync(request.TemplateId, cancellationToken);
        if (existing is null)
            return Result<AdminTemplateRow, DomainError>.Failure(DomainError.NotFound(
                "ACCESS_TEMPLATE_NOT_FOUND", "Template de acesso não encontrado."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<AdminTemplateRow, DomainError>.Failure(DomainError.Validation(
                "ACCESS_TEMPLATE_INVALID", "O nome do template não pode ficar vazio."));

        var grants = ValidateGrants(request.Grants);
        if (grants.IsFailure)
            return Result<AdminTemplateRow, DomainError>.Failure(grants.Error);

        var now = _clock.UtcNow;
        bool applied;
        try
        {
            applied = await _repository.UpdateTemplateAsync(
                request.TemplateId, request.Name.Trim(), grants.Value, request.Active,
                request.ExpectedUpdatedAt, now, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AdminTemplateRow, DomainError>.Failure(
                DomainError.ConcurrencyConflict("ADMIN_CONCURRENCY_CONFLICT", ex.Message));
        }

        if (!applied)
            return Result<AdminTemplateRow, DomainError>.Failure(DomainError.DomainConflict(
                "ADMIN_SELF_LOCKOUT",
                "Operação recusada: deve permanecer pelo menos um administrador ativo " +
                "com template ativo que conceda admin.gerir."));

        var action = request.Active != existing.Active
            ? (request.Active ? "activate" : "deactivate")
            : (grants.Value != existing.ModulesJson ? "update_modules" : "update");
        await AuditAsync(gate.Value, action, request.TemplateId, request.Name.Trim(),
            "succeeded", null, now, cancellationToken);

        return Result<AdminTemplateRow, DomainError>.Success(existing with
        {
            Name = request.Name.Trim(),
            ModulesJson = grants.Value,
            Active = request.Active,
            UpdatedAtUtc = now
        });
    }

    /// <summary>
    /// Strict canonical validation of submitted grants. Any entry outside the
    /// catalog (unknown module, capability not owned by the module, area
    /// grant, duplicates) rejects the whole write with an explicit report.
    /// Returns the canonical JSON persisted in access_templates.modules.
    /// </summary>
    private Result<string, DomainError> ValidateGrants(
        IReadOnlyList<TemplateGrantInput> grants)
    {
        var input = (grants ?? new List<TemplateGrantInput>())
            .Where(g => g is not null && !string.IsNullOrWhiteSpace(g.ModuleId))
            .Select(g => new ModuleGrant(
                g.ModuleId.Trim(),
                g.Capabilities ?? Array.Empty<string>()));

        var normalized = _normalizer.Normalize(input);
        if (normalized.DiscardedEntries.Count > 0)
            return Result<string, DomainError>.Failure(DomainError.Validation(
                "ACCESS_TEMPLATE_GRANTS_INVALID",
                "O template contém entradas fora do catálogo canónico: " +
                string.Join("; ", normalized.DiscardedEntries)));

        var payload = normalized.Grants
            .Select(g => new
            {
                moduleId = g.ModuleId,
                capabilities = g.Capabilities.OrderBy(c => c, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(g => g.moduleId, StringComparer.Ordinal);

        return Result<string, DomainError>.Success(
            JsonSerializer.Serialize(payload, JsonOptions));
    }

    private Task AuditAsync(
        AdminExecutor executor,
        string actionCode,
        string templateId,
        string templateName,
        string result,
        string? detail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        _repository.InsertAuditEventAsync(new AuditEntry(
            now,
            executor.ActorId,
            executor.DisplayName,
            CanonicalCapabilities.AdminModuleId,
            actionCode,
            "access_template",
            templateId,
            templateName,
            result,
            detail), cancellationToken);
}
