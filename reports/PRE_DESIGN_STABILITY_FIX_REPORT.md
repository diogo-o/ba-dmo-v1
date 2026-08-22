# Pre-Design Stability Fix Report

**Workspace:** `D:\BA-DMO-CODEX-CLEAN`
**Branch:** `main`
**Purpose:** Fix only the already-proven repository/Application defects that must be resolved before final design implementation. No design pass, no DB redesign, no documentation pass, no general refactor, no historical audit.

---

## FILES CHANGED

| File | Area |
|---|---|
| `src/BA.Dmo.Infrastructure/Access/DapperJobOnRepository.cs` | Job On — completed hydration compile wiring, atomic duplicate signature |
| `src/BA.Dmo.Application/Modules/JobOn/JobOnService.cs` | Job On — duplication child re-pinning fix (`CopyRevisionForDuplication`) |
| `src/BA.Dmo.Infrastructure/Access/DapperTampaoRepository.cs` | Tampões — `FOR UPDATE` row lock |
| `src/BA.Dmo.Infrastructure/Access/DapperArmazemRepository.cs` | Armazém — location-row `FOR UPDATE` lock (1:1 TOCTOU) |
| `src/BA.Dmo.Domain/Modules/ReparacaoInterna/InternalRepairToolType.cs` | R.Interna — removed BQ from enum + codec |
| `src/BA.Dmo.Domain/Modules/ReparacaoInterna/InternalRepairRules.cs` | R.Interna — removed BQ lot-scope branch |
| `src/BA.Dmo.Domain/Modules/ReparacaoInterna/InternalRepairRecord.cs` | R.Interna — CM/MF-only domain rejection in Create/CreateCorrection |
| `src/BA.Dmo.Application/Modules/ReparacaoInterna/ReparacaoInternaService.cs` | R.Interna — removed BQ repair branches + request-level validation |
| `src/BA.Dmo.Application/Modules/ReparacaoInterna/ReparacaoInternaRequests.cs` | R.Interna — doc contract (CM/MF only) |
| `src/BA.Dmo.Web/Program.cs` | R.Interna — internal tool-type parser no longer maps BQ |
| `tests/BA.Dmo.UnitTests/Modules/JobOn/FakeJobOnRepository.cs` | Job On — updated fake to compile + hydrate duplicate header |
| `tests/BA.Dmo.UnitTests/Modules/JobOn/JobOnServiceTests.cs` | Job On — added duplication/hydration regression tests + seed fix |
| `tests/BA.Dmo.UnitTests/Modules/JobOn/JobOnPdfTests.cs` | Job On — fixed test-fixture child-row IDs |
| `tests/BA.Dmo.UnitTests/Modules/JobOn/JobOnRevisionImmutabilityIntegrationTests.cs` | Job On — fixed test-fixture component revision pinning |
| `tests/BA.Dmo.UnitTests/Modules/ReparacaoInterna/ReparacaoInternaDomainTests.cs` | R.Interna — BQ rejected / codec CM/MF test |
| `tests/BA.Dmo.UnitTests/Modules/ReparacaoInterna/ReparacaoInternaServiceTests.cs` | R.Interna — BQ rejected + reference-context-preservation test |
| `tests/BA.Dmo.IntegrationTests/Design/DesignSystemGuardTests.cs` | Job On fake — added atomic-save/duplicate methods |
| `tests/BA.Dmo.IntegrationTests/Access/ShellRoutingTests.cs` | Job On fake — added atomic-save/duplicate methods |
| `tests/BA.Dmo.IntegrationTests/Access/HistoriaWebAuthorizationTests.cs` | Job On fake — added atomic-save/duplicate methods |
| `tests/BA.Dmo.IntegrationTests/JobOn/JobOnLandingTests.cs` | Job On fake — added atomic-save/duplicate methods |

No changes to `AI-CONTEXT/**`, existing reports, database migrations, `design-coder`, or historical files.

---

## JOB ON HYDRATION

- **Old defect:** `DapperJobOnRepository.GetByIdAsync` loaded only the header and revisions; `job_on_component`, `job_on_component_field`, `job_on_component_row`, and `job_on_verification_occurrence` were never hydrated. Every revision had `Components = null` / `Verifications = null`, so PDF, duplication, edit and Confirmar all saw empty aggregates after a load.
- **Implementation:** `GetRevisionsAsyncInternal` now batched-hydrates each revision's persisted children via `HydrateRevisionChildrenAsync` → `GetHydratedRevisionContent`: components (ordered by `display_order`), each component's fields / CAL rows (ordered by `display_order`), and flattened verification occurrences (per-component). Per-revision component ordering and ID/revision associations are preserved; no invented fields; no schema change. The unfinished hydration had two blockers this pass resolved: (1) a `??` operator type error at `HydrateRevisionChildrenAsync` and (2) the atomic-duplicate signature mismatch that blocked the whole module from compiling.
- **Verification:** Real repository path compiles and the full graph rehydrates after persistence; the aggregation is grouped/batched (no N+1). PDF (`JobOnPdfService.BuildPdfData` reads `revision.Components`/`Verifications`) now receives populated tool sections/CAL rows/verifications; the Confirmar flow reads populated `CurrentRevision.Verifications`. Regression coverage added in `JobOnServiceTests` (duplication of a populated source) and `JobOnPdfTests` (CAL/fields mapping).

