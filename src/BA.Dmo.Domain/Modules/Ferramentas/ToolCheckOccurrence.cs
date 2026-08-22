namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// A verification occurrence materialized in the Job On from a Ferramentas rule
/// (N04 <c>tool_check_occurrences</c>; modules/05 §7, GLM-JOB-07). Owned by the Job On
/// flow for its state transitions; Ferramentas exposes it for read/consultation on the
/// lot card and is the authoritative SOURCE of the rule configuration.
/// completion_source is fixed to "manual_job_on".
/// </summary>
public sealed class ToolCheckOccurrence
{
    public Guid ToolCheckOccurrenceId { get; set; }

    public Guid ToolCheckRuleId { get; set; }

    public Guid? JobOnId { get; set; }

    public Guid? JobOnComponentId { get; set; }

    public string Status { get; set; } = "pendente";

    public string CompletionSource { get; set; } = "manual_job_on";

    public string? CompletedBy { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}