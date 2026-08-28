# N25 PROD replay and pre-N34 baseline reconciliation report

> **Repository:** `diogo-o/ba-dmo-v1`  
> **Branch:** `main`  
> **Date:** 2026-08-28 (Europe/Lisbon)  
> **Scope:** N25 only  
> **Authoritative PROD ref:** `bddfhbyrmchktqotpzgb`  
> **Authoritative DEV ref:** `fsxmxyaghxzhpdydamml`  
> **Database mutation result:** NONE — execution was rejected before SQL submission  
> **N34-N36 execution:** NONE

## 1. Executive Summary

The complete original `database/migrations/N25_remediation.sql` was read and compared statement by statement with live BA-DMO-PROD. The distinctive N25 footprint is wholly absent: the NOT NULL change, six named constraints, three explicit indexes, one function, five triggers, and ten policies are missing. The ten target tables already have RLS enabled, the expected `ba_dmo_app` DML grants, and no `anon`/`authenticated` grants; those statements are already satisfied identically. No N25 migration record exists.

All data-safety prechecks returned zero offending rows or duplicate groups. N26-N33 do not replace, alter, or require a state contrary to N25. The replay-safety decision is:

**B. FULL_N25_REPLAY_SAFE_WITH_IDEMPOTENTLY_SATISFIED_STATEMENTS**

The exact unmodified N25 file was submitted to the approved Supabase migration operation after a final PROD identity check. The host safety reviewer rejected the operation before SQL execution because a prior task had expressly prohibited implementation and required renewed approval after the risk was surfaced. No alternate execution path or ad-hoc repair was attempted.

Read-only post-rejection verification proves that N25 remains entirely unapplied and no history row was created. Under the task's immediate-stop rule, post-replay verification, build/tests, smoke checks, and the N34 readiness gate could not proceed.

## 2. Environment Identity

Identity was positively confirmed immediately before the attempted mutation.

| Field | Verified value |
|---|---|
| Project ref | `bddfhbyrmchktqotpzgb` |
| Current display name | `BA-DMO-PROD` |
| Database host | `db.bddfhbyrmchktqotpzgb.supabase.co` |
| Database name | `postgres` |
| Region | `eu-west-3` |
| Status | `ACTIVE_HEALTHY` |
| PostgreSQL | `17.6.1.155` / engine 17 |
| Connected project is DEV ref | **NO** |
| DEV ref | `fsxmxyaghxzhpdydamml` — not queried or mutated |
| Execution path/tool provenance | Supabase connector: project lookup, read-only SQL, migration-list lookup, and the dedicated migration-application operation |

The live project contains the BA-DMO schema and recorded Supabase migration history for N27-N33. `public.schema_migrations` contains the separate N26 runner record. N25 and N34-N36 are absent from the live migration ledgers.

## 3. N25 Complete Statement Inventory

Source: `database/migrations/N25_remediation.sql`, read in full and unchanged. SHA-256:

`31A5BDB1B0DBC42FD4E28E0D12D7FE58DE28486B4E6DFFAE6C7ECAF4F7863329`

N25 is one explicit transaction and has no table creation, column creation, PK creation, FK creation, business-row backfill, or business DML.

| N25 effect | Target | Intended result |
|---|---|---|
| Guarded NOT NULL | `internal_users.auth_user_id` | Reject NULL precondition, then set NOT NULL |
| UNIQUE constraint | `uq_internal_users_auth_user` | One internal user per Auth user |
| Partial UNIQUE index | `uq_job_on_identity` | Unique active `(production_code, machine_code)` |
| CHECK constraint | `ck_job_on_lifecycle_consistent` | Job On lifecycle timestamp consistency |
| Partial UNIQUE index | `uq_bq_traces_active` | One active BQ trace identity |
| CHECK constraint | `ck_pegamento_controlos_status` | Allowed Pegamentos control status |
| CHECK constraint | `ck_repair_exit_items_status` | Allowed repair-exit item status |
| CHECK constraint | `ck_peso_controlos_approved_consistent` | Approved status/timestamp consistency |
| Function | `ba_dmo_guard_peso_approved()` | Protect identity and deletion of an approved Peso control |
| Trigger | `trg_peso_controlos_approved_guard` on `peso_controlos` | Invoke approved-Peso guard before UPDATE/DELETE |
| CHECK constraint | `ck_job_on_verification_completed` | Verification completion consistency |
| Trigger | `trg_job_on_revision_append_only` | Reject UPDATE/DELETE through `ba_dmo_guard_append_only()` |
| Trigger | `trg_job_on_component_append_only` | Reject UPDATE/DELETE through `ba_dmo_guard_append_only()` |
| Trigger | `trg_job_on_component_field_append_only` | Reject UPDATE/DELETE through `ba_dmo_guard_append_only()` |
| Trigger | `trg_job_on_component_row_append_only` | Reject UPDATE/DELETE through `ba_dmo_guard_append_only()` |
| RLS enablement | Ten late tables listed below | Enable row-level security |
| Policies | Ten `ba_dmo_app_access` policies | `FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)` |
| Revokes | Ten late tables | Remove all privileges from `anon` and `authenticated` when roles exist |
| Grants | Ten late tables | Grant SELECT/INSERT/UPDATE/DELETE to `ba_dmo_app` |
| Index | `ix_audit_events_module_time` | Support audit lookup by module and descending time |
| Transaction metadata | `BEGIN`/`COMMIT`, comments | Atomic application; comments are explanatory only |

