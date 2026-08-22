using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Master identity of a tool reference (N04 <c>tool_references</c>; GLM-FERR-02/AB-01).
/// Holds NO processo — the processo belongs to the lote in the Peso flow (TD-17).
/// Business identity is UNIQUE(tool_type, ref_code).
/// </summary>
public sealed class ToolReference
{
    public Guid ToolReferenceId { get; set; } = Guid.NewGuid();

    public FerramentasToolType ToolType { get; set; }

    public string RefCode { get; set; } = string.Empty;

    public string? TechnicalName { get; set; }

    /// <summary>Owner plant. Defaults to "MG — Marinha Grande" (design brief, V1).</summary>
    public string? OwnerPlant { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Result<ToolReference, DomainError> Create(
        FerramentasToolType toolType,
        string refCode,
        string? technicalName,
        string? ownerPlant,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        var code = refCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            return Result<ToolReference, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_REFCODE_REQUIRED", "A referência da ferramenta é obrigatória."));

        var techName = technicalName?.Trim();
        var plant = string.IsNullOrWhiteSpace(ownerPlant)
            ? FerramentasModuleCatalog.DefaultOwnerPlant
            : ownerPlant.Trim();

        var reference = new ToolReference
        {
            ToolReferenceId = Guid.NewGuid(),
            ToolType = toolType,
            RefCode = code,
            TechnicalName = techName,
            OwnerPlant = plant,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc,
            UpdatedBy = createdBy
        };

        return Result<ToolReference, DomainError>.Success(reference);
    }

    public Result<ToolReference, DomainError> EditEditableFields(
        string? technicalName, string? ownerPlant, DateTimeOffset nowUtc, string? actorId)
    {
        var techName = technicalName?.Trim();
        var plant = string.IsNullOrWhiteSpace(ownerPlant)
            ? FerramentasModuleCatalog.DefaultOwnerPlant
            : ownerPlant.Trim();

        TechnicalName = techName;
        OwnerPlant = plant;
        UpdatedAtUtc = nowUtc;
        UpdatedBy = actorId;

        return Result<ToolReference, DomainError>.Success(this);
    }
}