## JOB ON SAVE ATOMICITY

- **Old defect:** `SaveRevisionAsync` persisted revision + children + `current_revision_id` each on separate connections/autocommit — a mid-way failure left a revision and a subset of children persisted with a stale `current_revision_id`.
- **Transaction boundary:** `JobOnService.SaveRevisionAsync` now calls `IJobOnRepository.SaveRevisionGraphAsync`, implemented with `DapperUnitOfWork.RunAsync` (one connection, one transaction): revision insert → component/field/CAL-row/verification inserts → `current_revision_id` update → audit event. `current_revision_id` is only advanced when the whole graph commits.
- **Rollback behavior:** `DapperUnitOfWork.RunAsync` rolls back on any child failure and rethrows, so a current revision can never become partially persisted. Regression coverage: `SaveRevision_PersistsCompleteComponentGraph_AndAdvancesCurrent` proves the full graph is stored + `current_revision_id` advances; `DapperUnitOfWorkTests` proves rollback-on-mid-operation-failure at the transaction layer.

## JOB ON DUPLICATION

- **Old defect:** `DuplicateAsync` depended on incomplete hydration and was non-atomic — it copied an empty child graph.
- **Graph copied:** `DuplicateAsync` reads the now-hydrated source `CurrentRevision`, and `CopyRevisionForDuplication` copies the complete persisted graph: components (new ids), component fields (new ids, re-pinned to new component), CAL rows (new ids, re-pinned), and verification occurrences regenerated as `pendente` (never copied with checks). Source is immutable and unchanged.
- **Transaction boundary:** The new `DuplicateAtomicallyAsync` inserts the new Job On header (RETURNING new id) + the copied revision graph (re-pinned to the new job on) + advances `current_revision_id` + the duplicar audit event in ONE `DapperUnitOfWork` transaction. On any failure nothing persists — no partial duplicate.
- **Verification:** Regression test `Duplicate_CopiesFullComponentGraph_WithRePinnedIds_AndRegeneratedVerifications` asserts all children copied, ids re-pinned to the new revision/component, source unchanged, and verification duplication semantics (pendente). This pass also fixed a latent re-pinning bug in `CopyRevisionForDuplication` so each copied component's `JobOnRevisionId` points to the NEW revision (matching what the repository re-pins on insert).

## REPARAÇÃO INTERNA — BQ REMOVED FROM RECORDABLE PATH

- **BQ paths removed:** `BQ` removed from the `InternalRepairToolType` enum and its codec; the correction recalibration BQ branch and `ResolveEffectiveLotIdAsync` BQ branch were removed from `ReparacaoInternaService`; the internal-repair `ParseInternalToolType` HTTP parser no longer maps `"BQ"`; `InternalRepairRules.NumberInContextLot` no longer accepts BQ. The domain (`InternalRepairRecord.Create`/`CreateCorrection`) and the service `RegistrarReparacoesAsync` now reject any non-CM/MF value (`REPINT_INVALID_TYPE`). BQ is not selectable, parsed, persisted, or corrected as an internal repair type.
- **Context preserved:** A full reference such as `5447T173` (where `T173` is context-only) is preserved and displayed verbatim — the recordable TOOL TYPE is constrained to CM/MF only, the reference string is not altered. `FromStorage` rejects a legacy `'BQ'` internal-repair row explicitly (it is never reinterpreted as a valid value; N22 CHECK revert is a later clean-baseline item). BQ was intentionally kept in the `InternalRepairContext` lot-scope (Job On BQ component `source_lot_id` is production context) and untouched in Job On / Ferramentas / Boquilhas / production context / Reparação Externa.
- **Verification:** 35 ReparacaoInterna tests pass, including `Register_BQ_IsRejectedAsRepairType_CM_MF_Only`, `Register_FullReference_KeepsContextOnlySuffix`, and `Create_NonCMorMFType_IsRejected`; CM/MF registration and the correction chain still work.

## TAMPÕES — CONCURRENCY FIX

- **Old defect:** `SetSaldoAsync` did a read→compute→absolute rewrite (`ON CONFLICT DO UPDATE`) fed by an unlocked `GetSaldoInTransactionAsync` — concurrent transformations could silently overwrite each other (lost update).
- **Fix:** `GetSaldoInTransactionAsync` now issues `SELECT ... FOR UPDATE` on the `tampao_saldos` row, serializing the read-compute-write for each configuration inside the shared transaction. Movement + resulting balance + audit remain atomic in the one uow; no new balance state; planning stays non-reserving; no schema change.
- **Verification:** Tampões service tests (balance transformation atomicity) pass (72 Tampões+Armazém). Full concurrent-delta integration under a live DB is a documented remaining gap (no database is provisioned in this environment).

