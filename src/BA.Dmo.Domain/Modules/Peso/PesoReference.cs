namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// Peso master reference (N06 <c>peso_references</c>; GLM-PESO-04). Identity
/// UNIQUE(mold_number, neckring_number). The operational process is NOT stored
/// here — it lives in the Peso lot (TD-17). Editing an approved reference
/// withdraws approval, creates a new revision and logs the justification in the
/// change_log; the previous revision remains immutable.
/// </summary>
public sealed record PesoReference
{
    public Guid PesoReferenceId { get; set; } = Guid.NewGuid();

    public string MoldNumber { get; set; } = string.Empty;

    public string NeckringNumber { get; set; } = string.Empty;

    public string? CounterMold { get; set; }

    public decimal? Capacity { get; set; }

    public decimal? VolumeNeck { get; set; }

    public decimal? VolumePu { get; set; }

    public decimal? CaloteTp { get; set; }

    public string ChangeLogJson { get; set; } = "[]";
}

/// <summary>
/// Validates a Peso reference per GLM-PESO-04 / N06. Returns null on success or
/// a <see cref="PesoValidationError"/> describing the first violation.
/// </summary>
public sealed record PesoValidationError(string Code, string Message);

/// <summary>Domain validation rules for the Peso module (GLM-PESO-04/10).</summary>
public static class PesoValidator
{
    /// <summary>Validates a diagnostic key against Peso rules. Reserved.</summary>
    public static PesoValidationError? ValidateReference(
        string moldNumber,
        string neckringNumber) =>
        string.IsNullOrWhiteSpace(moldNumber)
            ? new PesoValidationError("PESO_REF_MOLD_REQUIRED", "O número do molde é obrigatório.")
            : string.IsNullOrWhiteSpace(neckringNumber)
                ? new PesoValidationError("PESO_REF_NECKRING_REQUIRED", "O número do neckring é obrigatório.")
                : null;

    /// <summary>
    /// Validates a Peso lot (GLM-PESO-04/06, TD-17, N06): processo mandatory,
    /// at least one allowed line limited to B1..C3, report_subfolder a relative
    /// name (never an absolute path), UNIQUE(reference, lote).
    /// </summary>
    public static PesoValidationError? ValidateLote(
        string lote,
        PesoProcesso processo,
        IReadOnlyList<string> allowedLines,
        string reportSubfolder)
    {
        if (string.IsNullOrWhiteSpace(lote))
            return new PesoValidationError("PESO_LOTE_REQUIRED", "O lote é obrigatório.");

        if (allowedLines is null || allowedLines.Count < PesoLoteRules.MinAllowedLines)
            return new PesoValidationError("PESO_LOTE_NO_ALLOWED_LINE",
                "Pelo menos uma máquina permitida é obrigatória.");

        var normalized = allowedLines.Select(l => l.Trim()).ToArray();
        if (normalized.Any(l => !PesoModuleCatalog.AllowedLines.Contains(l, StringComparer.Ordinal)))
            return new PesoValidationError("PESO_LOTE_INVALID_LINE",
                "As máquinas permitidas têm de pertencer a B1–C3.");

        if (allowedLines.Distinct(StringComparer.Ordinal).Count() != allowedLines.Count)
            return new PesoValidationError("PESO_LOTE_DUPLICATE_LINE",
                "As máquinas permitidas não podem repetir-se.");

        if (string.IsNullOrWhiteSpace(reportSubfolder))
            return new PesoValidationError("PESO_LOTE_SUBFOLDER_REQUIRED",
                "A subpasta dos relatórios é obrigatória.");

        if (ReportPathValidator.IsAbsoluteOrTraversal(reportSubfolder))
            return new PesoValidationError("PESO_LOTE_SUBFOLDER_ABSOLUTE",
                "A subpasta deve ser um nome relativo, não um caminho absoluto.");

        return null;
    }

    /// <summary>
    /// Validates a control/workflow edit per GLM-PESO-06.6/7/8 and the N40
    /// approved-readings rule: readings DML (and therefore draft editing) is
    /// confined to rascunho/nao_aprovado. An approved, pending (submitted) or
    /// otherwise non-draft sheet can never be edited in place — even with a
    /// change reason — because that would silently rewrite an approved
    /// baseline; the explicit audited reopen (revision+1, mandatory reason)
    /// is the only correction path (Manual 20:441-452, 20:477-485).
    /// </summary>
    public static PesoValidationError? ValidateControlEditable(string currentState, string? reason)
    {
        if (currentState is not ("rascunho" or "nao_aprovado"))
        {
            // A non-empty reason alone no longer unlocks an in-place edit: the
            // sheet must be explicitly reopened to rascunho first.
            _ = reason;
            return new PesoValidationError("PESO_CONTROL_REOPEN_REASON",
                "Editar um controlo submetido, aprovado ou não aprovado exige reabertura explícita para rascunho; reabra o controlo antes de editar.");
        }

        return null;
    }
}

/// <summary>
/// Ensures the report subfolder stays a relative name (GLM-PESO-09, DS-08):
/// no absolute path (drive/root/scheme), no traversal (".."), no leading slash
/// or backslash.
/// </summary>
public static class ReportPathValidator
{
    public static bool IsAbsoluteOrTraversal(string subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
            return true;
        var trimmed = subfolder.Trim();
        if (trimmed.StartsWith('/') || trimmed.StartsWith('\\') || trimmed.StartsWith("\\\\"))
            return true;
        if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
            return true;
        var segments = trimmed.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => s == "..") || trimmed.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the resolved report path: <see cref="mainOutputFolder"/> /
    /// <see cref="reportSubfolder"/> (e.g. <c>Capacidades / 5447T173</c>).
    /// </summary>
    public static string Resolve(string? mainOutputFolder, string reportSubfolder)
    {
        var root = string.IsNullOrWhiteSpace(mainOutputFolder) ? string.Empty : mainOutputFolder.Trim();
        var sub = reportSubfolder.Trim().TrimStart('/');
        return string.IsNullOrWhiteSpace(root)
            ? sub
            : $"{root} / {sub}";
    }
}