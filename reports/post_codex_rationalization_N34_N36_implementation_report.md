# POST-CODEX RATIONALIZATION — N34 / N35 / N36 IMPLEMENTATION REPORT

> **Type:** IMPLEMENTATION — the first SAFE STRUCTURAL RATIONALIZATION TRANCHE
> of the post-Codex stable database baseline. Implements exactly N34, N35, N36
> (the audited design), then validates and STOPS. Nothing beyond this tranche
> was implemented (confirmation list in §17).
>
> **Repo:** `diogo-o/ba-dmo-v1`, branch `main`, origin HEAD `8d916cb`
> ("Quiesce legacy access mirrors" — N33) + the Queue A hardening working tree.
>
> **Authority stack (unchanged hierarchy):**
> 1. `AI-CONTEXT/docs/Manual/*` — functional authority (unchanged; no Manual/SOT
>    rule was modified).
> 2. Current source code / Dapper / tests — implementation authority.
> 3. N01–N33 — immutable migration history (not modified; N34–N36 appended).
> 4. `database/consolidated_clean_install.sql` — target clean-install baseline
>    (updated to the N01→N36 final state).
> 5. Existing audit reports, principally:
>    - `reports/post_codex_database_rationalization_plan.md` (N34/N35/N36 designs §13.1–13.3)
>    - `reports/schema_rationalization_N34_legacy_mirror_removal_audit.md` (N34 design)
>    - `reports/controlo_schema_alignment_prebaseline_audit.md` (Controlo baseline-safe, N34–N36 unchanged)
>    - `reports/post_codex_remediation_functional_gate.md` (queue definitions)
>    - `reports/post_codex_queue_A_baseline_hardening_report.md` (baseline results)

---

## 1. Executive Summary

The first rationalization tranche of the post-Codex stable baseline is
implemented and verified:

- **N34 — legacy access mirror removal** (destructive, audited Option A, no
  cascading): `internal_user_access_templates` (junction table) and
  `internal_users.profile_title` (+ its CHECK `ck_internal_users_functional_profile`)
  are physically removed. The dependency assumptions from the N34 audit were
  re-run against the current schema evidence and hold (zero `src/` references,
  no incoming FKs, no views/functions over the mirrors, single table-owned
  CHECK as the only `profile_title` dependency, no CASCADE required).
- **N35 — safe index / constraint rationalization** (SAFE NOW items only):
  added `ix_bq_movements_noted_repairer` (BQ-16, deferred by Queue A) and
  removed the redundant `ix_pegamento_documentos_controlo`. The optional BQ-10
  CHECK trim was **excluded** — the rationalization plan classifies it as
  owner decision OD-16 ("owner choice"), not SAFE NOW.
- **N36 — policy rename / security consistency** (D-15 only):
  `access_template_profiles_app_access` → `ba_dmo_app_access` with byte-for-byte
  identical semantics (`FOR ALL TO ba_dmo_app USING (TRUE) WITH CHECK (TRUE)`),
  no grant changes, no RLS behavior change.
- **Consolidated baseline** updated to the N01→N36 final state (mirrors
  removed, index deltas applied, policy renamed, header/tail corrected).
- **Tests/guards** updated to encode the new intended schema: migration family
  list N01→N36, N34/N35/N36 source guards (MigrationDiscoveryTests), N34-era
  executed-PostgreSQL probes replacing the mirror-naming N32/N33 probes
  (RemediationGuardTests), PG seed INSERTs without `profile_title`, and the
  access-mirror quiescence guard doc-comment.
- **Validation:** build PASS (0 errors); unit **660/660** (baseline 660/660);
  integration **319 passed / 1 failed** (baseline 314 passed / 1 failed — the
  same pre-existing, untouched `ShellRoutingTests.Scenario7`); focused
  migration/schema guards **45/45**. No new failures.
- **Clean-install equivalence:** proven statically at the object-inventory
  level (tables/indexes/policies/functions/triggers name-sets) with the
  documented allow-list; the full catalog-level protocol requires a real
  PostgreSQL and remains `LIVE VERIFICATION REQUIRED` (§13, §15).
