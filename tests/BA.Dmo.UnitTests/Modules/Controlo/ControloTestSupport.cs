using System.Data;
using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Controlo;

/// <summary>Fixed UTC clock for deterministic Controlo service tests.</summary>
public sealed class ControloFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class ControloFakeAuthorship(string actorId = "controlo-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>In-memory fake of the shared unit-of-work factory (no DB).</summary>
public sealed class FakeControloUowFactory : IRepairUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakeControloUow());
}

/// <summary>No-op in-memory unit of work.</summary>
public sealed class FakeControloUow : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Current-user accessor for the Controlo sheet (Peso surface + controlo capabilities).</summary>
public sealed class ControloCurrentUser(string? actorId = "controlo-actor", string[]? capabilities = null)
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user = actorId is null ? null : new CurrentUser(
        Guid.NewGuid(), "Operador de Controlo",
        new[] { "peso" }, capabilities ?? new[] { ControloSheetModuleCatalog.ViewCapabilityId });

    public CurrentUser? Current => _user;

    public static ControloCurrentUser View() => new("controlo-actor", new[] { ControloSheetModuleCatalog.ViewCapabilityId });
    public static ControloCurrentUser Edit() => new("controlo-actor", new[] { ControloSheetModuleCatalog.ViewCapabilityId, ControloSheetModuleCatalog.EditCapabilityId, ControloSheetModuleCatalog.SubmitCapabilityId });
    public static ControloCurrentUser Review() => new("controlo-actor", new[] { ControloSheetModuleCatalog.ViewCapabilityId, ControloSheetModuleCatalog.ReviewCapabilityId });
    public static ControloCurrentUser WithoutSurface() => new(null);
}

/// <summary>In-memory fake of <see cref="IControloSheetRepository"/>.</summary>
public sealed class FakeControloSheetRepository : IControloSheetRepository
{
    public List<ControloFolha> Sheets { get; } = new();
    public List<ControloFolhaEvent> Events { get; } = new();
    public bool FailWrite { get; set; }

    public Task<Guid> InsertAsync(IDbUnitOfWork uow, ControloFolha sheet, CancellationToken ct = default)
    {
        if (FailWrite) throw new InvalidOperationException("simulated write failure");
        Sheets.Add(sheet);
        return Task.FromResult(sheet.ControloSheetId);
    }

    public Task<ControloFolha?> GetByIdAsync(Guid sheetId, CancellationToken ct = default)
        => Task.FromResult(Sheets.FirstOrDefault(s => s.ControloSheetId == sheetId));

    public Task<ControloFolha?> GetForProductionAsync(Guid jobOnId, Guid? jobOnRevisionId = null, CancellationToken ct = default)
        => Task.FromResult(Sheets
            .Where(s => s.JobOnId == jobOnId && (!jobOnRevisionId.HasValue || s.JobOnRevisionId == jobOnRevisionId.Value))
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefault());

    public Task<IReadOnlyList<ControloFolha>> ListByProductionAsync(Guid jobOnId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ControloFolha>>(Sheets.Where(s => s.JobOnId == jobOnId).ToList());

    public Task<IReadOnlyList<ControloFolha>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, string? machineCode, Guid? jobOnId, string? status, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ControloFolha>>(Sheets
            .Where(s => (from is null || s.CreatedAtUtc >= from) &&
                        (to is null || s.CreatedAtUtc <= to) &&
                        (machineCode is null || s.MachineCode == machineCode) &&
                        (jobOnId is null || s.JobOnId == jobOnId) &&
                        (status is null || ControloFolhaStateCodec.ToStorage(s.State) == status))
            .ToList());

    public Task UpdateAsync(IDbUnitOfWork uow, ControloFolha sheet, IReadOnlyList<ControloFolhaItem> currentItems, CancellationToken ct = default)
    {
        if (FailWrite) throw new InvalidOperationException("simulated write failure");
        sheet.SetItems(currentItems);
        return Task.CompletedTask;
    }

    public Task InsertEventAsync(IDbUnitOfWork uow, ControloFolhaEvent evt, CancellationToken ct = default)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory fake of <see cref="IControloProductionContextLookup"/>.</summary>
public sealed class FakeControloProductionContextLookup : IControloProductionContextLookup
{
    public Dictionary<Guid, ControloFolhaProductionContext> ByJobOn { get; } = new();

    public Task<Result<ControloFolhaProductionContext, DomainError>> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
        => Task.FromResult<Result<ControloFolhaProductionContext, DomainError>>(
            ByJobOn.TryGetValue(jobOnId, out var ctx)
                ? Result<ControloFolhaProductionContext, DomainError>.Success(ctx)
                : Result<ControloFolhaProductionContext, DomainError>.Failure(
                    DomainError.NotFound("CONTROLO_JOBON_NOT_FOUND", "produção não encontrada")));

    public Task<Result<ControloFolhaProductionContext, DomainError>> ResolveByProductionAsync(string productionCode, string? machineCode, CancellationToken ct = default)
    {
        var ctx = ByJobOn.Values.FirstOrDefault(c =>
            c.ProductionCode == productionCode && (string.IsNullOrWhiteSpace(machineCode) || c.MachineCode == machineCode));
        return Task.FromResult<Result<ControloFolhaProductionContext, DomainError>>(
            ctx is not null
                ? Result<ControloFolhaProductionContext, DomainError>.Success(ctx)
                : Result<ControloFolhaProductionContext, DomainError>.Failure(
                    DomainError.NotFound("CONTROLO_JOBON_NOT_FOUND", "produção não encontrada")));
    }

    public static ControloFolhaProductionContext Context(Guid jobOnId, params ControloFolhaComponent[] components) =>
        new(jobOnId, Guid.NewGuid(), "202601", "5447T173", "B1", components);
}

/// <summary>Builds <see cref="ControloSheetService"/> with in-memory fakes.</summary>
public static class ControloTestBuilder
{
    public static (ControloSheetService service, FakeControloSheetRepository repo,
        FakeControloProductionContextLookup ctx) Build(
        ControloCurrentUser? user = null, DateTimeOffset? now = null)
    {
        var repo = new FakeControloSheetRepository();
        var ctx = new FakeControloProductionContextLookup();
        var theUser = user ?? ControloCurrentUser.Edit();
        var service = new ControloSheetService(
            repo, ctx, new FakeControloUowFactory(),
            new ControloSheetAuthorizationGate(theUser, new ControloFakeAuthorship()),
            new ControloFixedClock(now ?? new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)));
        return (service, repo, ctx);
    }
}