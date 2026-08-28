# POST-CODEX QUEUE A — BASELINE HARDENING IMPLEMENTATION REPORT

> **Scope:** IMPLEMENT QUEUE A ONLY, as defined in
> `reports/post_codex_remediation_functional_gate.md` §4-A and
> `reports/post_codex_database_contract_audit.md` §20/§21.
> Every finding below was re-read from both audit reports, its classification
> re-confirmed as `SAFE_TECHNICAL_FIX`, and implemented as the smallest
> necessary change with focused tests.
>
> **HEAD:** `8d916cb` (unchanged origin). **Repo:** `diogo-o/ba-dmo-v1`, branch `main`.
> **Queue B, Queue C and N34 were NOT implemented** (confirmed at the end).

---

## 1. Findings implemented (all classified SAFE_TECHNICAL_FIX by the gate)

| # | Finding(s) | Classification (gate) | Change |
|---|---|---|---|
| 1 | **PC-01** (F1) | SAFE_TECHNICAL_FIX | Pegamentos create binds `updated_at_utc` falling back to `created_at_utc` (mirrors `UpdateAsync`) — explicit NULL never reaches the NOT NULL column |
| 2 | **PG-04** (F8) | SAFE_TECHNICAL_FIX | Pegamentos write flows (create, measurement, update/close, confirm-document) run in single explicit UoWs with a `FOR UPDATE` in-transaction control read; in-tx closed-control rule enforced |
| 3 | **ON-02 / JA-03 / TP-06 / BQ-15 / ADM-06** (F13) | SAFE_TECHNICAL_FIX | Raw 23505 unique violations mapped to clean domain conflicts on `job_on` identity, `tampao_configurations`, `bq_lotes`, `internal_users` auth-user, `physical_pieces` (sibling-pattern: repo throws domain exception, service maps to DomainError) |
| 4 | **FA-03** (F14) | SAFE_TECHNICAL_FIX | Ferramentas lot duplication (lot + copied rules + audit) now one UoW; stale “atomic duplication” doc claim corrected |
| 5 | **F17 code part** (dead code) | SAFE_TECHNICAL_FIX (code part) | Removed verified-runtime-dead methods, DTOs and stale catalog artifacts per audit §18 §17.11 — no DDL, no schema change |
| 6 | **PC-11 hardening part** (suppl.) | SAFE_TECHNICAL_FIX (voluntary) | Admin audit insert binds `AuditJson.Normalize(…)` for before/after summaries (already `::jsonb`-cast) so a future free-text payload can never 22P02 |
| 7 | **ADM-14** (F18) | SAFE_TECHNICAL_FIX | Deploy-order rule documented: `migrate` (incl. N33) must precede the first user write |
| 8 | **PC-10 / CB-01..05** (F20) | SAFE_TECHNICAL_FIX (content parity) | `consolidated_clean_install.sql` refreshed to chain parity: N31 objects, N29 RLS stanza, post-N33 mirror posture, corrected header — non-destructive, no destructive schema cleanup |
| 9 | **BQ-16** (optional, P3) | SAFE_TECHNICAL_FIX (optional) | **Deferred** — requires a new forward-only migration, and the next free name (N34) is reserved for the legacy-mirror removal design; per migration rules no N34 may be created in this task. Revisit in the post-N34 baseline phase |

## 2. What was NOT touched (scope discipline)

- **Queue B** (PC-08, PC-03, PC-05 D-5 dual-emit, PC-04 audit emission, PC-09 D-10, PC-06 production_folder, PC-13, PC-14) — not implemented.
- **Queue C / owner decisions** (PC-02 D-12, PC-07, FA-05, PA-01, F17 DDL part, PC-10 sequencing) — not implemented.
- **N34** legacy-mirror physical removal — not implemented; no migration was created at all (see §5).
- No Manual/SOT rule changed; no functional behavior redesigned; no module boundaries changed (interfaces/methods of the same modules only).
- No destructive schema rationalization; no CASCADE anywhere; no baseline DDL removals.

## 3. Details per finding