- **DEV database execution:** NOT EXECUTED in this session — no
  `BA_DMO_TEST_DATABASE`, no Supabase CLI/config, no reachable Docker engine,
  offline NuGet. All real-PostgreSQL items are marked `LIVE VERIFICATION
  REQUIRED`; BA-DMO-PROD was not touched (§14).

---

## 2. N34 — Legacy Access Mirror Removal

### 2.1 Files

- Migration: `database/migrations/N34_legacy_access_mirror_removal.sql` (new).
- No `src/` change (zero runtime references — grep-verified, §2.4).

### 2.2 Statement set (Option A, no cascading — N34 audit §5/§6.1)

```sql
DROP TABLE IF EXISTS internal_user_access_templates;

ALTER TABLE internal_users
    DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile;

ALTER TABLE internal_users
    DROP COLUMN IF EXISTS profile_title;
```

- Explicit `DROP CONSTRAINT` before `DROP COLUMN` (house style, Option A):
  the CHECK is the only catalog dependency of the mirror column, and dropping
  it first pins the intended semantics (never relies on same-table
  dependent-object removal).
- `IF EXISTS` guards (idempotent), whole-script own-transaction, no
  `BEGIN/COMMIT` (no N28/N29/N30-style debt), **no CASCADE anywhere**.

### 2.3 Dependency-assumption re-run (per the task's pre-destructive-DDL gate)

Re-verified against the current tree (not memory):

| Assumption (N34 audit) | Re-run evidence | Result |
|---|---|---|
| Zero `src/` references to either mirror | case-sensitive grep `internal_user_access_templates` / `profile_title` over `src/` → **0 matches** | PASS |
| No incoming FKs to the junction | grep `REFERENCES internal_user_access_templates` over `database/migrations/` → **none** | PASS |
| No views / matviews / functions over the mirrors | grep over `database/migrations/` → **none** | PASS |
| `profile_title` referenced only by N01 (origin), N27 (NOT NULL + CHECK), N31 (backfill/sync), N32 (comments), N33 (quiescence) | grep over `database/migrations/` → exactly those files; all precede N34 | PASS |
| `ck_internal_users_functional_profile` is the only constraint dependency of the column | N27:117-120 creates it; N33 leaves it NULL-tolerant; no index/policy/trigger on the column | PASS |
| No CASCADE needed | junction has no external dependents; column has only its own CHECK | PASS |
| Mirror-dependent migrations (N27/N31/N32/N33) all precede N34 | all execute before N34 in canonical order | PASS |

The N34 audit's dependency assumptions hold against the available schema
evidence; the destructive statements are justified and no CASCADE is required.

### 2.4 Zero runtime references (grep gate)

`internal_user_access_templates` and `profile_title` in `src/` → **0 matches**
(re-verified after implementation; CI-guarded forever by
`AccessMirrorQuiescenceGuardTests`).

### 2.5 N34 is gated & destructive — rollback/recovery posture

One-way (destructive): fossil `profile_title` values and junction rows are
discarded by design (dead since N33). Recovery contract = pre-drop backup
(pg_dump of the affected table/column) + the N34-era parity gates (live
catalog absence, `42P01`/`42703` behavioral probes, canonical-column privilege
assertion) — N34 audit §6.6 / rationalization plan §17. No runtime dependency
exists, so rollback is recorded-restore rather than schema-restore.

---

## 3. N35 — Safe Index / Constraint Rationalization

### 3.1 File

- Migration: `database/migrations/N35_index_rationalization.sql` (new).
- No `src/` change.

### 3.2 Content (SAFE NOW items only — rationalization plan §8.1/§8.2, I1/I2)

```sql
-- §1 ADD (BQ-16): repairer-filtered Boquilhas History
CREATE INDEX IF NOT EXISTS ix_bq_movements_noted_repairer
    ON bq_movements (noted_repairer_id);

-- §2 REMOVE: redundant index (duplicates UNIQUE (pegamento_controlo_id))
DROP INDEX IF EXISTS ix_pegamento_documentos_controlo;
```

