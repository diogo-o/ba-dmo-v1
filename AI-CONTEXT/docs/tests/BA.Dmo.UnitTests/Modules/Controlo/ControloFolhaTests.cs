using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Controlo;

/// <summary>
/// R010 — ControloFolha domain invariants: creation requires a pinned production context
/// (job_on + exact revision), components are snapshotted from that revision, the workflow
/// draft → submitted → approved/rejected with reopen (not a permanent lock), and edits after
/// submission are allowed and traced.
/// </summary>
public class ControloFolhaTests
{
    private static readonly DateTimeOffset When = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static ControloFolhaProductionContext Ctx(params ControloFolhaComponent[] components) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "202601", "5447T173", "B1", components);

    private static ControloFolhaComponent Component(string family = "MP_CM", string? reference = "5447", string? lot = "3") =>
        new(family, Guid.NewGuid(), Guid.NewGuid(), reference, lot, $"{family} {reference}");

    [Fact]
    public void Create_SnapshotsComponentsAndPinsRevision()
    {
        var result = ControloFolha.Create(
            Ctx(Component("MP_CM"), Component("MF"), Component("BQ"), Component("PU"), Component("CS")),
            "actor", When);

        Assert.True(result.IsSuccess);
        var sheet = result.Value;
        Assert.Equal(5, sheet.Items.Count);
        Assert.Equal("MP_CM", sheet.Items[0].Family);
        Assert.Equal(new[] { "BQ", "CS", "MF", "MP_CM", "PU" },
            sheet.Items.Select(item => item.Family).OrderBy(family => family));
        Assert.All(sheet.Items, item => Assert.Equal("3", item.LotSnapshot));
        Assert.NotEqual(Guid.Empty, sheet.JobOnRevisionId);
        Assert.Equal(ControloFolhaState.Rascunho, sheet.State);
        Assert.Equal("Controlo_202601_5447T173_B1", sheet.DisplayId);
        Assert.Equal("actor", sheet.CreatedBy);
    }

    [Fact]
    public void Create_WithoutContext_Fails()
    {
        var result = ControloFolha.Create(null!, "actor", When);
        Assert.True(result.IsFailure);
        Assert.Equal("CONTROLO_CONTEXT_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void Submit_ThenDecide_Flow_Approved()
    {
        var sheet = ControloFolha.Create(Ctx(Component()), "actor", When).Value;

        Assert.True(sheet.Submit("actor", "entrega", When).IsSuccess);
        Assert.Equal(ControloFolhaState.Submetido, sheet.State);
        Assert.True(sheet.HasBeenSubmitted);

        Assert.True(sheet.Decide(ControloFolhaDecision.Aprovado, "chefe", "ok", When).IsSuccess);
        Assert.Equal(ControloFolhaState.Aprovado, sheet.State);
        Assert.True(sheet.Decision == ControloFolhaDecision.Aprovado);
        Assert.Equal("chefe", sheet.DecidedBy);
    }

    [Fact]
    public void Decide_WithoutSubmission_Fails()
    {
        var sheet = ControloFolha.Create(Ctx(Component()), "actor", When).Value;
        var result = sheet.Decide(ControloFolhaDecision.Aprovado, "chefe", null, When);
        Assert.True(result.IsFailure);
        Assert.Equal("CONTROLO_NOT_SUBMITTED", result.Error.Code);
    }

    [Fact]
    public void Submit_AfterDecision_IsRejected_ReopenAllowsResubmit()
    {
        var sheet = ControloFolha.Create(Ctx(Component()), "actor", When).Value;
        sheet.Submit("actor", null, When);
        sheet.Decide(ControloFolhaDecision.Rejeitado, "chefe", "corrigir", When);

        // Cannot submit a decided sheet directly.
        Assert.True(sheet.Submit("actor", null, When).IsFailure);

        // Reopen → draft → resubmit.
        Assert.True(sheet.Reopen("actor", When).IsSuccess);
        Assert.Equal(ControloFolhaState.Rascunho, sheet.State);
        Assert.True(sheet.Submit("actor", "nova entrega", When).IsSuccess);
        Assert.Equal(ControloFolhaState.Submetido, sheet.State);
    }

    [Fact]
    public void EditItemsAfterSubmission_IsAllowed_AndUpdatesResults()
    {
        var sheet = ControloFolha.Create(Ctx(Component("MF", "MF-9", "L2")), "actor", When).Value;
        var itemId = sheet.Items[0].ControloSheetItemId;
        sheet.Submit("actor", null, When);

        sheet.ApplyItemControls(new[] { new ControloFolhaItemControlEdit(itemId, "NOK", "algo errado", "https://mcaliper/1") }, When);

        Assert.Equal("NOK", sheet.Items[0].Result);
        Assert.Equal("algo errado", sheet.Items[0].Observation);
        Assert.Equal("https://mcaliper/1", sheet.Items[0].McaliperLink);
        // Submission is not a permanent lock — state may remain; the change is traced by the caller's event.
        Assert.Equal(ControloFolhaState.Submetido, sheet.State);
    }

    [Fact]
    public void RecordEvent_IsAppendOnly()
    {
        var sheet = ControloFolha.Create(Ctx(Component()), "actor", When).Value;
        sheet.RecordEvent(new ControloFolhaEvent(Guid.NewGuid(), sheet.ControloSheetId, "criar", "actor", When, null, null, null));
        sheet.RecordEvent(new ControloFolhaEvent(Guid.NewGuid(), sheet.ControloSheetId, "submeter", "actor", When, null, null, null));
        Assert.Equal(2, sheet.Events.Count);
    }
}
