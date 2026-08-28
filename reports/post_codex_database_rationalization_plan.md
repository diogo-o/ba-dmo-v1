# POST-CODEX DATABASE RATIONALIZATION PLAN — FINAL BASELINE DESIGN

> **Type:** AUDIT + DESIGN ONLY — **GATED**. No source, migration, test, schema
> object, or database was modified. No DDL/DML executed. No Queue B
> implementation. No Queue C decision was taken. The ONLY artifact produced by
> this task is this report.
>
> **Baseline verified:** repo `diogo-o/ba-dmo-v1`, branch `main`, HEAD `8d916cb`
> ("Quiesce legacy access mirrors" — N33) plus the Queue A hardening working
> tree (`database/consolidated_clean_install.sql` modified; four untracked
> report files are pre-existing inputs). Migrations directory
> `database/migrations/` is clean — N01…N33 immutable.
>
> **Authority stack (unchanged hierarchy):**
> 1. `AI-CONTEXT/docs/Manual/*` — functional authority (consulted for
>    source-of-truth claims; extensive Manual reads are already recorded in
>    `reports/post_codex_remediation_functional_gate.md` §1 and §3, which this
>    plan incorporates by reference).
> 2. Current source code / Dapper / tests — implementation authority.
> 3. N01–N33 — immutable migration history.
> 4. `database/consolidated_clean_install.sql` — target clean-install baseline,
>    now parity-hardened by Queue A (verified this session, §2.3).
> 5. Existing audit reports under `reports/`.
>
> **Inputs read in full this session:** `post_codex_database_contract_audit.md`
> (1142 lines), `post_codex_remediation_functional_gate.md` (§4 queues +
> classification), `post_codex_queue_A_baseline_hardening_report.md`,
> `schema_rationalization_N34_legacy_mirror_removal_audit.md`,
> `schema_rationalization_03B_plan.md`,
> `schema_rationalization_owner_decisions.md` (D-1…D-16),
> `schema_rationalization_target_architecture.md` (§3 classification),
> `schema_rationalization_03A_postdeploy_parity_check.md` + `_live_parity.sql`;
> migrations N01, N03–N14, N18, N19, N25–N33 in full (others via exhaustive
> grep); `consolidated_clean_install.sql` verified by a full-file reader
> subagent plus direct spot checks. Live database state was NOT reachable
> (no connection env vars) — every claim that needs real-PostgreSQL execution
> is marked `LIVE VERIFICATION REQUIRED`, following the established evidence
> policy.

---

## 1. Executive Summary

The repository is at the first **stable post-Codex baseline**: the functional
safety gate closed (Queue A implemented and verified: 660/660 unit,
314 passed / 1 pre-existing unrelated integration failure), the migration chain
N01…N33 is immutable and complete, and the consolidated clean-install baseline
now reproduces the chain final state at the object level (62 tables, 3
functions, 19 triggers, 81 indexes) with **one residual catalog-level drift**
(an inert RLS policy on the quiesced junction — §9.4) and two documented
intentional privilege omissions (§2.3).

This plan converts the post-Codex contract audit's §17/§20/§21 backlog into a
**forward-only database rationalization sequence N34+**, classified per the
five required risk buckets. The centerpiece remains **N34 — physical removal of
the two legacy access mirrors** (`internal_user_access_templates`,
`internal_users.profile_title`): the N34 removal audit is re-validated against
the Queue A baseline and stands unchanged (Option A, no CASCADE). N34 keeps its
reserved name.

Every candidate is dispositioned: **SAFE NOW** (BQ-16 index, redundant index
removal, D-15 policy rename), **SAFE WITH DATA CHECK** (D-9
`peso_comparacao_anterior`, D-11 dormant columns, D-14 warehouse 1:1),
**REQUIRES OWNER DECISION** (D-12 `contra_costura`, D-10 `peso_leituras`
immutability, FA-05 `physical_pieces.status`, PA-01 occurrence consolidation,
PC-07 `app_settings` surface, N34 execution approval, plus the formal Go for
each destructive drop), **DEFER** (D-7 `job_on_field_option`, D-8
`tampao_planos`, HS-10 expression index, MC-02 transaction-debt), and **DO NOT
TOUCH** (all healthy structures listed in §3 — the 56-table KEEP set, the
append-only revision anchors, the D-1/D-2 authority chain, event streams, and
all migration files).

**Key numbers (final expected state):** 62 total tables (61 application +
`schema_migrations`), 3 trigger functions, 19 triggers, 81 indexes (chain and
consolidated agree — counting only explicit CREATE INDEX statements; inline
PK/UNIQUE constraint indexes are additional), 61 RLS policies (chain) / 60
(consolidated today — drift A disappears with N34), 61 application tables
RLS-enabled. After the full N34+ sequence: **59 application tables** (−
`internal_user_access_templates` N34, − `peso_comparacao_anterior` N37;
58 if N42a also retires `tool_check_occurrences`), **2 columns removed from
`internal_users`** (− `profile_title` N34, − `modules_override` N38), **1
column removed from `job_on_revision`** (− `image_asset_id` N38), **1
nullability widening** (`contra_costura` N39), **1 new append-only guard**
(N40: trigger count 19 → 20), **index deltas**: −2 (N34 junction) −1 (N35
redundant) −1 (N37 comparison-table) −2 (N42a occurrence tables, if approved)
and +1 (N35 BQ-16) +1 (N41 position unique), **1 policy rename** (N36), and the
consolidated baseline regenerated once to the final state (D-16, Phase G).

**Disposition counts (rationalization candidates):** 3 destructive table
removals (N34, N37), 1 destructive two-column removal (N38), 1 nullability
change (N39), 3 additive DDL changes (N40, N41, N35-add), 1 index removal
(N35-remove), 1 policy rename (N36), 5 owner decisions already taken
(D-7..D-9, D-11, D-16 recommend REMOVE_LATER/KEEP but need execution Go),
4 open owner decisions (D-10, D-12, D-14 approval; FA-05/PA-01 product),
1 open configuration-surface decision (PC-07), 2 deferred items (HS-10,
MC-02), 1 code-only corrective wave (Queue B — by reference, not implemented).

**Single most important structural conclusion:** after Queue A, all
*functional* schema defects that were code-fixable are fixed; the residual
schema surface is a small, fully-enumerated set of dead mirrors, dormant
columns, one dead table, index hygiene, and parameterized hardening — no
healthy structure needs reshaping. The risk concentration has moved from
"contract" to "execution discipline" (destructive drops guarded by row-count
zero probes, backups, and parity checks; consolidated refresh last).

---

## 2. Current Baseline State

### 2.1 Version and tree

- HEAD `8d916cb` + Queue A workloads applied (working tree verified; only
  `database/consolidated_clean_install.sql` and test/docs files differ from the
  tagged commit; `database/migrations/` clean).
- Chain: N01_identity … N33_legacy_access_mirror_quiescence (33 files,
  ~2,800 lines), asserted complete by
  `MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN33`
  (files test line 90-103; still the exact N01…N33 list).
- Build: PASS, 0 errors. Unit: 660/660. Integration: 314 passed /
  1 pre-existing unrelated failure (`ShellRoutingTests.Scenario7_AdminOnly_…`,
  Admin nav-item markup drift — owner-declared unrelated debt, never mixed into
  schema work; SCHEMA-RAT-03B plan §7).
- PG-gated suites self-skip without `BA_DMO_TEST_DATABASE` (`RemediationGuardTests`,
  `ArmazemReturnPostgresTests`, `JobOnLifecyclePostgresTests`,
  `RepairAtomicityTests`, `PegamentoPersistencePostgresTests`,
  `AuditJsonBindingTests` probes) — results above treat them as vacuous passes;
  real-PG execution remains a rollout step.

### 2.2 Migration-derived final schema (chain authority, verified this session)

| Artifact class | Count | Notes |
|---|---|---|
| Application tables | 61 | full list in §3.1; N32/N33 create none |
| Tracking table | 1 | `schema_migrations` (runner-owned; RLS enabled, no policy) |
| Functions | 3 | `ba_dmo_guard_append_only` (N01), `ba_dmo_guard_peso_approved` (N25), `ba_dmo_ensure_access_template_profile` (N31) |
| Triggers | **19** | verified by grep over all 33 files — **correction: the contract audit's "21" was an overcount**; chain = 19, consolidated = 19 (see §9.5) |
| Indexes | 81 | verified this session; chain and consolidated name-sets identical |
| RLS policies | 61 (chain) | 48 (N12) + 10 (N25 §2) + `article_reference_images` (N29) + `internal_user_access_templates_app_access` (N27) + `access_template_profiles_app_access` (N31) |
| RLS-enabled tables | 61 + `schema_migrations` | N12 (49 incl. tracking) + N25 §2 (10) + N29 (1) + N27 junction (1) + N31 profiles (1) |
| Column-level grants | `internal_users` SELECT/INSERT/UPDATE on the 8 canonical columns excluding `profile_title` (N33), DELETE unchanged table-level | verified against N33 §3 |
| Sequences | none | UUID PKs via `gen_random_uuid()` |

### 2.3 Consolidated clean-install baseline after Queue A (verified)

Full-file diff against the chain final state (this session, reader subagent +
spot checks) — **material parity achieved**:

- Table set: 62 CREATE TABLE = the exact 61 app tables + `schema_migrations` —
  zero missing, zero extra.
- N31 objects present: `access_template_profiles` (:1723), function (:1732),
  trigger (:1750), `ux_internal_user_access_templates_actor` (:1792),
  backfill/sync DML (:1758-1802), RLS/policy/grants (:1804-1825).
- N29 RLS stanza present immediately after `article_reference_images` CREATE
  (:481-497).
- N33 posture present: `profile_title` NULLABLE (:114, :1663) with NULL-tolerant
  CHECK retained (:1664-1668); junction has zero `ba_dmo_app` grants + guarded
  `REVOKE ALL` (:1690-1696); `internal_users` column-level grants on the exact
  8 columns excluding `profile_title` (:1705-1714); DELETE untouched.
- Indexes / functions / triggers: name-sets identical to the chain (81/3/19).

**Residual drift (all non-functional):**

| # | Item | Evidence | Class |
|---|---|---|---|
| D-A | Policy `internal_user_access_templates_app_access` (N27:137-143) is **absent** from the consolidated file → 60 policies vs 61. Functionally inert (junction has zero privileges; the policy dies with the table in N34) but breaks strict catalog parity | consolidated junction section :1648-1696 (RLS enable :1670, REVOKE :1693, no policy) | CONFIRMED — disappears with N34 |
| D-B | `GRANT USAGE ON SCHEMA public` (N01:26-27) not emitted; ALTER DEFAULT PRIVILEGES only comment-kept (:63-69). Intentional (guard-role pattern) — nil functional impact on stock PG (PUBLIC has schema USAGE by default) | consolidated :63-69 | CONFIRMED — intentional, document only |
| D-C | Header note at consolidated :1357-1359 cites `reports/database_owner_decisions.md` (does not exist) — copied verbatim from `N25:6-7`; actual register is `schema_rationalization_owner_decisions.md` | consolidated :1357-1359 | CONFIRMED — inherited stale citation |
| D-D | `03_MIGRATIONS.md` §2/§3 inventory still lists N01…N31 ("Migration count verified from disk: 31", :63, :103, :141); N32/N33 appear only in the ADM-14 deploy-order note (:1190-1199) | read :60-141 | CONFIRMED — docs drift, §19 |

### 2.4 Known live facts (owner-supplied, dated)

- Live Supabase project `bddfhbyrmchktqotpzgb`: provenance Supabase-CLI
  (`supabase_migrations.schema_migrations`, last row
  `20260827150130`/`n31_template_profiles_single_assignment` at the 03A
  observation; the N34 audit's later observation implies N33 at least was
  applied — mirrors quiesced, junction privilege-less, RLS on).
- 03A parity observed clean: 7 users / 7 junction rows / 0 multi-assignments /
  0 conflicts / 0 divergences.
- Everything else (row counts, per-table contents, applied DDL state) is
  `LIVE VERIFICATION REQUIRED` — §14.

---

## 3. Confirmed Healthy Structures

These are the KEEP surfaces (56 of 61 tables per the target-architecture
classification, re-verified post-Queue A). **DO NOT TOUCH** unless a listed
exception applies (exceptions are only: the two N34 mirrors, D-9
`peso_comparacao_anterior`, D-11 two columns, D-12 nullability, D-14 index,
D-15 policy name, index hygiene N35).

### 3.1 Per-table verdict summary (61 app tables)