- **Usage evidence (§1):** `DapperBoquilhasRepository.ListMovementsAsync`
  (`:267`) and `CountMovementsAsync` (`:296`) filter
  `AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)`; no existing
  index covers `noted_repairer_id` and it is not a prefix of any existing
  composite (verified against the 81-index inventory, plan §8.5). Write cost
  negligible (append-only table).
- **Redundancy evidence (§2):** N14 created both `UNIQUE (pegamento_controlo_id)`
  (constraint index) and the standalone `ix_pegamento_documentos_controlo`
  (N14:12 vs N14:20-21); identical leading column, zero added coverage; removal
  eliminates double write maintenance with zero read loss.
- **Not widened:** the optional BQ-10 CHECK trim (removing the unused FIM
  movement value from the `bq_movements` movement-type CHECK) was **not**
  implemented — the rationalization plan §11 (OD-16) classifies it as owner
  choice, NOT SAFE NOW; implementing it would require an owner decision (not
  taken). It stays parked (Df-9). The N35 source guard asserts its absence.

---

## 4. N36 — Policy Rename / Security Consistency (D-15 only)

### 4.1 File

- Migration: `database/migrations/N36_ba_dmo_app_access_policy_rename.sql` (new).
- No `src/` change (zero runtime code names policies — grep-verified).

### 4.2 Content

```sql
DROP POLICY IF EXISTS access_template_profiles_app_access
    ON access_template_profiles;

DROP POLICY IF EXISTS ba_dmo_app_access
    ON access_template_profiles;
CREATE POLICY ba_dmo_app_access
    ON access_template_profiles
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);
```

### 4.3 Parity verification (what is preserved)

- **Authorization semantics:** policy body identical to the N31 original and to
  the N12/N25/N29 convention (`FOR ALL TO ba_dmo_app USING (TRUE) WITH CHECK (TRUE)`)
  — asserted by file guard and by the env-gated executable policy-inventory
  probe (name + command `*` + role + body expressions).
- **Grants:** the N31 `GRANT SELECT, INSERT, UPDATE, DELETE ON access_template_profiles
  TO ba_dmo_app` is untouched (N36 file contains no grant/permission statements —
  asserted by guard).
- **RLS behavior:** RLS stays enabled; only the policy NAME changes.
- **Expected inventory after N34+N36:** 60 application tables, 60 policies,
  every one named `ba_dmo_app_access` (asserted by the env-gated
  `RemediationGuardTests.N36_PolicyInventory_IsUniform_BaDmoAppAccess_Only`).

---

## 5. Files changed

Implementation (this session):

| File | Change |
|---|---|
| `database/migrations/N34_legacy_access_mirror_removal.sql` | NEW — N34 migration |
| `database/migrations/N35_index_rationalization.sql` | NEW — N35 migration |
| `database/migrations/N36_ba_dmo_app_access_policy_rename.sql` | NEW — N36 migration |
| `database/consolidated_clean_install.sql` | updated to the N01→N36 final state (§7) |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Migrations/MigrationDiscoveryTests.cs` | family list N01→N36 + N34/N35/N36 source guards |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Integrity/RemediationGuardTests.cs` | N32/N33 mirror probes → N34-era absence/privilege probes + N35/N36 catalog probes |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Persistence/ArmazemReturnPostgresTests.cs` | seed INSERT without `profile_title` |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Persistence/JobOnLifecyclePostgresTests.cs` | seed INSERT without `profile_title` |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Persistence/RepairAtomicityTests.cs` | seed INSERT without `profile_title` |
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Access/AccessMirrorQuiescenceGuardTests.cs` | doc-comment allow-list N27…N33 → N27…N34 |
| `AI-CONTEXT/docs/Maps/03_MIGRATIONS.md` | migration inventory/dependency/RLS/final-state bookkeeping updated to N01–N36 |
| `AI-CONTEXT/docs/Maps/02_DATABASE.md` | mirror-object entries removed/annotated; policy convention + category lists updated |
| `reports/post_codex_rationalization_N34_N36_implementation_report.md` | NEW — this report |

