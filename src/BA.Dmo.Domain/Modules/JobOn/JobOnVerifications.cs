namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Verification occurrence per component (N05). Checked operator confirms from Ferramentas rules.
/// </summary>
public sealed record JobOnVerificationOccurrence
{
    /// <summary>Primary key.</summary>
    public Guid JobOnVerificationOccurrenceId { get; init; }

    /// <summary>Parent component ID.</summary>
    public Guid JobOnComponentId { get; init; }

    /// <summary>Optional rule from tool_check_rules.</summary>
    public Guid? SourceRuleId { get; init; }

    /// <summary>Rule text snapshot (from Ferramentas).</summary>
    public string? RuleTextSnapshot { get; init; }

    /// <summary>Status: pendente, confirmada, reposta, desativada.</summary>
    public string Status { get; init; } = "pendente"; // pending/confirmed/rejected/deactivated

    /// <summary>Completion source fixed to manual_job_on per N05 constraint.</summary>
    public string CompletionSource { get; init; } = "manual_job_on";

    /// <summary>User who confirmed/rejected.</summary>
    public string? CompletedBy { get; init; }

    /// <summary>Confirmation timestamp.</summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Update timestamp.</summary>
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Field options catalog for dropdowns (N05). Managed in Definições tab.
/// Deactivating preserves values in old revisions.
/// </summary>
public sealed record JobOnFieldOption
{
    /// <summary>Primary key.</summary>
    public Guid JobOnFieldOptionId { get; init; }

    /// <summary>Family (e.g., "PI", "MP").</summary>
    public string Family { get; init; } = null!;

    /// <summary>Field key (e.g., "clamp_material").</summary>
    public string FieldKey { get; init; } = null!;

    /// <summary>Option value.</summary>
    public string OptionValue { get; init; } = null!;

    /// <summary>Optional display label.</summary>
    public string? OptionLabel { get; init; }

    /// <summary>Display order.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>Active flag (deactivating preserves in history).</summary>
    public bool Active { get; init; } = true;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Update timestamp.</summary>
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
