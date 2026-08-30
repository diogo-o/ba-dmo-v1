using System.Text.Json;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>Create a new Job On (rascunho).</summary>
public sealed record CreateJobOnRequest(
    string ProductionCode,
    string MachineCode,
    DateTimeOffset? PlannedStartAt,
    DateTimeOffset? PlannedEndAt,
    string? Reference);

/// <summary>Duplicate a Job On from a source (modules/05 §6.2).</summary>
public sealed record DuplicateJobOnRequest(
    Guid SourceJobOnId,
    string ProductionCode,
    string MachineCode,
    DateTimeOffset? PlannedStartAt,
    DateTimeOffset? PlannedEndAt);

/// <summary>Save a new immutable revision (TD-18).</summary>
public sealed record SaveJobOnRevisionRequest(
    Guid JobOnId,
    string? GeneralNotes,
    string? ChangeReason,
    // Legacy transport member retained for compatibility. Article images are
    // reference-owned and are changed only through the dedicated image actions.
    string? ImageAssetId,
    IReadOnlyList<JobOnComponent> Components);

/// <summary>Transition the lifecycle state (TD-27).</summary>
public sealed record TransitionJobOnRequest(
    Guid JobOnId,
    JobOnLifecycleState NewState,
    string? CancelReason = null);

/// <summary>Associate an image with the current Article/Reference.</summary>
public sealed record AttachImageRequest(
    Guid JobOnId,
    string ImageAssetId);

/// <summary>Replace the image associated with the current Article/Reference.</summary>
public sealed record ReplaceImageRequest(
    Guid JobOnId,
    string ImageAssetId);

/// <summary>Remove the image associated with the current Article/Reference.</summary>
public sealed record RemoveImageRequest(
    Guid JobOnId);

/// <summary>
/// R011 — Body of <c>POST /api/jobon/current</c>: records the Job On this user
/// explicitly opened from the Universal Landing.
/// </summary>
public sealed record CurrentJobOnRequest(Guid JobOnId);

/// <summary>
/// Job On use cases (Plan-V3 modules/05, U-13). Every operation re-checks the
/// canonical capability server-side through the gate (GLM-ACC-04), executes
/// through the repository port, and records the module audit fact. Revisions
/// are immutable snapshots — saving always inserts a NEW revision, never a
/// destructive UPDATE (TD-18).
/// </summary>
public sealed class JobOnService
{
    private readonly JobOnAuthorizationGate _gate;
    private readonly IJobOnRepository _repository;
    private readonly IJobOnUserContextRepository _userContextRepository;
    private readonly IClock _clock;
    private readonly IArticleReferenceImageRepository? _articleImages;

    public JobOnService(
        JobOnAuthorizationGate gate,
        IJobOnRepository repository,
        IJobOnUserContextRepository userContextRepository,
        IClock clock,
        IArticleReferenceImageRepository? articleImages = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _userContextRepository = userContextRepository
            ?? throw new ArgumentNullException(nameof(userContextRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _articleImages = articleImages;
    }

    /// <summary>
    /// Create a new Job On in rascunho (modules/05 §6.1). Creation requires the
    /// minimum real production context — produção, referência e máquina — and
    /// atomically persists the header PLUS the initial immutable revision
    /// (revision 1) with that context: one transaction, so a Job On can never
    /// remain half-created. Tool associations are intentionally empty on
    /// creation (a rascunho may legitimately have none) and sections stay an
    /// optional user value (blank → "{}", never derived from machine).
    /// </summary>
    public async Task<Result<Guid, DomainError>> CreateAsync(
        CreateJobOnRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        var reference = request.Reference?.Trim();
        if (string.IsNullOrWhiteSpace(request.ProductionCode)
            || string.IsNullOrWhiteSpace(request.MachineCode)
            || string.IsNullOrWhiteSpace(reference))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_INVALID",
                "Produção, Referência e Máquina são obrigatórias."));

        var jobOn = new JobOnEntity(
            request.ProductionCode,
            request.MachineCode,
            request.PlannedStartAt,
            request.PlannedEndAt,
            Array.Empty<JobOnRevision>());

        var now = _clock.UtcNow.DateTime;
        // Initial immutable revision (TD-18): snapshot values come ONLY from the
        // user-entered context (production code, reference, machine, planned dates)
        // — never invented. Typed values (tipo/paragem/peso/processo), gota, notes
        // and tool components are absent on a fresh Job On.
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOn.Id,
            RevisionNumber = 1,
            ProductionSnapshot = SnapshotJson.Production(jobOn.ProductionCode),
            ReferenceSnapshot = SnapshotJson.Reference(reference!),
            MachineSnapshot = SnapshotJson.Machine(jobOn.MachineCode),
            DatesSnapshot = SnapshotJson.Dates(jobOn.PlannedStartAt, jobOn.PlannedEndAt),
            Sections = "{}",
            GeneralNotes = null,
            ChangeReason = null,
            SavedBy = gate.Value.ActorId,
            SavedAtUtc = now,
            Components = Array.Empty<JobOnComponent>()
        };