No historical migration N01–N33 was modified (`git status`:
`database/migrations/` contains only the 3 new files on top of the immutable
33). No `src/` file was modified.

---

## 6. Migrations created

Exactly three, forward-only, dependency-aware, house-style, no CASCADE:

| # | File | Kind | Destructive | Objects affected |
|---|---|---|---|---|
| N34 | `N34_legacy_access_mirror_removal.sql` | removal | YES | `internal_user_access_templates` (table + PK + FKs + both indexes + inert RLS policy + default + row type + TOAST); `internal_users.profile_title`; `ck_internal_users_functional_profile` |
| N35 | `N35_index_rationalization.sql` | index hygiene | NO | ADD `ix_bq_movements_noted_repairer` (bq_movements); DROP `ix_pegamento_documentos_controlo` (pegamento_documentos) |
| N36 | `N36_ba_dmo_app_access_policy_rename.sql` | policy rename | NO | one RLS policy on `access_template_profiles` |

No N37+ migration was created.

---

## 7. Consolidated baseline changes

`database/consolidated_clean_install.sql` now describes the N01→N36 final
state:

- **Header:** "N01 … N33" → "N01 … N36"; parity-scope list updated to call out
  the N34/N35/N36 final states and the resolved drift D-A.
- **`internal_users` replica:** `profile_title` column removed (and its N27-era
  CHECK/`DROP NOT NULL` blocks removed with the junction stanza).
- **Junction section (N27/N33 blocks):** `internal_user_access_templates`
  table, `ix_internal_user_access_templates_template`, the profile_title
  posture block, the junction RLS/REVOKE and the N33 §2 junction revoke are
  all gone; the N33 §3 column-level grant refactor (8 canonical columns) is
  retained verbatim (it matches the post-N34 column set).
- **N31 section:** `access_template_profiles`, the ensure-profile function and
  trigger, and the RLS/grant stanza are retained; the mirror-sync DML
  (junction collapse, unique actor index, `profile_title` sync) and the
  profile_title-sourced backfill (`MIN(u.profile_title)…`) are removed —
  replaced by the deterministic N32-style backfill (no user-column
  dependency). The policy is created under the convention name
  `ba_dmo_app_access` (N36 final state).
- **N35 deltas:** `ix_bq_movements_noted_repairer` added in the bq_movements
  section; `ix_pegamento_documentos_controlo` removed from the
  pegamento_documentos section (with an explanatory comment).
- **Tail:** "includes N25-N33" → "includes N25-N36".

Checks performed:

- No executable statement in the consolidated file references
  `internal_user_access_templates`, `profile_title`,
  `ix_pegamento_documentos_controlo`, or `access_template_profiles_app_access`
  (all remaining matches are explanatory comments).
- Every `CREATE POLICY` in the consolidated file is `ba_dmo_app_access` (4
  sites: N12 loop, N25 loop, N29 stanza, N31 stanza).

---

## 8. Tests added / updated

Updated (mechanically affected):

- `MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN33` →
  `…_N01ThroughN36` (36-file exact list).
- `RemediationGuardTests`: header N01-N33 → N01-N36; the mirror-naming N32/N33
  executed probes (junction conflict guard, `ProfileTitleStillNotNull` helper,
  N33 privilege/absence probes) retired per N34 audit §6.3 and replaced with
  N34-era probes; the N32 profile-backfill probe reworked without the
  `profile_title` mirror (deterministic-default semantics preserved).
- PG seed INSERTs in `ArmazemReturnPostgresTests`, `JobOnLifecyclePostgresTests`,
  `RepairAtomicityTests` — `profile_title` removed from column list + value.
- `AccessMirrorQuiescenceGuardTests` doc-comment allow-list N27…N33 → N27…N34.

Added (focused, DB-less):

- `MigrationDiscoveryTests.N34_RemovesBothLegacyAccessMirrors_Explicitly_NoCascade`
  (statement set + explicit order, no CASCADE, authority untouched, no
  transaction-control debt).
