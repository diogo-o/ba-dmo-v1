using BA.Dmo.Application.Modules.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// In-memory fake of <see cref="IJobOnUserContextRepository"/> (R011). Records the
/// Job On context a user explicitly opened so use-case tests can assert the
/// per-user "current open Job On" persistence contract without a live DB.
/// </summary>
public sealed class FakeJobOnUserContextRepository : IJobOnUserContextRepository
{
    public string? LastActorId { get; private set; }
    public JobOnUserCurrent? Current { get; private set; }

    public Task SetCurrentAsync(
        string actorId,
        Guid jobOnId,
        string productionCode,
        string reference,
        string machineCode,
        CancellationToken cancellationToken = default)
    {
        LastActorId = actorId;
        Current = new JobOnUserCurrent(jobOnId, productionCode, reference, machineCode, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<JobOnUserCurrent?> GetCurrentAsync(
        string actorId, CancellationToken cancellationToken = default)
        => Task.FromResult(Current);
}