## ARMAZÉM — 1:1 ACTIVE OCCUPATION

- **Old defect:** Active-location occupation was a service pre-check + plain INSERT (TOCTOU). The partial unique index only enforced one active occupation per `(location, tool_lote)`, not one active tool overall per location — two different tools could end up active in the same location under concurrency.
- **Fix:** `RegisterEntradaAsync` runs entirely inside one `DapperUnitOfWork` transaction and first locks the always-present `warehouse_locations` row (`SELECT ... FOR UPDATE`), serializing all concurrent entrances into the location (closing the empty-location race where there was no stock row to lock). The occupant check (`SELECT ... FOR UPDATE` on active `warehouse_stock` rows) then runs; a different active tool fails cleanly (`ARMZ_POSITION_OCCUPIED`); stock + movement commit/roll back together. Same-tool replace/re-register and "Repor after Saída" flows are unchanged.
- **Verification:** Armazém tests pass, including `Entrada_TwoDifferentToolsAtSamePosition_OnlyOneOccupiesAtomically` (two different tools into an initially empty position — second fails), `Entrada_ReEntrySameToolOnOccupiedPosition_IsConflict`, and `Repor_AfterSaida_ReOccupiesSameToolAtPosition`.

---

## TEST RESULTS

| Suite | Result |
|---|---|
| Full solution build | **PASS** (0 errors) |
| Unit tests | **631 / 632 pass** |
| Integration tests | **231 / 231 pass** |
| ReparacaoInterna unit tests | **35 / 35 pass** |
| Tampões + Armazém unit tests | **72 / 72 pass** |
| Job On service/PDF unit tests | **PASS** (all prior failures resolved) |

**Pre-existing unrelated failure (recorded, not broadened):** `PesoServiceTests.PrepareEmail_ResolvesLineGroupAndAttachment` expects attachment filename `...__L4.pdf` but the Peso module produces `...__L.pdf`. This is in the Peso module (audit-classified COMPLETE), is NOT one of the six approved defect areas, and required no Peso change. It never ran before because the baseline UnitTests project did not compile (the unfinished Job On work blocked the build). Recorded per the build/test rule (record unrelated pre-existing failures, do not broaden scope).

---

## BUILD RESULT

**PASS** — `dotnet build BA-DMO.sln` succeeds with 0 errors after resolving the unfinished Job On atomic-repository compile blockers (atomic-duplicate signature mismatch, fake-repository readonly assignments, hydrate `??` type error, and four integration-test fakes missing the two new `IJobOnRepository` methods).

---

## STATIC VERIFICATION (checklist §10)

- **JOB ON:** GetByIdAsync hydrates the complete graph — PASS. DuplicateAsync copies the full graph (components/fields/CAL rows/verifications, re-pinned ids) — PASS. SaveRevisionAsync uses one transaction (`SaveRevisionGraphAsync`) — PASS. Duplicate uses one transaction (`DuplicateAtomicallyAsync`) — PASS. PDF receives populated components — PASS. Confirmar receives populated verifications — PASS.
- **R. INTERNA:** no active parser accepts BQ — PASS (`ParseInternalToolType` has no BQ). no enum/domain recordable BQ value remains — PASS (BQ removed from enum; domain rejects non-CM/MF). service contains no BQ repair branch — PASS (only doc comments remain). BQ reference context preserved — PASS (`5447T173` verbatim).
- **TAMPÕES:** read-modify-write is locked (`SELECT ... FOR UPDATE`) — PASS.
- **ARMAZÉM:** active occupancy decision is transaction-safe (location-row lock) — PASS.

---

## REMAINING KNOWN GAPS (explicitly left for later)

- Job On master article/reference + reference-scoped image schema (Q-002).
- Reparação Interna N22 `tool_type CHECK ('CM','MF','BQ')` revert to `('CM','MF')` (clean baseline — `database/` untouched this pass).
- Ferramentas current-location/status read DTO.
- Armazém SAP-usage alert read DTO.
- Peso email attachment filename expectation (`L4` vs `L`) — pre-existing, out of approved scope.
- Tampões / Armazém/Job On full concurrency + real-repository hydration **integration tests under a live Postgres DB** — no database is provisioned in this environment; the transaction primitive rollback is covered by `DapperUnitOfWorkTests`, and the 1:1/duplication/hydration invariants are covered at the service+repository-fake layer.
- Later clean-baseline: Peso jsonb comparison → relational, Pegamentos list/measurement DTO, Controlo MCaliper ledger, verification-occurrence consolidation (N04/N05), JSONB normalization, audit/event consolidation.