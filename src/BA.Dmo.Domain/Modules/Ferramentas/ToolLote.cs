using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Physical/operational occurrence of a tool reference (N04 <c>tool_lotes</c>;
/// GLM-FERR-02, AB-02). UNIQUE(tool_reference_id, lote). Processo (NNPB/PS) belongs
/// to the lote in the Peso flow (TD-17), NOT the reference.
/// </summary>
public sealed class ToolLote
{
    public Guid ToolLoteId { get; set; } = Guid.NewGuid();

    public Guid ToolReferenceId { get; set; }

    public string Lote { get; set; } = string.Empty;

    public int? Qty { get; set; }

    public IReadOnlyList<string> AllowedLines { get; set; } = Array.Empty<string>();

    public string? DrawingCode { get; set; }

    public string? DrawingRevision { get; set; }

    /// <summary>Processo NNPB/PS when the lot belongs to the Peso flow; otherwise null.</summary>
    public string? Processo { get; set; }

    /// <summary>When the lot was created by duplicating another, points to the origin lot.</summary>
    public Guid? CopiedFromToolLoteId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Result<ToolLote, DomainError> CreateInitial(
        Guid toolReferenceId,
        string lote,
        int? qty,
        IReadOnlyList<string>? allowedLines,
        string? drawingCode,
        string? drawingRevision,
        string? processo,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        var lot = lote?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(lot))
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_LOTE_REQUIRED", "O número do lote é obrigatório."));

        var lines = allowedLines ?? Array.Empty<string>();
        if (lines.Count == 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_LINES_REQUIRED", "Selecione pelo menos uma máquina/linha permitida."));

        if (qty.HasValue && qty.Value < 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_QTY_INVALID", "A quantidade não pode ser negativa."));

        return Result<ToolLote, DomainError>.Success(new ToolLote
        {
            ToolLoteId = Guid.NewGuid(),
            ToolReferenceId = toolReferenceId,
            Lote = lot,
            Qty = qty,
            AllowedLines = lines.Distinct(StringComparer.Ordinal).ToList().AsReadOnly(),
            DrawingCode = drawingCode?.Trim(),
            DrawingRevision = drawingRevision?.Trim(),
            Processo = processo?.Trim(),
            CopiedFromToolLoteId = null,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc,
            UpdatedBy = createdBy
        });
    }

    /// <summary>
    /// Creates a NEW lot from a base, copying the lot-scoped (editable) data.
    /// The master identity (type, reference, technical name, owner plant, processo)
    /// is owned by the reference/duplication flow and is NOT set here.
    /// </summary>
    public static Result<ToolLote, DomainError> CreateFromBase(
        Guid toolReferenceId,
        Guid baseLoteId,
        string lote,
        int? qty,
        IReadOnlyList<string>? allowedLines,
        string? drawingCode,
        string? drawingRevision,
        string? processo,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        var lot = lote?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(lot))
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_LOTE_REQUIRED", "O novo número do lote é obrigatório."));

        var lines = allowedLines ?? Array.Empty<string>();
        if (lines.Count == 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_LINES_REQUIRED", "Selecione pelo menos uma máquina/linha permitida."));

        if (qty.HasValue && qty.Value < 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_QTY_INVALID", "A quantidade não pode ser negativa."));

        return Result<ToolLote, DomainError>.Success(new ToolLote
        {
            ToolLoteId = Guid.NewGuid(),
            ToolReferenceId = toolReferenceId,
            Lote = lot,
            Qty = qty,
            AllowedLines = lines.Distinct(StringComparer.Ordinal).ToList().AsReadOnly(),
            DrawingCode = drawingCode?.Trim(),
            DrawingRevision = drawingRevision?.Trim(),
            Processo = processo?.Trim(),
            CopiedFromToolLoteId = baseLoteId,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc,
            UpdatedBy = createdBy
        });
    }

    public Result<ToolLote, DomainError> EditEditableFields(
        int? qty,
        IReadOnlyList<string>? allowedLines,
        string? drawingCode,
        string? drawingRevision,
        DateTimeOffset nowUtc,
        string? actorId)
    {
        var lines = allowedLines ?? Array.Empty<string>();
        if (lines.Count == 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_LINES_REQUIRED", "Selecione pelo menos uma máquina/linha permitida."));

        if (qty.HasValue && qty.Value < 0)
            return Result<ToolLote, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_QTY_INVALID", "A quantidade não pode ser negativa."));

        Qty = qty;
        AllowedLines = lines.Distinct(StringComparer.Ordinal).ToList().AsReadOnly();
        DrawingCode = drawingCode?.Trim();
        DrawingRevision = drawingRevision?.Trim();
        UpdatedAtUtc = nowUtc;
        UpdatedBy = actorId;

        return Result<ToolLote, DomainError>.Success(this);
    }
}