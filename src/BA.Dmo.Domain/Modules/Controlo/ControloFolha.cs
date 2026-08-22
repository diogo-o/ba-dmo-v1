using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// R010 — Folha de Controlo aggregate root (N23 <c>controlo_sheets</c> + items +
/// append-only events). A production-level control summary sheet INSIDE the Controlo area.
///
/// Owns the workflow draft → submitted → approved/rejected, with the ability to REOPEN
/// and edit after submission (not a permanent lock); every change is traced in
/// <c>controlo_sheet_events</c> (append-only) so audit history is never silently rewritten.
/// Created from a pinned Job On revision and a snapshot of its components.
/// </summary>
public sealed class ControloFolha
{
    public Guid ControloSheetId { get; set; } = Guid.NewGuid();

    public Guid JobOnId { get; set; }
    public Guid JobOnRevisionId { get; set; }

    public string ProductionCode { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public string MachineCode { get; set; } = null!;

    /// <summary>Human-readable document id: Controlo_&lt;PROD&gt;_&lt;REF&gt;_&lt;MÁQUINA&gt;.</summary>
    public string DisplayId { get; set; } = null!;

    public ControloFolhaState State { get; set; } = ControloFolhaState.Rascunho;

    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? SubmittedNote { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public ControloFolhaDecision? Decision { get; set; }
    public string? DecisionNote { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Current version of the component items (editable).</summary>
    public IReadOnlyList<ControloFolhaItem> Items { get; private set; } = Array.Empty<ControloFolhaItem>();

    public IReadOnlyList<ControloFolhaEvent> Events { get; private set; } = Array.Empty<ControloFolhaEvent>();

    public bool HasBeenSubmitted => SubmittedAtUtc.HasValue && !string.IsNullOrWhiteSpace(SubmittedBy);
    public bool HasBeenDecided => DecidedAtUtc.HasValue && Decision.HasValue;

    /// <summary>
    /// Creates a NEW draft Folha de Controlo from a pinned production context and its
    /// component snapshot. The display id is generated (not the PK). The context is
    /// mandatory (a sheet is always associated to one production).
    /// </summary>
    public static Result<ControloFolha, DomainError> Create(
        ControloFolhaProductionContext context,
        string actorId,
        DateTimeOffset now)
    {
        if (context is null)
            return Result<ControloFolha, DomainError>.Failure(DomainError.Validation(
                "CONTROLO_CONTEXT_REQUIRED", "O contexto de produção é obrigatório para criar a folha de controlo."));
        if (context.JobOnId == Guid.Empty || context.JobOnRevisionId == Guid.Empty)
            return Result<ControloFolha, DomainError>.Failure(DomainError.Validation(
                "CONTROLO_CONTEXT_REQUIRED", "O contexto de produção/revisão é obrigatório para criar a folha de controlo."));
        if (string.IsNullOrWhiteSpace(context.ProductionCode) ||
            string.IsNullOrWhiteSpace(context.Reference) ||
            string.IsNullOrWhiteSpace(context.MachineCode))
            return Result<ControloFolha, DomainError>.Failure(DomainError.Validation(
                "CONTROLO_CONTEXT_REQUIRED", "Produção, referência e máquina são obrigatórios para criar a folha de controlo."));
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ControloFolha, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_ACTOR_REQUIRED", "Não foi possível resolver o utilizador autenticado."));

        var sheetId = Guid.NewGuid();
        var items = context.Components
            .Select(c => ControloFolhaItem.SnapshotFromComponent(
                sheetId, c.Family, c.SourceToolId, c.SourceLotId,
                c.ReferenceSnapshot, c.LotSnapshot, c.TechnicalNameSnapshot))
            .ToList();

        return Result<ControloFolha, DomainError>.Success(new ControloFolha
        {
            ControloSheetId = sheetId,
            JobOnId = context.JobOnId,
            JobOnRevisionId = context.JobOnRevisionId,
            ProductionCode = context.ProductionCode.Trim(),
            Reference = context.Reference.Trim(),
            MachineCode = context.MachineCode.Trim(),
            DisplayId = BuildDisplayId(context),
            State = ControloFolhaState.Rascunho,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items = items
        });
    }

    /// <summary>
    /// Applies a control assessment to the current component items. Editing after
    /// submission is allowed (not a permanent lock). Validation is minimal: unknown family
    /// or unknown item id is ignored; the applied values are persisted by the caller.
    /// </summary>
    public void ApplyItemControls(IEnumerable<ControloFolhaItemControlEdit> edits, DateTimeOffset now)
    {
        foreach (var edit in edits ?? Array.Empty<ControloFolhaItemControlEdit>())
        {
            var item = Items.FirstOrDefault(i => i.ControloSheetItemId == edit.ItemId);
            item?.ApplyControl(edit.Result, edit.Observation, edit.McaliperLink);
        }
        UpdatedAtUtc = now;
    }

    /// <summary>Submits/delivers the sheet (draft or reopened → submitted).</summary>
    public Result<ControloUnit, DomainError> Submit(string actorId, string? note, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ControloUnit, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_ACTOR_REQUIRED", "Não foi possível resolver o utilizador autenticado."));
        if (State == ControloFolhaState.Aprovado || State == ControloFolhaState.Rejeitado)
            return Result<ControloUnit, DomainError>.Failure(DomainError.DomainConflict(
                "CONTROLO_DECIDED", "Uma folha já decidida não pode ser submetida; reabra-a primeiro."));

        State = ControloFolhaState.Submetido;
        SubmittedBy = actorId;
        SubmittedAtUtc = now;
        SubmittedNote = note is null ? null : note.Trim();
        Decision = null;
        DecidedBy = null;
        DecidedAtUtc = null;
        DecisionNote = null;
        UpdatedAtUtc = now;
        return Result<ControloUnit, DomainError>.Success(new ControloUnit());
    }

    /// <summary>Reopens a submitted/sheet so it can be edited again (audit traced).</summary>
    public Result<ControloUnit, DomainError> Reopen(string actorId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ControloUnit, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_ACTOR_REQUIRED", "Não foi possível resolver o utilizador autenticado."));
        if (State == ControloFolhaState.Rascunho)
            return Result<ControloUnit, DomainError>.Failure(DomainError.DomainConflict(
                "CONTROLO_ALREADY_DRAFT", "A folha já está em rascunho."));

        State = ControloFolhaState.Rascunho;
        SubmittedBy = null;
        SubmittedAtUtc = null;
        SubmittedNote = null;
        Decision = null;
        DecidedBy = null;
        DecidedAtUtc = null;
        DecisionNote = null;
        UpdatedAtUtc = now;
        return Result<ControloUnit, DomainError>.Success(new ControloUnit());
    }

    /// <summary>
    /// Applies the responsible/chief review decision (aprovado/rejeitado). Requires the
    /// sheet to be submitted; a reopened draft must be submitted first.
    /// </summary>
    public Result<ControloUnit, DomainError> Decide(
        ControloFolhaDecision decision, string actorId, string? note, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ControloUnit, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_ACTOR_REQUIRED", "Não foi possível resolver o utilizador autenticado."));
        if (State != ControloFolhaState.Submetido)
            return Result<ControloUnit, DomainError>.Failure(DomainError.DomainConflict(
                "CONTROLO_NOT_SUBMITTED", "A folha tem de estar submetida para ser aprovada/rejeitada."));

        State = decision == ControloFolhaDecision.Aprovado ? ControloFolhaState.Aprovado : ControloFolhaState.Rejeitado;
        Decision = decision;
        DecidedBy = actorId;
        DecidedAtUtc = now;
        DecisionNote = note is null ? null : note.Trim();
        UpdatedAtUtc = now;
        return Result<ControloUnit, DomainError>.Success(new ControloUnit());
    }

    internal void AppendEvent(ControloFolhaEvent evt) =>
        Events = new List<ControloFolhaEvent>(Events) { evt };

    /// <summary>Appends an append-only history event (public for the service).</summary>
    public void RecordEvent(ControloFolhaEvent evt) => AppendEvent(evt);

    internal void SetEvents(IEnumerable<ControloFolhaEvent> events) =>
        Events = events.ToList().AsReadOnly();

    internal void SetItems(IEnumerable<ControloFolhaItem> items) =>
        Items = items.ToList().AsReadOnly();

    internal static string BuildDisplayId(ControloFolhaProductionContext context) =>
        $"Controlo_{context.ProductionCode.Trim()}_{context.Reference.Trim()}_{context.MachineCode.Trim()}";
}

/// <summary>A control edit for one item (result + observation + MCaliper link).</summary>
public sealed record ControloFolhaItemControlEdit(
    Guid ItemId,
    string? Result,
    string? Observation,
    string? McaliperLink);

/// <summary>A control edit for one item (result + observation + MCaliper link).</summary>
public sealed record ControloFolhaEvent(
    Guid ControloSheetEventId,
    Guid ControloSheetId,
    string EventType,
    string? ActorId,
    DateTimeOffset OccurredAtUtc,
    string? BeforeSummary,
    string? AfterSummary,
    string? Note);