- `MigrationDiscoveryTests.N35_AddsBqRepairerIndex_AndDropsRedundantPegamentoDocumentosIndex`
  (index add/drop, guard against widening into owner-gated items, incl. the
  BQ-10 exclusion).
- `MigrationDiscoveryTests.N36_UnifiesPolicyNaming_WithIdenticalSemantics`
  (old-name drop + convention-name create + body + no permission-surface change).

Added (focused, PG-gated — self-skip without `BA_DMO_TEST_DATABASE`):

- `RemediationGuardTests.N34_JunctionTable_IsAbsent_AndAnyDmlRaises42P01`
  (catalog absence + `42P01` behavioral probe).
- `RemediationGuardTests.N34_ProfileTitleColumn_IsAbsent_AndAnyDmlRaises42703`
  (catalog absence incl. CHECK, `42703` SELECT/INSERT/UPDATE probes).
- `RemediationGuardTests.N34_CanonicalColumnPrivileges_AreUnchanged_ForBaDmoApp`
  (has_column_privilege matrix on the 8 canonical columns + ba_dmo_app read).
- `RemediationGuardTests.N34_NewUserRows_AreInsertable_OnThePostRemovalSchema`
  (user INSERT as ba_dmo_app, rolled back).
- `RemediationGuardTests.N35_BqMovementsRepairerIndex_Exists_AndRedundantPegamentoIndex_IsGone`
  (catalog index-present/absent + UNIQUE index survives).
- `RemediationGuardTests.N36_PolicyInventory_IsUniform_BaDmoAppAccess_Only`
  (no divergent names; per-app-table single policy count; semantics equality).

No test was weakened: the new guards encode the new intended schema (mirrors
absent; `ba_dmo_app_access` everywhere; index deltas materialized).

---

## 9. Build result

`dotnet build BA-DMO.sln -c Debug` (single-node `-m:1`) → **PASS, 0 errors**.
13 warnings: 10× NU1900 (offline NuGet vulnerability-data fetch — pre-existing
environment condition) and 3× CS8601 in `AdminUserListResetTests` (pre-existing
in the Queue A working tree, untouched).

Environment note: the default multi-node solution build fails to start on this
host (MSBuild node fan-out; documented in the Queue A and N34 audit reports);
`-m:1` is the established host workaround and produces an identical result.

## 10. Unit result

`BA.Dmo.UnitTests`: **Passed! — Failed: 0, Passed: 660, Skipped: 0, Total: 660**
(baseline before session: 660/660 — no change).

## 11. Integration result

`BA.Dmo.IntegrationTests`: **Failed: 1, Passed: 319, Skipped: 0, Total: 320**.

The single failure is the pre-existing, owner-declared unrelated
`Access.ShellRoutingTests.Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`
("nav-item-admin" Admin nav markup drift) — present on the untouched baseline,
**not touched** by this session (no new failures: baseline was 314/1; the +5
tests are the new N34/N35/N36 guards; all pass).

PG-gated suites (`RemediationGuardTests`, `*PostgresTests`, `RepairAtomicityTests`)
self-skip without `BA_DMO_TEST_DATABASE` and count as vacuous passes — their
real-PG execution remains a rollout step (`LIVE VERIFICATION REQUIRED`).

## 12. Focused migration / schema results

`dotnet vstest … --TestCaseFilter:"MigrationDiscoveryTests | AccessMirrorQuiescenceGuardTests | AccessAuthorityGuardTests | DapperAdminRepositoryProjectionTests | RemediationGuardTests"` →
**45/45 PASSED**, including:

- `ShippedFreshBuildFamily_IsComplete_N01ThroughN36` (exact 36-file family).
- `N34_…NoCascade`, `N35_…`, `N36_…` content guards.
- `Src_HasZeroReferences_ToLegacyAccessMirrors` (grep gate).
- `AccessAuthorityGuardTests` (7 repository-SQL authority facts) and
  `DapperAdminRepositoryProjectionTests` (authority-join projection).
