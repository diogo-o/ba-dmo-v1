using System;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// R011 — The Job On THIS user explicitly opened/selected from the Universal Landing
/// (Owner §14/§15). NOT the globally-newest Job On, NOT the newest DB row, NOT a
/// clock/current-production derivation. It is user-scoped and only records an explicit
/// open. Read/write port backed by the additive <c>jobon_user_current</c> table (N24).
/// </summary>
public interface IJobOnUserContextRepository
{
    /// <summary>Stores (upserts) the Job On currently opened by <paramref name="actorId"/>.</summary>
    Task SetCurrentAsync(
        string actorId,
        Guid jobOnId,
        string productionCode,
        string reference,
        string machineCode,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the Job On context currently opened by <paramref name="actorId"/>, or null.</summary>
    Task<JobOnUserCurrent?> GetCurrentAsync(string actorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// R011 — A lightweight, user-scoped projection of the Job On this user explicitly opened.
/// Carries only the stable identity + readable context needed by consumers (e.g. a future
/// Controlo "Carregar Job On atual"), never a full Job On document.
/// </summary>
public sealed record JobOnUserCurrent(
    Guid JobOnId,
    string ProductionCode,
    string Reference,
    string MachineCode,
    DateTimeOffset OpenedAtUtc);