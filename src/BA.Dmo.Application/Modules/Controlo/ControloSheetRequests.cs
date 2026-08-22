using BA.Dmo.Domain.Modules.Controlo;

namespace BA.Dmo.Application.Modules.Controlo;

// ---- Commands --------------------------------------------------------------

/// <summary>Creates (or loads the existing) Folha de Controlo for the selected production.</summary>
public sealed record CreateControloSheetRequest(Guid JobOnId);

/// <summary>Applies control assessments to the sheet items (OK/NOK + observation + MCaliper link).</summary>
public sealed record UpdateControloSheetItemsRequest(
    Guid SheetId,
    IReadOnlyList<ControloFolhaItemControlEdit> Edits);

/// <summary>Submits/delivers the sheet.</summary>
public sealed record SubmitControloSheetRequest(Guid SheetId, string? Note);

/// <summary>Reopens a submitted/decided sheet for editing (audit traced).</summary>
public sealed record ReopenControloSheetRequest(Guid SheetId);

/// <summary>Applies the responsible/chief review decision.</summary>
public sealed record DecideControloSheetRequest(Guid SheetId, ControloFolhaDecision Decision, string? Note);

// ---- DTOs ------------------------------------------------------------------

/// <summary>Summary DTO of a Folha de Controlo.</summary>
public sealed record ControloSheetDto(
    Guid SheetId,
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string Reference,
    string MachineCode,
    string DisplayId,
    string Status,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAtUtc,
    string? SubmittedNote,
    string? DecidedBy,
    DateTimeOffset? DecidedAtUtc,
    string? Decision,
    string? DecisionNote,
    IReadOnlyList<ControloSheetItemDto> Items,
    IReadOnlyList<ControloSheetEventDto> Events);

/// <summary>One component item of a Folha de Controlo.</summary>
public sealed record ControloSheetItemDto(
    Guid ItemId,
    string Family,
    Guid? SourceToolId,
    Guid? SourceLotId,
    string? ReferenceSnapshot,
    string? LotSnapshot,
    string? TechnicalNameSnapshot,
    string? Result,
    string? Observation,
    string? McaliperLink);

/// <summary>One append-only history event of a Folha de Controlo.</summary>
public sealed record ControloSheetEventDto(
    Guid EventId,
    string EventType,
    string? ActorId,
    DateTimeOffset OccurredAtUtc,
    string? Note);