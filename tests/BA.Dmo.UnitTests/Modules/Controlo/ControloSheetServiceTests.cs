using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Domain.Modules.Controlo;

namespace BA.Dmo.UnitTests.Modules.Controlo;

/// <summary>
/// R010 — ControloSheetService use cases: create/load for the selected production (using the
/// already-selected context, never re-searching), apply item controls, submit, reopen, review
/// decide — each gated by the controlo.* capability and persisted with an append-only event.
/// </summary>
public class ControloSheetServiceTests
{
    private static readonly Guid JobOnId = Guid.NewGuid();

    [Fact]
    public async Task GetForProduction_NoExistingSheet_CreatesOneFromProductionContext()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build();
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(
            JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"));
        ctx.ByJobOn[JobOnId] = ctx.ByJobOn[JobOnId] with { };

        var result = await service.GetForProductionAsync(JobOnId);

        Assert.True(result.IsSuccess);
        var sheet = result.Value;
        Assert.Equal("202601", sheet.ProductionCode);
        Assert.Equal("B1", sheet.MachineCode);
        Assert.Equal("Controlo_202601_5447T173_B1", sheet.DisplayId);
        Assert.Equal("rascunho", sheet.Status);
        var created = Assert.Single(repo.Sheets);
        Assert.Equal(JobOnId, created.JobOnId);
    }

    [Fact]
    public async Task UpdateItems_AppliesControlAndLeavesState()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build(ControloCurrentUser.Edit());
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(JobOnId, new ControloFolhaComponent("MF", null, null, "MF-9", "L2", "MF-9"));
        var sheetId = (await service.CreateAsync(new CreateControloSheetRequest(JobOnId))).Value;
        var itemId = repo.Sheets.Single().Items[0].ControloSheetItemId;

        var result = await service.UpdateItemsAsync(new UpdateControloSheetItemsRequest(sheetId, new[] { new ControloFolhaItemControlEdit(itemId, "NOK", "obs", "https://mc/1") }));

        Assert.True(result.IsSuccess);
        Assert.Equal("NOK", repo.Sheets.Single().Items[0].Result);
        Assert.Contains(repo.Events, e => e.EventType == "editar"); // 'editar' history event
    }

    [Fact]
    public async Task Submit_ThenReview_Flow()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build(ControloCurrentUser.Edit());
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"));
        var sheetId = (await service.CreateAsync(new CreateControloSheetRequest(JobOnId))).Value;

        Assert.True((await service.SubmitAsync(new SubmitControloSheetRequest(sheetId, "entrega"))).IsSuccess);
        Assert.Equal(ControloFolhaState.Submetido, repo.Sheets.Single().State);
        Assert.Contains(repo.Events, e => e.EventType == "submeter");

        // Review requires the controlo.review capability. Use the SAME repository (a fresh
        // build would lack the sheet); drive the decision through the same service which the
        // review-capable user satisfies via a service rebuilt over the shared repo.
        var reviewService = new ControloSheetService(
            repo, ctx, new FakeControloUowFactory(),
            new ControloSheetAuthorizationGate(ControloCurrentUser.Review(), new ControloFakeAuthorship()),
            new ControloFixedClock(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)));
        var decide = await reviewService.DecideAsync(new DecideControloSheetRequest(sheetId, ControloFolhaDecision.Aprovado, "ok"));
        Assert.True(decide.IsSuccess);
        Assert.Equal(ControloFolhaState.Aprovado, repo.Sheets.Single().State);
    }

    [Fact]
    public async Task Reopen_AfterSubmission_ReturnsToDraft()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build(ControloCurrentUser.Edit());
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"));
        var sheetId = (await service.CreateAsync(new CreateControloSheetRequest(JobOnId))).Value;
        await service.SubmitAsync(new SubmitControloSheetRequest(sheetId, null));

        var result = await service.ReopenAsync(new ReopenControloSheetRequest(sheetId));

        Assert.True(result.IsSuccess);
        Assert.Equal(ControloFolhaState.Rascunho, repo.Sheets.Single().State);
        Assert.Contains(repo.Events, e => e.EventType == "reeabrir");
    }

    [Fact]
    public async Task Create_WithoutEditCapability_Forbidden()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build(ControloCurrentUser.View());
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"));

        var result = await service.CreateAsync(new CreateControloSheetRequest(JobOnId));

        Assert.True(result.IsFailure);
        Assert.Equal(BA.Dmo.Domain.Shared.Kernel.ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(repo.Sheets);
    }

    [Fact]
    public async Task GetForProductionByContext_ResolvesAndCreatesWithoutReSelection()
    {
        var (service, repo, ctx) = ControloTestBuilder.Build();
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"));

        var result = await service.GetForProductionByContextAsync("202601", "B1");

        Assert.True(result.IsSuccess);
        Assert.Equal("202601", result.Value.ProductionCode);
        Assert.Single(repo.Sheets);
    }

    [Fact]
    public async Task ListSheets_WorksInFreeMode_NoCardRequired()
    {
        // R012 §22/§23: history/list is usable without an active production card (free mode).
        var (service, repo, ctx) = ControloTestBuilder.Build(ControloCurrentUser.Edit());
        ctx.ByJobOn[JobOnId] = FakeControloProductionContextLookup.Context(
            JobOnId, new ControloFolhaComponent("MP_CM", null, null, "5447", "L3", "CM"),
            new ControloFolhaComponent("MF", null, null, "MF-9", "L2", "MF-9"));
        Assert.True((await service.CreateAsync(new CreateControloSheetRequest(JobOnId))).IsSuccess);

        var result = await service.ListSheetsAsync();

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal("202601", row.ProductionCode);
        Assert.Equal("B1", row.MachineCode);
    }
}