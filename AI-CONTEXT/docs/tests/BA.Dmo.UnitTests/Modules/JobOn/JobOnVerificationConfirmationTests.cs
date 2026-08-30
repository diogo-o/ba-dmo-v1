using System.Globalization;
using System.Text.Json;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// Job On verification occurrence confirmation (modules/05 §7,
/// 05_BRIEF_VERIFICATIONS §10) — focused coverage of the REAL flow:
///   1.  Authorized user can confirm a pending occurrence;
///   2.  Unauthorized user is denied server-side (zero writes);
///   3.  The correct occurrence is updated;
///   4.  Correct JobOnId / revision ownership is preserved;
///   5.  completed_by is persisted (server-resolved, never client-supplied);
///   6.  completed_at is persisted (server-generated);
///   7.  Reloading returns the persisted confirmed state;
///   8.  Previous revisions remain unchanged;
///   9.  An already-confirmed occurrence is handled cleanly / idempotently;
///   10. A duplicate request cannot duplicate the confirmation state or
///       silently overwrite another actor;
///   11. A stale / wrong-revision / other-Job On occurrence is rejected;
///   12. The audit event is emitted through the existing audit path
///       (job_on_audit_event fact: jobOnId, revision, occurrence, actor,
///       before/after status);
///   13. No Ferramentas / Armazém record is modified (or even read);
///   14. Duplication still regenerates new-production occurrences pending;
///   15. Save / edit and alter-date preserve same-production verification state.
/// All collaborators are fakes — no live DB.
/// </summary>
public class JobOnVerificationConfirmationTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTime FixedClockUtc =
        new(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc);

    private const string OperatorActorId = "bbbbbbbb-0000-0000-0000-000000000001";
    private const string OperatorBActorId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string ResponsibleActorId = "cccccccc-0000-0000-0000-000000000001";

    private readonly FakeJobOnRepository _repository = new();
    private readonly FakeFerramentasToolLookup _toolLookup = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly JobOnService _service;

    public JobOnVerificationConfirmationTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext, new FixedClock(new DateTimeOffset(FixedClockUtc)),
            _toolLookup);
        _identity.GrantResponsible();
    }

    // ---- 1. authorized confirmation --------------------------------------

    [Fact]
    public async Task AuthorizedOperator_ConfirmsPendingOccurrence_Success()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        var occurrence = FindOccurrence(stored!, occurrenceId);
        Assert.NotNull(occurrence);
        Assert.Equal("confirmada", occurrence!.Status);
    }

    // ---- 2. unauthorized is denied server-side ---------------------------

    [Theory]
    [InlineData(IdentityKind.ViewOnly)]
    [InlineData(IdentityKind.None)]
    public async Task UnauthorizedUser_IsDeniedServerSide_AndWritesNothing(IdentityKind identity)
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.Grant(identity);

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_repository.VerificationUpdates);
        // The seed operations (criar/guardar) already audited: the point is that
        // the DENIED confirmation itself writes nothing.
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.verificacao.confirmar");
        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal("pendente", FindOccurrence(stored!, occurrenceId)!.Status);
    }

    // ---- 3. correct occurrence --------------------------------------------

    [Fact]
    public async Task OnlyTheTargetedOccurrence_IsConfirmed()
    {
        var jobOnId = await SeedDraftAsync();
        var componentA = Guid.NewGuid();
        var componentB = Guid.NewGuid();
        var occurrenceA = Guid.NewGuid();
        var occurrenceB = Guid.NewGuid();
        var saved = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null,
            new[]
            {
                PendingComponent(componentA, ComponentFamily.MP_CM, occurrenceA),
                PendingComponent(componentB, ComponentFamily.MF, occurrenceB)
            }));
        Assert.True(saved.IsSuccess);
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceA);

        Assert.True(result.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal("confirmada", FindOccurrence(stored!, occurrenceA)!.Status);
        Assert.Equal("pendente", FindOccurrence(stored!, occurrenceB)!.Status);
    }

    // ---- 4 + 12. audit fact through the existing audit path --------------

    [Fact]
    public async Task Confirmation_RecordsAuditFact_WithJobOnRevisionOccurrenceActorAndStatus()
    {
        var (jobOnId, revisionId, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(_repository.AuditEvents, a => a.EventType == "jobon.verificacao.confirmar");
        Assert.Equal(jobOnId, audit.JobId);                       // correct JobOnId
        Assert.Equal(revisionId, audit.RevisionId);               // correct revision ownership
        Assert.Equal(OperatorActorId, audit.ActorId);             // the confirming actor
        Assert.NotNull(audit.Before);
        Assert.NotNull(audit.After);

        var before = JsonSerializer.Deserialize<JsonElement>(audit.Before!);
        Assert.Equal(occurrenceId, before.GetProperty("occurrence_id").GetGuid());
        Assert.Equal("pendente", before.GetProperty("status").GetString());

        var after = JsonSerializer.Deserialize<JsonElement>(audit.After!);
        Assert.Equal(occurrenceId, after.GetProperty("occurrence_id").GetGuid());
        Assert.Equal("confirmada", after.GetProperty("status").GetString());
        Assert.Equal(OperatorActorId, after.GetProperty("completed_by").GetString());
        Assert.Equal(
            FixedClockUtc,
            DateTime.Parse(after.GetProperty("completed_at_utc").GetString()!, CultureInfo.InvariantCulture)
                .ToUniversalTime());
    }

    // ---- 5 + 6. completed_by / completed_at persistence -------------------

    [Fact]
    public async Task Confirmation_PersistsCompletedBy_FromAuthenticatedSession()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal(OperatorActorId, FindOccurrence(stored!, occurrenceId)!.CompletedBy);
    }

    [Fact]
    public async Task Confirmation_PersistsCompletedAt_GeneratedServerSide()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal(FixedClockUtc, FindOccurrence(stored!, occurrenceId)!.CompletedAtUtc);
    }

    // ---- 7. reload returns the persisted state ----------------------------

    [Fact]
    public async Task Reload_ReturnsPersistedConfirmedState()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceId)).IsSuccess);

        // A fresh load (page reload) exposes the persisted confirmed state through
        // the SAME collections the folha renders (CurrentRevision.Verifications +
        // per-component occurrences).
        var reloaded = await _repository.GetByIdAsync(jobOnId);
        Assert.NotNull(reloaded);
        var flattened = reloaded!.CurrentRevision!.Verifications!
            .Single(v => v.JobOnVerificationOccurrenceId == occurrenceId);
        Assert.Equal("confirmada", flattened.Status);
        Assert.Equal(OperatorActorId, flattened.CompletedBy);
        Assert.Equal(FixedClockUtc, flattened.CompletedAtUtc);
        var perComponent = reloaded.CurrentRevision.Components
            .SelectMany(c => c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
            .Single(v => v.JobOnVerificationOccurrenceId == occurrenceId);
        Assert.Equal("confirmada", perComponent.Status);
    }

    // ---- 8. previous revisions remain unchanged ---------------------------

    [Fact]
    public async Task PreviousRevisions_RemainUnchanged_AfterConfirmationAndNewRevision()
    {
        var (jobOnId, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();
        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceA)).IsSuccess);

        // A subsequent save creates a NEW revision of the SAME Job On (the previous
        // revision's verification rows must stay untouched).
        _identity.GrantResponsible();
        var componentB = Guid.NewGuid();
        var occurrenceB = Guid.NewGuid();
        var saved = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { PendingComponent(componentB, ComponentFamily.MF, occurrenceB) }));
        Assert.True(saved.IsSuccess);

        // The repository-level verification row of the previous revision (revision
        // 1 of this Job On) remains untouched by the confirmation AND the save.
        var oldOccurrence = _repository.Verifications
            .Single(v => v.JobOnVerificationOccurrenceId == occurrenceA);
        Assert.Equal("confirmada", oldOccurrence.Status);
        Assert.Equal(OperatorActorId, oldOccurrence.CompletedBy);
        Assert.Equal(FixedClockUtc, oldOccurrence.CompletedAtUtc);

        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal("pendente", FindOccurrence(stored!, occurrenceB)!.Status);
    }

    // ---- 9. already confirmed: clean + idempotent --------------------------

    [Fact]
    public async Task AlreadyConfirmedOccurrence_IsIdempotent_Success()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();

        var first = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);
        var second = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess); // clean idempotent success, no error
        Assert.Single(_repository.VerificationUpdates); // zero writes on the repeat
        Assert.Single(_repository.AuditEvents, a => a.EventType == "jobon.verificacao.confirmar");
    }

    // ---- 10. duplicate request: no duplicated state, no silent overwrite ---

    [Fact]
    public async Task DuplicateRequest_CannotDuplicateState_NorOverwriteAnotherActor()
    {
        var (jobOnId, _, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();
        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceId)).IsSuccess);

        // A SECOND actor (e.g. a double-click from another session) repeats the
        // request: it must succeed idempotently WITHOUT overwriting the first
        // actor's persisted confirmation or adding a duplicated audit fact.
        _identity.GrantOperatorB();
        var repeat = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(repeat.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        var occurrence = FindOccurrence(stored!, occurrenceId)!;
        Assert.Equal(OperatorActorId, occurrence.CompletedBy); // the FIRST actor wins
        Assert.Equal(FixedClockUtc, occurrence.CompletedAtUtc);
        Assert.Single(_repository.VerificationUpdates);
        Assert.Single(_repository.AuditEvents, a => a.EventType == "jobon.verificacao.confirmar");
        // A single occurrence row remains — no duplicated confirmation state.
        Assert.Single(_repository.Verifications, v => v.JobOnVerificationOccurrenceId == occurrenceId);
    }

    // ---- 11. stale / wrong-revision / other-Job On occurrences -------------

    [Fact]
    public async Task StaleRevisionOccurrence_IsRejected_NotFound()
    {
        var (jobOnId, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync();

        // A new revision supersedes revision 1: occurrence A is no longer part of
        // the CURRENT revision and must be rejected, never confirmed in place.
        _identity.GrantResponsible();
        var componentB = Guid.NewGuid();
        var occurrenceB = Guid.NewGuid();
        var saved = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { PendingComponent(componentB, ComponentFamily.MF, occurrenceB) }));
        Assert.True(saved.IsSuccess);

        _identity.GrantOperator();
        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceA);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Equal("JOBON_VERIFICATION_NOT_FOUND", result.Error.Code);
        Assert.Empty(_repository.VerificationUpdates);
        // The rejected confirmation must not have audited anything.
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.verificacao.confirmar");
    }

    [Fact]
    public async Task OccurrenceFromAnotherJobOn_IsRejected_NotFound()
    {
        var (jobOnIdA, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync("202608", "LINHA-1");
        var (jobOnIdB, _, _) = await SeedJobOnWithPendingVerificationAsync("202609", "LINHA-2");
        _identity.GrantOperator();

        var result = await _service.ConfirmVerificationAsync(jobOnIdB, occurrenceA);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Equal("JOBON_VERIFICATION_NOT_FOUND", result.Error.Code);
        Assert.Empty(_repository.VerificationUpdates);
    }

    [Fact]
    public async Task UnknownJobOnOrUnknownOccurrence_AreNotFound()
    {
        var jobOnId = (await SeedJobOnWithPendingVerificationAsync()).JobOnId;
        _identity.GrantOperator();

        var unknownJobOn = await _service.ConfirmVerificationAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.True(unknownJobOn.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, unknownJobOn.Error.Category);
        Assert.Equal("JOBON_NOT_FOUND", unknownJobOn.Error.Code);

        var unknownOccurrence = await _service.ConfirmVerificationAsync(jobOnId, Guid.NewGuid());
        Assert.True(unknownOccurrence.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, unknownOccurrence.Error.Category);
        Assert.Equal("JOBON_VERIFICATION_NOT_FOUND", unknownOccurrence.Error.Code);
        Assert.Empty(_repository.VerificationUpdates);
    }

    // ---- 13. no Ferramentas / Armazém / new-record side effects ------------

    [Fact]
    public async Task Confirmation_NeverTouchesFerramentas_NorCreatesNewRecords()
    {
        var (jobOnId, revisionId, occurrenceId) = await SeedJobOnWithPendingVerificationAsync();
        var jobOnCountBefore = _repository.JobOns.Count;
        var revisionCountBefore = _repository.Revisions.Count;
        _toolLookup.Register(
            Guid.NewGuid(), Guid.NewGuid(),
            FerramentasToolType.CM, "5447", "1", "Contra molde 5447", "LINHA-1");
        var lotsBefore = _toolLookup.Lots.Count;
        Assert.Equal(0, _toolLookup.ResolveCalls);
        Assert.Equal(0, _toolLookup.SearchCalls);

        _identity.GrantOperator();
        var result = await _service.ConfirmVerificationAsync(jobOnId, occurrenceId);

        Assert.True(result.IsSuccess);
        // Ferramentas is neither read nor written; no Armazém dependency exists on
        // this use case; no new Job On or revision is created.
        Assert.Equal(lotsBefore, _toolLookup.Lots.Count);
        Assert.Equal(0, _toolLookup.ResolveCalls);
        Assert.Equal(0, _toolLookup.SearchCalls);
        Assert.Equal(jobOnCountBefore, _repository.JobOns.Count);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        var stored = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal(revisionId, stored!.CurrentRevisionId); // same current revision
    }

    // ---- 14. duplication regenerates pending occurrences -------------------

    [Fact]
    public async Task Duplicate_RegeneratesPendingOccurrences_ForNewProduction()
    {
        var (jobOnId, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();
        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceA)).IsSuccess);
        _identity.GrantResponsible();

        var duplicated = await _service.DuplicateAsync(new DuplicateJobOnRequest(
            jobOnId, "202610", "LINHA-3", Start.AddMonths(1), null));

        Assert.True(duplicated.IsSuccess);
        var newJobOn = await _repository.GetByIdAsync(duplicated.Value);
        Assert.NotNull(newJobOn);
        var newOccurrence = newJobOn!.CurrentRevision!.Components!
            .SelectMany(c => c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
            .Single();
        Assert.NotEqual(occurrenceA, newOccurrence.JobOnVerificationOccurrenceId); // new id
        Assert.Equal("pendente", newOccurrence.Status);                            // regenerated pending
        Assert.Null(newOccurrence.CompletedBy);
        Assert.Null(newOccurrence.CompletedAtUtc);

        // The source Job On's confirmation is untouched.
        var source = await _repository.GetByIdAsync(jobOnId);
        Assert.Equal("confirmada", FindOccurrence(source!, occurrenceA)!.Status);
    }

    // ---- 15. same-production flows preserve verification state -------------

    [Fact]
    public async Task AlterDate_PreservesVerificationState_SameProduction()
    {
        var (jobOnId, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();
        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceA)).IsSuccess);
        _identity.GrantResponsible();

        var altered = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(2), null));

        Assert.True(altered.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        // NEW revision (new occurrence ids) with the SAME production occurrence:
        // the confirmed state is preserved — never silently reset.
        var newOccurrence = stored!.CurrentRevision!.Components!
            .SelectMany(c => c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
            .Single();
        Assert.NotEqual(occurrenceA, newOccurrence.JobOnVerificationOccurrenceId);
        Assert.Equal("confirmada", newOccurrence.Status);
        Assert.Equal(OperatorActorId, newOccurrence.CompletedBy);
        Assert.Equal(FixedClockUtc, newOccurrence.CompletedAtUtc);
        // The repository-level row of the previous revision's occurrence is unchanged.
        Assert.Equal("confirmada", _repository.Verifications
            .Single(v => v.JobOnVerificationOccurrenceId == occurrenceA).Status);
    }

    [Fact]
    public async Task SaveRevision_PreservesVerificationState_SameProduction()
    {
        var (jobOnId, _, occurrenceA) = await SeedJobOnWithPendingVerificationAsync();
        _identity.GrantOperator();
        Assert.True((await _service.ConfirmVerificationAsync(jobOnId, occurrenceA)).IsSuccess);
        _identity.GrantResponsible();

        // The real UI copies the embedded current-revision graph WITH the current
        // verification state under fresh ids (R-002); the service must preserve it.
        // What the client sees is the loaded (hydrated) aggregate:
        var loaded = await _repository.GetByIdAsync(jobOnId);
        var confirmedBefore = loaded!.CurrentRevision!.Verifications!.Single();
        Assert.Equal("confirmada", confirmedBefore.Status);
        var newComponentId = Guid.NewGuid();
        var newOccurrenceId = Guid.NewGuid();
        var copy = PendingComponent(newComponentId, ComponentFamily.MP_CM, newOccurrenceId);
        var saved = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null,
            new[]
            {
                copy with
                {
                    Verifications = new[]
                    {
                        confirmedBefore with
                        {
                            JobOnVerificationOccurrenceId = newOccurrenceId,
                            JobOnComponentId = newComponentId
                        }
                    }
                }
            }));

        Assert.True(saved.IsSuccess);
        var stored = await _repository.GetByIdAsync(jobOnId);
        var newOccurrence = FindOccurrence(stored!, newOccurrenceId)!;
        Assert.Equal("confirmada", newOccurrence.Status); // state preserved, not reset
        Assert.Equal(OperatorActorId, newOccurrence.CompletedBy);
        Assert.Equal(FixedClockUtc, newOccurrence.CompletedAtUtc);
    }

    // ---- helpers -----------------------------------------------------------

    public enum IdentityKind { Operator, OperatorB, Responsible, ViewOnly, None }

    /// <summary>Creates a rascunho (revision 1 with the empty user context).</summary>
    private async Task<Guid> SeedDraftAsync(string production = "202608", string machine = "LINHA-1")
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest(production, machine, Start, null, "9262T288"));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    /// <summary>
    /// Creates a rascunho + revision 1 with ONE pending verification occurrence and
    /// returns (jobOnId, revision1Id, occurrenceId).
    /// </summary>
    private async Task<(Guid JobOnId, Guid Revision1Id, Guid OccurrenceId)> SeedJobOnWithPendingVerificationAsync(
        string production = "202608", string machine = "LINHA-1")
    {
        var jobOnId = await SeedDraftAsync(production, machine);
        var componentA = Guid.NewGuid();
        var occurrenceA = Guid.NewGuid();
        var saved = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, null, null, null, new[] { PendingComponent(componentA, ComponentFamily.MP_CM, occurrenceA) }));
        Assert.True(saved.IsSuccess);
        return (jobOnId, saved.Value, occurrenceA);
    }

    private static JobOnComponent PendingComponent(
        Guid componentId, ComponentFamily family, Guid occurrenceId) => new()
    {
        JobOnComponentId = componentId,
        JobOnRevisionId = Guid.NewGuid(),
        Family = family,
        ReferenceSnapshot = "CM 5447",
        LotSnapshot = "Lote 3",
        Verifications = new[]
        {
            new JobOnVerificationOccurrence
            {
                JobOnVerificationOccurrenceId = occurrenceId,
                JobOnComponentId = componentId,
                RuleTextSnapshot = "Verificar junta da boquilha",
                Status = "pendente"
            }
        }
    };

    private static JobOnVerificationOccurrence? FindOccurrence(
        JobOnEntity jobOn, Guid occurrenceId) =>
        (jobOn.CurrentRevision?.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
            .FirstOrDefault(v => v.JobOnVerificationOccurrenceId == occurrenceId);

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        /// <summary>OPERATOR profile: jobon.view + jobon.confirmar (NO jobon.edit) — the documented confirmer.</summary>
        public void GrantOperator() => User = new CurrentUser(
            Guid.Parse(OperatorActorId), "Operador Job On",
            new[] { "jobon" }, new[] { "jobon.view", "jobon.confirmar" });

        public void GrantOperatorB() => User = new CurrentUser(
            Guid.Parse(OperatorBActorId), "Operador B",
            new[] { "jobon" }, new[] { "jobon.view", "jobon.confirmar" });

        /// <summary>RESPONSÁVEL profile: all Job On capabilities (edit surface for seeding).</summary>
        public void GrantResponsible() => User = new CurrentUser(
            Guid.Parse(ResponsibleActorId), "Responsável Técnico",
            new[] { "jobon" }, new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });

        public void GrantViewOnly() => User = new CurrentUser(
            Guid.Parse("dddddddd-0000-0000-0000-000000000001"), "Só Consulta",
            new[] { "jobon" }, new[] { "jobon.view" });

        public void GrantNone() => User = new CurrentUser(
            Guid.Parse("dddddddd-0000-0000-0000-000000000002"), "Sem Acesso",
            Array.Empty<string>(), Array.Empty<string>());

        public void Grant(IdentityKind kind)
        {
            switch (kind)
            {
                case IdentityKind.Operator:
                    GrantOperator();
                    break;
                case IdentityKind.OperatorB:
                    GrantOperatorB();
                    break;
                case IdentityKind.Responsible:
                    GrantResponsible();
                    break;
                case IdentityKind.ViewOnly:
                    GrantViewOnly();
                    break;
                case IdentityKind.None:
                    GrantNone();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
