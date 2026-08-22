using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Pegamentos application service — use cases for create/open/load/save/update/close.
/// Consumes IJobOnProductionContextLookup for historical revision resolution.
/// </summary>
public sealed class PegamentoService
{
    private readonly IPegamentoRepository _repository;
    private readonly IJobOnProductionContextLookup _contextLookup;
    private readonly PegamentoAuthorizationGate _gate;
    private readonly IClock _clock;
    private readonly IAppSettingsReader _settings;
    private readonly IJobOnProductionFolderResolver _productionFolderResolver;

    public PegamentoService(
        IPegamentoRepository repository,
        IJobOnProductionContextLookup contextLookup,
        PegamentoAuthorizationGate gate,
        IClock clock,
        IAppSettingsReader settings,
        IJobOnProductionFolderResolver productionFolderResolver)
    {
        _repository = repository;
        _contextLookup = contextLookup;
        _gate = gate;
        _clock = clock;
        _settings = settings;
        _productionFolderResolver = productionFolderResolver;
    }

    // ---- Create -----------------------------------------------------------

    public async Task<Result<Guid, DomainError>> CreateControlAsync(CreatePegamentoRequest request, CancellationToken ct = default)
    {
        var actorId = _gate.ResolveActorId();
        if (actorId is null)
            return Result<Guid, DomainError>.Failure(DomainError.Forbidden(
                "PEGAMENTO_UNAUTHORIZED", "Acesso não autorizado ao módulo Pegamentos."));

        var context = await _contextLookup.ResolveAsync(request.JobOnRevisionId, ct);
        if (context is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_REVISION_NOT_FOUND",
                "O contexto de produção do Job On não foi encontrado ou está incompleto."));

