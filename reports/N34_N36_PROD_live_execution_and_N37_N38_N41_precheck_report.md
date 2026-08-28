# N34-N36 PROD live execution and N37/N38/N41 pre-check report

> **Repo:** `diogo-o/ba-dmo-v1`
> **Branch:** `main`
> **Execution date:** 2026-08-28 (Europe/Lisbon)
> **Authoritative PROD ref:** `bddfhbyrmchktqotpzgb`
> **Authoritative DEV ref:** `fsxmxyaghxzhpdydamml`
> **Outcome:** **STOP-3 before any migration mutation**

## 1. Environment identity proof

Project refs, not display names, were treated as authoritative after the owner's correction.

| Field | Verified PROD value |
|---|---|
| Project ref / id | `bddfhbyrmchktqotpzgb` |
| Current display name | `BA-DMO-PROD` |
| Database host | `db.bddfhbyrmchktqotpzgb.supabase.co` |
| Region | `eu-west-3` |
| Project status | `ACTIVE_HEALTHY` |
| Management-reported database version | `17.6.1.155` |
| SQL `current_database()` | `postgres` |
| SQL `current_user` | `postgres` |
| SQL `server_version` | `17.6` |
| SQL server port | `5432` |

Connection provenance was the installed Supabase connector scoped explicitly to project ref `bddfhbyrmchktqotpzgb`.

The corrected identity gate passed: the connected ref is authoritative PROD and its current display name is `BA-DMO-PROD`.

### Ref-correction note

Before the owner corrected the ref mapping, the earlier read-only attempt addressed `fsxmxyaghxzhpdydamml` because the original task identified that ref as PROD. It performed only project identity, migration-list, table-list, and SQL identity/catalog reads; it made **no mutation**. After the correction, no operation in this run addressed `fsxmxyaghxzhpdydamml`.

## 2. Migration provenance and N33 starting state

Both provenance mechanisms exist on authoritative PROD:

- `public.schema_migrations`: one repository-runner row, `N26_user_modules_override.sql`, applied 2026-08-21.
- `supabase_migrations.schema_migrations`: Supabase-managed history present.
- Supabase migration tail:
  - `20260824014318 / n27_access_convergence`
  - `20260824021400 / reparacao_interna_cm_mf_only`
  - `20260824023515 / jobon_reference_images`
  - `20260824023623 / jobon_reference_image_updated_by_index`
  - `20260827150130 / n31_template_profiles_single_assignment`
  - `20260827231009 / n32_access_authority_convergence`
  - `20260827233944 / n33_legacy_access_mirror_quiescence`
- N34, N35, and N36 are absent from the live Supabase history.
- Live public table posture before N34: 61 application tables plus `public.schema_migrations`.

The migration tail and N34 removal targets show that the database reached the N33 access-convergence phase. However, the complete approved N33 catalog invariant did not pass, as documented below.

## 3. N34 pre-checks

### 3.1 Removal targets and dependencies

| Check | Live result | Verdict |
|---|---|---|
| `internal_user_access_templates` | exists | PASS |
| `internal_users.profile_title` | exists at attnum 5; nullable; not dropped | PASS |
| `ck_internal_users_functional_profile` | exists with approved three-value CHECK | PASS |
| Incoming FKs to junction | 0 | PASS |
| Dependent views/matviews | 0 | PASS |
| Dependent functions by catalog | 0 | PASS |
| Function-body text references | 0 | PASS |
| External normal junction dependencies | 0 | PASS |
| `profile_title` dependencies | only the table-owned CHECK | PASS |
| Dangling junction rows | 0 | PASS |

### 3.2 Privileges and data posture