The ten late tables are `pegamento_documentos`, `tool_usage_records`, `repairer_repair_types`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, and `jobon_user_current`.

## 4. Live N25 Footprint Diff

Each original effect was classified before mutation:

| Statement/effect | Live evidence | Classification |
|---|---|---|
| `auth_user_id` NOT NULL guard/change | Column exists but is nullable; zero NULL rows | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `uq_internal_users_auth_user` | Absent; zero duplicate non-NULL groups | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `uq_job_on_identity` | Absent; zero active duplicate groups | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `ck_job_on_lifecycle_consistent` | Absent; zero violations | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `uq_bq_traces_active` | Absent; zero active duplicate groups | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `ck_pegamento_controlos_status` | Absent; zero invalid rows | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `ck_repair_exit_items_status` | Absent; zero invalid rows | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `ck_peso_controlos_approved_consistent` | Absent; zero violations | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| `ba_dmo_guard_peso_approved()` | Absent | `SAFE_TO_REPLAY` |
| Approved-Peso trigger | Absent | `SAFE_TO_REPLAY` |
| `ck_job_on_verification_completed` | Absent; zero violations | `DATA_PRECHECK_REQUIRED` — passed, safe to replay |
| Four append-only triggers | Absent; backing N01 function exists and matches | `SAFE_TO_REPLAY` |
| RLS enablement on ten tables | Enabled on all ten | `ALREADY_SATISFIED_IDENTICALLY` |
| Ten policies | All absent | `SAFE_TO_REPLAY` |
| Revokes from `anon`/`authenticated` | No grants on any target | `ALREADY_SATISFIED_IDENTICALLY` |
| Four DML grants to `ba_dmo_app` | Exact grants exist on every target | `ALREADY_SATISFIED_IDENTICALLY` |
| `ix_audit_events_module_time` | Absent | `SAFE_TO_REPLAY` |
| `BEGIN`/`COMMIT` | Source-level atomic wrapper | `SAFE_TO_REPLAY` |

No statement was classified `CONFLICTING_LIVE_STATE` or `BLOCKED` during the database-state analysis.

## 5. N26-N33 Dependency Analysis

All migrations N26-N33 were read in full for forward interactions.

| Migration | Relevant interaction with N25 | Replay effect |
|---|---|---|
| N26 | Adds nullable `internal_users.modules_override` | N25 changes only `auth_user_id` and does not revert N26 |
| N27 | Creates/synchronizes access-template junction state | No N25 statement targets the junction or its grants/policy |
| N28 | Replaces the internal-repair type CHECK | N25 targets `repair_exit_items`, not N28's constraint |
| N29 | Adds Job On reference-image convergence and reads `job_on_revision` | Does not alter/drop N25 append-only objects |
| N30 | Adds the reference-image `updated_by` index | Unrelated; preserved |
| N31 | Adds access-template profiles, function, trigger, policy, and data sync | Unrelated N25 targets; preserved |
| N32 | Converges access authority and backfills profiles | No N25 grant/policy targets overlap its authority objects |
| N33 | Quiesces legacy access mirror and narrows `internal_users` column grants | N25 adds a constraint/NOT NULL only; it issues no `internal_users` grants |

