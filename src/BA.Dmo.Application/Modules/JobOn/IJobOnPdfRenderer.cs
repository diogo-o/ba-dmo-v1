using BA.Dmo.Domain.Modules.JobOn;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// PDF renderer port for the Job On document set.
/// The backend generates a valid multi-page PDF from the current revision
/// snapshot; the concrete library is an implementation decision.
/// Application/domain code depends only on this interface.
/// </summary>
public interface IJobOnPdfRenderer
{
    /// <summary>
    /// Renders the full 4-page Job On document set as PDF bytes.
    /// Page 1 = Ficha de Artigo, Page 2 = Job-On Moldes,
    /// Page 3 = Trabalho de Equipa, Page 4 = Ficha de Artigo (duplicate).
    /// Deterministic output for deterministic input.
    /// </summary>
    byte[] RenderJobOnDocument(JobOnPdfData data);
}

/// <summary>
/// Structured data of the Job On document set derived from the current
/// revision snapshot. Never from mutable live values changed later.
/// </summary>
public sealed record JobOnPdfData
{
    // ---- Header context (shared across all pages) ----
    public string Reference { get; init; } = string.Empty;
    public string ProductionCode { get; init; } = string.Empty;
    public string MachineCode { get; init; } = string.Empty;
    public int Sections { get; init; }
    public decimal? DropCount { get; init; }
    public decimal? Weight { get; init; }
    public string? TypeSnapshot { get; init; }
    public string? ProcessSnapshot { get; init; }
    public DateTimeOffset? PlannedStartAt { get; init; }
    public DateTimeOffset? PlannedEndAt { get; init; }
    public string? GeneralNotes { get; init; }
    public int RevisionNumber { get; init; }

    // ---- Image (optional) ----
    public byte[]? ImageBytes { get; init; }
    public string? ImageMimeType { get; init; }

    // ---- Tool components (grouped by family) ----
    public JobOnPdfComponent? Cm { get; init; }     // MP_CM — Contra-Molde
    public JobOnPdfComponent? Mf { get; init; }     // MF — Molde Final
    public JobOnPdfComponent? Tp { get; init; }     // TP — Tampão
    public JobOnPdfComponent? Bq { get; init; }     // BQ — Boquilha
    public JobOnPdfComponent? An { get; init; }     // AN — Anilha/Anel
    public JobOnPdfComponent? Pu { get; init; }     // PU — Punção
    public JobOnPdfComponent? Arr { get; init; }    // ARR — Arrefecedor
    public JobOnPdfComponent? Pi { get; init; }     // PI — Pinça
    public JobOnPdfComponent? Cs { get; init; }     // CS — C. de Sopro
    public JobOnPdfComponent? Fo { get; init; }     // FO — Forro

    // ---- Calibres (CAL family rows) ----
    public IReadOnlyList<JobOnPdfCalibreRow> CalibreRows { get; init; } = Array.Empty<JobOnPdfCalibreRow>();

    // ---- Verifications ----
    public IReadOnlyList<JobOnPdfVerification> Verifications { get; init; } = Array.Empty<JobOnPdfVerification>();
}

/// <summary>One tool component with its typed fields and notes.</summary>
public sealed record JobOnPdfComponent
{
    public string Reference { get; init; } = string.Empty;
    public string? Lot { get; init; }
    public string? TechnicalName { get; init; }
    public decimal? Usage { get; init; }
    public string? Notes { get; init; }
    public int? Stock { get; init; }
    public int? MachineQuantity { get; init; }

    /// <summary>Typed field values keyed by field key.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
}

/// <summary>One calibre row from the CAL component.</summary>
public sealed record JobOnPdfCalibreRow(
    string Element,
    string? Value,
    decimal? Quantity);

/// <summary>One verification occurrence for display.</summary>
public sealed record JobOnPdfVerification(
    string RuleText,
    bool IsChecked,
    string StatusText);