- `RemediationGuardTests` (vacuous passes without a database; the N34/N35/N36
  probes are written and will execute against `BA_DMO_TEST_DATABASE`).

Additional static gates executed this session:

- grep `internal_user_access_templates|profile_title` in `src/` → **0 matches**.
- grep `CASCADE` in `database/migrations/` → only pre-existing N06/N23/N31
  `ON DELETE CASCADE` FKs (documented DO-NOT-TOUCH, plan §7.2); **no CASCADE
  in N34/N35/N36**.

## 13. Clean-install equivalence result

Goal: prove the chain `N01 → … → N36` and `database/consolidated_clean_install.sql`
describe structurally equivalent final databases.

**Executed here — static object-inventory comparison** (comment-stripped
statement extraction over both paths):

| Artifact class | Chain statements | Consolidated | Diff after allow-list |
|---|---|---|---|
| Tables | 61 | 61 | chain-only `internal_user_access_templates` (created N27, dropped N34 — history); cons-only `schema_migrations` (runner-owned on the chain path; documented allow-list #1). Final state both = **60 app tables + tracking** |
| Indexes | 82 | 79 | chain-only: junction's 2 indexes (dropped N34) + `ix_pegamento_documentos_controlo` (dropped N35). Final state both = **79** |
| Policies | 3 names | 1 | chain-only historical names `internal_user_access_templates_app_access` (died with N34) and `access_template_profiles_app_access` (renamed N36). Final state both = **`ba_dmo_app_access` only** |
| Functions | 3 | 3 | identical |
| Triggers | 19 | 19 | identical |

Every difference is either a documented allow-list item (rationalization plan
§15: `schema_migrations` content differs by construction) or an object that the
chain History creates and a later migration removes (N34/N35) while the
consolidated file describes the final state — i.e., exactly the intended
post-N34/N35/N36 schema. Grants/columns were verified by targeted reads (the
N33 column-level grants on the 8 surviving `internal_users` columns are
identical on both paths).

**Not executed here (LIVE VERIFICATION REQUIRED):** the full two-path catalog
snapshot protocol (plan §15, CLI/psql `createdb` + `migrate` vs `psql -f`) and
`diff -u` of the canonical snapshot query set on real PostgreSQL — no
PostgreSQL is reachable in this session (§14). The static evidence above is
the best tooling-permitted proof available; the live protocol remains a
rollout step.

## 14. DEV database execution status

**NOT EXECUTED.** Environment facts verified this session:

- No `BA_DMO_TEST_DATABASE` (process/user/machine scope).
- No Supabase CLI and no Supabase project configuration in the repo (no
  `supabase/` dir, no `config.toml`); no connection string in
  `appsettings*.json`/`launchSettings.json`.
- Docker Desktop installed but its engine cannot be brought up in this session
  (no backend process; WSL `docker-desktop` distro boots but the engine named
  pipe never appears); NuGet is offline.
- No `psql`/`pg_isready` on PATH; no local/embedded PostgreSQL service.

Per the task's rule ("If safe DEV execution is unavailable: perform static
validation and state what remains unverified"), all validation above is
static; nothing was executed against any database. **No database — DEV or
PROD — was mutated.** When DEV execution becomes available it MUST target
**BA-DMO-DEV only** (never BA-DMO-PROD), via the documented deploy order
(`migrate` the full N01…N36 family first), followed by the §15 live probes.

## 15. Live verification still required

| # | Item | Probe |
|---|---|---|
| 1 | N01→N36 fresh-chain replay on an empty `BA_DMO_TEST_DATABASE`; `schema_migrations` records the full 36-file family in order | runner whole-script semantics |
| 2 | N34 executed probes: junction table absent; any DML naming it → `42P01`; `profile_title` absent (column + CHECK); SELECT/INSERT/UPDATE naming it → `42703`; canonical-column privileges unchanged for `ba_dmo_app` | `RemediationGuardTests.N34_*` (written, self-skip without DB) |
| 3 | N35 executed catalog: `ix_bq_movements_noted_repairer` present; `ix_pegamento_documentos_controlo` absent; UNIQUE constraint index survives | `RemediationGuardTests.N35_*` |
| 4 | N36 executed policy inventory: 60 application tables / 60 policies, all `ba_dmo_app_access`; access_template_profiles policy body equality | `RemediationGuardTests.N36_*` |
| 5 | Consolidated baseline: execute `database/consolidated_clean_install.sql` in one pass on an empty database (Supabase-hosted guarded statements become NOTICEs) | psql -f |
| 6 | Two-path equivalence: the plan §15 catalog snapshot diff (tables+columns+constraints+indexes+functions+triggers+RLS+policies+grants) on real PostgreSQL | `createdb` scratch A/B + `diff -u` |
| 7 | PG-gated seed suites post-fix (`ArmazemReturnPostgresTests`, `JobOnLifecyclePostgresTests`, `RepairAtomicityTests`) run against the migrated DB | BA_DMO_TEST_DATABASE |
| 8 | Live catalog absence + privilege parity on the deployed DEV project (BA-DMO-DEV) after applying N34–N36 via the Supabase-CLI path (N32 application-path discipline); pre-drop backup of the mirrors before N34 | N34-era parity script (03A revision; historical `reports/` artifacts stay verbatim) |

## 16. Git diff summary

Session change set (on top of the Queue A working tree):

- `git diff --stat` (session files, 9 modified): `642 insertions(+), 312 deletions(-)` — `database/consolidated_clean_install.sql` (194 lines changed), `RemediationGuardTests.cs` (434), `03_MIGRATIONS.md` (134), `MigrationDiscoveryTests.cs` (129), `02_DATABASE.md` (39), `AccessMirrorQuiescenceGuardTests.cs` (12), three PG seed files (4 each).
- New files: `database/migrations/N34_legacy_access_mirror_removal.sql`,
  `database/migrations/N35_index_rationalization.sql`,
  `database/migrations/N36_ba_dmo_app_access_policy_rename.sql`,
  `reports/post_codex_rationalization_N34_N36_implementation_report.md`.
- `database/migrations/N01…N33` — untouched (immutable).
- No commit/push performed.

## 17. Explicit confirmation of scope

- **N37+ (N37/N38/N39/N41/N42) — UNTOUCHED.** No N37-or-later migration file
  exists; `peso_comparacao_anterior`, `modules_override`, `image_asset_id`,
  `contra_costura` nullability, warehouse per-position unique, and
  `tool_check_occurrences`/`physical_pieces.status` are untouched.
- **Queue B — UNTOUCHED.** No code wave implemented (PC-08/PC-03/PC-05/PC-04/
  PC-09/PC-06/PC-13/PC-14 remain open).
- **Queue C / owner decisions — UNTOUCHED.** No owner decision was made; the
  only disposition taken is executing the already-audited, already-approved
  N34/N35/N36 designs (OD-1 already recorded in the N34 audit; OD-10/D-15
  technical; OD-16 BQ-10 explicitly NOT taken). Pre-existing open decisions
  (D-9 execution Go pending N37; D-10/D-12/FA-05/PA-01/PC-07 etc.) remain open.
- **N40 — UNTOUCHED** (the approved-readings guard remains future work; its
  refined design requirement — code pairing so it cannot break approve/reopen
  flows — is recorded in the Controlo audit and preserved; Controlo was not
  redesigned).
- **PROD (BA-DMO-PROD) — UNTOUCHED.** No database execution occurred at all;
  when DEV execution is performed it must target BA-DMO-DEV only.
- **ShellRoutingTests.Scenario7 — UNTOUCHED.** Not modified, not "fixed";
  remains isolated as the one pre-existing unrelated failure (319/1).
- **Manual/SOT rules — UNCHANGED.** No functional rule was reinterpreted.
- **Controlo — NOT redesigned**; its schema is baseline-safe as-is per the
  Controlo alignment audit.

**STOP declaration:** the N34–N36 tranche is implemented and validated; this
session stops here by design — no automatic continuation into N37+ or any
owner-decision item.

— End of report.