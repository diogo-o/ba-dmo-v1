using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.UnitTests.Modules.JobOn;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.Peso;

/// <summary>
/// U-10 — Peso use-case tests (modules/03 §6/§8, GLM-PESO-02/04/06/15).
/// High-value coverage: capability gate on every operation, Job On context
/// inheritance (revision attribution), Novo controlo/Comparação creation,
/// approval workflow (approve/reject/reopen/delete policy), per-CM comparison
/// decisions, day approvals and settings. All collaborators are fakes.
/// </summary>
public class PesoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakePesoRepository _repository = new();
    private readonly FakeJobOnRepository _jobOns = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly PesoService _service;

    public PesoServiceTests()
    {
        var gate = new PesoAuthorizationGate(_identity);
        _service = new PesoService(gate, _repository, _jobOns, new FixedClock(Now));
        _identity.GrantOperador();
    }

    // ---- Job On seed with a revision + MP_CM component -------------------

    private Guid SeedJobOn(string referenceText = "5447T173", string production = "202601", string machine = "B3")
    {
        var jobOnId = _jobOns.CreateAsync(new JobOnEntity(production, machine, Now, Now.AddHours(8), [])).Result;
        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOnId,
            RevisionNumber = 1,
            ReferenceSnapshot = referenceText,
            ProcessSnapshot = "NNPB",
            Components = [new JobOnComponent { Family = ComponentFamily.MP_CM, ReferenceSnapshot = referenceText, LotSnapshot = "4" }]
        };
        _jobOns.Revisions.Add(revision);
        _jobOns.Components.AddRange(revision.Components.Select(component =>
            component with { JobOnRevisionId = revision.JobOnRevisionId }));
        _jobOns.JobOns[jobOnId].SaveRevision(revision);
        return jobOnId;
    }

    private Guid SeedReference()
    {
        var id = Guid.NewGuid();
        _repository.References[id] = new PesoReference { PesoReferenceId = id, MoldNumber = "5447", NeckringNumber = "T173" };
        _repository.Lotes[id] = new PesoLote
        {
            PesoLoteId = Guid.NewGuid(),
            PesoReferenceId = id,
            Lote = "4",
            Processo = PesoProcesso.Nnpb,
            AllowedLines = ["B3"],
            ReportSubfolder = "5447T173",
            NominalWeight = 200m
        };
        return id;
    }

    // ---- authorization gate -----------------------------------------------

    [Fact]
    public async Task Approve_WithoutAprovarCapability_IsDenied()
    {
        _identity.GrantNone();
        var result = await _service.ApproveControlAsync(new ApproveControlRequest(Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public async Task Operador_CanManageNonApprovalOps_ButNotApprove()
    {
        // Operador has module == peso but not peso.aprovar.
        _identity.GrantOperador();
        var refId = SeedReference();
        var lote = await _service.CreateLoteAsync(new CreateLoteRequest(
            refId, "7", PesoProcesso.Ps, ["C1"], "5447T173", 200m));
        Assert.True(lote.IsSuccess);

        var approve = await _service.ApproveControlAsync(new ApproveControlRequest(Guid.NewGuid()));
        Assert.True(approve.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, approve.Error.Category);
    }

    // ---- references -------------------------------------------------------

    [Fact]
    public async Task SaveReference_CreatesNewReference_AndAudits()
    {
        var result = await _service.SaveReferenceAsync(new SaveReferenceRequest(
            "5447", "T173", null, 150m, 5m, 2m, null, null));

        Assert.True(result.IsSuccess);
        Assert.Single(_repository.References);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "peso.referencia.criar");
    }

    [Fact]
    public async Task SaveReference_EditExistingWithoutReason_IsBlocked()
    {
        await _service.SaveReferenceAsync(new SaveReferenceRequest("5447", "T173", null, 150m, 5m, 2m, null, null));

        var edit = await _service.SaveReferenceAsync(new SaveReferenceRequest(
            "5447", "T173", null, 160m, 5m, 2m, null, ChangeReason: null));

        Assert.True(edit.IsFailure);
        Assert.Equal("PESO_REF_CHANGE_REASON_REQUIRED", edit.Error.Code);
    }

    // ---- lot ---------------------------------------------------------------

    [Fact]
    public async Task CreateLote_ValidatesProcessLinesAndSubfolder()
    {
        var refId = SeedReference();

        var bad = await _service.CreateLoteAsync(new CreateLoteRequest(refId, "5", PesoProcesso.Nnpb, [], "5447T173", 200m));
        Assert.True(bad.IsFailure);
        Assert.Equal("PESO_LOTE_NO_ALLOWED_LINE", bad.Error.Code);

        var abs = await _service.CreateLoteAsync(new CreateLoteRequest(refId, "5", PesoProcesso.Nnpb, ["C3"], "C:\\Capacidades", 200m));
        Assert.True(abs.IsFailure);
        Assert.Equal("PESO_LOTE_SUBFOLDER_ABSOLUTE", abs.Error.Code);

        var ok = await _service.CreateLoteAsync(new CreateLoteRequest(refId, "5", PesoProcesso.Nnpb, ["C3"], "5447T173", 200m));
        Assert.True(ok.IsSuccess);
    }

    // ---- Novo controlo + Job On context (TD-18/GLM-PESO-06.3/15) -------------

    [Fact]
    public async Task CreateControl_InheritsJobOnContext_AndPinsRevision()
    {
        var jobOnId = SeedJobOn("5447T173", "202601", "B3");
        var refId = SeedReference();
        // Align the reference text lookup
        _repository.References[refId] = _repository.References[refId] with { };

        var result = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", "obs", [new PesoLeituraInput("12", 152.43m)]));

        Assert.True(result.IsSuccess);
        var control = _repository.Controls[result.Value];
        Assert.Equal(jobOnId, control.JobOnId);
        Assert.True(control.JobOnRevisionId != Guid.Empty);
        Assert.Equal("202601", control.ProductionCode);
        Assert.Equal("B3", control.Line);
        Assert.Equal(PesoControlState.Rascunho, control.Status);
        Assert.Single(control.Leituras);
    }

    // ---- approval workflow ---------------------------------------------------

    [Fact]
    public async Task SubmitThenApprove_RegistersDayApproval()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));

        var submit = await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        Assert.True(submit.IsSuccess);

        _identity.GrantResponsavel();
        var approve = await _service.ApproveControlAsync(new ApproveControlRequest(create.Value));
        Assert.True(approve.IsSuccess);
        Assert.Equal(PesoControlState.Aprovado, _repository.Controls[create.Value].Status);
        Assert.Single(_repository.DayApprovals);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "peso.controlo.aprovar");
    }

    [Fact]
    public async Task Submit_WithoutReading_IsHardBlocked()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, Array.Empty<PesoLeituraInput>()));

        var submit = await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        Assert.True(submit.IsFailure);
        Assert.Equal("PESO_CONTROL_NO_READING", submit.Error.Code);
    }

    [Fact]
    public async Task Reject_WithoutNote_IsHardBlocked()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        _identity.GrantResponsavel();

        var reject = await _service.RejectControlAsync(new RejectControlRequest(create.Value, "   "));
        Assert.True(reject.IsFailure);
        Assert.Equal("PESO_CONTROL_REJECT_NOTE_REQUIRED", reject.Error.Code);
    }

    [Fact]
    public async Task Reopen_Approved_IncrementsRevision()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(create.Value));

        var reopen = await _service.ReopenControlAsync(new ReopenControlRequest(create.Value, "Ajustar leitura"));
        Assert.True(reopen.IsSuccess);
        Assert.Equal(PesoControlState.Rascunho, _repository.Controls[create.Value].Status);
        Assert.Equal(2, _repository.Controls[create.Value].Revision);
    }

    [Fact]
    public async Task Delete_ApprovedControl_IsDenied()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(create.Value));

        var del = await _service.DeleteControlAsync(new DeleteControlRequest(create.Value));
        Assert.True(del.IsFailure);
        Assert.Equal("PESO_CONTROL_DELETE_STATE", del.Error.Code);
        Assert.True(_repository.Controls.ContainsKey(create.Value));
    }

    [Fact]
    public async Task Delete_RascunhoByNonAuthor_IsDenied()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));

        // A different Operador (no peso.aprovar) who is not the author.
        _identity.User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
            "Outro Operador", ["peso"], []);

        var del = await _service.DeleteControlAsync(new DeleteControlRequest(create.Value));
        Assert.True(del.IsFailure);
        Assert.Equal("PESO_CONTROL_DELETE_UNAUTHORIZED", del.Error.Code);
    }

    // ---- comparison (GLM-PESO-06.4/5) ------------------------------------------

    [Fact]
    public async Task CreateComparison_RequiresExplicitApprovedPreviousControl()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var current = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null,
            [new PesoLeituraInput("34", 142m)]));

        var result = await _service.CreateComparisonAsync(new CreateComparisonRequest(
            current.Value, Guid.NewGuid(), null, [new PesoComparisonPairRequest("34", "12")]));

        Assert.True(result.IsFailure);
        Assert.Equal("PESO_COMPARISON_NO_APPROVED_BASE", result.Error.Code);
    }

    [Fact]
    public async Task CreateComparison_PinsBothJobOnIdentities_AndExplicitCmSnapshot()
    {
        SeedReference();
        var previousJobOnId = SeedJobOn(production: "202512");
        var previous = await _service.CreateControlAsync(new CreateControlRequest(
            previousJobOnId, new DateTime(2025, 12, 10), 20m, "Novo", null,
            [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(previous.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(previous.Value));

        _identity.GrantOperador();
        var currentJobOnId = SeedJobOn(production: "202601");
        var current = await _service.CreateControlAsync(new CreateControlRequest(
            currentJobOnId, new DateTime(2026, 1, 10), 20m, "Novo", null,
            [new PesoLeituraInput("34", 142m)]));
        var comp = await _service.CreateComparisonAsync(new CreateComparisonRequest(
            current.Value, previous.Value, "comparar", [new PesoComparisonPairRequest("34", "12")]));

        Assert.True(comp.IsSuccess);
        var comparison = _repository.Controls[comp.Value];
        Assert.Equal(PesoRecordType.Comparacao, comparison.RecordType);
        Assert.Equal(currentJobOnId, comparison.JobOnId);
        Assert.Equal(_repository.Controls[current.Value].JobOnRevisionId, comparison.JobOnRevisionId);

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<PesoComparisonSnapshot>(
            comparison.PreviousControlJson!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(snapshot);
        Assert.Equal(current.Value, snapshot.CurrentControlId);
        Assert.Equal(previous.Value, snapshot.PreviousControlId);
        var row = Assert.Single(snapshot.Rows);
        Assert.Equal("34", row.CurrentCmNumber);
        Assert.Equal("12", row.PreviousCmNumber);
        Assert.NotEqual(0m, row.CurrentGlassWeight);
        Assert.NotEqual(0m, row.PreviousGlassWeight);
        Assert.DoesNotContain("capacidade", comparison.PreviousControlJson!, StringComparison.OrdinalIgnoreCase);

        _identity.GrantResponsavel();
        var decided = await _service.ConfirmComparisonDecisionsAsync(new ConfirmComparisonDecisionsRequest(
            comp.Value, "aprovado", [new DecideComparisonCmRequest("34", PesoCmDecision.Manter)]));
        Assert.True(decided.IsSuccess);
        Assert.DoesNotContain("capacidade", comparison.ComparisonDecisionsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(row.CurrentGlassWeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
            comparison.ComparisonDecisionsJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateComparison_RequiresEveryCurrentCmAndUniquePreviousCm()
    {
        SeedReference();
        var previousJobOnId = SeedJobOn(production: "202512");
        var previous = await _service.CreateControlAsync(new CreateControlRequest(
            previousJobOnId, new DateTime(2025, 12, 10), 20m, "Novo", null,
            [new PesoLeituraInput("12", 152.43m), new PesoLeituraInput("13", 153m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(previous.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(previous.Value));

        _identity.GrantOperador();
        var currentJobOnId = SeedJobOn(production: "202601");
        var current = await _service.CreateControlAsync(new CreateControlRequest(
            currentJobOnId, new DateTime(2026, 1, 10), 20m, "Novo", null,
            [new PesoLeituraInput("34", 142m), new PesoLeituraInput("35", 143m)]));

        var result = await _service.CreateComparisonAsync(new CreateComparisonRequest(
            current.Value, previous.Value, null, [new PesoComparisonPairRequest("34", "12")]));
        Assert.True(result.IsFailure);
        Assert.Equal("PESO_COMPARISON_PAIRING_INVALID", result.Error.Code);
    }

    [Fact]
    public async Task ConfirmComparisonDecisions_UsesSnapshotRows_AndAsideNeedsJustification()
    {
        SeedReference();
        var previousJobOnId = SeedJobOn(production: "202512");
        var previous = await _service.CreateControlAsync(new CreateControlRequest(
            previousJobOnId, new DateTime(2025, 12, 10), 20m, "Novo", null,
            [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(previous.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(previous.Value));

        _identity.GrantOperador();
        var currentJobOnId = SeedJobOn(production: "202601");
        var current = await _service.CreateControlAsync(new CreateControlRequest(
            currentJobOnId, new DateTime(2026, 1, 10), 20m, "Novo", null,
            [new PesoLeituraInput("34", 142m)]));
        var comp = await _service.CreateComparisonAsync(new CreateComparisonRequest(
            current.Value, previous.Value, null, [new PesoComparisonPairRequest("34", "12")]));

        _identity.GrantResponsavel();
        var result = await _service.ConfirmComparisonDecisionsAsync(new ConfirmComparisonDecisionsRequest(
            comp.Value, null, [new DecideComparisonCmRequest("34", PesoCmDecision.ColocarDeParte)]));
        Assert.True(result.IsFailure);
        Assert.Equal("PESO_COMPARISON_JUSTIFICATION_REQUIRED", result.Error.Code);

        var mismatch = await _service.ConfirmComparisonDecisionsAsync(new ConfirmComparisonDecisionsRequest(
            comp.Value, "motivo", [new DecideComparisonCmRequest("999", PesoCmDecision.Manter)]));
        Assert.True(mismatch.IsFailure);
        Assert.Equal("PESO_COMPARISON_DECISIONS_MISMATCH", mismatch.Error.Code);
    }

    // ---- document/email (GLM-PESO-09) ---------------------------------------

    [Fact]
    public async Task SaveSettings_ChangesDensity_ForFutureOnly_NotHistorical()
    {
        // OC-6: changing the NNPB density affects only future calculations; the
        // historically used constant is preserved on each control.
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        Assert.True(create.IsSuccess);
        var savedConstant = _repository.Controls[create.Value].ConstanteGlassUsada;
        Assert.NotNull(savedConstant);

        // Responsável changes NNPB.
        _identity.GrantResponsavel();
        var set = await _service.SaveSettingsAsync(new SaveSettingsRequest("constant_nnpb", "2.5000"));
        Assert.True(set.IsSuccess);

        // Historical control keeps the previous constant (never rewritten).
        Assert.Equal(savedConstant, _repository.Controls[create.Value].ConstanteGlassUsada);

        // A new control picks the configured value.
        _identity.GrantOperador();
        var jobOn2 = SeedJobOn("5447T173", "202602", "C1");
        var create2 = await _service.CreateControlAsync(new CreateControlRequest(
            jobOn2, new DateTime(2026, 8, 18), 20m, "Novo", null, [new PesoLeituraInput("22", 142m)]));
        Assert.True(create2.IsSuccess);
        Assert.Equal(2.5000m, _repository.Controls[create2.Value].ConstanteGlassUsada);
    }

    [Fact]
    public void PdfFilenameConvention_MatchesConfirmedReference()
    {
        var control = new PesoControl
        {
            MoldNumber = "9262", NeckringNumber = "T288", ProductionCode = "202604",
            Line = "C3", Lote = "16", Revision = 1, Status = PesoControlState.Aprovado
        };
        Assert.Equal("9262T288__202604__C3__L16.pdf", PesoFileName.Builder(control, "Peso"));
    }

    [Fact]
    public async Task GenerateDocument_RequiresApprovedControl()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));

        var result = await _service.GenerateDocumentAsync(new NoopPdfRenderer(), new GenerateDocumentRequest(create.Value));
        Assert.True(result.IsFailure);
        Assert.Equal("PESO_DOC_NOT_APPROVED", result.Error.Code);
    }

    [Fact]
    public async Task PrepareEmail_RequiresRecipientsConfig()
    {
        var jobOnId = SeedJobOn();
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(create.Value));

        var email = await _service.PrepareEmailAsync(new PrepareEmailRequest(create.Value));
        Assert.True(email.IsFailure);
        Assert.Equal("PESO_EMAIL_NO_RECIPIENTS", email.Error.Code);
    }

    [Fact]
    public async Task PrepareEmail_ResolvesLineGroupAndAttachment()
    {
        var jobOnId = SeedJobOn(referenceText: "5447T173", production: "202601", machine: "B3");
        SeedReference();
        var create = await _service.CreateControlAsync(new CreateControlRequest(
            jobOnId, new DateTime(2026, 8, 17), 20m, "Novo", null, [new PesoLeituraInput("12", 152.43m)]));
        await _service.SubmitControlAsync(new SubmitControlRequest(create.Value));
        _identity.GrantResponsavel();
        await _service.ApproveControlAsync(new ApproveControlRequest(create.Value));
        _repository.Settings["email_recipients_linhab"] = "linha@baglass.com";

        var email = await _service.PrepareEmailAsync(new PrepareEmailRequest(create.Value));
        Assert.True(email.IsSuccess);
        Assert.Equal("Linha B", email.Value.LineGroup);
        Assert.Contains("5447T173", email.Value.Subject);
        Assert.Equal("5447T173__202601__B3__L4.pdf", email.Value.AttachmentFileName);
    }

    // ---- test doubles --------------------------------------------------------

    private sealed class NoopPdfRenderer : IPdfRenderer
    {
        public byte[] RenderPesoFolha(PesoFolhaPdf data) => [1, 2, 3];
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantOperador() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Operador",
            ["peso"],
            []);

        public void GrantResponsavel() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Responsável",
            ["peso"],
            ["peso.aprovar"]);

        public void GrantNone() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            "Sem Acesso",
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