### 3.1 PC-01 — Pegamentos create 23502 (updated_at_utc)
- `src/BA.Dmo.Infrastructure/Access/DapperPegamentoRepository.cs` — `CreateAsync` now binds
  `UpdatedAtUtc = control.UpdatedAtUtc ?? control.CreatedAtUtc` (previously `(object?)control.UpdatedAtUtc ?? DBNull.Value`).
- The write is now also UoW-scoped (see PG-04).
- Test: `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Persistence/PegamentoPersistencePostgresTests.cs`
  (`CreateControlAsync_PersistsUpdatedAtUtc_NeverNull`) — real-PG proof, skips without
  `BA_DMO_TEST_DATABASE`. A live deployed-DDL probe (audit §22.2) remains a rollout step.

### 3.2 PG-04 — Pegamentos single UoWs
- New port: `IPegamentoUnitOfWorkFactory` (`src/BA.Dmo.Application/Modules/Pegamentos/`)
  implemented by `DapperPegamentoUnitOfWorkFactory` (`src/BA.Dmo.Infrastructure/Access/`); registered in `src/BA.Dmo.Web/Program.cs`.
- `IPegamentoRepository`: write methods now take `IDbUnitOfWork uow`; new in-transaction locked read
  `GetByIdInTransactionAsync(uow, id)` (SELECT … FOR UPDATE + in-tx measurements + full hydration).
- `PegamentoService`: `CreateControlAsync`, `AddMeasurementAsync`, `UpdateControlAsync`,
  `CloseControlAsync`, `ConfirmDocumentSavedAsync` each open one UoW; the read→domain-rule→write
  sequence (measurement on a just-closed control, double document confirm) now serializes on the
  control row and commits/rolls back atomically.
- Focused tests: `PegamentoServiceTests` (closed-control measurement/update blocked, nothing persisted),
  existing `PegamentoDocumentConfirmationTests` still pass, and real-PG
  `AddMeasurement_WithinUoW_ReadsLockedControlAndPersistsMeasurement`.

### 3.3 F13 — unique/duplicate violation mapping
Domain exceptions (same pattern as `ArmazemLocationOccupiedException`): `JobOnIdentityDuplicateException`,
`TampaoConfigurationDuplicateException`, `BqLoteDuplicateException`, `PhysicalPieceDuplicateException`
(`BA.Dmo.Domain/Modules/…`), `InternalUserAuthDuplicateException` (`BA.Dmo.Domain/Shared/Access/`).
Repositories catch `PostgresException` (SqlState 23505) and rethrow the domain exception;
services map to DomainError:

| Flow | Domain error code |
|---|---|
| `job_on` create/duplicate (uq_job_on_identity) | `JOB_ON_IDENTITY_DUPLICATE` |
| `tampao_configurations` destination create (uq_tampao_configurations_values) | `TAMPAO_CONFIGURATION_DUPLICATE` |
| `bq_lotes` create (uq_bq_lotes_reference_batch) | `BQ_DUPLICATE_LOT` (same code as the fast-path pre-check) |
| `internal_users` create (uq_internal_users_auth_user) | `ADMIN_USER_ALREADY_REGISTERED` (same code as the pre-check) |
| `physical_pieces` register (uq_physical_pieces_lote_number) | `FERRAMENTAS_PIECE_DUPLICATE` |

### 3.4 FA-03 — atomic lot duplication
- New port method `IFerramentasRepository.CreateLoteWithRulesAtomicallyAsync(lote, copiedRules, sourceLoteId, actorId, ct)`;
  implemented with `DapperUnitOfWork.RunAsync` (lote insert + rule inserts + `ferramentas.lote.duplicar`
  audit event in ONE transaction). Service `CreateLoteFromBaseAsync` builds the copied-rules list
  (configuration only, `copied_from_rule_id` preserved) and calls the atomic method once.
- Corrected the stale repository doc claim (the duplication is now genuinely atomic).
- Focused test: `FerramentasServiceTests.DuplicateLote_IsAtomic_NoPartialStateOnFailure`.