| Family | Tables | Verdict | Evidence (post-Queue A re-verified) |
|---|---|---|---|
| Identity/Access | `access_templates`, `internal_users`, `access_template_profiles` | KEEP (authority chain) | D-1/D-2 chain; N32 fail-closed guards; mirrors removed in N34/N38 |
| Access mirrors | `internal_user_access_templates`, `internal_users.profile_title` | **REMOVE (N34)** | zero src refs (re-grepped this session); N33 quiescence; N34 audit §3 |
| Global audit | `audit_events` | KEEP | append-only trigger; 7 indexes + PERF-01; single global authority |
| Catalog mirror | `module_catalog_mirror` | KEEP (read-model; D-6 Option A) | synchronizer sole writer; never consulted by authorization |
| Boquilhas | `bq_lotes`…`bq_utilisation_readings` (6) | KEEP | bundled UoW; append-only streams; `noted_repairer_id` column has FK but needs index (N35) |
| Ferramentas | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_usage_records` | KEEP | `physical_pieces.status` unconstrained (FA-05 — constraint fix pending owner) |
| Ferramentas legacy | `tool_check_occurrences` | **DECIDE (PA-01)** | zero readers (reader removed by Queue A) and zero writers; N05 sibling is the authority |
| Job On family | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `article_reference_images`, `jobon_user_current` | KEEP | revision family append-only (N25); lifecycle CHECK (N25); `image_asset_id` column DORMANT (N38 drops it) |
| Job On dormant catalog | `job_on_field_option` | KEEP (D-7 default A) | zero consumers; empty by construction |
| Peso | `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_day_approvals`, `peso_settings` | KEEP | approved-control guard (N25); `peso_leituras` lacks approved-parent guard (N40); day_approvals write-only (keep) |
| Peso dead mirror | `peso_comparacao_anterior` | **REMOVE (N37/D-9)** | zero SQL in src (re-grepped); previous-approved now resolved via `previous_control` JSON snapshot + `GetControlByIdAsync` |
| Pegamentos | `pegamento_controlos`, `pegamento_medicoes`, `pegamento_documentos` | KEEP | Queue A fixed create (PC-01) + UoW (PG-04); `nominal_average` column DORMANT (§10); `contra_costura` NOT NULL (D-12/N39) |
| Arms/zém | `warehouse_locations`, `warehouse_stock`, `warehouse_movements` | KEEP | locked transitions (FOR UPDATE); partial unique per (location, lot) — 1:1-per-position gap (D-14/N41) |
| Reparação Externa | `repairers`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events` | KEEP | repairer registry TD-15; append-only `repair_events` (write-only today — legitimate event stream); return status machine defect is CODE (Queue B F4), not schema |
| Reparação Interna | `internal_repair_records` | KEEP | CM/MF-only CHECK validated (N28) |
| Tampões | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` | KEEP | N21 normalized design (DUP-01); saldos FOR UPDATE; append-only streams |
| Tampões dormant | `tampao_planos` | KEEP (D-8 default C) | full service/repo, zero routes; parked |
| Shared | `app_settings` | KEEP (no writers — PC-07 owner decision on surface) | only reader `DapperAppSettingsReader` (+`FileSystemJobOnImageProvider`, `PegamentoService`); zero INSERT/UPDATE/DELETE re-grepped |

### 3.2 Confirmed clean contract surfaces (explicitly PASS, re-verified)

- **Migration chain integrity** N01→N33 (family test; ordinal discovery;
  whole-script + SHA-256 runner).
- **Append-only trigger discipline** on 13 fact tables + 4 revision-family
  tables (19 triggers §2.2).
- **RLS/policy/grants parity** across all 61 app tables in the chain.
- **ON CONFLICT arbiters** — every arbiter exists as a PK/unique (audit §8
  list; DAP-OK).
- **Immutable revision anchors** — `job_on_revision_id` pinned by
  `peso_controlos`, `pegamento_controlos`, `controlo_sheets`,
  `internal_repair_records`; anchors append-only since N25 (FK-01).
- **Tampões transform atomicity** (UoW + FOR UPDATE, TP-07) and **Boquilhas
  bundle atomicity**.
- **Armazém locked transitions** and **repair-return TOCTOU closure**
  (FOR UPDATE, commit `838afe8`).
- **D-1/D-2 access authority chain** with N32 fail-closed guards and N33
  kill-switches (PA-05 RESOLVED).
- **Job On lifecycle** persists status + timestamps + audit in one UoW
  (JA-01 fixed, re-verified at Queue A state).
- **Timestamps UTC-consistent** (DT-04); status codecs align with CHECKs
  (DT-05).

---

## 4. Legitimate Duplication (NOT rationalization candidates)

Resolved pairs/groups — each is a *different concept*, a *snapshot*, or a
*one-way derived projection*, not accidental duplication:

| # | Objects | Same fact? | Why legitimate | Action |
|---|---|---|---|---|
| DUP-01 | Tampões: `tampao_configuration_machines` (current set) / `tampao_configuration_machine_event` (change stream) / `tampao_configuration_notes` / `tampao_saldos` (balances) / `tampao_movements` (facts) | NO | current-set vs events vs notes vs balances vs fact-history are complementary layers (A11/A10); N21 owner decision mandates the normalized N:M, never per-machine copies (TP-13) | DO NOT TOUCH |
| DUP-02 | `peso_comparacao_anterior` (table) vs previous-approved resolution | **YES — stored vs computed** | table never implemented; authority is the control snapshot + selection flow | **REMOVE (N37)** — the only "legitimate-looking" pair that is actually a dead mirror |
| DUP-03 | `tool_check_occurrences` (N04) vs `job_on_verification_occurrence` (N05) | PARTIAL — same business concept, different linkage | N05 is the live materialization (real component FK + writer); N04 schema-only (PA-01) | **DECIDE (owner/product)** — §11; default: retire N04 table in a later migration |
| DUP-04 | `audit_events` (global) vs `job_on_audit_event`/`controlo_sheet_events`/`bq_lifecycle_history`/`tampao_configuration_*`/`repair_events` | NO — compliance projection vs domain streams | different concepts (HS-01); the gap is a missing *projection* (JobOn/Pegamentos audit emission — Queue B code), never a merge | DO NOT TOUCH; Queue B closes the projection gap |
| DUP-05 | `access_template_profiles.functional_profile` vs `internal_users.profile_title` | YES — mirror (D-1) | resolved N32/N33; authority = template profile | **REMOVE mirror (N34)** |
| DUP-06 | `internal_users.template_id` vs `internal_user_access_templates` | YES — 1:1 mirror (D-2) | resolved N32 (direct FK authority) + N33 (junction dead) | **REMOVE mirror (N34)** |
| DUP-07 | `article_reference_images` vs `job_on_revision.image_asset_id` | YES — mirror | N29 resolved (authority = reference table; column dormant) | **REMOVE column (N38, D-11)** |
| Snapshots | `*_snapshot` columns; `repairer_snapshot`; `balances_before/after`; `relevant previous_control` JSON; revision graph copies | NO — fact fidelity | captured at write time from the pinned revision; never live authority (A4) | DO NOT TOUCH |
| Status + history | `warehouse_stock` active rows vs `warehouse_movements`; `bq_traces.status` vs `bq_lifecycle_history`; `tampao_saldos` vs `tampao_movements` | NO — current-state vs fact-stream | intentional layering | DO NOT TOUCH |
| `sap_start/sap_end` | `bq_traces` vs `tool_usage_records` | NO — different domains (BQ trace vs CM/MF lote utilisation) | different scopes (A14) | DO NOT TOUCH |

---

## 5. Confirmed Rationalization Candidates (complete inventory)

Classification legend: **SAFE NOW** (no data/owner dependency) ·
**SAFE WITH DATA CHECK** (execution allowed after read-only probes pass) ·
**REQUIRES OWNER DECISION** (disposition or branch unresolved) ·
**DEFER** (parked with stated reason) · **DO NOT TOUCH** (healthy).

### 5.1 Legacy tables

| # | Object | Created | Evidence | Class |
|---|---|---|---|---|
| T1 | `internal_user_access_templates` | N27 | zero src refs (re-grepped); N33 revoked all privileges; live audit: no external `pg_depend`, no incoming FKs | **SAFE NOW** for N34 (execution still gated by owner Go + backup per project convention) |
| T2 | `internal_users.profile_title` (column) | N01/N27/N33 | zero src refs; only dependency = own CHECK; live audit: no external dependents | **SAFE NOW** for N34 |
| T3 | `peso_comparacao_anterior` | N06 | zero SQL anywhere in src (re-grepped); D-9 = REMOVE_LATER; empty table by construction (N33-era owner facts show 0 divergence rows; row-count probe §14 must confirm 0) | **SAFE WITH DATA CHECK** (N37) — owner Go needed (D-9 execution) |
| T4 | `tool_check_occurrences` | N04 | zero writers; **zero readers after Queue A removed `GetOccurrencesByRuleAsync`** (F17); N05 sibling is the live materialization (PA-01) | **REQUIRES OWNER DECISION** (F16) — default: RETIRE with N04 family in a later migration (N42), guarded by row-count probe |

### 5.2 Dormant / future-owned tables (KEEP by decision)

| # | Object | Evidence | Class |
|---|---|---|---|
| T5 | `tampao_planos` | full service/repo, no routes; tests assert 404; D-8 default C = KEEP dormant | **DEFER** (owner product decision; wiring or retirement later) |
| T6 | `job_on_field_option` | zero consumers; empty; D-7 default A = KEEP dormant | **DEFER** (parked) |
| T7 | `repair_events` | write-only (Repair + RI writers); zero readers — but it is the legitimate shared repair fact stream (TD-15/§16) | **DO NOT TOUCH** (readers may emerge with Repair history surfaces; not a mirror) |

### 5.3 Legacy / dormant columns

| # | Column | Evidence | Class |
|---|---|---|---|
| C1 | `internal_users.modules_override` | N26; NULLed by N27; writer removed by Queue A (F17); still projected by `DapperAdminRepository` (:52) and `DapperInternalUserRepository` (:37); the 42703 schema-gate (`SchemaMigrationRequiredException`) keys off it | **SAFE WITH DATA CHECK** (N38, D-11) — code cleanup first (projections + gate), then drop |
| C2 | `job_on_revision.image_asset_id` | N05; superseded by N29; writers force NULL (JA-15); still present in revision INSERT/UPDATE SQL (`DapperJobOnRepository` :232, :288, :577, :743, :1041) | **SAFE WITH DATA CHECK** (N38, D-11) — code cleanup first |
| C3 | `pegamento_controlos.nominal_average` | N07; zero src reads/writes (PG-10); authority = N16 per-component nominals | **REQUIRES OWNER DECISION** (small) — recommend REMOVE_LATER (N38 group) after confirming no report reads it |
| C4 | `bq_traces.sap_end` | N03; no non-NULL value writer — the column IS in the trace INSERT list (`DapperBoquilhasRepository.cs:199-206`) but is always bound NULL; the only reader is `BoquilhasService.cs:297`; utilisation "final" lives in `bq_utilisation_readings` | **REQUIRES OWNER DECISION** (BQ-08) — recommend KEEP either way (populate on close via code, or drop later; **correction to audit BQ-08: the column is syntactically inserted, never populated**) |
| C5 | `bq_discrepancies.resolved_by` / `resolved_at_utc` | **LIVE columns — written today**: `UpdateDiscrepancyAsync` sets both (`DapperBoquilhasRepository.cs:355-357`; the INSERT binds them at :349) behind `POST /api/boquilhas/discrepancies/{id}/resolve` (`Program.cs:1563`) | **DO NOT TOUCH** — **correction to audit BQ-04: these columns have a real writer**; the Queue B F12 defect (PC-14) concerns `expected_qty` semantics, not the absence of a writer |
| C6 | `job_on.production_folder` | N13; no application writer (JA-05/PC-06); read by resolver + `GetByIdAsync`; omitted from `GetActiveAsync`/`GetByProductionCodeAsync` SELECTs (JA-04) | **DO NOT TOUCH (schema)** — needs a writer + SELECT fixes (Queue B F10, code-only) |

### 5.4 Redundant / missing indexes

| # | Index | Class | Detail |
|---|---|---|---|
| I1 | MISSING: `bq_movements.noted_repairer_id` | **SAFE NOW** (N35) | BQ-16; column added N18 without index; filter `AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)` in `DapperBoquilhasRepository.ListMovementsAsync` (:267) + `CountMovementsAsync`; no supporting index exists |
| I2 | REDUNDANT: `ix_pegamento_documentos_controlo` | **SAFE NOW** (N35) | duplicates the UNIQUE(pegamento_controlo_id) constraint index (N14 created both); double write maintenance, zero read benefit |
| I3 | CANDIDATE (defer): História group-key / concat expression | DEFER | HS-10: `GROUP BY entity_type,entity_id … MAX(occurred)` + `entity_type\|\|'|'\|\|entity_id = ANY(…)` cannot use `ix_audit_events_entity`; expression index only after EXPLAIN on populated table |
| I4 | CANDIDATE (data-check): warehouse per-position partial unique | **SAFE WITH DATA CHECK** (N41, D-14) | pattern: occupancy reads / `ConfirmReturnAsync` `WHERE released_at_utc IS NULL AND warehouse_location_id = @…` FOR UPDATE; existing `uq_warehouse_stock_active_occupation` covers only (location, tool_lote) |

### 5.5 Missing / weak FKs and UNIQUEs

| # | Finding | Evidence | Class |
|---|---|---|---|
| U1 | 1:1-per-position not DB-enforced | ON-04; `uq_warehouse_stock_active_occupation` weaker than the physical invariant | **SAFE WITH DATA CHECK** (N41) |
| U2 | Logical uuid links without FK (documented) | FK-02: `job_on.article_reference_id`, `tool_check_occurrences.job_on_id/job_on_component_id`, `tampao_planos.job_on_id/production_code`, `internal_repair_records.job_on_id/lot_id` | **DO NOT TOUCH** (contract-level coupling by design) — except `job_on.article_reference_id` has no visible producer (flagged for owner §11) |
| U3 | Guid.Empty sentinel FK risk (Peso) | FK-03/PESO-05: unresolved reference/lot routes bind `Guid.Empty` into real FKs → latent 23503 | **DEFER** (code pre-validation; not schema) |
| U4 | `bq_movements.noted_repairer_id` FK exists without index | FK-06/BQ-16 | covered by I1 |

### 5.6 CHECK / nullability / type corrections

| # | Finding | Evidence | Class |
|---|---|---|---|
| N1 | `pegamento_medicoes.contra_costura` NOT NULL vs nullable domain | PC-02/D-12; one-sided measurement impossible (23502) | **REQUIRES OWNER DECISION** (branch A: nullable + domain rule — recommended; branch B: keep NOT NULL) → N39 |
| N2 | `physical_pieces.status` unconstrained double-meaning | FA-05; condition codec written into a no-CHECK column; `MapPiece` hard-codes "operational" | **REQUIRES OWNER DECISION** (CHECK on 4 technical states vs split column) → constraint DDL later |
| N3 | `internal_users.profile_title` CHECK | `ck_internal_users_functional_profile` — dies with the column | N34 drops it (Option A) |
| N4 | `pegamento_controlos.updated_at_utc` explicit-NULL insert | PC-01 → **FIXED by Queue A** (fallback to `created_at_utc`) | **RESOLVED — no schema item** |
| N5 | `audit_events.before/after_summary` uncast payload convention (5 emitters) | PC-03; BQ/Tampões/Peso/Ferramentas/RI + Controlo sheet events | **DEFER — Queue B code fix (F3), not DDL** |
| N6 | Cast-less snapshot binds (`repair_exits.repairer_snapshot`, RI `before_snapshot`) | DT-07 | **DEFER — code hardening (convention-fragile, currently JSON-valid)** |
| N7 | RI `tool_type` CHECK | N22 widen → N28 narrowed CM/MF (validated) — final state correct | **DO NOT TOUCH** (N22 header comment stale — docs item) |
| N8 | `peso_controlos` approved immutability | N25 guard covers the control row only; `peso_leituras` rewritable (PC-09/D-10) | **REQUIRES OWNER DECISION** (Option A recommended) → N40 trigger |

### 5.7 Stale triggers / functions / policies

| # | Finding | Evidence | Class |
|---|---|---|---|
| P1 | Policy naming divergence (`access_template_profiles_app_access` vs `ba_dmo_app_access`) | RLS-02/D-15 | **SAFE NOW** (N36 rename; only one renames after N34) |
| P2 | `internal_user_access_templates_app_access` — absent from consolidated (drift D-A) | this session | resolves itself with N34 (table + policy removed) |
| P3 | No stale trigger/function on live path | verified §2.2 (19 triggers, 3 functions, all referenced) | **DO NOT TOUCH** |
| P4 | `RepairAtomicityTests` teardown `DELETE FROM audit_events` conflicts with append-only trigger | RLS-06 | **DEFER** (test change when PG-gated suites run with `BA_DMO_TEST_DATABASE`) |

### 5.8 Source-of-truth conflicts (condensed — full matrix §6)

- `app_settings.main_documents_output_root` — zero writers → dead configuration
  authority (PC-07, owner decision on the surface; **not** a schema conflict).
- `peso_comparacao_anterior` vs snapshot/selection (D-9 → N37).
- `tool_check_occurrences` vs `job_on_verification_occurrence` (PA-01).
- `profile_title`/junction vs template authority (N34).
- `nominal_average` vs N16 nominals (C3).
- `job_on.production_folder` — no writer (C6 — ownership gap, not conflict).

---

## 6. Source-of-Truth Conflicts (matrix)

For every major business concept — exactly one current authority; conflicts
flagged with their resolution owner/action. **History/snapshot** = legitimate
derived records; they are never treated as live authority (A4).

### Concept: Functional profile (Admin / Operador / Responsável)
- Current authority: `access_template_profiles.functional_profile` (1:1 per template; PK + CHECK; N31 trigger backfills; D-1 Option A).
- History/snapshot: `internal_users.profile_title` fossil values (mirror, quiesced N33).
- Readers: `DapperAdminRepository` users projection (`LEFT JOIN access_template_profiles`, :46); Admin Users page; shell header (template name).
- Writers: `DapperAdminRepository` template/profile upserts (UoW); N31/N32 migration-time backfill/trigger.
- Conflict: `profile_title` mirror (dead).
- Action: **N34** drops the mirror column + its CHECK. No runtime reader/writer exists (re-grepped).

### Concept: User→template assignment
- Current authority: `internal_users.template_id` NOT NULL FK + `ux_internal_user_access_templates_actor`-era single assignment (D-2 Option A).
- History/snapshot: junction rows (mirror, quiesced N33).
- Readers: `DapperInternalUserRepository.FindByAuthUserIdSql` (direct-FK path); admin projection; self-lockout count.
- Writers: admin user create/change-template (UoW, direct FK only post-N33).
- Conflict: junction mirror (dead).
- Action: **N34** drops the junction table (its PK, FKs, both indexes, inert policy, row type, TOAST).

### Concept: Module catalog / Applications
- Current authority: in-code `CanonicalModuleCatalog` (D-6 Option A); `access_templates.modules` stores the validated selection; legacy `capabilities[]` inert (`[]` since N27).
- History/snapshot: `module_catalog_mirror` = one-way display read-model (synchronizer sole writer; never consulted for authorization).
- Readers: `/admin/applications`; tests.
- Writers: `DapperModuleCatalogMirrorRepository.UpsertAllAsync` via `AdminMirrorService`.
- Conflict: none (read-model recognized as derived; D-6 keeps it).
- Action: **DO NOT TOUCH** (Option A). Optional later simplification (Option B) is an owner choice, not a defect.

### Concept: Global audit / História
- Current authority: `audit_events` (single global append-only store; module/action/entity indexed).
- History/snapshot: domain streams (`job_on_audit_event`, `controlo_sheet_events`, `bq_lifecycle_history`, `tampao_configuration_*`, `repair_events`) — different concepts (DUP-04).
- Readers: `DapperHistoriaRepository` (group + flat + text queries), Admin Auditoria (`QueryAuditAsync`/export).
- Writers: 9 global emitters (5 still uncast — PC-03, Queue B F3); JobOn (D-5) and Pegamentos (PC-04) emit none today (Queue B F5/F6, code-only).
- Conflict: *missing projection*, not duplicate authority.
- Action: **DO NOT TOUCH schema**; Queue B code wave closes the gap; no migration needed.

### Concept: Job On production context
- Current authority: `job_on` + revision graph (append-only; lifecycle CHECK; `uq_job_on_identity` partial unique).
- History/snapshot: revision components/fields/rows; `job_on_audit_event`; control snapshots pinned to `job_on_revision_id`.
- Readers: calendar (`GetActiveAsync`), production lookup, context lookups (Peso/Pegamentos/Controlo/RI), images, document endpoints.
- Writers: dormant write surface (repository-complete, no routes — D-4 Option B); image/current/document endpoints live; `production_folder` has NO writer (PC-06).
- Conflict: `production_folder` ownership gap (C6); write surface dormant (not a conflict — D-4 Option B deferral).
- Action: **DO NOT TOUCH**; Queue B F10 adds the folder writer + SELECT column fixes (code-only).

### Concept: Article/Reference images
- Current authority: `article_reference_images` (N29; PK reference_code; CHECKs on asset name).
- History/snapshot: `job_on_revision.image_asset_id` (dormant mirror; writers force NULL).
- Readers: image provider (`FileSystemJobOnImageProvider`), set/remove endpoints.
- Writers: `DapperArticleReferenceImageRepository` (UoW + job_on_audit_event with CAST jsonb).
- Conflict: dormant mirror column.
- Action: **N38** drops `image_asset_id` (D-11), after code cleanup of revision INSERT/UPDATE SQL.

### Concept: Peso previous-approved comparison
- Current authority: the control selection flow + `peso_controlos.previous_control` JSON snapshot + `GetControlByIdAsync` (post-Queue A; the old `GetPreviousApprovedAsync` port was removed by Queue A F17).
- History/snapshot: `peso_comparacao_anterior` table (never populated).
- Readers: compare/decision flows (`PesoService` :548-621), PDF renderer.
- Writers: `PesoService` comparison creation writes `previous_control` snapshot; day-approval upsert.
- Conflict: dead mirror table.
- Action: **N37** drops the table (D-9; row-count guard). Doc comments still referencing the table (`IPesoRepository.cs:9`, `PesoControl.cs:220`, `DapperPesoRepository.cs:14`) must be refreshed in the same change set.

### Concept: Balance / occupancy current state
- Current authority: `tampao_saldos` (per config, 2 CHECKs) and `warehouse_stock` active rows (`released_at_utc IS NULL`).
- History/snapshot: `tampao_movements` (balances_before/after), `warehouse_movements`.
- Readers: balance views, occupancy queries, repair return path.
- Writers: Tampões UoW transforms (FOR UPDATE); Armazém entrada/saida/corrigir (UoW + FOR UPDATE).
- Conflict: warehouse 1:1-per-position not DB-enforced (U1).
- Action: **N41** additive partial unique (D-14) after data check; **DO NOT TOUCH** Tampões layering.

### Concept: BQ lot/trace/utilisation state
- Current authority: `bq_lotes` lifecycle_state + `bq_traces` status/active partial unique; utilisation facts in `bq_utilisation_readings`.
- History/snapshot: `bq_lifecycle_history`, `bq_movements`, `bq_discrepancies`.
- Readers/writers: `DapperBoquilhasRepository` (bundle UoW).
- Conflict: `bq_traces.sap_end` never populated (PA-07 partial mirror — C4; correction to audit BQ-08: syntactically inserted, always NULL); `bq_discrepancies.resolved_by/resolved_at_utc` ARE written by the live resolve flow (correction to audit BQ-04 — see §5.3/C5).
- Action: **DO NOT TOUCH columns**; Queue B F12 (PC-14) corrects `expected_qty` semantics in code; C4 disposition is an owner question (populate on close via code, or drop later — no DDL required either way).

### Concept: Tool / piece identity and condition
- Current authority: `tool_references`/`tool_lotes` (Ferramentas master; CM/MF/BQ/PU/CS CHECK; `uq_*` type+ref, ref+lote); `physical_pieces` identity (uq per lote+number).
- History/snapshot: pieces' `status` column (condition codec — FA-05 conflict), `tool_usage_records` (utilisation facts), `tool_check_rules` (config).
- Readers/writers: `DapperFerramentasRepository`; usage records append-only.
- Conflict: `physical_pieces.status` double-meaning (condition stored in an unconstrained column; readers interpret "New").
- Action: **owner decision FA-05** → later CHECK DDL; default **SAFE WITH DATA CHECK** after owner chooses the value set (30:244 manual: Novo/Reparado/Por reparar/Sucatado).

### Concept: Repairer registry / capability / defaults
- Current authority: `repairers` (TD-15 registry), `repairer_repair_types` (M:N capability), `line_repairer_defaults` (display default, NOT capability — N20).
- Readers/writers: Repair + Boquilhas repos (`line_repairer_defaults` ON CONFLICT PK).
- Conflict: none.
- Action: **DO NOT TOUCH**.

### Concept: Settings
- Current authority: `app_settings` (key/value; only key `main_documents_output_root` JSON-string) and `peso_settings` (module settings).
- Readers: `DapperAppSettingsReader` (`GetOutputRootAsync`); consumers `FileSystemJobOnImageProvider` (:45), `PegamentoService.ConfirmDocumentSavedAsync` (:243-247).
- Writers: **NONE for app_settings** (HS-06; re-grepped zero INSERT/UPDATE/DELETE).
- Conflict: dead configuration authority (PC-07) — the Manual (20:526-528) requires the root to be manually configured but does not define the surface.
- Action: **owner decision PC-07** (Admin settings UI vs documented manual seed). Optional DDL additions (e.g. CHECK on `setting_key`, seed row) only after the decision; default: no schema change, document the seed SQL in the runbook.

### Concept: História transversal
- Current authority: transversal projection over `audit_events` only; not a module with own tables.
- Readers: `DapperHistoriaRepository`; Admin Auditoria.
- Writers: global emitters (gap for JobOn/Pegamentos — Queue B).
- Conflict: none (domain hierarchy: Controlo ⊃ Peso/Pegamentos; História transversal).
- Action: **DO NOT TOUCH**; Queue B F5/F6 add emitters.

---

## 7. Constraint Findings

### 7.1 Coverage check (all 61 app tables)

- **PK:** present on all 61 tables (UUID `gen_random_uuid()` defaults or
  natural/text keys: `access_templates`, `internal_users`, `module_catalog_mirror`,
  `article_reference_images`, `access_template_profiles`,
  `tampao_saldos`(→config 1:1), `jobjon_user_current`(actor_id),
  `line_repairer_defaults`, `repairer_repair_types` composite, `peso_settings`,
  `tampao_field_defs/values`, etc.).
- **FK:** all direct referential links carry FKs; documented logical-uuid links
  are intentionally FK-less (FK-02, §5.5/U2). `audit_events.job_on_id` /
  `revision_id` intentionally denormalized (N01 comment).
- **UNIQUE:** strong domain uniques present — `uq_job_on_identity` (partial,
  non-canceled), `uq_bq_lotes_reference_batch`, `uq_bq_traces_active` (partial),
  `uq_peso_controlos_identity`, `uq_peso_leituras_controlo_cm`,
  `uq_peso_day_approvals_identity`, `uq_tampao_configurations_values`,
  `uq_warehouse_stock_active_occupation` (partial), `uq_physical_pieces_lote_number`,
  `uq_internal_users_auth_user`, `uq_tool_references_type_code`,
  `uq_tool_lotes_reference_lote`, `uq_pegamento_documentos_controlo` (1:1),
  `uq_peso_references_mold_neckring`, etc. — verified ON CONFLICT arbiters all
  exist (audit §8).
- **CHECK:** status/lifecycle/state machines all CHECK-constrained and codec
  aligned (DT-05): job_on lifecycle (N25), `bq_*` (N03), `pegamento_controlos`
  status (N25), `repair_exit_items` status (N25), `peso_controlos` approved
  consistency (N25), `job_on_verification_occurrence` completed (N25), RI
  `tool_type` CM/MF validated (N28), `internal_repair_records` correction
  self-link (N08), `tampao_*` (N10/N21), `controlo_sheets` status/decision (N23),
  `article_reference_images` asset/reference (N29), `access_template_profiles`
  profile values (N31), `audit_events` result/year (N01).
- **NOT NULL / defaults:** consistent `created_at_utc/updated_at_utc DEFAULT
  now()`; the one historical violation (PC-01 `updated_at_utc` explicit-NULL
  insert) is fixed by Queue A; the remaining mismatch is `contra_costura`
  NOT NULL vs nullable domain (N1 → N39).

### 7.2 Constraint findings with actions

| # | Finding | Evidence | Class / Action |
|---|---|---|---|
| CF-1 | `pegamento_medicoes.contra_costura` NOT NULL vs nullable domain | N07:63; `PegamentoControlo.AddMeasurement(…, decimal? contraCostura)`; `PegamentoMeasurementCalculator` one-sided support | REQUIRES OWNER (D-12) → N39 |
| CF-2 | `physical_pieces.status` no CHECK, double-meaning | N04:72 (`NOT NULL DEFAULT 'operational'`, no CHECK); `RegisterPieceAsync` writes condition codec; `MapPiece` hard-codes "operational" | REQUIRES OWNER (FA-05) → later CHECK DDL |
| CF-3 | `ck_internal_users_functional_profile` — dies with column | N27:119-120; N33 keeps (NULL-tolerant) | N34 drops it explicitly (Option A) |
| CF-4 | `peso_leituras` no approved-parent guard | N06 (no trigger); `DapperPesoRepository.UpdateControlAsync` DELETE+re-INSERT (:383-399) | REQUIRES OWNER (D-10) → N40 |
| CF-5 | `controlo_sheet_items/events` CASCADE on sheet delete without DB delete-guard | N23:67,88; no DELETE path in `DapperControloSheetRepository` (good today) | DEFER (document; no backstop needed while no delete path exists) |
| CF-6 | `peso_comparacao_anterior`/`peso_leituras` CASCADE on controlo delete | N06:120,135 — guarded for approved rows by `ba_dmo_guard_peso_approved` | DO NOT TOUCH (approved immutability covers the destructive path; D-10 extends to readings) |
| CF-7 | `access_template_profiles` CASCADE on template delete | N31:14 — templates are deactivated, never deleted (`AdminTemplateService`) | DO NOT TOUCH (consistent with current behavior) |
| CF-8 | `job_on.article_reference_id` logical link, no producer visible in src | N05:21; no writer found in src | REQUIRES OWNER (is the article-reference link a sanctioned dormant/lookup field?) — default DEFER |
| CF-9 | `bq_movements.noted_repairer_id` FK without index | N18:16 | N35 index (I1) |
| CF-10 | `repair_exits.repairer_snapshot` / RI `before_snapshot` cast-less binds | DT-07; `DapperRepairRepository` :62,69; `DapperReparacaoInternaRepository` :36,56-57 | DEFER (code convention hardening; currently JSON-valid at call sites) |

---

## 8. Index Findings

81 indexes exist in the chain (full inventory verified this session, §2.2).
Every proposed add/remove below is tied to a real Dapper/query pattern — no
speculative indexes.

### 8.1 PROPOSED ADD — `ix_bq_movements_noted_repairer` (BQ-16, deferred by Queue A to the post-N34 baseline phase)

- **Query pattern:** Boquilhas History filter by repairer:
  `DapperBoquilhasRepository.ListMovementsAsync` :267 —
  `AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)` (same
  predicate in `CountMovementsAsync` :294+), driving
  `WHERE t.bq_lote_id = @LoteId … ORDER BY m.occurred_at_utc DESC`.
- **Benefit:** the filter is hit-testable; the only existing usable indexes are
  `ix_bq_movements_trace` (bq_trace_id) and `ix_bq_movements_occurred`
  (occurred_at_utc) — neither covers `noted_repairer_id`, so a repairer-filtered
  history scan walks the full movement set. Adding a single-column index turns
  the filter into an index scan. Write cost: one index on an append-only table
  (insert-only, no UPDATE/DELETE) — negligible.
- **Redundancy check:** no overlapping index exists (no index on
  `noted_repairer_id`; not a prefix of any existing composite).
- **Migration:** N35 (with I2). Classified **SAFE NOW** (additive; no data/owner
  dependency).

### 8.2 PROPOSED REMOVE — `ix_pegamento_documentos_controlo` (informational, audit §14)

- **Query pattern:** `DapperPegamentoRepository` document upsert/read by
  `pegamento_controlo_id` (:334-339) — served by the **UNIQUE constraint index**
  created by `UNIQUE (pegamento_controlo_id)` (N14:12). The separate index
  (N14:20-21) has identical leading column and zero additional coverage.
- **Benefit of removal:** eliminates double maintenance on every
  pegamento_documentos write (insert + ON CONFLICT update); zero read loss.
- **Redundancy proof:** `\d pegamento_documentos` on any migrated DB shows the
  constraint index already covering the column — the standalone index is
  provably a duplicate (same single-column btree).
- **Migration:** N35. Classified **SAFE NOW**.

### 8.3 PROPOSED ADD (data-checked) — warehouse per-position partial unique (D-14)

- **Query pattern:** occupancy assertions and the locked repair-return check:
  `DapperArmazemRepairMovementRepository.ConfirmReturnAsync` (:79-98)
  `SELECT … FROM warehouse_stock WHERE warehouse_location_id = @… AND
  released_at_utc IS NULL FOR UPDATE`; entrada/saida occupancy checks in
  `DapperArmazemRepository`.
- **Benefit:** closes the invariant gap (two different lots could occupy one
  position concurrently — ON-04); DB backstop independent of code locking.
- **Precondition:** read-only probe §14.6 (zero active rows sharing a position);
  if violations exist → report, do not auto-fix (fail-closed convention).
- **Migration:** N41. Classified **SAFE WITH DATA CHECK** (+ owner Go on D-14).

### 8.4 CANDIDATES — explicitly NOT recommended without evidence

| Query pattern | Existing coverage | Verdict |
|---|---|---|
| História group keys `GROUP BY entity_type,entity_id … MAX(occurred)` and `entity_type\|\|'|'\|\|entity_id = ANY(…)` (`DapperHistoriaRepository` :67-82, :107) | `ix_audit_events_entity` (entity_type, entity_id) covers the plain columns but not the concatenation expression | **DEFER (HS-10)** — measure on populated `audit_events` (EXPLAIN); an expression index is only justified with demonstrated scan overhead |
| História free-text `ILIKE '%…%'` (:192-199) | leading-wildcard cannot use a btree | **DO NOT TOUCH** — accepted at current scale; pg_trgm GIN would be speculative until user-base data exists |
| Peso per-control leituras N+1 | `uq_peso_leituras_controlo_cm` doubles as the per-control index | present; performance test only with load evidence |
| JobOn calendar `machine_code + status + planned_start_at` | `ix_job_on_machine_planned` | PRESENT — no change |
| BQ trace/movements by trace or occurred; warehouse movements by stock/occurred; audit module+time | present (N03/N09/N25 PERF-01) | PRESENT — no change |
| Peso `(status, control_date)` previous-approved ordering | `ix_peso_controlos_status_date` | PRESENT — no change |

### 8.5 Index inventory (all 81, migration origin)

N01 (11): `ix_access_templates_active`, `ix_internal_users_auth_user_id`,
`ix_internal_users_active`, `ix_internal_users_template_id`,
`ix_audit_events_year`, `ix_audit_events_module_action`, `ix_audit_events_actor`,
`ix_audit_events_entity`, `ix_audit_events_occurred_at`,
`ix_audit_events_job_on_id`, + append-only trigger.
N02 (1): `ix_module_catalog_mirror_order`. N03 (9): `ix_bq_lotes_lifecycle`,
`ix_bq_traces_lote`, `ix_bq_traces_status`, `ix_bq_movements_trace`,
`ix_bq_movements_occurred`, `ix_bq_discrepancies_lote`,
`ix_bq_discrepancies_status`, `ix_bq_lifecycle_history_lote`,
`ix_bq_utilisation_readings_trace`. N04 (5): `ix_tool_lotes_reference`,
`ix_physical_pieces_lote`, `ix_tool_check_rules_lote`,
`ix_tool_check_occurrences_rule`, `ix_tool_check_occurrences_job_on`.
N05 (10): `ix_job_on_production_code`, `ix_job_on_status`,
`ix_job_on_machine_planned`, `ix_job_on_revision_job_on`,
`ix_job_on_component_revision`, `ix_job_on_component_field_component`,
`ix_job_on_component_row_component`, `ix_job_on_verification_component`,
`ix_job_on_audit_event_job_on`, `ix_job_on_field_option_lookup`.
N06 (4): `ix_peso_lotes_reference`, `ix_peso_controlos_reference`,
`ix_peso_controlos_job_on`, `ix_peso_controlos_job_on_revision`,
`ix_peso_controlos_status_date`. N07 (4): `ix_pegamento_controlos_job_on`,
`ix_pegamento_controlos_job_on_revision`, `ix_pegamento_controlos_production`,
`ix_pegamento_medicoes_controlo`. N08 (7): `ix_repair_exits_status`,
`ix_repair_exits_planned_date`, `ix_repair_exit_items_exit`,
`ix_repair_events_exit_item`, `ix_repair_events_internal`,
`ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`.
N09 (5): `uq_warehouse_stock_active_occupation` (partial UNIQUE),
`ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote`,
`ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`.
N10 (5): `ix_tampao_field_values_field`, `ix_tampao_movements_origin`,
`ix_tampao_movements_occurred`, `ix_tampao_planos_configuration`,
`ix_tampao_planos_date`. N14 (1): `ix_pegamento_documentos_controlo`
(**REMOVE — N35**). N15 (1): `ix_pegamento_medicoes_component_tool`.
N19 (1): `ix_tool_usage_records_lote`. N21 (3):
`ix_tampao_configuration_machines_machine`, `ix_tampao_configuration_notes_config`,
`ix_tampao_configuration_machine_event_config`. N22 (1):
`ix_internal_repair_records_revision`. N23 (7): `ix_controlo_sheets_job_on`,
`ix_controlo_sheets_revision`, `ix_controlo_sheets_production`,
`ix_controlo_sheets_status`, `ix_controlo_sheet_items_sheet`,
`ix_controlo_sheet_items_family`, `ix_controlo_sheet_events_sheet`.
N25 (3): `uq_job_on_identity` (partial UNIQUE), `uq_bq_traces_active`
(partial UNIQUE), `ix_audit_events_module_time`. N27 (1):
`ix_internal_user_access_templates_template` (dies with N34). N30 (1):
`ix_article_reference_images_updated_by`. N31 (1):
`ux_internal_user_access_templates_actor` (UNIQUE; dies with N34).

---

## 9. RLS / Policy / Trigger Findings

### 9.1 RLS coverage (verified this session)

- Every application table is RLS-enabled with a single technical policy
  `ba_dmo_app_access` (`FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)`),
  except the two deliberately-named ones (§9.2) and `schema_migrations`
  (RLS on, no policy — migrate CLI only).
- `anon`/`authenticated` have zero table access (guarded REVOKE in N12/N25/
  N27/N29/N31); functional authorization is C#-side only (GLM-DATA-06.3) — no
  per-user/module RLS policies (RLS-05).
- Grants: explicit per-table `ba_dmo_app` DML grants as defense-in-depth plus
  ALTER DEFAULT PRIVILEGES (N01).

### 9.2 Policy naming divergence (RLS-02 / D-15)

- `access_template_profiles_app_access` (N31:115-120) and the (post-N34
  removed) `internal_user_access_templates_app_access` (N27:137-143) vs the
  convention `ba_dmo_app_access` used by N12/N25/N29. Identical semantics.
- After N34, only `access_template_profiles_app_access` remains divergent →
  **N36** renames it to `ba_dmo_app_access` (drop/create, same policy body) and
  documents the convention for all future migrations. SAFE NOW.

### 9.3 N33 privilege surgery (verified coherent)

- `internal_users` column-level SELECT/INSERT/UPDATE: exactly the 8 canonical
  columns, excluding `profile_title` (which N34 removes) and including
  `modules_override` (which N38 removes — **N38 must re-issue the grants without
  it**); DELETE remains table-level. RLS-04 PASS.
- Junction: zero privileges (N33 §2). RLS-03 closed at the chain level; the
  baseline drift D-A is the only residual (§2.3).

### 9.4 Consolidated baseline policy drift (D-A) — resolved by N34

The inert `internal_user_access_templates_app_access` missing from
`consolidated_clean_install.sql` (60 vs 61 policies). Because the junction is
dropped by N34, the N35+ consolidated refresh (§15) no longer needs to emit
either the junction or its policy → drift self-heals. (If N34 were refused, a
one-line policy add would restore strict parity — recorded as fallback.)

### 9.5 Trigger/function inventory (correction + confirmation)

- **3 functions / 19 triggers** (verified by grep over all 33 files): the
  append-only guard fires on 13 fact tables
  (`audit_events`, `bq_movements`, `bq_lifecycle_history`,
  `bq_utilisation_readings`, `pegamento_medicoes`, `repair_events`,
  `warehouse_movements`, `tampao_movements`, `tampao_configuration_notes`,
  `tampao_configuration_machine_event`, `controlo_sheet_events`,
  `tool_usage_records`, `job_on_audit_event`) + 4 revision-family tables
  (`job_on_revision`, `job_on_component`, `job_on_component_field`,
  `job_on_component_row`); `ba_dmo_guard_peso_approved` on `peso_controlos`;
  `ba_dmo_ensure_access_template_profile` on `access_templates` (AFTER INSERT).
- **Correction:** the contract audit's "21 triggers" (§3) is an overcount — the
  chain contains exactly **19** `CREATE TRIGGER` statements and the consolidated
  file matches at 19. Any acceptance criterion using "21" must be corrected to
  19 (chain and baseline both).
- No stale trigger exists on any live path; no function is orphaned.

### 9.6 Findings with actions

| # | Finding | Class / Action |
|---|---|---|
| R1 | Policy naming divergence (9.2) | N36 (SAFE NOW) |
| R2 | Drift D-A inert junction policy | self-heals with N34; fallback one-liner if N34 refused |
| R3 | `RepairAtomicityTests` teardown `DELETE FROM audit_events` vs append-only trigger (RLS-06) | DEFER — fix test teardown when PG-gated suites run (use a test-owned connection/set session_replication_role or truncate via owner role); LIVE VERIFICATION REQUIRED |
| R4 | `schema_migrations` RLS no-policy — by design | DO NOT TOUCH |
| R5 | N33 grants include `modules_override` which N38 drops | N38 re-issues the column-level grants without it (explicit list) |

---

## 10. Legacy / Dormant Objects

Complete inventory (re-verified this session; classifications only — no removal
decision beyond those already taken in D-7…D-16 / Queue A).

### 10.1 Tables

| Object | Status | Evidence | Disposition |
|---|---|---|---|
| `internal_user_access_templates` | LEGACY (quiesced N33) | zero src refs; zero privileges; no external pg_depend | **N34 drop** |
| `peso_comparacao_anterior` | LEGACY (dead mirror) | zero SQL in src; D-9 = REMOVE_LATER | **N37 drop** (data-checked) |
| `tool_check_occurrences` | LEGACY (schema-only) | zero writers; **zero readers post-Queue A** (F17 removed the only reader) | owner decision (PA-01) → candidate N42 drop |
| `tampao_planos` | DORMANT (future-owned) | full implementation, zero routes (D-8) | KEEP (DEFER) |
| `job_on_field_option` | DORMANT (future-owned) | zero consumers (D-7) | KEEP (DEFER) |
| `repair_events` | HISTORY (write-only) | Repair/RI writers; no readers yet | KEEP (legitimate stream; §4/T7) |

### 10.2 Columns

| Column | Status | Evidence | Disposition |
|---|---|---|---|
| `internal_users.profile_title` | LEGACY mirror | zero src refs; N33 quiesced | **N34 drop** |
| `internal_users.modules_override` | LEGACY (dormant) | N27 NULLed; writer removed by Queue A; projections remain | **N38 drop** (code cleanup first) |
| `job_on_revision.image_asset_id` | LEGACY (dormant mirror) | N29 superseded; writers force NULL | **N38 drop** (code cleanup first) |
| `pegamento_controlos.nominal_average` | LEGACY (dormant) | zero reads/writes (PG-10); N16 nominals are the authority | owner decision → candidate N38 group or N42 |
| `bq_traces.sap_end` | LEGACY (never populated) | BQ-08 — column in INSERT list, always bound NULL (correction §5.3/C4); utilisation lives in `bq_utilisation_readings` | owner decision (C4) |
| `bq_discrepancies.resolved_by/resolved_at_utc` | **LIVE** (writer exists) | `UpdateDiscrepancyAsync` sets both (correction §5.3/C5) | DO NOT TOUCH |
| `job_on.production_folder` | no writer (ownership gap) | JA-05/PC-06 | DO NOT TOUCH (code wave F10) |

### 10.3 Runtime-dead code surfaces (already removed by Queue A — verified)

`SetUserModulesOverrideAsync`, `CountActiveAdminsAsync`, `BuildSyncRows`,
`CopyCheckRuleAsync`, `GetOccurrencesByRuleAsync` (+`FerramentasOccurrenceItem`),
`GetActiveStocksAsync`, `GetStockByToolIdAsync`, `SubstituirAsync` +
`ReplaceOccupationAsync` (+`SubstituirRequest`), `GetApprovedControlsForJobOnAsync`,
`GetPreviousApprovedAsync`, `GetChainRootAsync`, `CountLotesAsync`,
`ListMovementsByLoteAsync`, `VoidMovementAsync`, `ListVoidedMovementIdsAsync`,
`GetOpenDiscrepancyForTraceAsync`, `GetLineRepairerDefaultAsync`,
`BqCloseSnapshot`/`FinalCount`, `NavigationArea`, `ModuleKind.FunctionalArea`,
`ControloSheetModuleCatalog.ComponentFamilies`, `PesoModuleCatalog.ReportSubfolderMinLength`,
`TampaoMovement.IsSingleBalance`, `TampaoBalanceKindCodec`,
`PesoCmDecisionCodec`. **Post-Queue A residual (verified this session):**
- one dormant Job On repository method remains: `InsertImageMutationAsync`
  (`IJobOnRepository.cs:68`; `DapperJobOnRepository.cs:559-638`, includes a
  full revision INSERT + current_revision_id UPDATE + `job_on_audit_event`
  INSERT) — **zero callers** (its former call site was replaced; live image
  flows go through `DapperArticleReferenceImageRepository`). Candidate for
  removal in the N38 change set (code cleanup, no DDL).
- doc comments still naming the removed objects (§19), the
  `SchemaMigrationRequiredException` 42703 gate (ADM-11 — code hygiene, DEFER),
  and the still-present dormant write surface of Job On (create/duplicate/
  save-revision/transition/confirm-verification — D-4 Option B, deliberately
  kept).

### 10.4 Documentation drift to refresh alongside the N34+ change sets

`03_MIGRATIONS.md` §2/§3 (stops at N31 — D-D), `02_DATABASE.md`
(`:38,102,121,124,160-177,711,718-720`), `04_DAPPER_INFRASTRUCTURE.md`
(pre-03A junction SQL narratives), `15_ADMIN.md`, `16_USERS_ACCESS.md`,
`18_LOGIN.md`, `19_APPLICATION.md` (stale `TemplateProfileStore` claim :444),
`20_WEB.md` (:245), plus the Peso doc comments referencing
`peso_comparacao_anterior` (§6). Historical `reports/*` and
`AI-CONTEXT/docs/old-design/*` stay verbatim.

---

## 11. Owner Decisions Required

Nothing below is decided by this plan; each item states the question, the
evidence, and the recommended default so the owner can approve or override.
**Queue C items are reproduced for completeness but remain OPEN — no decision
was taken in this task.**

| # | Decision | Question | Evidence | Recommended default | Blocks |
|---|---|---|---|---|---|
| OD-1 | **N34 execution Go** | Approve the physical removal of the two legacy mirrors (drop junction table + `profile_title` + its CHECK), incl. the pre-drop backup and post-deploy parity gates | N34 audit §5/§6; zero src refs; live audit no external dependents | Approve (Option A; no CASCADE) | N34 |
| OD-2 | **D-12 (PC-02)** | Is a Pegamentos measurement without contra costura a valid business record? A: column nullable + domain rule; B: require contra costura, keep NOT NULL | Manual two-axis (20:301-316); Queue C F2 | **A** (nullable + domain completeness rule) | N39 |
| OD-3 | **D-10 (PC-09)** | Protect approved Peso readings with a DB guard on `peso_leituras` (plus service assertion)? | Manual 20:263,481,485; Queue C-free (P1 approved in register) | **A** (trigger + service assertion) | N40 |
| OD-4 | **D-14** | Enforce 1:1 active occupation per warehouse position with a partial unique index? | physical invariant; ON-04; audit §11/§21 | **A** (hard rule; data probe first) | N41 |
| OD-5 | **FA-05** | `physical_pieces.status` state model: CHECK on the 4 technical states / split column / free-text? Keep condition distinct from whereabouts (30:244) | FA-05; Queue C F15 | CHECK on Novo/Reparado/Por reparar/Sucatado (or split) — schema change in a later migration | N42 (or N38 group) |
| OD-6 | **PA-01 (occurrence consolidation)** | Retire `tool_check_occurrences` and its CHECKs/indexes now that the only reader was removed (Queue A) and `job_on_verification_occurrence` is the live materialization? | F16; §5.1/T4 | RETIRE (with N04-family guard), data-checked | N42 |
| OD-7 | **D-9 execution Go** | Drop `peso_comparacao_anterior` (decision D-9 = Option A REMOVE_LATER already recorded; formal execution approval + backup) | D-9; §5.1/T3 | Approve drop with row-count-zero guard | N37 |
| OD-8 | **D-11 execution Go** | Drop `modules_override` + `image_asset_id` after the code cleanup (projections, revision INSERT SQL, 42703 gate) | D-11; §5.3/C1-C2 | Approve drop with null-rate/none-guard upgrades | N38 |
| OD-9 | **nominal_average / sap_end** | Dormant columns: drop (`nominal_average`), or write-on-close vs drop (`sap_end`)? | PG-10; BQ-08; §5.3/C3-C4 | Drop `nominal_average` with N38 group; `sap_end`: write on trace close (code) and keep, or drop — owner choice | N38/N42 |
| OD-10 | **D-15** | Unify RLS policy naming (rename `access_template_profiles_app_access`)? | RLS-02; §9.2 | Approve (technical) | N36 |
| OD-11 | **PC-07 (app_settings)** | Who writes `app_settings.main_documents_output_root`? Admin settings UI vs documented manual seed | Manual 20:526-528; HS-06; Queue C F7 | Document the manual seed now; UI later (no DDL needed) | code only |
| OD-12 | **PC-06 (production_folder)** | Approve the Job On folder writer + auto-resolution (Root/Reference/Production subfolders) replacing out-of-band SQL admin | Manual 20:513-532; JA-05; Queue B F10 (code-only) | Approve code-only change (no DDL; column exists) | code only |
| OD-13 | **D-7 / D-8** | Keep dormant `job_on_field_option` / `tampao_planos` (default A/C) or wire/retire later? | D-7/D-8 registers | KEEP dormant (no action) | none (P2) |
| OD-14 | **job_on.article_reference_id** | Is the logical article-reference link a sanctioned dormant/lookup field (no producer visible)? | N05:21; FK-02 | Confirm as dormant design intent (no action) or provide a producer — no DDL | none |
| OD-15 | **History backfill (D-5)** | Backfill past JobOn/Pegamentos facts into `audit_events`, or forward-only? | D-5 register; historical data volume unknown | Forward-only (recommended); backfill only if História retro-completeness is required | code only |
| OD-16 | **BQ-10 ('fim' movement)** | Is `bq_movements.movement_type='fim'` part of the close contract (no producer today; `BqCloseSnapshot` removed by Queue A)? | BQ-10/BQ-17; §10.3 | Remove the unused enum value from the CHECK (one-line, N35 group) OR keep as reserved — owner choice | N35 (optional) |

---

## 12. Proposed Migration Sequence N34+

**Naming rule (unchanged):** next free name after N33 is **N34**, which this
plan keeps reserved for the audited legacy-mirror removal (the N34 audit design
is preserved and re-validated in §13.1). Later changes are N35, N36, … in
dependency order. No migration file is created by this plan.

**Wave plan:**

| Wave | Migration | Purpose | Destructive | Class |
|---|---|---|---|---|
| 1 | **N34** | legacy access-mirror removal (junction table + `profile_title` + its CHECK) | **YES** | owner Go (OD-1) |
| 2 | **N35** | index hygiene: add `ix_bq_movements_noted_repairer`; drop `ix_pegamento_documentos_controlo`; optional BQ-10 CHECK trim | NO | SAFE NOW |
| 3 | **N36** | D-15 policy rename (`access_template_profiles_app_access` → `ba_dmo_app_access`) + policy-inventory guard | NO | SAFE NOW |
| 4 | **N37** | D-9 drop `peso_comparacao_anterior` | **YES** | owner Go (OD-7); data-checked |
| 5 | **N38** | D-11 drop `internal_users.modules_override` + `job_on_revision.image_asset_id` (+ re-issue N33 grants without the dropped column; optional `nominal_average` if OD-9 approves) | **YES** | owner Go (OD-8); data-checked; code cleanup first |
| 6 | **N39** | D-12 `pegamento_medicoes.contra_costura` DROP NOT NULL (+ domain rule in code, same release) | NO (widening) | owner branch (OD-2) |
| 7 | **N40** | D-10 `peso_leituras` approved-parent append-only guard (new trigger) | NO | owner Go (OD-3) |
| 8 | **N41** | D-14 per-position partial unique on `warehouse_stock` | NO | owner Go (OD-4); data probe first |
| 9 | **N42** | PA-01 drop `tool_check_occurrences` (if OD-6 approves) + optional FA-05 CHECK on `physical_pieces.status` (if OD-5 approves) | **YES** (N42a) / NO (N42b) | owner Go |

**Ordering dependencies:**

1. **N34 before everything else** — it converges the object inventory (the
   only policy-name pair involving the junction disappears), unblocks the
   single consolidated baseline refresh (D-16 Phase G happens ONCE at the final
   state), and its N34-era test re-texts (N34 audit §6.3) make the later
   migrations' guards cleaner. Nothing later references the mirrors.
2. **N35/N36 independent of N34** but kept after it for a single
   post-N34 consolidated refresh and to avoid touching the junction's index/
   policy before its removal confirms zero dependents.
3. **N37/N38 independent of N34** (different objects), but destructive →
   grouped after all additive hardening has landed and after the
   `BA_DMO_TEST_DATABASE` replay proves the fresh-build chain.
4. **N39 before the Pegamentos one-sided measurement code lands** (same
   release; the domain rule must be deployed with the DDL to avoid transient
   nullable-with-no-rule state; N33-style deploy discipline: `migrate` → deploy
   → probes).
5. **N40 after N37** — `peso_leituras` rows are CASCADE-deleted with
   `peso_controlos`; the N37 drop does not touch `peso_leituras`, but the D-10
   guard interacts with the delete path; keeping N40 later lets the N37 guards
   prove the controlo lifecycle first.
6. **N41 needs the data probe first** (§14.6) — no migration otherwise.
7. **N42 last** — it is the only remaining owner/product-gated surface
   removal; by then the consolidated baseline refresh (Phase G) is queued, so
   one refresh per N42 decision keeps D-16 single-pass only if N42 is decided
   before Phase G. (Fallback: refresh twice — N34-state, then N42-state.)
8. **Queue B (code-only) floats independently** — PC-03/PC-04/PC-05/PC-06/
   PC-08/PC-13/PC-14 need no DDL; they should land in the same release train as
   N39/N40 where they touch the same modules (Pegamentos, Peso, BQ).

**Hard rules carried from the audits:** never mix schema changes with
dormant-surface removals in one migration; never drop a table/column without
row-count/parity guards + the owner decision (GLM-DATA-12); N34 stays separate
and gated; no CASCADE unless independently proven necessary (nothing here
requires CASCADE — §13 per-migration proofs).

---

## 13. Per-Migration Design

Fields per migration: Purpose · Objects affected · Destructive · Data backfill ·
Live verification · Code dependency · Test dependency · Clean-install change ·
Rollback · Risk · Owner decision.

### 13.1 N34 — `N34_legacy_access_mirror_removal.sql`

- **Purpose:** physically remove the two legacy access mirrors after N33
  quiescence (SCHEMA-RAT-03B completion; D-1/D-2 final state).
- **Objects affected:** `internal_user_access_templates` (table + PK + both FKs
  + `ix_internal_user_access_templates_template` +
  `ux_internal_user_access_templates_actor` + inert RLS policy + row type +
  TOAST + `assigned_at_utc` default); `internal_users.profile_title` (column,
  attnum 5) + `ck_internal_users_functional_profile` (CHECK).
- **Destructive:** YES (fossil values discarded by design; gated by the N34
  audit + pre-drop backup + parity).
- **Data backfill:** NO (mirror values are dead by design — N33; junction rows
  and fossil `profile_title` values are never read).
- **Live verification:** REQUIRED — catalog absence after deploy; `ba_dmo_app`
  canonical-column privileges unchanged; `schema_migrations` records N34.
- **Code dependency:** NONE (zero src references — re-grepped this session).
- **Test dependency:** N34 audit §6.3 change set: `RemediationGuardTests`
  N32/N33 executed probes → N34-era catalog-absence/42P01/42703 probes; PG seed
  INSERTs drop `profile_title`
  (`ArmazemReturnPostgresTests:176-177`, `JobOnLifecyclePostgresTests:162-163`,
  `RepairAtomicityTests:139-141`); `MigrationDiscoveryTests` family →
  N01…N34 + N34 content guards; `AccessMirrorQuiescenceGuardTests` doc-comment →
  N27…N34.
- **Clean-install change:** D-16 refresh removes the `profile_title` replica
  (consolidated :114) and the junction/CHECK stanza (:1648-1696, incl. the
  post-N33 DROP NOT NULL block) — drift D-A resolves.
- **Rollback considerations:** one-way (destructive). Pre-drop backup of the
  junction table + `internal_users.profile_title` values (pg_dump table/column)
  allows forensic reconstruction; no runtime dependency exists, so rollback is
  recorded-restore rather than schema-restore.
- **Risk:** MEDIUM (destructive but zero dependency). Mitigations: backup;
  03B/N33 parity gates; live catalog probe before/after; N34-era guard tests.
- **Owner decision required:** OD-1 execution Go.
- **Statement set (Option A, no CASCADE — from the N34 audit §5):**
  ```sql
  DROP TABLE IF EXISTS internal_user_access_templates;
  ALTER TABLE internal_users DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile;
  ALTER TABLE internal_users DROP COLUMN IF EXISTS profile_title;
  ```
  (Explicit constraint drop before column drop; whole-script, own transaction,
  no BEGIN/COMMIT, `IF EXISTS` guards, no CASCADE — all dependency directions
  proven zero: live audit §2, repository §3, fresh-build replay §4.)

### 13.2 N35 — index hygiene + optional BQ-10 CHECK trim

- **Purpose:** close BQ-16 (additive) and remove the provably redundant
  `ix_pegamento_documentos_controlo`; optionally trim the `'fim'` value from
  `ck_bq_movements_type` if OD-16 approves.
- **Objects affected:** `bq_movements` (add index `ix_bq_movements_noted_repairer`);
  `pegamento_documentos` (drop `ix_pegamento_documentos_controlo`); optionally
  `ck_bq_movements_type` (drop/re-add without `'fim'`).
- **Destructive:** NO (index add/drop is object-level; the optional CHECK trim
  is a constraint rewrite with proven zero `'fim'` rows — data probe §14.7).
- **Data backfill:** NO.
- **Live verification:** REQUIRED (EXPLAIN the repairer-filtered BQ history
  before/after; `\d bq_movements` / `\d pegamento_documentos` confirm index set).
- **Code dependency:** NONE.
- **Test dependency:** guard asserting the index exists / the redundant index
  absent (catalog probe in `RemediationGuardTests` style); MigrationDiscovery
  list N01…N35.
- **Clean-install change:** N35 objects added to the refreshed consolidated
  file (drop the redundant index CREATE, add the new index).
- **Rollback considerations:** fully reversible (`CREATE INDEX`/`CREATE INDEX`
  back) with zero behavior impact.
- **Risk:** LOW.
- **Owner decision required:** OD-16 only (for the optional CHECK trim).

### 13.3 N36 — D-15 RLS policy rename

- **Purpose:** unify policy naming (`access_template_profiles_app_access` →
  `ba_dmo_app_access`).
- **Objects affected:** one RLS policy on `access_template_profiles`.
- **Destructive:** NO (drop/create same-semantics policy; no data).
- **Data backfill:** NO. **Live verification:** REQUIRED (policy inventory
  equals 60 after N34+N36 with today's 61-table set minus the junction).
- **Code dependency:** NONE (no runtime code names policies).
- **Test dependency:** policy-inventory guard (61 table set → 60 policies after
  N34; name set check `ba_dmo_app_access` everywhere).
- **Clean-install change:** policy stanza renamed in the refreshed baseline.
- **Rollback:** YES (re-create the old name — cosmetic).
- **Risk:** LOW. **Owner decision:** OD-10 (technical).

### 13.4 N37 — D-9 drop `peso_comparacao_anterior`

- **Purpose:** remove the dead previous-approved mirror table (D-9 Option A).
- **Objects affected:** `peso_comparacao_anterior` (PK + FK to
  `peso_controlos` ON DELETE CASCADE).
- **Destructive:** YES.
- **Data backfill:** NO (row-count-zero guard required — probe §14.3).
- **Live verification:** REQUIRED — row count = 0; zero SQL refs (already
  proven static); FK orphan check for `previous_peso_controlo_id` none
  (table removed with no dependents).
- **Code dependency:** none (doc comments refreshed — §10.4).
- **Test dependency:** N37-era catalog-absence probe; doc-comment sweep.
- **Clean-install change:** remove the table replica from the refreshed
  baseline.
- **Rollback:** one-way; table DDL is in N06 history — reconstructible empty,
  no data ever existed.
- **Risk:** LOW (empty by construction). **Owner decision:** OD-7.

### 13.5 N38 — D-11 drop `modules_override` + `image_asset_id`

- **Purpose:** remove the two dormant legacy columns (D-11 Option A).
- **Objects affected:** `internal_users.modules_override`; `job_on_revision.image_asset_id`;
  re-issue of the N33 `internal_users` column-level grants **without**
  `modules_override`; optional `pegamento_controlos.nominal_average` (OD-9).
- **Destructive:** YES.
- **Data backfill:** NO — probes: `modules_override` all-NULL (N27 guarantees;
  confirm live §14.4); `image_asset_id` NULL/absent on all current
  `article_reference_images`-authoritative flows (probe §14.5);
  `nominal_average` null-rate probe if included.
- **Live verification:** REQUIRED (null-rate probes; catalog after drop;
  `ba_dmo_app` can still SELECT/INSERT/UPDATE the remaining 7 canonical columns
  of `internal_users` — the re-issued grants must be asserted).
- **Code dependency:** **REQUIRED FIRST** — remove `u.modules_override::text` from
  `DapperAdminRepository` (:52) and `DapperInternalUserRepository` (:37)
  projections; remove `image_asset_id` from `DapperJobOnRepository` revision
  INSERT/UPDATE SQL (:232,:288,:577,:743,:1041); retire the
  `SchemaMigrationRequiredException` 42703 gate (replaced by explicit
  fail-fast comments); remove `nominal_average` reads if any.
- **Test dependency:** regression on admin projection + identity + revision
  save (fakes/contracts); N38-era catalog-absence probes for both columns.
- **Clean-install change:** columns removed from the refreshed baseline;
  column-level grants updated.
- **Rollback:** one-way; inert columns — reconstruction trivial (DDL in
  N05/N26 history; values NULL by design).
- **Risk:** LOW-MEDIUM (code cleanup must land first or admin pages 42703).
  **Owner decision:** OD-8 (+OD-9 for optional column).

### 13.6 N39 — D-12 `contra_costura` DROP NOT NULL

- **Purpose:** align the schema with the one-sided-measurement domain rule
  (D-12 branch A — pending OD-2).
- **Objects affected:** `pegamento_medicoes.contra_costura` (nullability
  only); no CHECK change.
- **Destructive:** NO (widening; existing rows unchanged).
- **Data backfill:** NO (probe: existing rows all NOT NULL — no null present
  today, so no backfill; the domain rule governs future writes).
- **Live verification:** REQUIRED (create a one-sided measurement via API
  against a migrated DB; confirm no 23502 — the Queue A UoW + codec path).
- **Code dependency:** same release — `PegamentoControlo` one-sided rule
  enforcement (measurement must have `costura`; `contra_costura` optional with
  explicit semantics); `DapperPegamentoRepository` already binds nullable.
- **Test dependency:** unit rule tests; PG-gated one-sided-measurement test
  (extend `PegamentoPersistencePostgresTests`).
- **Clean-install change:** column DEF NULL in the refreshed baseline.
- **Rollback:** YES (re-apply NOT NULL after a null-absence check).
- **Risk:** LOW (widening). **Owner decision:** OD-2 (branch).

### 13.7 N40 — D-10 `peso_leituras` approved-parent guard

- **Purpose:** close the silent-rewrite path of readings under an approved
  control (D-10 Option A).
- **Objects affected:** `peso_leituras` — new trigger
  `trg_peso_leituras_approved_guard` using a new/existing guard function
  (pattern: `ba_dmo_guard_peso_approved` extended or a sibling function that
  raises on UPDATE/DELETE when the parent `peso_controlos.status='aprovado'`).
- **Destructive:** NO (additive).
- **Data backfill:** NO. **Live verification:** REQUIRED (attempt a DELETE on
  an approved control's readings as `ba_dmo_app` → denied; reopen flow still
  works via the audited path).
- **Code dependency:** service assertion in `UpdateControlAsync` (primary
  gate); DB trigger as backstop.
- **Test dependency:** PG-gated probe (existing `RemediationGuardTests` style);
  unit assertion tests.
- **Clean-install change:** trigger block in the refreshed baseline.
- **Rollback:** YES (drop trigger).
- **Risk:** LOW-MEDIUM (a legitimate legacy edit of approved readings would now
  fail — none known; service paths audited). **Owner decision:** OD-3.

### 13.8 N41 — D-14 per-position partial unique

- **Purpose:** physically enforce "at most one active occupation per position"
  (D-14 Option A).
- **Objects affected:** `warehouse_stock` — new partial unique
  `uq_warehouse_stock_active_position ON warehouse_stock
  (warehouse_location_id) WHERE released_at_utc IS NULL`.
- **Destructive:** NO (additive).
- **Data backfill:** NO. **Live verification:** REQUIRED — §14.6 probe must
  return zero violating active rows BEFORE the migration; on violation the
  migration is blocked (fail-closed) and reported.
- **Code dependency:** none required (existing FOR UPDATE on live paths remains
  the concurrency backstop; the index adds the DB invariant).
- **Test dependency:** PG-gated two-lot-same-position rejection test; unit
  occupancy tests unchanged.
- **Clean-install change:** index in the refreshed baseline.
- **Rollback:** YES (drop index).
- **Risk:** LOW (additive; blocked on data). **Owner decision:** OD-4.

### 13.9 N42 — PA-01 / FA-05 (owner-gated group)

- **Purpose:** (a) retire `tool_check_occurrences` (PA-01) if OD-6 approves —
  drop table + its 2 CHECKs + 2 indexes (N04 objects; `job_on_verification_occurrence`
  remains the live materialization); (b) optionally add the `physical_pieces.status`
  CHECK (FA-05) if OD-5 approves.
- **Destructive:** (a) YES — row-count-zero guard (probe §14.8); (b) NO.
- **Data backfill:** none for (a) (empty by construction — no writers ever);
  (b) requires a value-reconciliation probe first (condition codec values in
  `status` must all be in the CHECK set; the probe enumerates distinct values).
- **Live verification:** REQUIRED for both probes.
- **Code dependency:** none for (a) (only reader removed by Queue A); (b) none
  (codec already writes the target values — verify via probe).
- **Test dependency:** N42-era catalog-absence probes; status-codec guard.
- **Clean-install change:** refresh removes the N04 table block / adds the
  CHECK.
- **Rollback:** (a) one-way but empty-table-reconstructible; (b) reversible.
- **Risk:** LOW. **Owner decision:** OD-6, OD-5.

---

## 14. Live Read-Only Verification Queries

All queries are **SELECT-only** (no DDL/DML/functions/transaction control) —
run as `ba_dmo_app`, a read-only service role, or the migration owner on the
live Supabase project; RLS is bypassed for owner/service roles.
Follow the `schema_rationalization_03A_live_parity.sql` convention: a labelled
query returning ZERO rows = PASS; 1+ rows = FAIL enumerating offenders.

### 14.1 Deployed-DDL drift vs chain final state (pre-N34)

```sql
-- 14.1.1  All 61 app tables present (expect exactly 61 rows; junction + profiles included).
SELECT tablename FROM pg_tables
 WHERE schemaname = 'public'
   AND tablename <> 'schema_migrations'
 ORDER BY tablename;

-- 14.1.2  N31/N33 posture: profile_title NULLABLE; junction privileges NONE.
SELECT a.attname, a.attnotnull
  FROM pg_attribute a
 WHERE a.attrelid = 'public.internal_users'::regclass
   AND a.attname = 'profile_title';

SELECT grantee, privilege_type
  FROM information_schema.role_table_grants
 WHERE table_name = 'internal_user_access_templates' AND grantee = 'ba_dmo_app';

-- 14.1.3  Column-level grants on internal_users (SELECT — expect 8 columns, no profile_title).
SELECT privilege_type, column_name
  FROM information_schema.role_column_grants
 WHERE table_name = 'internal_users' AND grantee = 'ba_dmo_app'
 ORDER BY privilege_type, column_name;

-- 14.1.4  RLS enabled on every app table (expect 0 rows).
SELECT c.relname
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
   AND NOT c.relrowsecurity;
```

### 14.2 N34 pre-flight (mirror usage proof)

```sql
-- 14.2.1  Incoming FKs to the junction (expect 0).
SELECT conname FROM pg_constraint
 WHERE confrelid = 'public.internal_user_access_templates'::regclass;

-- 14.2.2  Catalog dependencies on the junction / on profile_title (pg_depend,
--         expect only self-owned objects).
SELECT d.classid::regclass, d.objid, d.refclassid::regclass, d.refobjid
  FROM pg_depend d
 WHERE d.refobjid IN ('public.internal_user_access_templates'::regclass,
                      'public.internal_users'::regclass);

-- 14.2.3  Junction row count + fossil profile_title null-rate (informational).
SELECT COUNT(*) AS junction_rows FROM public.internal_user_access_templates;
SELECT COUNT(*) AS total_users,
       COUNT(profile_title) AS with_profile_title,
       COUNT(*) - COUNT(profile_title) AS null_profile_title
  FROM public.internal_users;

-- 14.2.4  Views/matviews/functions referencing either mirror (expect 0 rows).
SELECT DISTINCT dependent_ns.nspname, dependent_view.relname
  FROM pg_depend
  JOIN pg_rewrite ON pg_depend.objid = pg_rewrite.oid
  JOIN pg_class AS dependent_view ON pg_rewrite.ev_class = dependent_view.oid
  JOIN pg_namespace dependent_ns ON dependent_view.relnamespace = dependent_ns.oid
 WHERE pg_depend.refobjid IN ('public.internal_user_access_templates'::regclass,
                              'public.internal_users'::regclass);
```

### 14.3 N37 pre-flight (`peso_comparacao_anterior`)

```sql
SELECT COUNT(*) AS comparison_rows FROM public.peso_comparacao_anterior;  -- expect 0
SELECT COUNT(*) AS fk_refs
  FROM pg_constraint
 WHERE confrelid = 'public.peso_comparacao_anterior'::regclass;           -- expect 0 (from other tables)
```

### 14.4 N38 pre-flight (`modules_override`)

```sql
SELECT COUNT(*) AS nonnull_overrides
  FROM public.internal_users
 WHERE modules_override IS NOT NULL;                                      -- expect 0
```

### 14.5 N38 pre-flight (`image_asset_id`)

```sql
SELECT COUNT(*) AS legacy_images
  FROM public.job_on_revision r
  JOIN public.job_on j ON j.current_revision_id = r.job_on_revision_id
 WHERE r.image_asset_id IS NOT NULL;                                      -- expect 0 (N29 promoted/NULLed)
```

### 14.6 N41 pre-flight (warehouse 1:1 violations)

```sql
-- Expect ZERO rows: two distinct tool_lotes active in the same position.
SELECT warehouse_location_id, COUNT(*) AS active_occupations
  FROM public.warehouse_stock
 WHERE released_at_utc IS NULL
 GROUP BY warehouse_location_id
HAVING COUNT(*) > 1;
```

### 14.7 N35 pre-flight (BQ-10 'fim' + redundant index)

```sql
SELECT COUNT(*) FROM public.bq_movements WHERE movement_type = 'fim';     -- expect 0 for CHECK trim
SELECT indexname FROM pg_indexes WHERE tablename = 'pegamento_documentos'; -- expect UNIQUE + redundant pair today
```

### 14.8 N42 pre-flight (`tool_check_occurrences`, `physical_pieces.status`)

```sql
SELECT COUNT(*) AS occurrence_rows FROM public.tool_check_occurrences;    -- expect 0
SELECT DISTINCT status FROM public.physical_pieces;                        -- enumerate values vs proposed CHECK set
SELECT COUNT(*) AS fork_rows
  FROM public.physical_pieces
 WHERE status IS NULL OR status NOT IN ('Novo','Reparado','Por reparar','Sucatado');
```

### 14.9 Queue-B-relevant health probes (evidence for later waves)

```sql
-- 14.9.1  audit_events coverage gaps (expect 0 rows for module jobon / pegamentos).
SELECT module_id, COUNT(*) FROM public.audit_events GROUP BY module_id ORDER BY module_id;

-- 14.9.2  app_settings contents (informational — expect 0 or a manual seed).
SELECT setting_key, setting_value FROM public.app_settings ORDER BY setting_key;

-- 14.9.3  null-rates / usage for the previously-suspect columns (informational).
--         sap_end is never populated (C4); resolved_by/resolved_at_utc ARE
--         written by the resolve flow (C5) — this query sizes both.
SELECT COUNT(*) FILTER (WHERE sap_end IS NOT NULL)    AS sap_end_set,
       COUNT(r.*)                                     AS resolved_discrepancies
  FROM public.bq_traces t
  LEFT JOIN public.bq_discrepancies r ON r.bq_trace_id = t.bq_trace_id
                                     AND r.resolved_by IS NOT NULL;

-- 14.9.4  FK orphan candidates for the logical-uuid links (informational).
SELECT COUNT(*) AS orphan_article_refs
  FROM public.job_on j
  LEFT JOIN public.article_reference_images i ON i.reference_code = upper(btrim(j.article_reference_id::text))
 WHERE j.article_reference_id IS NOT NULL AND i.reference_code IS NULL;
```

### 14.10 Post-deploy catalog probes (per migration)

```sql
-- Generic pattern per destructive migration: object absence + privilege intact.
SELECT to_regclass('public.' || 'internal_user_access_templates') AS junction_still_present;  -- expect NULL after N34
SELECT COUNT(*) AS profile_title_columns
  FROM information_schema.columns
 WHERE table_name = 'internal_users' AND column_name = 'profile_title'; -- expect 0 after N34
SELECT grantee, privilege_type, column_name
  FROM information_schema.role_column_grants
 WHERE table_name = 'internal_users' AND grantee = 'ba_dmo_app';        -- 7 canonical columns after N38
```

---

## 15. Clean-Install Equivalence Plan

**Goal:** prove that the two build paths produce *structurally equivalent*
databases at every stable checkpoint (today at N33; later at each destructive
migration N34/N37/N38/N42 and finally at the post-N42 end state):

- **Path A — chain:** empty database → `migrate` CLI applies N01…Nxx
  (whole-script, SHA-256, record-after-success).
- **Path B — consolidated:** empty database → execute
  `database/consolidated_clean_install.sql` (self-contained file).

**Equivalence definition (allow-list explicit):** the two databases are
equivalent when their **catalog snapshots** are identical modulo the following
documented allow-list:

1. `schema_migrations` content differs by construction (Path A records each
   file; Path B records `consolidated_clean_install` or nothing — the file is
   the baseline). Only its **existence** (RLS on, no policy) is compared.
2. `GRANT USAGE ON SCHEMA public` (N01) is intentionally not emitted by the
   consolidated file (drift D-B) — functionally inert on stock PostgreSQL.
3. `ALTER DEFAULT PRIVILEGES` (N01) is comment-kept, not executed (drift D-B) —
   verified not to matter because every table receives explicit grants.
4. N27/N28/N29/N32 reconciliation DML is not reproduced by the consolidated
   file (documented at consolidated :34-36) — on an **empty** database all
   those statements are no-ops, so the final catalog is unaffected; on partial
   databases the chain migrations remain the authority (never use the
   consolidated file on a non-empty database).
5. Ownership/ACL on `schema_migrations` and the `ba_dmo_migrate` role's
   objects may differ by execution role; application-facing grants are compared
   on the `ba_dmo_app` role.
6. After N34: the junction's inert policy (drift D-A) is gone from both paths —
   the drift disappears by construction.

**Protocol (executable, CI-able):**

1. **Scratch A:** `createdb ba_dmo_equiv_a` → run `migrate` (or apply
   `database/migrations/*.sql` in canonical order) → stop.
2. **Scratch B:** `createdb ba_dmo_equiv_b` → `psql -f database/consolidated_clean_install.sql` → stop.
3. **Canonical snapshot per DB** (single query set, run on both, output to
   files, then `diff -u`):

```sql
-- Tables + columns + types + nullability + defaults + identity.
SELECT c.relname AS table_name,
       a.attname AS column_name,
       format_type(a.atttypid, a.atttypmod) AS data_type,
       a.attnotnull,
       pg_get_expr(d.adbin, d.adrelid) AS default_expr
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
LEFT JOIN pg_attrdef d ON d.adrelid = c.oid AND d.adnum = a.attnum
WHERE n.nspname = 'public' AND c.relkind = 'r'
ORDER BY c.relname, a.attnum;

-- Constraints (PK/FK/UNIQUE/CHECK — normalized definition text).
SELECT conrelid::regclass::text AS table_name,
       conname, contype,
       pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE connamespace = 'public'::regnamespace
ORDER BY conrelid::regclass::text, conname;

-- Indexes.
SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;

-- Triggers.
SELECT event_object_table, trigger_name, action_statement
FROM information_schema.triggers
WHERE trigger_schema = 'public'
ORDER BY event_object_table, trigger_name;

-- Functions.
SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS args,
       pg_get_function_result(p.oid) AS result,
       p.prosrc
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public'
ORDER BY p.proname, args;

-- RLS.
SELECT c.relname, c.relrowsecurity
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r'
ORDER BY c.relname;

-- Policies.
SELECT polrelid::regclass::text, polname, pg_get_expr(polqual, polrelid) AS using_expr,
       pg_get_expr(polwithcheck, polrelid) AS check_expr
FROM pg_policy
WHERE polrelid IN (SELECT oid FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public')
ORDER BY 1, 2;

-- ba_dmo_app privileges (tables, columns, sequences).
SELECT grantee, table_schema, table_name, privilege_type
FROM information_schema.role_table_grants
WHERE grantee = 'ba_dmo_app' AND table_schema = 'public'
ORDER BY table_name, privilege_type;
SELECT grantee, table_name, column_name, privilege_type
FROM information_schema.role_column_grants
WHERE grantee = 'ba_dmo_app' AND table_schema = 'public'
ORDER BY table_name, column_name, privilege_type;
```

4. **Diff:** normalize (strip schema_migrations rows, apply the allow-list) →
   `diff` must be empty. Any difference outside the allow-list is a defect in
   either the chain or the consolidated file and blocks the checkpoint.
5. **Behavioral cross-check:** run the DB-less test suite against both
   snapshots' guards (catalog probes), and the PG-gated suites
   (`RemediationGuardTests` etc.) against Path A (chain) — Path B's object
   identity is covered by the catalog diff.
6. **Per-checkpoint cadence:** today (N33) — one run to baseline the protocol;
   after N34 (mandatory — the destructive checkpoint that changes the mirror
   surface); after N37/N38; final run at the post-N42 end state (Phase G of
   D-16: regenerate the consolidated file from the chain end state, re-run the
   protocol, archive the equivalence evidence next to the baseline).

**Failure handling:** any catalog difference = STOP; do not ship the next
migration until the drift is identified as chain-vs-file and fixed on the
**consolidated file side** (migrations are immutable), re-run the protocol.

---

## 16. Fresh Database Verification Plan

Applies to every new migration N34+/N35/… (executed in the N34-era verification
protocol; N34 audit §6.6 is the model).

1. **Build:** `dotnet build BA-DMO.sln -c Debug` → 0 errors.
2. **DB-less suites:** `BA.Dmo.UnitTests` (all modules on fakes) +
   `BA.Dmo.IntegrationTests` DB-less suites (migration discovery/content guards,
   access guards, web guards, projection tests). Expected: 660/0 unit;
   integration as §2.1 minus PG-gated skips; the pre-existing
   `ShellRoutingTests.Scenario7` failure is owner-declared unrelated debt and
   must NOT be "fixed" in schema work.
3. **Fresh-chain replay** (requires disposable PostgreSQL or
   `BA_DMO_TEST_DATABASE`): apply N01…Nxx to an empty database via the runner;
   assert `schema_migrations` records the full family in order.
4. **Post-replay probes**: N34-era `RemediationGuardTests` probes (catalog
   absence of dropped objects; `42P01`/`42703` behavioral probes connecting as
   `ba_dmo_app`); canonical-column privilege assertion on `internal_users`;
   N35/N36/N37/N38/N39/N40/N41/N42-specific probes per §13.
5. **Two-path equivalence run** (§15) at each destructive checkpoint.
6. **Application smoke:** thumbnail set: admin login, user create (no mirror
   columns), template edit (profile propagates via authority), one creation in
   each module family whose migration touched it (pegamento one-sided
   measurement after N39; peso approved-control reading protection after N40;
   warehouse dual-return rejection after N41).
7. **Success criteria:** all suites green (except documented pre-existing
   failure), probes pass, equivalence diff empty, no `schema_migrations`
   mismatch, no unexpected `PostgresException` in the smoke run.

---

## 17. Production Parity Plan

1. **Deploy order (unchanged discipline):** `migrate` (the whole N01…Nxx family,
   including the newest) → deploy the application build → run live parity →
   open to users. N33-before-first-write stays a hard rule (ADM-14); N34 and
   every later destructive migration follow the same order with a **pre-deploy
   backup** (pg_dump of the affected tables/columns) and a **post-deploy
   parity run**.
2. **Live provenance:** the live project applies migrations via Supabase-CLI
   (`supabase_migrations.schema_migrations`) — land N34+ through that exact
   path (mirroring the documented N32 application path:
   `reports/schema_rationalization_n32_application_path.md`); reconcile
   `schema_migrations` vs the CLI's record before/after each destructive
   migration.
3. **Parity script revision:** extend `reports/schema_rationalization_03A_live_parity.sql`
   into an N34-era script (N34 audit §6.6.3):
   - the 03B flip ("users without junction rows are the norm") becomes
     "junction rows and profile_title values are impossible (objects absent)";
   - add catalog-absence checks (§14.10), column-privilege checks (§14.1.3),
     and per-migration probes (§14.2…14.8);
   - confirm `supabase_migrations.schema_migrations` records the removal.
4. **Deployed-DDL drift gate:** before N34 execution, run §14.1 (drift vs chain
   final state) and §14.2 (mirror usage proof). Any PASS-negative → STOP and
   reconcile before proceeding.
5. **Rollback posture for destructive steps:** no rely on in-flight rollback;
   the pre-deploy backup plus empty-table/null-rate evidence (§14.3-14.8) is the
   recovery contract (consistent with GLM-DATA-12 and prior phases). N35/N36/
   N39/N40/N41 remain reversible in place.
6. **Effective-access validation** after N34: Admin / Operador-Controlador /
   Responsável each land on their expected surface (03A §4 pattern re-run) —
   proves the authority chain survived the mirror drop.

---

## 18. Final Baseline Acceptance Criteria

The post-rationalization baseline is ACCEPTED when all of the following hold:

1. **Chain integrity:** N01…N42 (as finally numbered) apply cleanly from empty;
   `MigrationDiscoveryTests` asserts the exact family; no checksum drift; no
   migration file was ever edited (N01-N33 hash-pinned conceptually).
2. **Schema end state:** 59 application tables (− junction, −
   `peso_comparacao_anterior`; 58 if N42a also retires
   `tool_check_occurrences`); columns `profile_title`, `modules_override`,
   `image_asset_id` absent; 3 functions (4 if N40 introduces a dedicated
   readings guard function); 19 → 20 triggers (N40); index deltas per §1/§8
   (−2 N34, −1 N35, +1 N35, −1 N37, +1 N41, optional −2 N42a) with no other
   index changes; RLS enabled on every remaining app table; exactly one policy
   name convention (`ba_dmo_app_access`); column-level grants on
   `internal_users` list the surviving columns only (7 after N38).
3. **Equivalence:** §15 protocol diff empty for the final checkpoint; the
   consolidated baseline is regenerated once to this end state (D-16 Phase G)
   with a corrected header and no stale citations (D-C fixed).
4. **Behavior:** unit 660/0 (or ≥current with zero new failures); integration
   ≥314 pass with only the documented pre-existing failure; PG-gated suites
   green against `BA_DMO_TEST_DATABASE` (incl. N34-era probes); live parity
   script green on the deployed database.
5. **Zero runtime mirror/surprise surface:** grep gate —
   `internal_user_access_templates`, `profile_title` in `src/` = 0 matches
   (guards enforce forever); `peso_comparacao_anterior`,
   `tool_check_occurrences`, `modules_override`, `image_asset_id` absent from
   `src/` SQL (post-N38/N42); no writes to `app_settings` except the approved
   surface (PC-07 decision outcome).
6. **Ownership:** every destructive change carries an owner decision ID (OD-x)
   recorded in this report's §11 and the pre-execution backup + parity evidence
   archived in `reports/`.
7. **Docs:** Maps 02/03/04/15/16/18/19/20 refreshed to the final state; stale
   Peso doc comments corrected; no report references a removed object as live.

---

## 19. Deferred Items

| # | Item | Why deferred | Unblock condition |
|---|---|---|---|
| Df-1 | **Queue B code wave** (PC-03 audit casts+Normalize; PC-04/PC-05 emitters; PC-06 production_folder writer + SELECT fixes; PC-08 return status; PC-13 balances fidelity; PC-14 expected_qty; PC-09 service assertion) | FUNCTIONAL_ALIGNMENT changes — Gate §4-B; no DDL required | Owner queue sign-off; land with the matching migrations (N39/N40 releases) |
| Df-2 | **HS-10** — História group/expression index | no load evidence; cannot use existing indexes | EXPLAIN on populated `audit_events`; only add an expression index with demonstrated cost |
| Df-3 | **MC-02** — N28/N29/N30 inner BEGIN/COMMIT | immutable history; tolerated today (25P01 warning-level no-op) | real-PG migration-execution test added (test-only, no file edit); document in runbook |
| Df-4 | **RLS-06** — test teardown `DELETE FROM audit_events` | only bites when PG-gated suites run | fix teardown (test change) when `BA_DMO_TEST_DATABASE` runs |
| Df-5 | **PESO-05 / Guid.Empty FK sentinel** | code pre-validation; no DDL | service-level guard (owner/Queue C-lite) |
| Df-6 | **D-7 / D-8** — wire or retire `job_on_field_option` / `tampao_planos` | product scope (P2) | product decision (no action today) |
| Df-7 | **U2 / OD-14** — `job_on.article_reference_id` producer | no producer visible; logical link by design | owner confirms dormant-by-design or provides a writer |
| Df-8 | **ADM-11** — `SchemaMigrationRequiredException` 42703 gate | vestigial; removed naturally by N38 code cleanup | land with N38 change set |
| Df-9 | **BQ-10 `'fim'` movement value** | no producer; BqCloseSnapshot removed | OD-16 (optional CHECK trim in N35) |
| Df-10 | **History backfill (D-5)** | forward-only recommended | owner decision if retro-completeness required |
| Df-11 | **Consolidated file privilege-note items D-B/D-C** | intentional/harmless | fix the stale citation (D-C) in the Phase G refresh |
| Df-12 | **ShellRoutingTests.Scenario7** | owner-declared unrelated Admin-nav markup debt | separate frontend fix; never mixed into schema work |

---

## 20. Explicit DO-NOT-TOUCH List

**Migration files and mechanics:**
1. `database/migrations/N01…N33` — immutable history, never edited (forward-only
   discipline; hashes pinned by the runner).
2. `MigrationRunner`/`MigrationDiscovery`/checksum mechanics — no change beyond
   the family-list test extension.
3. The N28/N29/N30 transaction-control files — tolerated (Df-3), not rewritten.

**Healthy schema objects (verified §3/§4):**
4. `audit_events` and its append-only trigger + 8 indexes (incl. PERF-01) —
   single global audit authority.
5. The four append-only revision-family tables + their N25 triggers — immutable
   attribution anchors for Peso/Pegamentos/Controlo/RI.
6. `access_templates`, `internal_users` (minus N34/N38 columns),
   `access_template_profiles` + `ba_dmo_ensure_access_template_profile`
   trigger + CHECK — the D-1/D-2 authority chain (N32 fail-closed, N33
   quiescence posture, column-level grants).
7. `module_catalog_mirror` — derived read-model (D-6 Option A), synchronizer
   sole writer.
8. All event/fact streams: `bq_*` (incl. `bq_movements`/`bq_lifecycle_history`/
   `bq_utilisation_readings`), `warehouse_movements`, `tampao_movements` +
   `tampao_configuration_*` streams, `tool_usage_records`,
   `pegamento_medicoes`, `repair_events`, `controlo_sheet_events`,
   `job_on_audit_event` — append-only, never merged into `audit_events`.
9. Tampões normalized layering (field defs/values/configurations/saldos/
   movements/machines/notes/events, and dormant `tampao_planos`) — N21 owner
   decision; no consolidation.
10. `warehouse_locations`/`warehouse_stock`/`warehouse_movements` —
    current+history split; partial unique kept (N41 adds the per-position
    invariant, does not reshape).
11. `repairers`/`repairer_repair_types`/`line_repairer_defaults` —
    canonical registry + joins (TD-15).
12. `peso_references`/`peso_lotes`/`peso_controlos` (approved-immutable guard)/
    `peso_leituras`/`peso_day_approvals`/`peso_settings` — layered control
    model; `peso_day_approvals` write-only is by design (approval surfaces via
    `approval_log`).
13. `pegamento_controlos` (Queue A UoW + PC-01 fix) / `pegamento_documentos`
    (1:1 UNIQUE) — only `ix_pegamento_documentos_controlo` is dropped (N35);
    `contra_costura` nullability change is the only column-level edit planned
    (N39, owner-gated).
14. `controlo_sheets`/`controlo_sheet_items`/`controlo_sheet_events` — kept;
    CASCADE semantics unchanged.
15. `tool_references`/`tool_lotes`/`tool_check_rules`/`physical_pieces`
    (identity + FKs + uniques) — only the FA-05 CHECK addition is optional
    (N42, owner-gated); `tool_check_occurrences` removal (N42) is the only
    table drop in this family and is owner-gated.
16. `article_reference_images` + N29 RLS/policy/grants + N30 index — image
    authority (only `job_on_revision.image_asset_id` is dropped in N38).
17. `jobon_user_current` upsert pattern.
18. `internal_repair_records` (CM/MF CHECK validated N28; corrections as new
    rows) — no UPDATE/DELETE path changes.
19. `schema_migrations` RLS posture (enabled, no policy).
20. Role model (`ba_dmo_app` NOLOGIN technical role, `ba_dmo_migrate` DDL role,
    anon/authenticated zero access) and all privilege statements except the
    N38 grant re-issue.
21. RLS policies' runtime semantics — only the D-15 **name** change (N36) and
    the junction's removal with N34.
22. The consolidated file's N29/N31/N33 stanzas in their hardened form — to be
    *reproduced* in the Phase G refresh, not re-engineered.
23. PJM/PDF renderers and filesystem image provider contracts — code-only
    consumers; no schema coupling changes.
24. Historical reports/audit artifacts under `reports/` and
    `AI-CONTEXT/docs/old-design/` — verbatim records, never rewritten
    (new reports supersede, they do not edit).

---

## Audit validation checklist

- ✅ All 33 migrations read in full or exhaustively grepped; final-state counts
  re-derived (62 tables, 3 functions, **19** triggers — correction vs the
  "21" in the contract audit, 81 indexes, 61 policies).
- ✅ Consolidated baseline read in full and diffed against the chain final
  state (parity confirmed; residual drifts D-A…D-D enumerated).
- ✅ Dapper/source readers-writers matrix re-verified across all named review
  areas (mirrors = 0 refs; app_settings writers = 0; bq discrepancy resolution
  columns LIVE — correcting audit BQ-04; `InsertImageMutationAsync` residual
  dead code; FOR UPDATE sites confirmed).
- ✅ Source-of-truth matrix completed for every major business concept with
  reader/writer/conflict/action rows.
- ✅ N34 legacy-mirror removal re-evaluated against the Queue A baseline —
  design preserved (Option A, no CASCADE), N34 name reservation honoured.
- ✅ Forward-only migration sequence N34…N42 designed with ordering
  dependencies, per-migration fields, risk classes, and live read-only probes.
- ✅ Clean-install equivalence protocol, fresh-DB verification plan, production
  parity plan, and acceptance criteria specified.
- ✅ NO implementation, NO DDL/DML, NO database mutation, NO Queue B, NO Queue C
  decision; the only artifact produced is this report.

— End of report.