| Check | Live result | Verdict |
|---|---|---|
| `ba_dmo_app` role | exists; `NOLOGIN`; connection limit `-1` | PASS |
| Junction grants to `ba_dmo_app` | 0 | PASS |
| `profile_title` column grants | 0 | PASS |
| Canonical `internal_users` grants | exact 24-row matrix: SELECT/INSERT/UPDATE across 8 approved columns | PASS |
| Junction rows | 7 | informational |
| `internal_users` rows | 7 | informational |
| Non-null `profile_title` | 7 | informational |
| Null `profile_title` | 0 | informational |

Raw user/profile values and actor-template identifiers were not exported. The connector rejected that sensitive data egress. The safe replacement retained structural metadata and aggregate posture only.

### 3.3 Junction-owned surface

The junction has exactly the approved surface:

- `internal_user_access_templates_pkey`
- `ix_internal_user_access_templates_template`
- `ux_internal_user_access_templates_actor`
- `internal_user_access_templates_app_access`
- zero non-internal triggers

### 3.4 N33 function/trigger invariant — FAIL

The package requires three public functions and nineteen non-internal triggers before/after N34. Live PROD contains only:

- Functions: **2**, not 3.
  - present: `ba_dmo_guard_append_only`
  - present: `ba_dmo_ensure_access_template_profile`
  - missing: `ba_dmo_guard_peso_approved`
- Non-internal triggers: **14**, not 19.

The five expected missing triggers are:

- `trg_peso_controlos_approved_guard`
- `trg_job_on_revision_append_only`
- `trg_job_on_component_append_only`
- `trg_job_on_component_field_append_only`
- `trg_job_on_component_row_append_only`

This is **STOP-3 — pre-check catalog does not match the expected N33 state**. N34-N36 do not create these missing N25 objects, so applying them would preserve an already-incomplete safety baseline and violate the execution contract.

### 3.5 Index-count note

`pg_indexes` reports 158 total public indexes, including PK/UNIQUE backing indexes. The planning report's figure of 81 explicitly counts only explicit `CREATE INDEX` statements and states that inline PK/UNIQUE indexes are additional. The verification package's headline query counts all `pg_indexes` rows while labelling 79 as expected; this mixes two count conventions. This ambiguity was recorded but is not needed to trigger the stop because the function/trigger deficit independently fails P4.10.

## 4. N34 recovery evidence

Captured safely:

- legacy table existence and owned-object structure;
- row count: 7;
- zero dangling references;
- profile column shape and aggregate null posture;
- CHECK definition;
- role/privilege posture;
- dependency counts.

A recoverable raw data export was not produced because the connector blocked sensitive live-row egress. No fake rollback SQL, backup table, mirror schema, or duplicate persistence was created.

## 5. N34 execution

**NOT EXECUTED.** STOP-3 occurred during the N34 pre-check gate.

## 6. N34 post-checks

**NOT RUN.** N34 was not executed.

## 7. N35 pre-checks

**NOT RUN after STOP-3.** The package requires an immediate halt and forbids continuing to the next migration/pre-check stage after an N34 gate failure.

The pre-N34 index snapshot did incidentally confirm that `ix_bq_movements_noted_repairer` is absent and `ix_pegamento_documentos_controlo` is present, but this is not represented as a complete N35 pre-check PASS.

## 8. N35 execution

**NOT EXECUTED.**

## 9. N35 post-checks

**NOT RUN.**

## 10. N36 pre-checks

**NOT RUN after STOP-3.** No policy semantic anchor was captured for execution because the N34 checkpoint failed.

## 11. N36 execution

**NOT EXECUTED.**

## 12. N36 semantic parity

**NOT RUN.**

## 13. Final live catalog inventory

No post-N36 inventory exists because no migration was applied. The pre-N34 inventory relevant to the stop is:

- 61 application tables;
- 158 total `pg_indexes` rows (different counting convention from the 81 explicit-index design number);
- 2 public functions instead of 3;
- 14 non-internal triggers instead of 19;
- N34-N36 absent from Supabase migration history.

The approved N01-to-N36 state cannot be certified.

## 14. Build/test results

**NOT RUN after STOP-3.** No earlier result is represented as a result of this execution.

