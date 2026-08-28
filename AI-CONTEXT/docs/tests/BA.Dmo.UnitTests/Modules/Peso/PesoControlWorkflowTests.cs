using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Peso;

/// <summary>
/// U-10 — Peso control + comparison workflow tests (GLM-PESO-06/11).
/// Covers rascunho→pendente→aprovado/nao_aprovado, revision increments,
/// reopen policy, delete eligibility, comparison per-CM decisions and the
/// immutable approved base (never altered by a comparison).
/// </summary>
public class PesoControlWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);

    private static PesoControl NewControl(PesoRecordType type = PesoRecordType.NovoControlo) => new()
    {
        PesoControloId = System.Guid.NewGuid(),
        PesoReferenceId = System.Guid.NewGuid(),
        PesoLoteId = System.Guid.NewGuid(),
        RecordType = type,
        MoldNumber = "5447",
        NeckringNumber = "T173",
        ProductionCode = "202601",
        Line = "B3",
        Lote = "4",
        ControlDate = new DateTime(2026, 8, 17),
        JobOnId = System.Guid.NewGuid(),
        JobOnRevisionId = System.Guid.NewGuid(),
        Status = PesoControlState.Rascunho,
        Revision = 1,
        TemperaturaC = 20m,
        Leituras = [new PesoLeitura { CmNumber = "12", PesoEmAgua = 152.43m }]
    };

    // ---- workflow transitions -------------------------------------------

    [Fact]
    public void Submit_WithNoReading_IsHardBlocked()
    {
        var control = NewControl();
        control.Leituras = [];

        var result = control.Submit();

        Assert.True(result.IsFailure);
        Assert.Equal("PESO_CONTROL_NO_READING", result.Error.Code);
        Assert.Equal(PesoControlState.Rascunho, control.Status);
    }

    [Fact]
    public void Submit_WithAtLeastOneReading_BecomesPendente()
    {
        var control = NewControl();
        var result = control.Submit();
        Assert.True(result.IsSuccess);
        Assert.Equal(PesoControlState.Pendente, control.Status);
    }

    [Fact]
    public void Reject_WithoutMandatoryNote_IsHardBlocked()
    {
        var control = NewControl();
        control.Submit();

        var result = control.Reject("   ");

        Assert.True(result.IsFailure);
        Assert.Equal("PESO_CONTROL_REJECT_NOTE_REQUIRED", result.Error.Code);
        Assert.Equal(PesoControlState.Pendente, control.Status);
    }

    [Fact]
    public void Reject_WithNote_BecomesNaoAprovado()
    {
        var control = NewControl();
        control.Submit();

        var result = control.Reject("Valores fora de tolerância");

        Assert.True(result.IsSuccess);
        Assert.Equal(PesoControlState.NaoAprovado, control.Status);
    }

    [Fact]
    public void Approve_FromPendente_RecordsApproverAndTime()
    {
        var control = NewControl();
        control.Submit();

        var result = control.Approve("user-9", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(PesoControlState.Aprovado, control.Status);
        Assert.Equal("user-9", control.ApprovedBy);
        Assert.Equal(Now, control.ApprovedAtUtc);
    }

    // ---- edit / reopen policy (GLM-PESO-06.6/8) -------------------------

    [Fact]
    public void ValidateEditable_ApprovedWithoutReason_IsBlocked()
    {
        var control = NewControl();
        control.Submit();
        control.Approve("user-9", Now);

        var error = PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(control.Status), reason: null);

        Assert.NotNull(error);
        Assert.Equal("PESO_CONTROL_REOPEN_REASON", error!.Code);
    }

    // ---- N40: in-place editing is confined to rascunho/nao_aprovado -----

    [Fact]
    public void ValidateEditable_ApprovedEvenWithReason_IsBlocked()
    {
        var control = NewControl();
        control.Submit();
        control.Approve("user-9", Now);

        // A non-empty change reason must NOT unlock an in-place edit of an
        // approved baseline — the explicit reopen is the only path (N40).
        var error = PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(control.Status), reason: "corrigir leitura");

        Assert.NotNull(error);
        Assert.Equal("PESO_CONTROL_REOPEN_REASON", error!.Code);
    }

    [Fact]
    public void ValidateEditable_PendenteEvenWithReason_IsBlocked()
    {
        var control = NewControl();
        control.Submit();

        var error = PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(control.Status), reason: "corrigir antes da decisão");

        Assert.NotNull(error);
        Assert.Equal("PESO_CONTROL_REOPEN_REASON", error!.Code);
    }

    [Fact]
    public void ValidateEditable_Rascunho_And_NaoAprovado_AreEditable()
    {
        var draft = NewControl();
        Assert.Null(PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(draft.Status), reason: null));

        var rejected = NewControl();
        rejected.Submit();
        rejected.Reject("nota");
        Assert.Null(PesoValidator.ValidateControlEditable(
            PesoControlStateCodec.ToStorage(rejected.Status), reason: null));
    }

    [Fact]
    public void Reopen_ApprovedControl_IncrementsRevisionAndBackToRascunho()
    {
        var control = NewControl();
        control.Submit();
        control.Approve("user-9", Now);

        var result = control.Reopen("Ajustar leituras", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(PesoControlState.Rascunho, control.Status);
        Assert.Equal(2, control.Revision);
        Assert.Null(control.ApprovedBy);
        Assert.Null(control.ApprovedAtUtc);
    }

    [Fact]
    public void Reopen_WithoutReason_IsBlocked()
    {
        var control = NewControl();
        control.Submit();
        control.Approve("user-9", Now);

        var result = control.Reopen("   ", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("PESO_CONTROL_REOPEN_REASON", result.Error.Code);
        Assert.Equal(PesoControlState.Aprovado, control.Status);
    }

    // ---- delete policy (GLM-PESO-06.7, 08_SUPABASE §9 CONFIRMED) --------

    [Fact]
    public void DeleteEligibility_OnlyRascunhoOrNaoAprovado()
    {
        var draft = NewControl();
        Assert.True(draft.IsDeletable);

        var rejected = NewControl();
        rejected.Submit();
        rejected.Reject("nota");
        Assert.True(rejected.IsDeletable);

        var pending = NewControl();
        pending.Submit();
        Assert.False(pending.IsDeletable);

        var approved = NewControl();
        approved.Submit();
        approved.Approve("user-9", Now);
        Assert.False(approved.IsDeletable);
    }

    // ---- comparison (GLM-PESO-06.4/5) -----------------------------------

    [Fact]
    public void Comparison_UsesApprovedBase_AndBaseStaysImmutable()
    {
        // The base (approved Novo controlo) is immutable; a comparison modifies
        // only its own CM decisions — never the approved base.
        var baseControl = NewControl(PesoRecordType.NovoControlo);
        baseControl.Submit();
        baseControl.Approve("user-9", Now);
        var baseDecisionSnapshot = baseControl.ComparisonDecisionsJson;

        var comparison = NewControl(PesoRecordType.Comparacao);
        comparison.ComparisonDecisionsJson = "[{\"cm\":\"34\",\"decision\":\"manter\"}]";
        comparison.Submit();

        Assert.Equal(PesoRecordType.Comparacao, comparison.RecordType);
        Assert.Equal(PesoControlState.Aprovado, baseControl.Status);
        Assert.Equal(baseDecisionSnapshot, baseControl.ComparisonDecisionsJson);
        Assert.Equal(PesoControlState.Pendente, comparison.Status);
    }
}