using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>
/// R004 — Repairer capability (many-to-many). A repairer can repair CM, MF, BQ (any
/// valid combination); capability is distinct from the line-default convenience and a
/// repairer is NEVER duplicated to represent multiple types. Invalid types rejected;
/// deactivation preserves history.
/// </summary>
public class RepairerCapabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);

    private static (ReparacaoExternaService svc, FakeRepairRepository repo) Build()
    {
        var repo = new FakeRepairRepository();
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.Authorized(), new ReparacaoExternaFakeAuthorship());
        var svc = new ReparacaoExternaService(
            repo, new FakeToolPieceResolver(), new FakeArmazemRepairMovementPort(),
            new FakeRepairUnitOfWorkFactory(), gate, new ReparacaoExternaFixedClock(Now));
        return (svc, repo);
    }

    [Fact]
    public async Task CreateRepairer_WithMultipleTypes_SupportsAll()
    {
        var (svc, repo) = Build();

        var id = await svc.CreateRepairerAsync(new CreateRepairerRequest("Reparador CM+MF+BQ",
            new[] { "CM", "MF", "BQ" }));

        Assert.True(id.IsSuccess);
        var types = await repo.ListRepairerRepairTypesAsync(id.Value);
        Assert.Equal(3, types.Count);
        Assert.Contains("CM", types);
        Assert.Contains("MF", types);
        Assert.Contains("BQ", types);
    }

    [Fact]
    public async Task CreateRepairer_InvalidType_IsRejected()
    {
        var (svc, _) = Build();

        var result = await svc.CreateRepairerAsync(new CreateRepairerRequest("X", new[] { "XX" }));

        Assert.True(result.IsFailure);
        Assert.Equal("REPEXT_REPAIRER_TYPE_INVALID", result.Error.Code);
    }

    [Fact]
    public async Task UpdateRepairer_ChangesSupportedTypes()
    {
        var (svc, repo) = Build();
        var id = (await svc.CreateRepairerAsync(new CreateRepairerRequest("A", new[] { "CM" }))).Value;

        var result = await svc.UpdateRepairerAsync(new UpdateRepairerRequest(id, "A", new[] { "CM", "MF" }));

        Assert.True(result.IsSuccess);
        var types = await repo.ListRepairerRepairTypesAsync(id);
        Assert.Contains("MF", types);
        Assert.Contains("CM", types);
    }

    [Fact]
    public async Task ListRepairers_ReturnsSupportedTypes()
    {
        var (svc, _) = Build();
        await svc.CreateRepairerAsync(new CreateRepairerRequest("B", new[] { "MF", "BQ" }));

        var list = await svc.ListRepairersAsync();

        Assert.True(list.IsSuccess);
        var dto = list.Value.Single();
        Assert.Contains("MF", dto.SupportedTypes);
        Assert.Contains("BQ", dto.SupportedTypes);
    }

    [Fact]
    public async Task Capability_IsSeparate_FromLineDefault()
    {
        var (svc, repo) = Build();
        // Repairer capable of BQ only.
        var id = (await svc.CreateRepairerAsync(new CreateRepairerRequest("BQ-only", new[] { "BQ" }))).Value;

        // A line default may still assign this repairer for CM (convenience) — capability
        // is NOT defined by the default; the default does not grant CM capability.
        var lineDefault = await svc.UpsertLineDefaultAsync(new UpsertLineDefaultRequest("B1", "CM", id));

        Assert.True(lineDefault.IsSuccess);
        Assert.Single(repo.LineDefaults);
        // The repairer's capability remains { BQ }.
        var types = await repo.ListRepairerRepairTypesAsync(id);
        Assert.DoesNotContain("CM", types);
        Assert.Contains("BQ", types);
    }
}