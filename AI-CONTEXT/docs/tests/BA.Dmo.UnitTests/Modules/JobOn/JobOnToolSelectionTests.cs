using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// "Alterar CM/MF/BQ associado" — real tool selection tests (Manual 10 §4/§8,
/// TD-18). A tool selection is identified by the tuple (tipo, referência,
/// lote, máquina/linha): CM, MF and BQ are DISTINCT tools (same reference
/// code under another type/line = different tool, never merged), the options
/// come ONLY from the real registered tooling (N04 tool_references/tool_lotes),
/// the association persists through the existing "Guardar nova revisão" flow
/// (new immutable revision; previous revision untouched), invalid/nonexistent
/// combinations are rejected server-side, jobon.edit is required, and no
/// Ferramentas/Armazém record is ever created. All collaborators are fakes —
/// no live DB.
/// </summary>
public class JobOnToolSelectionTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    // Seeded N04 register (FakeFerramentasToolLookup) — real distinct tools:
    //   CM 5447 Lote 1 (B2)  ·  CM 5447 Lote 3 (C3)  — same ref, different lines
    //   MF 5447 Lote 2 (B2)  — SAME reference code, DIFFERENT tool (type MF)
    //   BQ 5447 Lote 9 (C3)  — SAME reference code, DIFFERENT tool (type BQ)
    //   BQ BQ-100 Lote 7 (B2, C3)
    private static readonly Guid Cm5447Ref = Guid.Parse("50000000-0000-4000-8000-000000000001");
    private static readonly Guid Cm5447Lote1 = Guid.Parse("50000000-0000-4000-8000-000000000011");
    private static readonly Guid Cm5447Lote3 = Guid.Parse("50000000-0000-4000-8000-000000000013");
    private static readonly Guid Mf5447Ref = Guid.Parse("50000000-0000-4000-8000-000000000002");
    private static readonly Guid Mf5447Lote2 = Guid.Parse("50000000-0000-4000-8000-000000000012");
    private static readonly Guid Bq5447Ref = Guid.Parse("50000000-0000-4000-8000-000000000003");
    private static readonly Guid Bq5447Lote9 = Guid.Parse("50000000-0000-4000-8000-000000000019");
    private static readonly Guid Bq100Ref = Guid.Parse("50000000-0000-4000-8000-000000000004");
    private static readonly Guid Bq100Lote7 = Guid.Parse("50000000-0000-4000-8000-000000000017");

    private readonly FakeJobOnRepository _repository = new();
    private readonly FakeFerramentasToolLookup _tools = new();
    private readonly SelectionTestIdentity _identity = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly JobOnService _service;

    public JobOnToolSelectionTests()
    {
        _tools.Register(Cm5447Ref, Cm5447Lote1, FerramentasToolType.CM, "5447", "1", "Contra molde 5447", "B2");
        _tools.Register(Cm5447Ref, Cm5447Lote3, FerramentasToolType.CM, "5447", "3", "Contra molde 5447", "C3");
        _tools.Register(Mf5447Ref, Mf5447Lote2, FerramentasToolType.MF, "5447", "2", "Molde final 5447", "B2");
        _tools.Register(Bq5447Ref, Bq5447Lote9, FerramentasToolType.BQ, "5447", "9", "Boquilha 5447", "C3");
        _tools.Register(Bq100Ref, Bq100Lote7, FerramentasToolType.BQ, "BQ-100", "7", "Boquilha BQ-100", "B2", "C3");

        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext,
            new SelectionTestClock(new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero)),
            _tools,
            articleImages: null);
        _identity.GrantResponsible();
    }

    // ---- options (read-only, real data only, line-filtered) ----------------

    [Fact]
    public async Task ToolOptions_Cm_OnC3_ReturnsOnlyRegisteredLotsForThatLine()
    {
        var jobOnId = await CreateJobOnAsync(machine: "C3");

        var result = await _service.GetToolSelectionOptionsAsync(jobOnId, "CM", null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("C3", result.Value.Machine);
        Assert.Equal("CM", result.Value.Family);
        // Only the CM 5447 lote registered for C3 is a valid combination.
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(Cm5447Lote3, item.LoteId);
        Assert.Equal(Cm5447Ref, item.ReferenceId);
        Assert.Equal("CM", item.Type);
        Assert.Equal("5447", item.Reference);
        Assert.Equal("3", item.Lot);
        Assert.Contains("C3", item.AllowedLines);
    }

    [Fact]
    public async Task ToolOptions_OnlyShowsLotsRegisteredForTheJobOnMachine()
    {
        // Job On on B2: CM options include Lote 1 (B2) and exclude Lote 3 (C3 only).
        var b2 = await CreateJobOnAsync(machine: "B2", production: "202609");

        var cm = await _service.GetToolSelectionOptionsAsync(b2, "CM", null, null);

        Assert.True(cm.IsSuccess);
        Assert.Contains(cm.Value.Items, i => i.LoteId == Cm5447Lote1);
        Assert.DoesNotContain(cm.Value.Items, i => i.LoteId == Cm5447Lote3);
    }

    [Fact]
    public async Task ToolOptions_ReferenceFragmentFilters_RealData()
    {
        var c3 = await CreateJobOnAsync(machine: "C3");

        var all = (await _service.GetToolSelectionOptionsAsync(c3, "BQ", null, null)).Value;
        var filtered = (await _service.GetToolSelectionOptionsAsync(c3, "BQ", "BQ-1", null)).Value;

        // BQ on C3: BQ 5447 Lote 9 + BQ-100 Lote 7; the fragment keeps only BQ-100.
        Assert.Equal(2, all.Items.Count);
        Assert.Single(filtered.Items, i => i.LoteId == Bq100Lote7);
    }

    [Fact]
    public async Task ToolOptions_SameReference_DistinctTypes_NeverMerged()
    {
        // "5447" is registered as CM, MF and BQ — three DIFFERENT tools.
        var b2 = await CreateJobOnAsync(machine: "B2", production: "202610");

        var cm = (await _service.GetToolSelectionOptionsAsync(b2, "CM", "5447", null)).Value;
        var mf = (await _service.GetToolSelectionOptionsAsync(b2, "MF", "5447", null)).Value;

        Assert.Single(cm.Items);
        Assert.Single(mf.Items);
        Assert.Equal("5447", cm.Items[0].Reference);
        Assert.Equal("5447", mf.Items[0].Reference);
        Assert.NotEqual(cm.Items[0].ReferenceId, mf.Items[0].ReferenceId);
        Assert.NotEqual(cm.Items[0].LoteId, mf.Items[0].LoteId);

        // BQ "5447" (Lote 9, C3 only) is NOT offered on B2 — the combination is
        // invalid for this machine/line even though the tool exists.
        var bq = (await _service.GetToolSelectionOptionsAsync(b2, "BQ", "5447", null)).Value;
        Assert.Empty(bq.Items);
    }

    [Theory]
    [InlineData("PU")]
    [InlineData("CS")]
    [InlineData("TP")]
    [InlineData("")]
    [InlineData("cm-x")]
    public async Task ToolOptions_InvalidFamily_Rejected(string family)
    {
        var jobOnId = await CreateJobOnAsync();

        var result = await _service.GetToolSelectionOptionsAsync(jobOnId, family, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_FAMILY_INVALID", result.Error.Code);
    }

    [Fact]
    public async Task ToolOptions_JobOnNotFound_ReturnsNotFound()
    {
        var result = await _service.GetToolSelectionOptionsAsync(Guid.NewGuid(), "CM", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
    }

    [Fact]
    public async Task ToolOptions_WithoutEditCapability_Denied()
    {
        var jobOnId = await CreateJobOnAsync();
        _identity.GrantViewOnly();

        var result = await _service.GetToolSelectionOptionsAsync(jobOnId, "CM", null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- persistence through "Guardar nova revisão" (identity tuple) -------

    [Fact]
    public async Task SaveRevision_ValidCmSelection_PersistsAssociation_PreviousRevisionUnchanged()
    {
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var previousRevision = _repository.Revisions.Single(r => r.JobOnId == jobOnId);
        Assert.Equal(1, previousRevision.RevisionNumber);

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        var newRevision = _repository.Revisions.Single(r => r.JobOnRevisionId == result.Value);
        Assert.Equal(2, newRevision.RevisionNumber);
        Assert.Equal(jobOnId, newRevision.JobOnId); // SAME Job On — never a new one

        // The NEW revision carries the selected association (stable ids + snapshots).
        var saved = Assert.Single(_repository.Components.Where(c => c.JobOnRevisionId == newRevision.JobOnRevisionId));
        Assert.Equal(ComponentFamily.MP_CM, saved.Family);
        Assert.Equal(Cm5447Ref, saved.SourceToolId);
        Assert.Equal(Cm5447Lote3, saved.SourceLotId);
        Assert.Equal("5447", saved.ReferenceSnapshot);
        Assert.Equal("3", saved.LotSnapshot);

        // The PREVIOUS revision stays immutable: no component, no source link.
        Assert.Empty(previousRevision.Components ?? Array.Empty<JobOnComponent>());

        // current_revision_id advanced + audit fact recorded.
        Assert.Contains(_repository.CurrentRevisionUpdates,
            u => u.JobOnId == jobOnId && u.RevisionId == newRevision.JobOnRevisionId);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
    }

    [Fact]
    public async Task SaveRevision_ValidMfSelection_PersistsAssociation()
    {
        var jobOnId = await CreateJobOnAsync(machine: "B2", production: "202609");

        var component = ToolComponent(ComponentFamily.MF, Mf5447Ref, Mf5447Lote2, "5447", "2");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_repository.Components
            .Where(c => c.JobOnRevisionId == result.Value));
        Assert.Equal(ComponentFamily.MF, saved.Family);
        Assert.Equal(Mf5447Ref, saved.SourceToolId);
        Assert.Equal(Mf5447Lote2, saved.SourceLotId);
        Assert.Equal("5447", saved.ReferenceSnapshot);
        Assert.Equal("2", saved.LotSnapshot);
    }

    [Fact]
    public async Task SaveRevision_BqRemainsDistinctFromCmAndMf()
    {
        // Same reference code "5447" registered as CM/MF/BQ: the BQ family must
        // resolve to the BQ tool record — never the CM or MF record.
        var jobOnId = await CreateJobOnAsync(machine: "C3");

        var component = ToolComponent(ComponentFamily.BQ, Bq5447Ref, Bq5447Lote9, "5447", "9");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_repository.Components
            .Where(c => c.JobOnRevisionId == result.Value));
        Assert.Equal(ComponentFamily.BQ, saved.Family);
        Assert.Equal(Bq5447Ref, saved.SourceToolId);
        Assert.Equal(Bq5447Lote9, saved.SourceLotId);
        Assert.NotEqual(Cm5447Ref, saved.SourceToolId);
        Assert.NotEqual(Mf5447Ref, saved.SourceToolId);
    }

    [Fact]
    public async Task SaveRevision_SameReferenceOnDifferentLines_ResolvesDifferentLots()
    {
        // CM 5447 exists on B2 (Lote 1) and C3 (Lote 3): two Job Ons on the two
        // lines each persist the DIFFERENT lot registered for their line.
        var b2 = await CreateJobOnAsync(machine: "B2", production: "202609");
        var c3 = await CreateJobOnAsync(machine: "C3", production: "202611");

        var b2Save = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            b2, null, null, null,
            new[] { ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote1, "5447", "1") }));
        var c3Save = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            c3, null, null, null,
            new[] { ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "5447", "3") }));

        Assert.True(b2Save.IsSuccess);
        Assert.True(c3Save.IsSuccess);

        var b2Component = Assert.Single(_repository.Components
            .Where(c => c.JobOnRevisionId == b2Save.Value));
        var c3Component = Assert.Single(_repository.Components
            .Where(c => c.JobOnRevisionId == c3Save.Value));
        Assert.Equal(Cm5447Lote1, b2Component.SourceLotId);
        Assert.Equal(Cm5447Lote3, c3Component.SourceLotId);
        Assert.NotEqual(b2Component.SourceLotId, c3Component.SourceLotId);
        Assert.Equal("1", b2Component.LotSnapshot);
        Assert.Equal("3", c3Component.LotSnapshot);
    }

    [Fact]
    public async Task SaveRevision_LineNotAllowed_Rejected()
    {
        // CM 5447 Lote 3 is registered for C3 only — a B2 Job On cannot use it.
        var b2 = await CreateJobOnAsync(machine: "B2", production: "202609");
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            b2, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_LINE_NOT_ALLOWED", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count); // nothing persisted
    }

    [Fact]
    public async Task SaveRevision_NonexistentLot_Rejected()
    {
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Guid.NewGuid(), "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_NOT_FOUND", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_MismatchedToolLotPair_Rejected()
    {
        // Real lot + real reference, but a PAIR that does not exist in the register.
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MP_CM, Bq100Ref, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_LINK_MISMATCH", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_TypeMismatch_Rejected()
    {
        // A BQ tool record cannot be associated through the MF family (distinct tools).
        var c3 = await CreateJobOnAsync(machine: "C3");
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MF, Bq5447Ref, Bq5447Lote9, "5447", "9");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            c3, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_TYPE_MISMATCH", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_SnapshotMismatch_Rejected()
    {
        // The snapshots must agree with the REAL registered identity — invented
        // values are not persisted.
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "9999", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_SNAPSHOT_MISMATCH", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_PartialLink_Rejected()
    {
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var revisionCountBefore = _repository.Revisions.Count;

        // Lot without its reference (incomplete physical link).
        var component = ToolComponent(ComponentFamily.MP_CM, null, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_TOOL_LINK_INCOMPLETE", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_WithoutEditCapability_Denied_AndWritesNothing()
    {
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        _identity.GrantViewOnly();
        var revisionCountBefore = _repository.Revisions.Count;

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveRevision_ValidSelection_DoesNotCreateFerramentasRecords()
    {
        // The association only READS the register: no tool reference/lot is
        // created or modified (the fake exposes no write API at all).
        var jobOnId = await CreateJobOnAsync(machine: "C3");
        var registerBefore = _tools.Lots
            .Select(l => (l.ToolReferenceId, l.ToolLoteId, l.Reference, l.Lot, l.AllowedLines))
            .OrderBy(l => l.ToolLoteId)
            .ToList();

        var component = ToolComponent(ComponentFamily.MP_CM, Cm5447Ref, Cm5447Lote3, "5447", "3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        Assert.True(_tools.ResolveCalls > 0, "the flow must READ the register");
        Assert.Equal(
            registerBefore,
            _tools.Lots
                .Select(l => (l.ToolReferenceId, l.ToolLoteId, l.Reference, l.Lot, l.AllowedLines))
                .OrderBy(l => l.ToolLoteId)
                .ToList());
    }

    [Fact]
    public async Task SaveRevision_SnapshotOnlyComponent_RemainsAllowed()
    {
        // Backward compatibility: a CM/MF/BQ component WITHOUT a physical link
        // (legacy manual snapshot values) is not register-backed and still saves.
        var jobOnId = await CreateJobOnAsync(machine: "C3");

        var component = ToolComponent(ComponentFamily.MP_CM, null, null, "CM 5447", "Lote 3");
        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_repository.Components
            .Where(c => c.JobOnRevisionId == result.Value));
        Assert.Null(saved.SourceToolId);
        Assert.Null(saved.SourceLotId);
    }

    // ---- helpers ------------------------------------------------------------

    private async Task<Guid> CreateJobOnAsync(
        string machine = "C3", string production = "202608", string reference = "7080C002")
    {
        var result = await _service.CreateAsync(
            new CreateJobOnRequest(production, machine, Start, null, reference));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static JobOnComponent ToolComponent(
        ComponentFamily family, Guid? toolId, Guid? lotId, string? reference, string? lot) =>
        new()
        {
            JobOnComponentId = Guid.NewGuid(),
            JobOnRevisionId = Guid.NewGuid(),
            Family = family,
            SourceToolId = toolId,
            SourceLotId = lotId,
            ReferenceSnapshot = reference,
            LotSnapshot = lot
        };

    private sealed class SelectionTestIdentity : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantResponsible() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Responsável Técnico",
            new[] { "jobon" },
            new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });

        public void GrantViewOnly() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Operador",
            new[] { "jobon" },
            new[] { "jobon.view" });
    }

    private sealed class SelectionTestClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
