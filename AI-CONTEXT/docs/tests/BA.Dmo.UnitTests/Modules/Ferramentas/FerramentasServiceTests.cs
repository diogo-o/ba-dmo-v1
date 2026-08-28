using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Ferramentas;

/// <summary>
/// U-12 — Ferramentas use-case behavior (GLM-FERR-05/06/09/10/14, TD-17, TD-26):
/// atomic reference+lote creation, reference NOT carrying processo (lote only),
/// duplication copies configuration only with read-only master identity,
/// CM/MF kept distinct, per-lot verification-rule configuration gated by
/// <c>ferramentas.configure</c>, and authorization fail-closed.
/// </summary>
public class FerramentasServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeFerramentasRepository _repository = new();
    private readonly FakeRuleLookup _ruleLookup = new();
    private readonly FerramentasService _service;

    public FerramentasServiceTests()
    {
        var gate = new FerramentasAuthorizationGate(FakeCurrentUser.Authorized(), new FakeAuthorshipAccessor("ferr-actor"));
        _service = new FerramentasService(_repository, _ruleLookup, gate, new FixedClock(Now));
    }

    private CreateFerramentasRequest ValidCreate(string refCode = "CM-01", string lote = "4",
        FerramentasToolType type = FerramentasToolType.CM)
        => new(type, refCode, "Contra-molde de teste", "MG — Marinha Grande", lote, 12,
            new[] { "B1", "B3" }, "CR-01", "A", "NNPB");

    [Fact]
    public async Task CreateReferenceWithFirstLote_PersistsStableIdentityAtomically()
    {
        var result = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal("CM", detail.ToolType);
        Assert.Equal("CM-01", detail.RefCode);
        Assert.Single(detail.Lotes);
        Assert.Equal("4", detail.Lotes[0].Lote);
        Assert.Contains(detail.ReferenceId, _repository.References.Keys);
        Assert.Contains(_repository.Lotes.Values, l => l.ToolReferenceId == detail.ReferenceId);
    }

    [Fact]
    public async Task Reference_DoesNotCarryProcesso_ProcessoLivesOnLote()
    {
        var result = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());

        Assert.True(result.IsSuccess);
        var reference = _repository.References[result.Value.ReferenceId];
        // ToolReference has no Processo property (TD-17).
        // ToolReference has no Processo property (TD-17).
        Assert.DoesNotContain(reference.GetType().GetProperties().Select(p => p.Name), n => n == "Processo");
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == result.Value.ReferenceId);
        Assert.Equal("NNPB", lote.Processo);
    }

    [Fact]
    public async Task Create_DuplicateReference_IsHardBlocked()
    {
        var first = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate("CM-01"));
        Assert.True(first.IsSuccess);

        var second = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate("CM-01"));
        Assert.True(second.IsFailure);
        Assert.Equal(ErrorCategory.DomainConflict, second.Error.Category);
        Assert.Equal("FERRAMENTAS_DUPLICATE_REFERENCE", second.Error.Code);
    }

    [Fact]
    public async Task Create_NoLinesSelected_IsValidationError()
    {
        var request = ValidCreate() with { AllowedLines = Array.Empty<string>() };
        var result = await _service.CreateReferenceWithFirstLoteAsync(request);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
    }

    [Fact]
    public async Task Create_WithoutModule_IsForbidden()
    {
        var gate = new FerramentasAuthorizationGate(FakeCurrentUser.WithoutModule(), new FakeAuthorshipAccessor(null));
        var unauthorized = new FerramentasService(_repository, _ruleLookup, gate, new FixedClock(Now));

        var result = await unauthorized.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- Duplication: configuration only, master identity read-only -------

    [Fact]
    public async Task DuplicateLote_IsConfigurationOnly_MasterIdentityReadOnly()
    {
        // Create reference + first lot with a rule.
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var baseLote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        var addRuleGate = new FerramentasAuthorizationGate(FakeCurrentUser.Configurator(), new FakeAuthorshipAccessor("ferr-actor"));
        var configService = new FerramentasService(_repository, _ruleLookup, addRuleGate, new FixedClock(Now));
        var ruleAdded = await configService.AddCheckRuleAsync(new CheckRuleRequest(baseLote.ToolLoteId, "Verificar encaixe", FerramentasCheckFrequency.OncePerLot));
        Assert.True(ruleAdded.IsSuccess);

        // Duplicate the lot.
        var duplicate = await _service.CreateLoteFromBaseAsync(new CreateLoteFromBaseRequest(
            baseLote.ToolLoteId, "5", 6, new[] { "B2" }, "CR-01", "B"));
        Assert.True(duplicate.IsSuccess);

        var newLote = _repository.Lotes[duplicate.Value.LoteId];
        Assert.Equal("5", newLote.Lote);
        Assert.Equal(baseLote.ToolReferenceId, newLote.ToolReferenceId); // master identity unchanged
        Assert.Equal(baseLote.ToolLoteId, newLote.CopiedFromToolLoteId); // origin recorded

        // Rule configuration copied (with copied_from origin), NOT occurrences/history.
        var newRules = _repository.CheckRules[newLote.ToolLoteId];
        Assert.Single(newRules);
        Assert.Equal("Verificar encaixe", newRules[0].RuleText);
        Assert.Equal(ruleAdded.Value, newRules[0].CopiedFromRuleId);
        // No occurrences/pieces copied to the new lot (configuration-only duplication).
        Assert.DoesNotContain(newLote.ToolLoteId, _repository.Pieces.Keys);
    }

    [Fact]
    public async Task Duplicate_ExistingLoteInSameReference_IsHardBlocked()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var baseLote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        // Second lote "5" via duplicate.
        var d1 = await _service.CreateLoteFromBaseAsync(new CreateLoteFromBaseRequest(baseLote.ToolLoteId, "5", 1, new[] { "B1" }, null, null));
        Assert.True(d1.IsSuccess);

        var d2 = await _service.CreateLoteFromBaseAsync(new CreateLoteFromBaseRequest(baseLote.ToolLoteId, "5", 2, new[] { "B1" }, null, null));
        Assert.True(d2.IsFailure);
        Assert.Equal("FERRAMENTAS_DUPLICATE_LOTE", d2.Error.Code);
    }

    // ---- Verification rules: per-lot, ferramentas.configure ----

    [Fact]
    public async Task AddCheckRule_WithoutConfigureCapability_IsForbidden()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        // _service uses Authorized() WITHOUT configure.
        var result = await _service.AddCheckRuleAsync(new CheckRuleRequest(lote.ToolLoteId, "R", FerramentasCheckFrequency.PerProduction));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public async Task AddCheckRule_WithConfigureCapability_SucceedsPerLot()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        var gate = new FerramentasAuthorizationGate(FakeCurrentUser.Configurator(), new FakeAuthorshipAccessor("ferr-actor"));
        var configService = new FerramentasService(_repository, _ruleLookup, gate, new FixedClock(Now));

        var added = await configService.AddCheckRuleAsync(new CheckRuleRequest(lote.ToolLoteId, "Verificar encaixe", FerramentasCheckFrequency.OncePerLot));
        Assert.True(added.IsSuccess);

        var rules = await configService.ListCheckRulesByLoteAsync(lote.ToolLoteId);
        Assert.True(rules.IsSuccess);
        var rule = Assert.Single(rules.Value);
        Assert.Equal("Verificar encaixe", rule.RuleText);
        Assert.Equal("uma_vez_no_lote", rule.Frequency);
    }

    // ---- Register piece + condition ----

    [Fact]
    public async Task RegisterPiece_AndSetCondition_AreExplicitFacts()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        var pieceId = await _service.RegisterPieceAsync(new RegisterPieceRequest(lote.ToolLoteId, 1, "42"));
        Assert.True(pieceId.IsSuccess);

        var condition = await _service.SetConditionAsync(new SetConditionRequest(lote.ToolLoteId, "42", ToolCondition.Repaired, "Reparação efetuada"));
        Assert.True(condition.IsSuccess);

        var pieces = await _service.ListPiecesByLoteAsync(lote.ToolLoteId);
        Assert.True(pieces.IsSuccess);
        var piece = Assert.Single(pieces.Value);
        Assert.Equal("42", piece.Number);
        Assert.Equal("repaired", piece.Condition);
    }

    [Fact]
    public async Task SetCondition_WithoutReason_IsValidationError()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);

        await _service.RegisterPieceAsync(new RegisterPieceRequest(lote.ToolLoteId, 1, "42"));
        var result = await _service.SetConditionAsync(new SetConditionRequest(lote.ToolLoteId, "42", ToolCondition.Sucatado, " "));
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
    }

    // ---- List search ----

    [Fact]
    public async Task ListReferences_MapsProcessoAndLinesFromLotes()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);

        var list = await _service.ListReferencesAsync(new FerramentasSearchRequest(null, null, null, null, null, null, null));
        Assert.True(list.IsSuccess);
        var item = Assert.Single(list.Value);
        Assert.Equal("CM-01", item.RefCode);
        Assert.Equal("NNPB", item.Processo);
        Assert.Contains("B1", item.AllowedLinesCsv);
    }

    // ---- Rule lookup consumed by Job On ----

    [Fact]
    public async Task RegisterPiece_ConcurrentDuplicate_Raw23505_MapsToCleanDomainConflict()
    {
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var lote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);
        _repository.FailPieceDuplicate = true; // uq_physical_pieces_lote_number raced (audit ON-02)

        var result = await _service.RegisterPieceAsync(new RegisterPieceRequest(lote.ToolLoteId, 1, "42"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.DomainConflict, result.Error.Category);
        Assert.Equal("FERRAMENTAS_PIECE_DUPLICATE", result.Error.Code);
        Assert.DoesNotContain(lote.ToolLoteId, _repository.Pieces.Keys);
        Assert.DoesNotContain(_repository.AuditEvents, a => a.eventType == "ferramentas.peca.registar");
    }

    [Fact]
    public async Task DuplicateLote_IsAtomic_NoPartialStateOnFailure()
    {
        // Create reference + first lot with a rule.
        var created = await _service.CreateReferenceWithFirstLoteAsync(ValidCreate());
        Assert.True(created.IsSuccess);
        var baseLote = _repository.Lotes.Values.Single(l => l.ToolReferenceId == created.Value.ReferenceId);
        var addRuleGate = new FerramentasAuthorizationGate(FakeCurrentUser.Configurator(), new FakeAuthorshipAccessor("ferr-actor"));
        var configService = new FerramentasService(_repository, _ruleLookup, addRuleGate, new FixedClock(Now));
        await configService.AddCheckRuleAsync(new CheckRuleRequest(baseLote.ToolLoteId, "Verificar encaixe", FerramentasCheckFrequency.OncePerLot));

        // Simulate a mid-transaction failure of the atomic duplication (audit FA-03):
        // the lot + copied rules + audit must all roll back together.
        _repository.FailAtomicCreate = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateLoteFromBaseAsync(new CreateLoteFromBaseRequest(
                baseLote.ToolLoteId, "5", 6, new[] { "B2" }, "CR-01", "B")));

        Assert.Single(_repository.Lotes); // only the base lot remains
        Assert.Single(_repository.CheckRules.Values.SelectMany(r => r)); // only the base rule
        Assert.DoesNotContain(_repository.AuditEvents, a => a.eventType == "ferramentas.lote.duplicar");
    }

    [Fact]
    public async Task ResolveActiveRules_ReturnsRuleLookupResult()
    {
        _ruleLookup.Rules = new[]
        {
            new Domain.Modules.JobOn.VerificationRule(Guid.NewGuid(), "Verificar encaixe", Domain.Modules.JobOn.VerificationFrequency.OncePerLot)
        };
        var result = await _service.ResolveActiveRulesAsync(Guid.NewGuid());
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }
}