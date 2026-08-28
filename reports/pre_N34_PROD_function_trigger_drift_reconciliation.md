# Pre-N34 PROD function/trigger drift reconciliation

> **Type:** READ-ONLY LIVE ANALYSIS / REPAIR DESIGN ONLY
> **Repo:** `diogo-o/ba-dmo-v1`
> **Branch:** `main`
> **Date:** 2026-08-28 (Europe/Lisbon)
> **Authoritative PROD ref:** `bddfhbyrmchktqotpzgb`
> **Authoritative DEV ref:** `fsxmxyaghxzhpdydamml`
> **Database mutations:** NONE
> **N34-N36 execution:** NONE

## 1. Executive verdict

The six missing function/trigger objects are not obsolete and were not superseded. Each is classified:

`REQUIRED_CURRENT_BASELINE`

However, the live evidence disproves the narrower hypothesis that only six isolated objects drifted. The complete distinctive footprint of `N25_remediation.sql` is absent from PROD: its constraints, explicit indexes, function, five triggers, and ten policy creations are all missing. The live migration ledgers also contain no N25 record.

The strongest evidence-backed conclusion is therefore:

> N25 was omitted from the live deployment path; the six missing objects were not individually removed later.

This is not sanctioned catalog drift. It is an incomplete pre-N34 baseline. No repair was implemented.

**N34 READY: NO.**

## 2. Target identity and safety

The live connector was scoped only to project ref `bddfhbyrmchktqotpzgb`.

| Field | Verified value |
|---|---|
| Project ref | `bddfhbyrmchktqotpzgb` |
| Current display name | `BA-DMO-PROD` |
| Host | `db.bddfhbyrmchktqotpzgb.supabase.co` |
| Status | `ACTIVE_HEALTHY` |
| PostgreSQL | 17.6 |

The DEV ref `fsxmxyaghxzhpdydamml` was not queried or mutated during this reconciliation.

## 3. Authorities compared

The following sources agree on the intended pre-N34 state:

1. `database/migrations/N01_identity.sql` creates `ba_dmo_guard_append_only()`.
2. `database/migrations/N25_remediation.sql` creates `ba_dmo_guard_peso_approved()` and all five missing triggers.
3. `database/consolidated_clean_install.sql` reproduces the same N25 function and trigger statements.
4. `reports/N34_N36_DEV_live_verification_package.md` requires 3 functions and 19 triggers before N34.
5. `reports/post_codex_database_rationalization_plan.md` identifies the same 3-function/19-trigger set as the N01-N33 chain authority.
6. Current integration guards explicitly test approved-Peso immutability and revision-family append-only behavior.

N26-N33 contain no `DROP`, replacement, rename, or semantic modification of any of the six missing objects. N29 reads `job_on_revision` for reference-image convergence but does not alter its append-only trigger. N32 only documents the dormant `job_on_revision.image_asset_id` column and does not touch these guards.

## 4. Live migration provenance and root-cause evidence

### 4.1 Live provenance

- `public.schema_migrations` contains only `N26_user_modules_override.sql`.
- Supabase CLI history contains N27, N28, N29, N30, N31, N32, and N33.
- Neither ledger contains N25.
- N34-N36 remain absent.

### 4.2 Remaining N25 footprint

Read-only catalog checks found all of the following N25 objects absent:

- `uq_internal_users_auth_user`
- `uq_job_on_identity`
- `ck_job_on_lifecycle_consistent`
- `uq_bq_traces_active`
- `ck_pegamento_controlos_status`
- `ck_repair_exit_items_status`
- `ck_peso_controlos_approved_consistent`
- `ba_dmo_guard_peso_approved`
- `trg_peso_controlos_approved_guard`
- `ck_job_on_verification_completed`
- the four revision-family append-only triggers
- `ix_audit_events_module_time`
- the ten `ba_dmo_app_access` policies issued by N25 §2

`internal_users.auth_user_id` also remains nullable, which is the pre-N25 column posture.

The ten late tables do have RLS enabled and four `ba_dmo_app` DML grants, but no policy. Those parts can originate from other baseline/default-privilege paths and do not prove N25 execution. Their missing N25 policies reinforce the omitted-N25 conclusion.

### 4.3 Root-cause classification

The evidence is sufficient to reject these explanations:

- **SUPERSEDED:** no later authority replaces the function or triggers.
- **INTENTIONALLY_REMOVED:** no approved migration or report authorizes removal.
- **EXPECTED_CATALOG_DRIFT:** the verification package explicitly treats absence as a STOP.
- **isolated LIVE_SCHEMA_CORRUPTION:** possible in theory, but inconsistent with every distinctive N25 object being absent and N25 having no migration record.

The most likely cause is a skipped N25 deployment between the baseline/N24-era installation and the separately applied N26-N33 migrations. Exact operator history outside the repository is unavailable, so the report does not claim who or what skipped it.

## 5. Missing function

### `ba_dmo_guard_peso_approved()`