### 3.5 F17 — dead runtime methods removed (no DDL)
Repository/interface ports: `SetUserModulesOverrideAsync`, `CountActiveAdminsAsync` (Admin);
`BuildSyncRows` (ModuleCatalogMirrorSynchronizer); `CopyCheckRuleAsync` + `GetOccurrencesByRuleAsync`
+ `FerramentasOccurrenceItem` (Ferramentas); `GetActiveStocksAsync`, `GetStockByToolIdAsync`,
`SubstituirAsync` + `SubstituirRequest`, `ReplaceOccupationAsync` (Armazém); `GetApprovedControlsForJobOnAsync`,
`GetPreviousApprovedAsync` (+ private helper) (Peso); `GetChainRootAsync` (Reparação Interna);
`CountLotesAsync`, `ListMovementsByLoteAsync`, `VoidMovementAsync`, `ListVoidedMovementIdsAsync`,
`GetOpenDiscrepancyForTraceAsync`, `GetLineRepairerDefaultAsync` + `VoidBqMovementRequest` +
`CloseBqTraceRequest.FinalCount` + domain `BqCloseSnapshot` (Boquilhas void family).
Stale catalogs/domain artifacts: `NavigationArea`, `ModuleKind.FunctionalArea`,
`ControloSheetModuleCatalog.ComponentFamilies`, `PesoModuleCatalog.ReportSubfolderMinLength`,
`TampaoMovement.IsSingleBalance`, `TampaoBalanceKindCodec`, `PesoCmDecisionCodec`.
All removals were pre-verified to have zero `src/` callers; corresponding fakes/tests updated or removed.

### 3.6 PC-11 — Admin audit defensive hardening
`DapperAdminRepository.InsertAuditEventAsync` now binds `AuditJson.Normalize(entry.BeforeSummary)` /
`AuditJson.Normalize(entry.AfterSummary)` instead of hardcoded `null`s (cast `::jsonb` already present).
No caller passes summaries today → runtime behavior unchanged (NULL stays NULL).
Test: `AuditJsonBindingTests.AdminAudit_NullSummaries_StayNull_AndFreeTextIsNormalizedBeforeCast`.

### 3.7 ADM-14 — deploy order
`AI-CONTEXT/docs/Maps/03_MIGRATIONS.md` (Migration Execution & Bookkeeping) now documents that
`migrate` (the full N01…N33 family, including N33) MUST complete before the first user write, and that
consolidated-built databases get the same guarantee from the refreshed baseline.

### 3.8 PC-10 — consolidated baseline parity (non-destructive)
`database/consolidated_clean_install.sql`:
- Header corrected (N01…N33 final state; stale `/reports/consolidated_schema_equivalence.md` reference removed).
- N29 RLS/policy/grant stanza added immediately after the `article_reference_images` CREATE + index (CB-02).
- N31 block added at the tail: `access_template_profiles` table + `ba_dmo_ensure_access_template_profile`
  function/trigger + profile backfill + junction collapse/sync + `ux_internal_user_access_templates_actor`
  unique index + profile mirror sync + RLS/policy/grant (CB-01).
- N33 posture applied to the legacy mirrors (CB-04): `profile_title DROP NOT NULL` (was SET NOT NULL),
  junction `REVOKE ALL` from `ba_dmo_app`, and N33 §3 column-level SELECT/INSERT/UPDATE grants on
  `internal_users` excluding `profile_title`.
- No destructive change: nothing dropped/renamed/reshaped; no CASCADE; N27/N28/N29 reconciliation DML
  semantics preserved (no-ops on a fresh database); chain migrations remain the authority.

## 4. Migrations

**None created.** Migrations N01–N33 are immutable and untouched (`git status` confirms
`database/migrations/` clean). No DDL was required for any implemented finding; the only schema-surface
file changed is the consolidated baseline (§3.8). BQ-16 (index DDL) was deferred because the next free
migration name N34 is reserved for the separate legacy-removal design (see §1 item 9).

## 5. Tests added / changed