        // Validate incomplete context (DS-05: block with actionable message)
        if (context.CmSnapshot is null || context.BqSnapshot is null || context.MfSnapshot is null)
            return Result<Guid, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_INCOMPLETE_CONTEXT",
                "Corrigir ferramentas no Job On"));

        var createResult = PegamentoControlo.Create(
            context,
            request.Tolerance,
            request.Notes,
            _clock.UtcNow,
            actorId);

        if (createResult.IsFailure)
            return Result<Guid, DomainError>.Failure(createResult.Error);

        var controloId = await _repository.CreateAsync(createResult.Value, ct);
        return Result<Guid, DomainError>.Success(controloId);
    }

    // ---- Load / Get -------------------------------------------------------

    public async Task<Result<PegamentoControlDetail, DomainError>> GetControlDetailAsync(Guid controloId, CancellationToken ct = default)
    {
        var control = await _repository.GetByIdAsync(controloId, ct);
        if (control is null)
            return Result<PegamentoControlDetail, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        // Use the hydrated aggregate's measurements — they carry reconstructed
        // Ovalizacao/Media/ToleranceStatus from historical persisted inputs.
        // Do NOT reload raw measurements here.
        return Result<PegamentoControlDetail, DomainError>.Success(MapToDetail(control, control.Measurements));
    }

    public async Task<Result<IReadOnlyList<PegamentoControlItem>, DomainError>> ListByRevisionAsync(
        Guid jobOnRevisionId, CancellationToken ct = default)
    {
        var controls = await _repository.GetByRevisionAsync(jobOnRevisionId, ct);
        return Result<IReadOnlyList<PegamentoControlItem>, DomainError>.Success(
            controls.Select(MapToItem).ToList().AsReadOnly());
    }

    public async Task<Result<IReadOnlyList<PegamentoControlItem>, DomainError>> ListByJobOnAsync(
        Guid jobOnId, CancellationToken ct = default)
    {
        var controls = await _repository.GetByJobOnAsync(jobOnId, ct);
        return Result<IReadOnlyList<PegamentoControlItem>, DomainError>.Success(
            controls.Select(MapToItem).ToList().AsReadOnly());
    }

    /// <summary>
    /// Read-only resolution of the exact historical production context for a
    /// pinned revision (production/revision → Pegamentos list-of-records view).
    /// Returns Failure when the revision is missing or lacks required components.
    /// </summary>
    public async Task<Result<PegamentoProductionContext, DomainError>> ResolveProductionContextAsync(
        Guid jobOnRevisionId, CancellationToken ct = default)
    {
        var context = await _contextLookup.ResolveAsync(jobOnRevisionId, ct);
        if (context is null)
            return Result<PegamentoProductionContext, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_REVISION_NOT_FOUND",
                "O contexto de produção do Job On não foi encontrado ou está incompleto."));

        return Result<PegamentoProductionContext, DomainError>.Success(context);
    }

    /// <summary>
    /// Measurement history / audit trail for a control (consultation).
    /// Measurements carry reconstructed historical ovalização/média values.
    /// </summary>
    public async Task<Result<IReadOnlyList<PegamentoMeasurementDetail>, DomainError>> GetHistoryAsync(
        Guid controloId, CancellationToken ct = default)
    {
        var control = await _repository.GetByIdAsync(controloId, ct);
        if (control is null)
            return Result<IReadOnlyList<PegamentoMeasurementDetail>, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        var history = control.Measurements
            .Select(m => new PegamentoMeasurementDetail(
                MedicaoId: m.PegamentoMedicaoId,
                ComponentKey: m.ComponentKey.ToString(),
                ToolNumber: m.ToolNumber,
                Costura: m.Costura,
                ContraCostura: m.ContraCostura,
                Ovalizacao: m.Ovalizacao,
                Media: m.Media,
                ToleranceStatus: m.ToleranceStatus.ToString(),
                CreatedAtUtc: m.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<PegamentoMeasurementDetail>, DomainError>.Success(history);
    }

    public async Task<Result<IReadOnlyList<PegamentoControlItem>, DomainError>> SearchAsync(
        ControlFilterRequest filter, CancellationToken ct = default)
    {
        var controls = await _repository.SearchAsync(
            filter.Reference, filter.ProductionCode, filter.MachineCode,
            filter.From, filter.To, ct);
        return Result<IReadOnlyList<PegamentoControlItem>, DomainError>.Success(
            controls.Select(MapToItem).ToList().AsReadOnly());
    }

    // ---- Update -----------------------------------------------------------

    public async Task<Result<bool, DomainError>> UpdateControlAsync(UpdatePegamentoRequest request, CancellationToken ct = default)
    {
        var control = await _repository.GetByIdAsync(request.ControloId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        var updateResult = control.UpdateEditableFields(
            request.Tolerance, request.Notes, _clock.UtcNow);

        if (updateResult.IsFailure)
            return Result<bool, DomainError>.Failure(updateResult.Error);

        await _repository.UpdateAsync(control, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Measurements -----------------------------------------------------

    public async Task<Result<Guid, DomainError>> AddMeasurementAsync(AddMeasurementRequest request, CancellationToken ct = default)
    {
        var actorId = _gate.ResolveActorId();
        if (actorId is null)
            return Result<Guid, DomainError>.Failure(DomainError.Forbidden(
                "PEGAMENTO_UNAUTHORIZED", "Acesso não autorizado ao módulo Pegamentos."));

        var control = await _repository.GetByIdAsync(request.ControloId, ct);
        if (control is null)
            return Result<Guid, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        var addResult = control.AddMeasurement(
            request.Component,
            request.ToolNumber,
            request.Costura,
            request.ContraCostura,
            _clock.UtcNow);

        if (addResult.IsFailure)
            return Result<Guid, DomainError>.Failure(addResult.Error);

        var medicaoId = await _repository.AddMeasurementAsync(request.ControloId, addResult.Value, actorId, ct);
        return Result<Guid, DomainError>.Success(medicaoId);
    }

    // ---- Close ------------------------------------------------------------

    public async Task<Result<bool, DomainError>> CloseControlAsync(CloseControlRequest request, CancellationToken ct = default)
    {
        var control = await _repository.GetByIdAsync(request.ControloId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        var closeResult = control.Close(_clock.UtcNow);

        if (closeResult.IsFailure)
            return Result<bool, DomainError>.Failure(closeResult.Error);

        await _repository.UpdateAsync(control, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Document confirmation --------------------------------------------

    public async Task<Result<bool, DomainError>> ConfirmDocumentSavedAsync(
        Guid controloId, CancellationToken ct = default)
    {
        var actorId = _gate.ResolveActorId();
        if (actorId is null)
            return Result<bool, DomainError>.Failure(DomainError.Forbidden(
                "PEGAMENTO_UNAUTHORIZED", "Acesso não autorizado ao módulo Pegamentos."));

        var control = await _repository.GetByIdAsync(controloId, ct);
        if (control is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "PEGAMENTO_CONTROL_NOT_FOUND", "Controlo de pegamentos não encontrado."));

        // Server derives ALL metadata from authoritative state
        var filename = PegamentoPdfFilename.Compute(control);

        // Resolve global output root from shared settings
        var outputRoot = await _settings.GetOutputRootAsync(ct);
        if (string.IsNullOrWhiteSpace(outputRoot))
            return Result<bool, DomainError>.Failure(DomainError.Unexpected(
                "PEGAMENTO_OUTPUT_ROOT_MISSING",
                "O diretório principal de documentos não está configurado."));

        // Resolve Job On production folder from the exact historical context
        var productionFolder = await _productionFolderResolver.ResolveAsync(control.JobOnId, ct);
        if (string.IsNullOrWhiteSpace(productionFolder))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "PEGAMENTO_PRODUCTION_FOLDER_MISSING",
                "A pasta de produção do Job On não está configurada."));

        // Enforce closed document freeze
        var existingDocument = await _repository.GetDocumentAsync(controloId, ct);

        if (control.Status == PegamentoControloStatus.Fechado &&
            existingDocument is not null)
        {
            return Result<bool, DomainError>.Failure(
                DomainError.DomainConflict(
                    "PEGAMENTO_FINAL_DOCUMENT_FROZEN",
                    "O documento final deste controlo está fechado e não pode ser substituído."));
        }

        var document = new PegamentoDocumento
        {
            PegamentoDocumentoId =
                existingDocument?.PegamentoDocumentoId ?? Guid.NewGuid(),
            PegamentoControloId = controloId,
            Filename = filename,
            OutputRootSnapshot = outputRoot.Trim(),
            ProductionFolderSnapshot = productionFolder.Trim(),
            GeneratedAtUtc = _clock.UtcNow,
            GeneratedBy = actorId
        };

        await _repository.UpsertDocumentAsync(document, ct);
        return Result<bool, DomainError>.Success(true);
    }

    // ---- Mapping helpers --------------------------------------------------

    private static PegamentoControlDetail MapToDetail(PegamentoControlo control, IReadOnlyList<PegamentoMedicao> measurements)
    {
        return new PegamentoControlDetail(
            ControloId: control.PegamentoControloId,
            JobOnId: control.JobOnId,
            JobOnRevisionId: control.JobOnRevisionId,
            ProductionCode: control.ProductionCode,
            MachineCode: control.MachineCode,
            Reference: control.ReferenceSnapshot,
            CmReference: control.CmSnapshot?.ReferenceSnapshot,
            CmLot: control.CmSnapshot?.LotSnapshot,
            CmNominal: control.CmNominal,
            BqReference: control.BqSnapshot?.ReferenceSnapshot,
            BqLot: control.BqSnapshot?.LotSnapshot,
            BqNominal: control.BqNominal,
            MfReference: control.MfSnapshot?.ReferenceSnapshot,
            MfLot: control.MfSnapshot?.LotSnapshot,
            MfNominal: control.MfNominal,
            Tolerance: control.Tolerance,
            Status: control.Status.ToString(),
            Notas: control.Notas,
            Measurements: measurements.Select(m => new PegamentoMeasurementDetail(
                MedicaoId: m.PegamentoMedicaoId,
                ComponentKey: m.ComponentKey.ToString(),
                ToolNumber: m.ToolNumber,
                Costura: m.Costura,
                ContraCostura: m.ContraCostura,
                Ovalizacao: m.Ovalizacao,
                Media: m.Media,
                ToleranceStatus: m.ToleranceStatus.ToString(),
                CreatedAtUtc: m.CreatedAtUtc)).ToList().AsReadOnly(),
            CreatedAtUtc: control.CreatedAtUtc,
            CreatedBy: control.CreatedBy);
    }

    private static PegamentoControlItem MapToItem(PegamentoControlo control)
    {
        return new PegamentoControlItem(
            ControloId: control.PegamentoControloId,
            JobOnId: control.JobOnId,
            JobOnRevisionId: control.JobOnRevisionId,
            ProductionCode: control.ProductionCode,
            MachineCode: control.MachineCode,
            Reference: control.ReferenceSnapshot,
            Status: control.Status.ToString(),
            CreatedAtUtc: control.CreatedAtUtc);
    }
}