| Question | Finding |
|---|---|
| Live state | Missing |
| Creating migration | N25 §1.7b, lines 137-159 |
| Later migration changes | None in N26-N33 |
| Consolidated baseline | Present with the same body |
| Backing trigger | `trg_peso_controlos_approved_guard` |
| Expected purpose | For an already-approved `peso_controlos` row, reject DELETE and reject changes to mold, neckring, production, line, lot, or control date; allow non-identity updates |
| Classification | `REQUIRED_CURRENT_BASELINE` |

The function is a database backstop for approved-control identity and deletion. It is not the broader N40 readings guard: N25 deliberately leaves non-identity fields and `peso_leituras` editable. The rationalization plan keeps this function and separately schedules stronger readings protection for N40.

No current C# code calls the function by name; its application dependency is semantic and trigger-driven.

## 6. Missing triggers

| Trigger | Table | Backing function | Created by | Later changes | Current application dependency | Expected behavior | Classification |
|---|---|---|---|---|---|---|---|
| `trg_peso_controlos_approved_guard` | `peso_controlos` | `ba_dmo_guard_peso_approved()` | N25 §1.7b | none | `DapperPesoRepository` updates and deletes controls; `PesoService` normally blocks approved deletes and sends unchanged identity values | BEFORE UPDATE/DELETE; protect approved identity and prevent approved deletion | `REQUIRED_CURRENT_BASELINE` |
| `trg_job_on_revision_append_only` | `job_on_revision` | `ba_dmo_guard_append_only()` from N01 | N25 §1.9 | none; N29 only reads revisions | Dapper writes revisions with INSERT; many Peso/Pegamentos/Controlo readers pin `job_on_revision_id` as historical authority | reject every UPDATE/DELETE; permit INSERT | `REQUIRED_CURRENT_BASELINE` |
| `trg_job_on_component_append_only` | `job_on_component` | `ba_dmo_guard_append_only()` from N01 | N25 §1.9 | none | revision graphs are inserted and later read as immutable snapshots | reject every UPDATE/DELETE; permit INSERT | `REQUIRED_CURRENT_BASELINE` |
| `trg_job_on_component_field_append_only` | `job_on_component_field` | `ba_dmo_guard_append_only()` from N01 | N25 §1.9 | none | field rows are inserted with the revision graph and reused by historical production-context readers | reject every UPDATE/DELETE; permit INSERT | `REQUIRED_CURRENT_BASELINE` |
| `trg_job_on_component_row_append_only` | `job_on_component_row` | `ba_dmo_guard_append_only()` from N01 | N25 §1.9 | none | row snapshots are inserted with the revision graph and reused by historical readers | reject every UPDATE/DELETE; permit INSERT | `REQUIRED_CURRENT_BASELINE` |

The shared `ba_dmo_guard_append_only()` function is present live and matches N01. Only its four N25 trigger bindings are absent.

## 7. Current code dependency and compensation

### 7.1 Peso guard

Current normal application paths partially compensate:

- `PesoService.DeleteControlAsync` permits deletion only for `rascunho` or `nao_aprovado` and checks author/responsible authorization.
- `PesoService.SaveControlAsync` runs `PesoValidator.ValidateControlEditable`.
- `DapperPesoRepository.UpdateControlAsync` sends identity columns back with their loaded values rather than intentionally changing them.

This is not equivalent to the DB guard:

- `ba_dmo_app` has SELECT/INSERT/UPDATE/DELETE grants and a permissive `FOR ALL USING (true) WITH CHECK (true)` policy on `peso_controlos`.
- A direct SQL write, future repository defect, alternative process, or compromised application path can delete an approved row or change its identity.
- `ValidateControlEditable` allows an approved control through when a change reason is supplied; it relies on higher-layer behavior and does not enforce the database identity invariant.

### 7.2 Revision-family guards

Current repository code contains INSERT paths for all four tables and no UPDATE/DELETE SQL for them. This is useful application-level discipline, but it is not enforcement:

- each table grants all four DML privileges to `ba_dmo_app`;
- each table has a permissive technical policy;
- without the triggers, direct or future application SQL can rewrite or delete pinned snapshots.

Peso, Pegamentos, Controlo, Reparação Interna, and Job On readers treat `job_on_revision_id` and its component graph as immutable historical context. The DB triggers are therefore an active integrity dependency even though the current Dapper write path is insert-only.

## 8. Impact assessment

### 8.1 Per-object impact

| Object/group | Intended invariant bypassable? | Audit/history risk | Append-only bypass | Lifecycle/state risk | Code compensation | Live data affected? |
|---|---|---|---|---|---|---|
| Peso function + trigger | Yes: approved identity can change and approved row can be deleted | Approved comparison base and downstream attribution could disappear or change | Not a general append-only rule | Approved control identity/lifecycle anchor can be broken | Partial service checks only | No current rows; no evidence of impact |
| Four revision triggers | Yes: any revision-graph row can be updated/deleted | Historical snapshots can be silently rewritten or removed | Yes, completely | Pinned revision context for downstream modules can become false or incomplete | Current Dapper is insert-only | No current rows; no evidence of impact |

### 8.2 Live read-only data results

All target and related live tables currently contain zero rows:

- `peso_controlos`: 0
- `job_on`: 0
- `job_on_revision`: 0
- `job_on_component`: 0
- `job_on_component_field`: 0
- `job_on_component_row`: 0
- `job_on_audit_event`: 0

Aggregated `audit_events` counts for Peso- or Job On-related entity/module identifiers are also zero.

Therefore:

- no current row can exhibit a forbidden approved-Peso identity mutation;
- no current revision graph can exhibit silent update/delete corruption;
- no current internal audit evidence indicates previously created target records.

This does not cryptographically prove that rows never existed and were deleted outside the application. Proving historical absence beyond the current catalog would require external database backups, PITR/WAL evidence, or retained platform logs; those are outside this repository and this read-only session.

### 8.3 N25 replay precondition checks

Read-only checks currently return zero offenders for:

- null or duplicate `internal_users.auth_user_id`;
- duplicate active Job On identity;
- inconsistent Job On lifecycle timestamps;
- duplicate active BQ traces;
- invalid Pegamentos or repair-item status;
- inconsistent approved-Peso timestamp/status;
- inconsistent completed verification timestamp/status.

The live data posture is therefore compatible with an N25 replay, but execution remains unauthorized in this task.

## 9. Read-only checks required if data appears before repair

Immediately before any future repair, rerun at minimum:

1. Target-table row counts and the N25 preconditions listed in §8.3.
2. Approved Peso rows whose current identity differs from their persisted creation/audit snapshots, where such snapshots exist.
3. Approved Peso deletes inferred from audit/PITR evidence without a surviving `peso_controlos` row.
4. Revision graphs with missing parents/children or a `job_on.current_revision_id` whose snapshot graph is incomplete.
5. Revision-family audit events whose before/after snapshots indicate UPDATE/DELETE activity.
6. Supabase migration history and all N25 object-presence checks immediately before mutation.

Current hard FKs catch orphans that survive, but cannot detect a successfully cascaded deletion or a same-row rewrite after the fact. External backup/log evidence is required for those historical cases.

## 10. Minimum repair design

### 10.1 Recommendation

**Recommended option: B — operational replay of the already-approved, idempotent N25 statement set.**

Reasoning:

- the evidence shows N25 as a whole was skipped;
- replaying only the six function/trigger statements would leave the rest of the N25 baseline absent;
- a narrow six-object repair would make the 3-functions/19-triggers check pass while falsely implying the complete N33 baseline is healthy;
- N25 is explicitly written as one idempotent, whole-file transaction and is preserved in the chain and consolidated baseline;
- current read-only data checks satisfy its documented guards.

### 10.2 Required execution shape for a future authorized task

1. Reconfirm PROD ref `bddfhbyrmchktqotpzgb`; refuse `fsxmxyaghxzhpdydamml`.
2. Re-run all N25 precondition/data checks.
3. Apply the exact, unmodified `database/migrations/N25_remediation.sql` statement set as one transaction through the Supabase CLI provenance path that owns N27-N33.
4. Record the operational replay with a new, explicit Supabase migration version/name such as `pre_n34_n25_baseline_replay`; do not forge an old timestamp and do not insert bookkeeping manually without owner approval.
5. Record the repository N25 SHA-256 source hash used for replay: `31A5BDB1B0DBC42FD4E28E0D12D7FE58DE28486B4E6DFFAE6C7ECAF4F7863329`.
6. Verify the complete N25 footprint, not only the six objects.
7. Run the N25 PostgreSQL integration guards, then rerun the full N34 pre-check package from the beginning.

No SQL was executed to implement this design.

### 10.3 Rejected alternatives

- **A — dedicated narrow pre-N34 six-object migration:** rejected as insufficient. It would conceal the broader omitted-N25 baseline and leave required constraints, indexes, and policies absent.
- **C — no repair:** rejected. All six objects remain current authorities and are explicitly tested/required by the chain and consolidated baseline.
- **Edit N01-N33 or fold into N34:** prohibited and unnecessary.
- **CASCADE or recreated compatibility objects:** prohibited and unnecessary.

If the owner does not authorize the full N25 replay scope, the correct result is to remain blocked rather than implement a selective function/trigger patch.

## 11. N34 readiness

**N34 READY: NO**

Minimum exact blockers:

1. N25 has no live migration record.
2. `ba_dmo_guard_peso_approved()` is absent.
3. `trg_peso_controlos_approved_guard` is absent.
4. The four revision-family append-only triggers are absent.
5. The remaining distinctive N25 constraints, indexes, and ten policies are also absent; a six-object-only repair cannot establish the approved N33 baseline.
6. The complete N25 replay must be separately authorized, applied through the Supabase provenance path, and live-verified.
7. After repair, N34 pre-checks and recovery evidence must be rerun from the beginning.

## 12. Scope and no-change attestation

- N34-N36 were not applied.
- No migration was implemented or edited.
- No database DDL or DML was executed.
- PROD access was read-only.
- DEV was not queried or mutated.
- No unrelated remediation was performed.
- Existing working-tree changes were preserved.
- The only file created by this task is this report.

**STOP after reconciliation.**
