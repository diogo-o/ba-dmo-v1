using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// R008 — Canonical machines/lines a Tampões configuration may be associated with
/// (B1–C3). Server-side allowed set (mirrors the application's canonical line set).
/// A configuration is associated with zero/one/many machines via the normalized
/// <c>tampao_configuration_machines</c> — it is NEVER duplicated per machine.
/// </summary>
public static class TampaoMachine
{
    public const string B1 = "B1";
    public const string B2 = "B2";
    public const string B3 = "B3";
    public const string C1 = "C1";
    public const string C2 = "C2";
    public const string C3 = "C3";

    /// <summary>Canonical, ordered machine set {B1..C3}.</summary>
    public static readonly string[] All =
    {
        B1, B2, B3, C1, C2, C3
    };

    private static readonly System.Collections.Generic.HashSet<string> Allowed =
        new(All, System.StringComparer.Ordinal);

    public static bool IsValid(string? machine) =>
        machine is not null && Allowed.Contains(machine);

    /// <summary>
    /// Validates and canonicalizes a machine code; returns it trimmed+uppercased
    /// or a DomainError when not one of {B1..C3}.
    /// </summary>
    public static Result<string, DomainError> Validate(string? machine)
    {
        var code = machine?.Trim().ToUpperInvariant();
        if (code is null || !Allowed.Contains(code))
            return Result<string, DomainError>.Failure(DomainError.Validation(
                "TAMPAO_INVALID_MACHINE",
                $"Máquina inválida. Permitidas: {string.Join(", ", All)}."));
        return Result<string, DomainError>.Success(code);
    }
}

/// <summary>
/// R008 — An append-only comment/note attached to a Tampões configuration. The
/// latest comment is the current one; older comments are preserved (never silently
/// lost) with the actor and timestamp.
/// </summary>
public sealed class TampaoConfigurationNote
{
    public Guid TampaoConfigurationNoteId { get; set; } = Guid.NewGuid();

    public Guid TampaoConfigurationId { get; set; }

    public string Note { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>
/// R008 — An append-only audit fact of a machine association change
/// (added/removed) on a Tampões configuration, with actor + timestamp. Preserves
/// the history of machine assignments without silent loss.
/// </summary>
public sealed class TampaoMachineEvent
{
    public Guid TampaoConfigurationMachineEventId { get; set; } = Guid.NewGuid();

    public Guid TampaoConfigurationId { get; set; }

    public string Machine { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty; // "added" | "removed"

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}