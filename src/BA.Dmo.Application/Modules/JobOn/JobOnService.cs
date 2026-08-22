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
    DateTimeOffset? PlannedEndAt);

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
    string? ImageAssetId,
    IReadOnlyList<JobOnComponent> Components);

/// <summary>Transition the lifecycle state (TD-27).</summary>
public sealed record TransitionJobOnRequest(
    Guid JobOnId,
    JobOnLifecycleState NewState);

/// <summary>Attach an image to the current revision (TD-23).</summary>
public sealed record AttachImageRequest(
    Guid JobOnId,
    string ImageAssetId);

/// <summary>Replace the image association on the current revision (TD-23).</summary>
public sealed record ReplaceImageRequest(
    Guid JobOnId,
    string ImageAssetId);

/// <summary>Remove the image association from the current revision (TD-23).</summary>
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

    public JobOnService(
        JobOnAuthorizationGate gate,
        IJobOnRepository repository,
        IJobOnUserContextRepository userContextRepository,
        IClock clock)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _userContextRepository = userContextRepository
            ?? throw new ArgumentNullException(nameof(userContextRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Create a new Job On in rascunho (modules/05 §6.1).</summary>
    public async Task<Result<Guid, DomainError>> CreateAsync(
        CreateJobOnRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.ProductionCode)
            || string.IsNullOrWhiteSpace(request.MachineCode))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_INVALID",
                "Produção e Máquina são obrigatórias."));

        var jobOn = new JobOnEntity(
            request.ProductionCode,
            request.MachineCode,
            request.PlannedStartAt,
            request.PlannedEndAt,
            Array.Empty<JobOnRevision>());

        var id = await _repository.CreateAsync(jobOn, cancellationToken);
        await _repository.InsertAuditEventAsync(
            id, null, "jobon.criar", null, null, gate.Value.ActorId, cancellationToken);
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
        var id = await _repository.DuplicateAtomicallyAsync(
            duplicated, revision, request.SourceJobOnId, gate.Value.ActorId, cancellationToken);

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
            ImageAssetId = request.ImageAssetId,
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
            jobOn.TransitionTo(request.NewState);
        }
        catch (Exception ex)
        {
            return Result<JobOnLifecycleState, DomainError>.Failure(DomainError.DomainConflict(
                "JOBON_INVALID_TRANSITION", ex.Message));
        }

        await _repository.UpdateLifecycleStateAsync(
            jobOn.Id, jobOn.LifecycleState, gate.Value.ActorId, cancellationToken);
        await _repository.InsertAuditEventAsync(
            jobOn.Id, null, "jobon.transicao", null, jobOn.LifecycleState.ToString(), gate.Value.ActorId, cancellationToken);
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
    /// Attach an image to the exact current revision (TD-23, modules/05 §5.7).
    /// Creates a new revision with the image_asset_id set. Requires jobon.edit.
    /// Records audit event with before/after image_asset_id.
    /// Uses atomic InsertImageMutationAsync for transactional safety.
    /// </summary>
    public async Task<Result<Guid, DomainError>> AttachImageAsync(
        AttachImageRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.ImageAssetId))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_IMAGE_INVALID",
                "O identificador da imagem é obrigatório."));

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        if (currentRevision is null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On não tem uma revisão atual para associar a imagem."));

        var beforeImageAssetId = currentRevision.ImageAssetId;

        var newRevision = currentRevision.CloneWithChanges(
            imageAssetId: request.ImageAssetId);

        await _repository.InsertImageMutationAsync(
            newRevision, jobOn.Id, "jobon.imagem.anexar",
            beforeImageAssetId, request.ImageAssetId, gate.Value.ActorId, cancellationToken);

        return Result<Guid, DomainError>.Success(newRevision.JobOnRevisionId);
    }

    /// <summary>
    /// Replace the image association on the current revision (TD-23, modules/05 §5.7).
    /// Creates a new revision with the new image_asset_id. Requires jobon.edit.
    /// Records audit event with before/after image_asset_id.
    /// Uses atomic InsertImageMutationAsync for transactional safety.
    /// </summary>
    public async Task<Result<Guid, DomainError>> ReplaceImageAsync(
        ReplaceImageRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.ImageAssetId))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_IMAGE_INVALID",
                "O identificador da imagem é obrigatório."));

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        if (currentRevision is null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On não tem uma revisão atual para substituir a imagem."));

        if (string.IsNullOrWhiteSpace(currentRevision.ImageAssetId))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_IMAGE",
                "Não existe imagem associada para substituir. Use anexar para adicionar uma nova imagem."));

        var beforeImageAssetId = currentRevision.ImageAssetId;

        var newRevision = currentRevision.CloneWithChanges(
            imageAssetId: request.ImageAssetId);

        await _repository.InsertImageMutationAsync(
            newRevision, jobOn.Id, "jobon.imagem.substituir",
            beforeImageAssetId, request.ImageAssetId, gate.Value.ActorId, cancellationToken);

        return Result<Guid, DomainError>.Success(newRevision.JobOnRevisionId);
    }

    /// <summary>
    /// Remove the image association from the current revision (TD-23, modules/05 §5.7).
    /// Creates a new revision with image_asset_id set to null. Requires jobon.edit.
    /// Records audit event with before/after image_asset_id.
    /// Uses CreateImageRemovalRevision to unambiguously clear the image association.
    /// Uses atomic InsertImageMutationAsync for transactional safety.
    /// </summary>
    public async Task<Result<Guid, DomainError>> RemoveImageAsync(
        RemoveImageRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(JobonModuleCatalog.JobonEditCapabilityId);
        if (gate.IsFailure)
            return Result<Guid, DomainError>.Failure(gate.Error);

        var jobOn = await _repository.GetByIdAsync(request.JobOnId, cancellationToken);
        if (jobOn is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "JOBON_NOT_FOUND", "Job On não encontrado."));

        var currentRevision = jobOn.CurrentRevision;
        if (currentRevision is null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_REVISION",
                "O Job On não tem uma revisão atual para remover a imagem."));

        if (string.IsNullOrWhiteSpace(currentRevision.ImageAssetId))
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "JOBON_NO_IMAGE",
                "Não existe imagem associada para remover."));

        var beforeImageAssetId = currentRevision.ImageAssetId;

        var newRevision = currentRevision.CreateImageRemovalRevision(
            gate.Value.ActorId, _clock.UtcNow.DateTime);

        await _repository.InsertImageMutationAsync(
            newRevision, jobOn.Id, "jobon.imagem.remover",
            beforeImageAssetId, null, gate.Value.ActorId, cancellationToken);

        return Result<Guid, DomainError>.Success(newRevision.JobOnRevisionId);
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
        var reference = ExtractReadableReference(currentRevision?.ReferenceSnapshot);

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

    /// <summary>
    /// Reads a readable reference string from a <c>reference_snapshot</c> jsonb value.
    /// The snapshot is either a plain string or <c>{ "reference": "..." }</c> (owner D2
    /// shape). Defaults to an empty string when nothing readable is present.
    /// </summary>
    private static string ExtractReadableReference(object? snapshot)
    {
        if (snapshot is null || snapshot is DBNull)
            return string.Empty;

        var raw = snapshot switch { string s => s, _ => snapshot.ToString() };
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString() ?? string.Empty;

            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("reference", out var refProp)
                && refProp.ValueKind == JsonValueKind.String)
                return refProp.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // Not JSON — treat the raw text as the readable reference.
            return raw.Trim();
        }

        return string.Empty;
    }

    private static (JobOnRevision Revision, IReadOnlyList<JobOnComponent> Components) CopyRevisionForDuplication(
        Guid newJobOnId, JobOnRevision source, DateTime now, string actorId)
    {
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

            components.Add(sourceComponent with
            {
                JobOnComponentId = componentId,
                Fields = fields,
                Rows = rows,
                Verifications = verifications
            });
        }

        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
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
            ImageAssetId = source.ImageAssetId,
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