        Guid id;
        try
        {
            id = await _repository.CreateAtomicallyAsync(
                jobOn, initialRevision, gate.Value.ActorId, cancellationToken);
        }
        catch (JobOnIdentityDuplicateException)
        {
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "JOB_ON_IDENTITY_DUPLICATE",
                "Já existe um Job On não cancelado com esta produção e máquina."));
        }
        return Result<Guid, DomainError>.Success(id);
    }

    /// <summary>
    /// Duplicate a Job On (modules/05 §6.2): copies the full snapshot
    /// (components, fields, CAL rows, applicable occurrences), assigns a new
    /// id, new production/dates and <c>copied_from_job_on_id</c>. The source
    /// is immutable; occurrences are regenerated (never copied checks).
    /// </summary>
    public async Task<Result<Guid, DomainError>> DuplicateAsync(
        DuplicateJobOnRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.ProductionCode)
            || string.IsNullOrWhiteSpace(request.MachineCode))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_INVALID",
                "Produção e Máquina são obrigatórias."));

        var source = await _repository.GetByIdAsync(request.SourceJobOnId, cancellationToken);
        if (source is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On de origem não encontrado."));

        var duplicated = JobOnEntity.DuplicateFrom(
            source,
            request.ProductionCode,
            request.MachineCode,
            request.PlannedStartAt,
            request.PlannedEndAt,
            Array.Empty<JobOnRevision>());

        // Copy the source's current revision snapshot into a new (revision 1) graph.
        // GetByIdAsync fully hydrates the source aggregate, so the complete component /
        // field / CAL-row / verification graph is copied.
        var sourceRevision = source.CurrentRevision;
        if (sourceRevision is null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On de origem não tem uma revisão atual para duplicar."));

        var (revision, _) = CopyRevisionForDuplication(
            duplicated.Id, sourceRevision, _clock.UtcNow.DateTime, gate.Value.ActorId);

        // The whole new Job On + revision + children + current link + audit commit as ONE
        // logical transaction: no partially duplicated Job On can remain on failure.
        Guid id;
        try
        {
            id = await _repository.DuplicateAtomicallyAsync(
                duplicated, revision, request.SourceJobOnId, gate.Value.ActorId, cancellationToken);
        }
        catch (JobOnIdentityDuplicateException)
        {
            return Result<Guid, DomainError>.Failure(DomainError.DomainConflict(
                "JOB_ON_IDENTITY_DUPLICATE",
                "Já existe um Job On não cancelado com esta produção e máquina."));
        }

        return Result<Guid, DomainError>.Success(id);
    }

    /// <summary>
    /// Save a new immutable revision (TD-18). Editing a closed revision
    /// requires a change_reason (modules/05 §4/§5.4).
    /// </summary>
    public async Task<Result<Guid, DomainError>> SaveRevisionAsync(
        SaveJobOnRevisionRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        if (jobOn.LifecycleState == JobOnLifecycleState.Fechado
            && string.IsNullOrWhiteSpace(request.ChangeReason))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_CHANGE_REASON_REQUIRED",
                "Alterar uma revisão fechada exige um motivo (change_reason)."));

        // Complete immutable snapshot (JOB_ON_DATA_MODEL §1/§2, TD-18). The
        // production/machine/dates/reference come from the already-loaded Job On
        // header/context; revision-owned typed values (type/stop/weight/process)
        // come from the already-loaded current revision only — never invented, and
        // preserved as null when genuinely absent (owner D2 decision).
        var currentRevision = jobOn.CurrentRevision;

        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOn.Id,
            RevisionNumber = jobOn.RevisionCount + 1,
            ProductionSnapshot = SnapshotJson.Production(jobOn.ProductionCode),
            ReferenceSnapshot = currentRevision?.ReferenceSnapshot,
            MachineSnapshot = SnapshotJson.Machine(jobOn.MachineCode),
            DatesSnapshot = SnapshotJson.Dates(jobOn.PlannedStartAt, jobOn.PlannedEndAt),
            TypeSnapshot = currentRevision?.TypeSnapshot,
            StopSnapshot = currentRevision?.StopSnapshot,
            WeightSnapshot = currentRevision?.WeightSnapshot,
            ProcessSnapshot = currentRevision?.ProcessSnapshot,
            Sections = currentRevision?.Sections ?? "{}",
            DropCount = currentRevision?.DropCount,
            GeneralNotes = request.GeneralNotes,
            // The legacy revision column remains dormant for historical
            // compatibility. The active image association is master-reference owned.
            ImageAssetId = null,
            ChangeReason = request.ChangeReason,
            SavedBy = gate.Value.ActorId,
            SavedAtUtc = _clock.UtcNow.DateTime,
            Components = request.Components
        };

        // The complete graph (revision + components + fields + CAL rows + verifications) +
        // the current_revision_id advance + the audit event commit atomically in ONE
        // transaction — a current revision can never become partially persisted.
        await _repository.SaveRevisionGraphAsync(
            revision, "jobon.guardar", gate.Value.ActorId, cancellationToken);

        return Result<Guid, DomainError>.Success(revision.JobOnRevisionId);
    }

    /// <summary>Transition the lifecycle state with validation (TD-27).</summary>
    public async Task<Result<JobOnLifecycleState, DomainError>> TransitionAsync(
        TransitionJobOnRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<JobOnLifecycleState, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<JobOnLifecycleState, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        try
        {
            var now = _clock.UtcNow.UtcDateTime;
            switch (request.NewState)
            {
                case JobOnLifecycleState.Fechado:
                    jobOn.Close(now);
                    break;
                case JobOnLifecycleState.Cancelado:
                    jobOn.Cancel(request.CancelReason ?? string.Empty, gate.Value.ActorId, now);
                    break;
                default:
                    jobOn.TransitionTo(request.NewState);
                    break;
            }
        }
        catch (Exception ex)
        {
            return Result<JobOnLifecycleState, DomainError>.Failure(DomainError.DomainConflict(
                "JOBON_INVALID_TRANSITION", ex.Message));
        }

        await _repository.TransitionLifecycleAsync(
            jobOn, gate.Value.ActorId, cancellationToken);
        return Result<JobOnLifecycleState, DomainError>.Success(jobOn.LifecycleState);
    }

    /// <summary>
    /// Canonical activity lookup <c>Resolve(line, at)</c> (TD-27, modules/05 §5.5).
    /// Requires <c>jobon.view</c>; consumers block in an actionable way on None.
    /// </summary>
    public async Task<Result<JobOnResolution, DomainError>> ResolveAsync(
        string line, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonViewCapabilityId);
        if (gate.IsFailure)
            return Result<JobOnResolution, DomainError>.Failure(gate.Error);

        var candidates = await _repository.GetActiveAsync(line, cancellationToken: cancellationToken);
        return Result<JobOnResolution, DomainError>.Success(
            JobOnActivityResolver.Resolve(candidates, at));
    }

    /// <summary>
    /// Confirm a verification occurrence (modules/05 §7, <c>jobon.confirmar</c>).
    /// Persists operator/date and <c>completion_source = manual_job_on</c>.
    /// </summary>
    public async Task<Result<Unit, DomainError>> ConfirmVerificationAsync(
        Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonConfirmarCapabilityId);
        if (gate.IsFailure)
            return Result<Unit, DomainError>.Failure(gate.Error);

        await _repository.UpdateVerificationStatusAsync(
            occurrenceId, "confirmada", gate.Value.ActorId, _clock.UtcNow.DateTime, cancellationToken);
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    /// <summary>
    /// Associates an image with the current Article/Reference. Requires jobon.edit.
    /// The association write and audit fact are atomic; no Job On revision is created.
    /// </summary>
    public async Task<Result<ArticleReferenceImage, DomainError>> AttachImageAsync(
        AttachImageRequest request, CancellationToken cancellationToken = default)
    {
        return await SetArticleImageAsync(
            request.JobOnId,
            request.ImageAssetId,
            "jobon.referencia.imagem.anexar",
            cancellationToken);
    }

    /// <summary>
    /// Replaces the image associated with the current Article/Reference.
    /// The association write and audit fact are atomic; no Job On revision is created.
    /// </summary>
    public async Task<Result<ArticleReferenceImage, DomainError>> ReplaceImageAsync(
        ReplaceImageRequest request, CancellationToken cancellationToken = default)
    {
        return await SetArticleImageAsync(
            request.JobOnId,
            request.ImageAssetId,
            "jobon.referencia.imagem.substituir",
            cancellationToken);
    }

    /// <summary>
    /// Removes the image associated with the current Article/Reference.
    /// The association delete and audit fact are atomic; no Job On revision is created.
    /// Uses atomic InsertImageMutationAsync for transactional safety.
    /// </summary>
    public async Task<Result<ArticleReferenceImage, DomainError>> RemoveImageAsync(
        RemoveImageRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<ArticleReferenceImage, DomainError>.Failure(gate.Error);

        if (_articleImages is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.DomainConflict(
                "JOBON_IMAGE_STORE_UNAVAILABLE",
                "O repositório de imagens por Referência não está disponível."));

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        if (currentRevision is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On não tem uma revisão atual com Referência."));

        var referenceCode = ArticleReferenceImageRules.ExtractReferenceCode(currentRevision.ReferenceSnapshot);
        if (string.IsNullOrWhiteSpace(referenceCode))
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_REFERENCE_MISSING",
                "O Job On não tem uma Referência legível para associar a imagem."));

        var existing = await _articleImages.GetAsync(referenceCode, cancellationToken);
        if (existing is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_IMAGE",
                "Não existe imagem associada à Referência para remover."));

        await _articleImages.RemoveAsync(
            referenceCode,
            jobOn.Id,
            currentRevision.JobOnRevisionId,
            "jobon.referencia.imagem.remover",
            existing.ImageAssetId,
            gate.Value.ActorId,
            _clock.UtcNow,
            cancellationToken);

        return Result<ArticleReferenceImage, DomainError>.Success(existing);
    }

    private async Task<Result<ArticleReferenceImage, DomainError>> SetArticleImageAsync(
        Guid jobOnId,
        string imageAssetId,
        string eventType,
        CancellationToken cancellationToken)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<ArticleReferenceImage, DomainError>.Failure(gate.Error);

        if (_articleImages is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.DomainConflict(
                "JOBON_IMAGE_STORE_UNAVAILABLE",
                "O repositório de imagens por Referência não está disponível."));

        if (!ArticleReferenceImageRules.TryNormalizeImageAssetId(imageAssetId, out var normalizedAssetId))
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_IMAGE_INVALID",
                "Selecione um ficheiro de imagem válido no diretório de imagens da empresa."));

        var jobOn = await _repository.GetByIdAsync(jobOnId, cancellationToken);
        if (jobOn is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        if (currentRevision is null)
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On não tem uma revisão atual com Referência."));

        var referenceCode = ArticleReferenceImageRules.ExtractReferenceCode(currentRevision.ReferenceSnapshot);
        if (string.IsNullOrWhiteSpace(referenceCode))
            return Result<ArticleReferenceImage, DomainError>.Failure(DomainError.Validation(
                "JOBON_REFERENCE_MISSING",
                "O Job On não tem uma Referência legível para associar a imagem."));

        var before = await _articleImages.GetAsync(referenceCode, cancellationToken);
        var association = new ArticleReferenceImage(
            referenceCode,
            normalizedAssetId,
            gate.Value.ActorId,
            _clock.UtcNow);

        await _articleImages.SetAsync(
            association,
            jobOn.Id,
            currentRevision.JobOnRevisionId,
            eventType,
            before?.ImageAssetId,
            gate.Value.ActorId,
            _clock.UtcNow,
            cancellationToken);

        return Result<ArticleReferenceImage, DomainError>.Success(association);
    }

    /// <summary>
    /// R011 — Records the Job On this user EXPLICITLY opened/selected (Owner §14/§15).
    /// Requires <c>jobon.view</c> (viewing planning is enough to open the folha). Only
    /// records the stable user-scoped context of the specified <paramref name="jobOnId"/>;
    /// it is NOT the globally-newest Job On, NOT a clock/current-production derivation.
    /// Missing Job On → NotFound (never fabricates a context).
    /// </summary>
    public async Task<Result<Unit, DomainError>> SetCurrentOpenAsync(
        Guid jobOnId, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonViewCapabilityId);
        if (gate.IsFailure)
            return Result<Unit, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(jobOnId, cancellationToken);
        if (jobOn is null)
            return Result<Unit, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        var reference = ArticleReferenceImageRules.ExtractReferenceCode(
            currentRevision?.ReferenceSnapshot);

        await _userContextRepository.SetCurrentAsync(
            gate.Value.ActorId,
            jobOn.Id,
            jobOn.ProductionCode,
            reference,
            jobOn.MachineCode,
            cancellationToken);

        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    /// <summary>
    /// R011 — Reads the Job On context this user explicitly opened/selected, or NotFound
    /// when none is recorded. Requires <c>jobon.view</c>. The exact production identity is
    /// preserved (job_on_id), ready for consumers such as a future Controlo
    /// "Carregar Job On atual".
    /// </summary>
    public async Task<Result<JobOnUserCurrent, DomainError>> GetCurrentOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonViewCapabilityId);
        if (gate.IsFailure)
            return Result<JobOnUserCurrent, DomainError>.Failure(gate.Error);

        var current = await _userContextRepository.GetCurrentAsync(gate.Value.ActorId, cancellationToken);
        if (current is null)
            return Result<JobOnUserCurrent, DomainError>.Failure(DomainError.NotFound(
                "JOBON_CURRENT_NOT_FOUND", "Nenhum Job On aberto pelo utilizador."));

        return Result<JobOnUserCurrent, DomainError>.Success(current);
    }

    private static (JobOnRevision Revision, IReadOnlyList<JobOnComponent> Components) CopyRevisionForDuplication(
        Guid newJobOnId, JobOnRevision source, DateTime now, string actorId)
    {
        // The duplicated revision is a NEW immutable snapshot (revision 1) with a new id.
        var newRevisionId = Guid.NewGuid();

        var components = new List<JobOnComponent>();
        foreach (var sourceComponent in source.Components ?? Array.Empty<JobOnComponent>())
        {
            var componentId = Guid.NewGuid();
            var fields = (sourceComponent.Fields ?? Array.Empty<JobOnComponentField>())
                .Select(f => f with { JobOnComponentFieldId = Guid.NewGuid(), JobOnComponentId = componentId })
                .ToList();
            var rows = (sourceComponent.Rows ?? Array.Empty<JobOnComponentRow>())
                .Select(r => r with { JobOnComponentRowId = Guid.NewGuid(), JobOnComponentId = componentId })
                .ToList();
            // Occurrences are regenerated pendente — never copied with checks.
            var verifications = (sourceComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
                .Select(v => v with
                {
                    JobOnVerificationOccurrenceId = Guid.NewGuid(),
                    JobOnComponentId = componentId,
                    Status = "pendente",
                    CompletedBy = null,
                    CompletedAtUtc = null,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                })
                .ToList();

            // Re-pin the copied component to the NEW revision id (not the source's), so the
            // whole in-memory graph is consistent — the same re-pinning the repository applies
            // on insert (R-002: all new child rows belong to the new revision).
            components.Add(sourceComponent with
            {
                JobOnComponentId = componentId,
                JobOnRevisionId = newRevisionId,
                Fields = fields,
                Rows = rows,
                Verifications = verifications
            });
        }

        var revision = new JobOnRevision
        {
            JobOnRevisionId = newRevisionId,
            JobOnId = newJobOnId,
            RevisionNumber = 1,
            ProductionSnapshot = source.ProductionSnapshot,
            ReferenceSnapshot = source.ReferenceSnapshot,
            MachineSnapshot = source.MachineSnapshot,
            DatesSnapshot = source.DatesSnapshot,
            Sections = source.Sections,
            DropCount = source.DropCount,
            TypeSnapshot = source.TypeSnapshot,
            StopSnapshot = source.StopSnapshot,
            WeightSnapshot = source.WeightSnapshot,
            ProcessSnapshot = source.ProcessSnapshot,
            GeneralNotes = source.GeneralNotes,
            // Article images are reference-owned. Legacy revision metadata is
            // intentionally not copied into a new production revision.
            ImageAssetId = null,
            SavedBy = actorId,
            SavedAtUtc = now,
            Components = components
        };

        return (revision, components);
    }
}

/// <summary>
/// Builds the canonical JSON payloads of the N05 job_on_revision *_snapshot
/// jsonb columns (owner D2 decision). The shape is fixed by the owner:
/// production_snapshot/reference_snapshot/machine_snapshot are { field: value },
/// dates_snapshot is { start_at, end_at }, and type/stop/weight/process are
/// { value }. Serialization must not invent sources: values are provided by
/// the caller from already-loaded authoritative context only.
/// </summary>
internal static class SnapshotJson
{
    public static string Production(string productionCode) =>
        JsonSerializer.Serialize(new { production_code = productionCode });

    public static string Reference(string referenceCode) =>
        JsonSerializer.Serialize(new { article_reference = referenceCode });

    public static string Machine(string machineCode) =>
        JsonSerializer.Serialize(new { machine_code = machineCode });

    public static string Dates(DateTimeOffset? startAt, DateTimeOffset? endAt) =>
        JsonSerializer.Serialize(new { start_at = startAt, end_at = endAt });
}

/// <summary>Unit result marker for void-like use cases.</summary>
public readonly record struct Unit
{
    public static Unit Value { get; } = new();
}