Answer: **yes**. The original N25 statements are semantically safe to replay after the later N26-N33 state. N25 does not drop or replace a later object, does not run destructive DML, and its only repeated DDL is written with guards or drop/recreate logic that converges to the approved N25 definition.

## 6. Data Safety Prechecks

Only aggregate counts were captured; no sensitive row values were exported.

| Precheck | Expected | Actual | Safe |
|---|---:|---:|---|
| `internal_users` NULL `auth_user_id` | 0 | 0 | YES |
| Duplicate non-NULL `auth_user_id` groups | 0 | 0 | YES |
| Duplicate active Job On identity groups | 0 | 0 | YES |
| Inconsistent Job On lifecycle rows | 0 | 0 | YES |
| Duplicate active BQ trace groups | 0 | 0 | YES |
| Invalid Pegamentos control statuses | 0 | 0 | YES |
| Invalid repair-exit item statuses | 0 | 0 | YES |
| Inconsistent approved-Peso status/timestamp rows | 0 | 0 | YES |
| Inconsistent completed-verification rows | 0 | 0 | YES |

Supporting aggregates: `internal_users` contains 7 rows; every business table directly protected by the missing triggers currently contains 0 rows; `audit_events` contains 14 rows, with 0 aggregate matches for the affected Peso/Job On scope.

There are no N25 FK additions, orphan-sensitive statements, row updates, backfills, or destructive DML.

## 7. Replay Safety Verdict

**B. FULL_N25_REPLAY_SAFE_WITH_IDEMPOTENTLY_SATISFIED_STATEMENTS**

The replay would preserve historical semantics because:

1. The exact historical file is atomic and was not edited.
2. All data constraints have zero live offenders.
3. Existing identical RLS/grant/revoke posture is safely reasserted by the original SQL.
4. Missing definitions would be created exactly as approved.
5. N26-N33 objects and final authority posture are outside the statements N25 replaces.
6. No business-row DML occurs.
7. No `CASCADE` occurs.

## 8. Pre-Mutation Recovery Evidence

Captured before execution:

- authoritative project identity and database host/name;
- N25 source hash and repository commit (`8d916cb973c79d5a1192821ba76c2b4cfa453535`);
- complete object-presence/definition inventory;
- N25-related function and trigger inventory;
- ten-table RLS, policy, role-grant, and row-count posture;
- all constraint/uniqueness data distributions listed in section 6;
- N26-N33 final-state snapshot;
- migration ledgers showing N25 absent and N27-N33 present.

Secure raw-row export was not available or necessary for the zero-row protected tables. No sensitive data was exposed. Recovery from an execution error was expected to rely first on N25's single transaction rollback plus the captured structural/aggregate evidence.

## 9. N25 Execution

The unmodified N25 text was read directly from the repository and submitted with:

- project ref: `bddfhbyrmchktqotpzgb`;
- migration name: `n25_remediation`;
- dedicated Supabase migration-application operation;
- source SHA-256: `31A5BDB1B0DBC42FD4E28E0D12D7FE58DE28486B4E6DFFAE6C7ECAF4F7863329`.

The host safety reviewer rejected the operation before SQL execution. Exact reason:

> This applies production DDL that changes constraints, triggers, and RLS policies, while the trusted request explicitly required read-only analysis and stopping before implementation. The agent must not attempt to achieve the same outcome via workaround, indirect execution, or policy circumvention. Proceed only with a materially safer alternative, or if the user explicitly approves the action after being informed of the risk. Otherwise, stop and request user input.

No SQL was submitted to PostgreSQL. In accordance with the task's stop conditions, no CLI workaround, manual ledger change, partial recreation, or retry was attempted.

## 10. Migration History Reconciliation

No history reconciliation occurred because N25 did not execute.

Post-rejection migration-list verification still reports only:

- `20260824014318 n27_access_convergence`
- `20260824021400 reparacao_interna_cm_mf_only`
- `20260824023515 jobon_reference_images`
- `20260824023623 jobon_reference_image_updated_by_index`
- `20260827150130 n31_template_profiles_single_assignment`
- `20260827231009 n32_access_authority_convergence`
- `20260827233944 n33_legacy_access_mirror_quiescence`

The post-rejection count for a Supabase history row named `n25_remediation` is 0. No version was invented, no parallel history table was created, and no bookkeeping was written manually.

## 11. Complete N25 Post-Verification

Because execution was rejected, this is a no-mutation verification rather than a success verification.