Added (focused):
- `PegamentoPersistencePostgresTests` (PC-01 + PG-04, real-PG, skips without `BA_DMO_TEST_DATABASE`)
- `JobOnServiceTests` ×2 (JOB_ON_IDENTITY_DUPLICATE on create + duplicate)
- `TampaoServiceTests` ×1 (TAMPAO_CONFIGURATION_DUPLICATE)
- `BoquilhasServiceTests` ×1 (BQ_DUPLICATE_LOT race path)
- `FerramentasServiceTests` ×2 (FERRAMENTAS_PIECE_DUPLICATE; atomic duplication rollback)
- `AdminUserServiceTests` ×1 (ADMIN_USER_ALREADY_REGISTERED race path)
- `PegamentoServiceTests` ×2 (measurement/update on closed control blocked)
- `AuditJsonBindingTests` ×1 (Admin audit Normalize hardening)

Changed: fakes updated for UoW-based Pegamento writes and all removed ports
(FakePegamentoRepository, FakePegamentoUnitOfWorkFactory/UnitOfWork, Ferramentas/Armazem/Peso/Admin/
Boquilhas/ReparaçãoInterna/JobOn fakes, web-test fakes and test-support doubles); removed the tests of
removed dead code (Substituir ×3, BuildSyncRows, PesoCmDecisionCodec round-trip, ModuleKind.FunctionalArea).

## 6. Validation results

| Suite | Pre-remediation baseline | Post Queue A |
|---|---|---|
| Solution build (`dotnet build BA-DMO.sln -c Debug`) | PASS (0 errors) | **PASS (0 errors)** |
| Unit (`BA.Dmo.UnitTests`) | 657 passed / 0 failed | **660 passed / 0 failed** |
| Integration (`BA.Dmo.IntegrationTests`) | 311 passed / 1 failed* | **314 passed / 1 failed*** |

\* **Known pre-existing failure (unrelated, also failing on the untouched baseline at HEAD `8d916cb`):**
`BA.Dmo.IntegrationTests.Access.ShellRoutingTests.Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`
(“nav-item-admin” not found in the rendered Admin shell HTML). It is untouched by Queue A and was NOT
“fixed” (out of scope). Real-PG tests (`*PostgresTests`, `RemediationGuardTests`, `RepairAtomicityTests`,
`AuditJsonBindingTests` admin probe) self-skip without `BA_DMO_TEST_DATABASE` — `LIVE VERIFICATION
REQUIRED` items from audit §22 remain rollout steps for environments with a disposable PostgreSQL.

## 7. Remaining known failures / deferred items

1. `ShellRoutingTests.Scenario7_…` — pre-existing, unrelated (above).
2. BQ-16 (`bq_movements.noted_repairer_id` index) — deferred (N34 name reservation; needs the
   post-N34 baseline phase).
3. Audit §22 live probes (deployed-DDL drift, PG-01/PG-02, audit jsonb, RE-01, JA-04, HS-10,
   migration execution) — run against `BA_DMO_TEST_DATABASE` when available.
4. Queue B/C and N34 remain open by design (not part of this baseline).

## 8. Git diff summary

`74 files changed, 1011 insertions(+), 1152 deletions(-)` (net deletions — consistent with the approved
dead-code removal). Changed: 35 `src/` files, 1 `database/` file (baseline only), 36 test/docs files.
New files (untracked): the 5 duplicate-exception types, the Pegamento UoW factory interface +
implementation, `PegamentoPersistencePostgresTests.cs` (8 new source/test files), plus the three
pre-existing report files (`post_codex_database_contract_audit.md`,
`post_codex_remediation_functional_gate.md`, `schema_rationalization_N34_legacy_mirror_removal_audit.md`)
which the task already contained — they are inputs, not outputs, of this task.

## 9. Confirmation of scope

- **Queue B:** NOT implemented. **Queue C:** NOT implemented. **N34:** NOT implemented.
- No new migrations; N01–N33 immutable; no destructive schema changes; no CASCADE.
- No owner decisions made; no Manual/SOT rules changed; no functional redesign; no module-boundary
  changes; no unrelated cleanup beyond the audit-listed dead code; nothing of Queue B/C was pulled in.

— End of report.