The documentary baseline remains: build PASS; unit 660/660; integration 319 passed / 1 unrelated `ShellRoutingTests.Scenario7` failure; focused guards 45/45.

## 15. Smoke verification

**NOT RUN.** The live migration gate failed before application smoke verification.

## 16. N37 live pre-check result

**NOT RUN — BLOCKED.** N37/N38/N41 pre-checks were authorized only if N34-N36 became fully live verified.

Classification: `NEEDS_MORE_EVIDENCE`.

## 17. N38 live pre-check result

**NOT RUN — BLOCKED.** The N34-N36 live-verification prerequisite failed.

Classification: `NEEDS_MORE_EVIDENCE`.

## 18. N41 live pre-check result

**NOT RUN — BLOCKED.** The N34-N36 live-verification prerequisite failed.

Classification: `NEEDS_MORE_EVIDENCE`.

## 19. Remaining blockers

1. Determine why the authoritative PROD catalog lacks the N25 function `ba_dmo_guard_peso_approved`.
2. Determine why the five approved N25 protection/append-only triggers are absent.
3. Reconcile the live baseline through a separately authorized migration/remediation path; N34-N36 must not be expanded to repair this drift.
4. Clarify/correct the package's index-count convention before final N36 catalog acceptance (`pg_indexes` total versus explicit `CREATE INDEX` count).
5. After baseline reconciliation, rerun the entire identity, provenance, N34 pre-check, and recovery-evidence sequence from the beginning.

## 20. Git status/diff

- Branch: `main`, tracking `origin/main`.
- HEAD: `8d916cb973c79d5a1192821ba76c2b4cfa453535`.
- The working tree was already dirty before database work: 81 tracked modified files and 19 untracked files were observed before this report.
- N34, N35, and N36 were already untracked working-tree files and were not edited.
- Verified SHA-256 values:
  - N34: `924EBD14653EAA7835E2E701D5CBFDC924CB84E61138C1A46DE0F0BB4A69BF68`
  - N35: `0183B3849831B6882B83794BC0A3F9F34C51AB751D302AA34DB3C94F42ABAD90`
  - N36: `A69FC517F95E2D54230C449C5083BC62CB5FE6F277C66FCF0132B3B667537754`
- `database/consolidated_clean_install.sql` was already modified and was not edited in this execution.
- The only local file changed by this corrected execution is this report.
- No staging, commit, push, branch change, or destructive Git command occurred.

## 21. Explicit confirmations

- N37+ not implemented: **CONFIRMED**.
- Queue B untouched by this execution: **CONFIRMED**.
- Queue C untouched by this execution: **CONFIRMED**.
- N40 untouched: **CONFIRMED**.
- `ShellRoutingTests.Scenario7` untouched: **CONFIRMED**.
- N34 not executed: **CONFIRMED**.
- N35 not executed: **CONFIRMED**.
- N36 not executed: **CONFIRMED**.
- Authoritative PROD ref recorded as `bddfhbyrmchktqotpzgb`: **CONFIRMED**.
- Authoritative DEV ref `fsxmxyaghxzhpdydamml` received no mutation: **CONFIRMED**.
- Absolute DEV no-touch across both attempts cannot be claimed: the earlier, pre-correction attempt made read-only identity/catalog queries to that ref under the then-supplied PROD mapping. This corrected attempt did not query it.

## Final verdicts

**N34-N36 LIVE STATUS: FAIL** — stopped safely at the N34 pre-check catalog invariant; no migration mutation occurred.

**N37: BLOCKED** — conditional pre-check not reached.

**N38: BLOCKED** — conditional pre-check not reached.

**N41: BLOCKED** — conditional pre-check not reached.

## Stop attestation

`STOP-3` was triggered and evidence was frozen. No migration, DDL, DML, backup mutation, policy change, grant change, application change, smoke action, or query against the authoritative DEV ref occurred after the corrected target mapping was supplied.