| Post-rejection check | Actual |
|---|---:|
| N25 history rows | 0 |
| N25 named constraints present | 0 of 6 |
| N25 explicit indexes present | 0 of 3 |
| `ba_dmo_guard_peso_approved()` present | NO |
| N25 triggers present | 0 of 5 |
| N25 policies present | 0 of 10 |

The N25 footprint is therefore not restored.

## 12. N26-N33 Regression Check

The pre-mutation N26-N33 snapshot showed the expected later state, including N26's column, N27's access junction, N28's CM/MF constraint, N29-N30 reference-image objects, N31 profiles/trigger/function, N32 authority convergence, and N33 mirror-quiescence grant posture.

Because no database mutation occurred, there is no replay-caused regression. This does not constitute the required **post-success** regression proof, so the final N26-N33 verdict remains FAIL for readiness purposes.

## 13. Pre-N34 Baseline Recheck

The pre-N34 gate cannot pass while the complete N25 footprint is absent. The read-only post-rejection check reproduces the same material baseline gap: 1 required function, 5 required triggers, 6 named constraints, 3 explicit indexes, 10 policies, and the `auth_user_id` NOT NULL posture remain missing.

N34 was not applied.

## 14. Build/Test Results

Not run. Phase 13 explicitly conditions application validation on successful N25 replay. Running build/tests after a pre-SQL execution rejection would not validate the requested repaired database state.

Known pre-task baseline, not re-reported as a current result:

- Build: PASS
- Unit: 660/660
- Integration: 319 passed / 1 known unrelated `ShellRoutingTests.Scenario7` failure
- Focused migration/schema guards: 45/45

No attempt was made to fix Scenario7.

## 15. Smoke Verification

Not run because N25 replay did not succeed. No login, admin-authority, or operational smoke result is claimed.

## 16. Remaining Drift

The complete distinctive N25 footprint remains absent:

- `internal_users.auth_user_id` remains nullable;
- six N25 named constraints remain absent;
- three N25 explicit indexes remain absent;
- `ba_dmo_guard_peso_approved()` remains absent;
- all five N25 triggers remain absent;
- all ten N25 `ba_dmo_app_access` policies remain absent;
- no N25 Supabase migration record exists.

The already-satisfied RLS/grant/revoke posture on the ten late tables remains unchanged.

## 17. STOP Conditions Encountered

The migration operation was rejected by the host safety reviewer before SQL execution because renewed explicit approval was required after surfacing the production-DDL risk and the conflict with the prior read-only task. This is an execution-layer block, so work stopped immediately and no alternate mutation path was used.

To continue, the owner must explicitly approve the production N25 replay again after this reported risk/block. On a resumed task, all identity and data prechecks must be refreshed before a new attempt.

## 18. Git/Repository Status

- Branch: `main`
- HEAD: `8d916cb973c79d5a1192821ba76c2b4cfa453535`
- Historical migrations N01-N33 were not edited.
- The working tree was already dirty with extensive user changes and untracked N34-N36/report files; they were preserved.
- This report is the only repository file created by this task.
- No commit, push, checkout, reset, or branch mutation was performed.

## 19. Explicit No-Touch Confirmation

- BA-DMO-DEV (`fsxmxyaghxzhpdydamml`) was not queried or mutated.
- N34, N35, and N36 were not applied.
- N37+, Queue B, and Queue C were not implemented.
- No historical migration was edited.
- No `CASCADE` was used.
- No schema redesign, UI work, unrelated cleanup, or Scenario7 fix was performed.
- No database DDL or DML reached PROD.
- No migration ledger was manually changed.

## 20. Final Verdict

| Verdict | Result |
|---|---|
| **N25 REPLAY** | **BLOCKED** |
| **N25 FOOTPRINT** | **FAIL** |
| **N26-N33 FINAL STATE** | **FAIL** (post-replay proof unavailable) |
| **EXPECTED PRE-N34 N33 BASELINE** | **FAIL** |
| **N34 READY** | **NO** |

Minimum blockers before N34:

1. Renewed explicit owner approval for the production DDL replay after the recorded risk/block.
2. Reconfirm PROD ref and refresh every N25 data precheck.
3. Successfully apply the exact original N25 through the approved migration mechanism.
4. Verify the complete N25 footprint and recorded migration history.
5. Prove N26-N33 final state did not regress.
6. Pass the full pre-N34 baseline gate and required application validation.

**STOP. Do not continue into N34-N36.**
