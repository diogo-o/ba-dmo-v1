# SCHEMA-RAT-N34 — Legacy Access Mirror Removal (Repository + Fresh-Build Audit)

> **Type:** READ-ONLY AUDIT / PREPARATION — **GATED**. No source, migration,
> schema, or database object was modified; no DDL/DML executed; no live write
> performed; no commit or push. **N34 is NOT implemented** and the live
> database was **NOT mutated** (all live facts below come from the
> owner-supplied read-only audit of project `bddfhbyrmchktqotpzgb`).
>
> **Head verified:** `8d916cb` ("Quiesce legacy access mirrors" — N33), branch
> `main`, working tree clean.
>
> **Purpose:** this is the audit that the SCHEMA-RAT-03B plan §6 gated the
> destructive phase on ("then — and only then — design the destructive N33+
> removal phase + D-16 consolidated-baseline refresh"). It combines the live
> DB audit (authoritative read-only Supabase metadata, §2) with the
> repository audit (§3) and the fresh-build audit (§4), and produces the
> removal design consequence (§5) and a draft N34 specification (§6) for
> review — nothing is executed.

---

## 1. Objective and audit inputs

N34 physically removes the two legacy access mirrors that N33 quiesced:

| Object | Kind | Created by | Quiesced by |
|---|---|---|---|
| `internal_user_access_templates` | junction table `(actor_id, template_id)` | N27 | N33 (§2 revoke) |
| `internal_users.profile_title` | user-level profile mirror column | N01 (nullable) → N27 (NOT NULL + CHECK) → N33 (nullable again) | N33 (§1 + §3) |

Three independent audits feed this report:

1. **Live database audit** (§2) — owner-supplied, authoritative read-only
   metadata for project `bddfhbyrmchktqotpzgb`. No DB mutation was performed.
2. **Repository audit** (§3) — every reference to the two mirror identifiers
   (and the `ProfileTitle` shape) across `src/`, `database/`,
   `AI-CONTEXT/docs/tests/`, `AI-CONTEXT/docs/`, `reports/`.
3. **Fresh-build audit** (§4) — does the forward-only migration family
   (N01→N33 today; N01→N34 after the change) replay cleanly from an empty
   database, and what does the consolidated clean-install baseline need?

**Hard constraints honoured:** no `DROP … CASCADE` unless a repository or
fresh-build finding exposes an external dependency missing from the live audit
(it does not — §3.5/§4.2); the report takes an explicit position on the
CHECK-constraint removal (A vs B) as requested (§5).

---

## 2. Live database audit — authoritative metadata (project `bddfhbyrmchktqotpzgb`)

> Source: owner-supplied read-only audit (this turn). `ba_dmo_app` has **no
> privileges** on either mirror (N33 quiescence confirmed). Only
> postgres/service_role carry administrative privileges.

### 2.1 A. `public.internal_user_access_templates`

| Aspect | Live state |
|---|---|
| Table | EXISTS; RLS enabled TRUE; FORCE RLS FALSE; `ba_dmo_app` privileges: **NONE** |
| PK | `internal_user_access_templates_pkey` `(actor_id, template_id)` |
| FK out | `actor_id → internal_users(actor_id)`; `template_id → access_templates(template_id)` |
| FK in (from other tables) | **NONE** |
| Indexes | pkey (UNIQUE btree); `ix_internal_user_access_templates_template` (btree `(template_id, actor_id)`); `ux_internal_user_access_templates_actor` (UNIQUE btree `(actor_id)`) |
| Triggers | NONE |
| RLS policy | `internal_user_access_templates_app_access` — command ALL, role `ba_dmo_app`, USING true, WITH CHECK true — **still physically present, but inert** (no privileges) |
| Views / matviews | NONE |
| Functions / procedures | NONE |
| Sequences owned | NONE |
| External `pg_depend` | **NONE** — only self-owned objects (PK, FKs, indexes, `assigned_at_utc` default, policy, row type, TOAST) |

### 2.2 B. `public.internal_users.profile_title`

| Aspect | Live state |
|---|---|
| Column | EXISTS; attnum 5; nullable **YES**; default NONE |
| Constraint | **`ck_internal_users_functional_profile`** — CHECK `profile_title = ANY (ARRAY['Admin','Operador / Controlador','Responsável'])` — the **only** catalog dependency |
| Indexes / triggers / RLS policies / views / functions / defaults | NONE (no index, no trigger, no policy, no view, no function, no default/generated) |
| `pg_depend` | only `ck_internal_users_functional_profile`; no external dependent object |
| `ba_dmo_app` privileges (after N33) | SELECT **FALSE**, INSERT **FALSE**, UPDATE **FALSE** |

### 2.3 C. Design consequence (live audit verdict)

The live audit found **no external database dependency blocking physical
removal** of either mirror. N34 must still decide explicitly between:

- **A)** `DROP CONSTRAINT ck_internal_users_functional_profile` first, then
  `DROP COLUMN profile_title`; or
- **B)** rely on PostgreSQL's same-table dependent-object removal semantics.

> Prefer **explicit treatment (A)** if it makes migration intent clearer.
> **DO NOT use `DROP … CASCADE`** unless the repository/fresh-build audit
> discovers an external dependency absent from the live audit.
>
> This report's position is in §5 (recommendation: **A**, with the full
> statement set).

---

## 3. Repository audit

Method: case-sensitive grep of `internal_user_access_templates`,
`profile_title` and `ProfileTitle` over the whole tree (excluding `bin/`,
`obj/` build output), plus read-through of every hit in `src/` and
`AI-CONTEXT/docs/tests/`. The repository's own source guards
(`AccessMirrorQuiescenceGuardTests`, `AccessAuthorityGuardTests`) are quoted
as executable evidence.

### 3.1 Application runtime layer (`src/`) — ZERO mirror references

`grep -r "internal_user_access_templates\|profile_title" src/` → **no matches
in any file type**. The PascalCase `ProfileTitle` shape exists only as
presentation/compatibility shape, fed exclusively by the template-owned
authority (`access_template_profiles`), never by the mirror column:

| File | Role | Mirror dependency |
|---|---|---|
| `src/BA.Dmo.Infrastructure/Access/DapperAdminRepository.cs` | Admin users projection | `LEFT JOIN access_template_profiles pt … pt.functional_profile AS ProfileTitle` (`:46`) — reads the **authority**; no mirror token anywhere |
| `src/BA.Dmo.Infrastructure/Identity/DapperInternalUserRepository.cs` | Identity resolution | `NULL::text AS ProfileTitle` (`:31`); `InsertInternalUserSql` has an **explicit column list without `profile_title`** (`:67-77`); bootstrap = template + user + audit in one UoW, **no mirror writes** (`:170-211`) |
| `src/BA.Dmo.Application/Modules/Admin/AdminModels.cs` | `AdminUserRow.ProfileTitle` view-model slot | shape only (doc comment: "resolved through the template-owned profile", `:9`) |
| `src/BA.Dmo.Application/Modules/Admin/AdminUserService.cs` | Admin user service | `ProfileTitle = profileTitle` from the authority read (`:347`) |
| `src/BA.Dmo.Application/Shared/Identity/IInternalUserRepository.cs` + `IdentityResolutionService.cs` | Identity record / resolution | record slot always NULL; resolver presents template name / functional profile (`:16`, `:137`) |
| `src/BA.Dmo.Application/Shared/Shell/IShellService.cs` + `src/BA.Dmo.Web/Shell/RequestShellService.cs` | Shell state | shape (`IShellService:13`; `RequestShellService:71` consumes `identity.ProfileTitle`) |
| `src/BA.Dmo.Web/Pages/Admin/Users/Index.cshtml.cs` | Admin users search | `u.ProfileTitle` = view-model property from the authority (`:76`) |
| `src/BA.Dmo.Web/Identity/SessionClaims.cs` | Session cookie | carries **only** `ba_dmo.auth_user_id`; no profile claim, no DB column read |

**Conclusion 3.1:** the runtime has zero readers and zero writers of either
mirror — at the source level and enforced by guards.

### 3.2 Database artifacts

| Artifact | Mirror occurrences | N34 action |
|---|---|---|
| `database/migrations/N01_identity.sql` | `profile_title text NULL` (`:79`) — origin | **immutable history — do not touch** |
| `database/migrations/N27_access_convergence.sql` | creates `internal_user_access_templates` + index + RLS/policy/grants (`:8-14`, `:122-143`); `profile_title` inference + `SET NOT NULL` + CHECK (`:20-40`, `:113-120`) | immutable history — do not touch |
| `database/migrations/N31_template_profiles_single_assignment.sql` | junction collapse + `ux_internal_user_access_templates_actor` (`:75-88`); backfill **reads** `u.profile_title` (`:56-60`); sync **writes** `profile_title` (`:92-97`) | immutable history — do not touch |
| `database/migrations/N32_access_authority_convergence.sql` | junction join guards, fail-closed RAISE (`:82-117`); self-doc "stays until parity" (`:144-145`) | immutable history — do not touch |
| `database/migrations/N33_legacy_access_mirror_quiescence.sql` | DROP NOT NULL; junction REVOKE; column-level privilege refactor (`:63-108`) | immutable history — do not touch |
| `database/consolidated_clean_install.sql` | `internal_users.profile_title text NULL` (`:108`) **and** the N27 stanza: junction table + index, `profile_title` SET NOT NULL + CHECK, RLS/policy/grants (`:1621-1664`) | **D-16 refresh (§6.4)** — remove both blocks, refresh header, reproduce equivalence |
| anything else (views/functions over mirrors) | NONE | — |

Note: the consolidated baseline header is already stale (claims "N01 … N24",
references a missing `/reports/consolidated_schema_equivalence.md`, tail
comment says "includes N25-N27" while the body already carries N28-N30
objects) — the D-16 refresh must fix this regardless.

### 3.3 Test layer — full inventory

#### 3.3.1 No change needed (keeps passing post-drop)

| Area | Files | Why it survives N34 |
|---|---|---|
| Identity/admin source guards | `AccessAuthorityGuardTests.cs` (7 facts) | all assertions are `DoesNotContain("internal_user_access_templates")` / `DoesNotContain("u.profile_title")` etc. against repository SQL — they only *forbid* the mirrors; a dropped column changes nothing (`:33,45,56,61-62,71,79,107-108,120-127`) |
| Mirror-quiescence architecture guard | `AccessMirrorQuiescenceGuardTests.cs` | scans `src/` for the identifiers — already zero (see 3.1); unchanged |
| ADO.NET-double projections | `DapperAdminRepositoryProjectionTests.cs` | in-memory `DataTable` with an alias `ProfileTitle` column (`:47`), asserts against issued SQL (`:76-89`) — no catalog dependency |
| Contract/unit tests (fakes) | `FakeAdminRepository.cs`, `AdminUserServiceTests.cs`, `AdminTemplateServiceTests.cs`, `IdentityResolutionServiceTests.cs` | fake/record level; `ProfileTitle` is shape (`:131-133`, `:221-222` etc.) |
| Web API tests | `TampaoWebApiTests.cs`, `FerramentasWebApiTests.cs`, `ReparacaoInternaWebApiTests.cs`, `ReparacaoExternaWebApiTests.cs`, `PegamentoWebApiTests.cs`, `WebAuthSessionTests.cs`, `DesignSystemGuardTests.cs`, `ShellAndCalendarGuardTests.cs` | `ProfileTitle:` is a **constructor argument of the `InternalUserRecord` domain record** (test fixture), never a database column |

#### 3.3.2 Breakage after the drop — must change in the N34 change set

| File | Location | Reference | Post-drop failure mode |
|---|---|---|---|
| `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/Integrity/RemediationGuardTests.cs` | `:508-510`, `:532-537` | N32 conflict probe: junction DELETE/INSERT/`JOIN internal_user_access_templates` | relation does not exist → `42P01` (expected `20000`/diagnostic) |
| same | `:571-572`, `:597` | N32 profile-backfill probe: INSERT/SELECT `profile_title` | column does not exist → `42703` |
| same | `:104-112` | `ProfileTitleStillNotNull` helper queries `information_schema.columns … column_name='profile_title'` | returns no row → helper misreads as "N33 not applied" → N33 probes silently self-skip forever |
| same | `:614-636` | N33 junction-privilege probe (catalog + `INSERT INTO internal_user_access_templates`) | catalog probe returns 0 (passes by luck); behaviour probe `42P01` vs expected `42501` → **fail** |
| same | `:639-688` | N33 profile-title privilege probes (`has_column_privilege` + UPDATE/INSERT/SELECT `profile_title`) | `has_column_privilege` returns FALSE (passes); DML probes `42703` vs expected `42501` → **fail** |
| `…/Persistence/ArmazemReturnPostgresTests.cs` | `:176-177` | seed: `INSERT INTO internal_users (…, profile_title, active) VALUES (…, 'Admin', TRUE)` | `42703` (column does not exist) — schema-level failure |
| `…/Persistence/JobOnLifecyclePostgresTests.cs` | `:162-163` | identical seed INSERT | `42703` |
| `…/Persistence/RepairAtomicityTests.cs` | `:139-141` | identical seed INSERT | `42703` |
| `…/Migrations/MigrationDiscoveryTests.cs` | `:90-106` | `ShippedFreshBuildFamily_IsComplete_N01ThroughN33` — exact 33-file list | family becomes N01…N34 → list must be extended or the test fails |
| `…/Migrations/MigrationDiscoveryTests.cs` | (new) | no N34 content guard exists yet | N34 needs its own source guard (N28-N33 pattern: `:108-300`) |

> All breakage is **self-inflicted test/database-probe text** — none of it is a
> runtime product dependency. The N34 change set rewrites these statements to
> be post-drop-correct (nets to: drop `profile_title` from seed column lists;
> replace the N32/N33 *executed* probes with N34-era catalog-absence + 42P01
> probes; extend the family list; add N34 source guards).

### 3.4 Documentation tree

| Area | Files with mirror references | Nature | N34 action |
|---|---|---|---|
| `AI-CONTEXT/docs/Maps/02_DATABASE.md` | `:102,121,124,160-177,711,718-720` | schema maps (junction table entry, `profile_title` column history) | refresh pass: mark both objects removed post-N34 (history preserved verbatim) |
| `AI-CONTEXT/docs/Maps/03_MIGRATIONS.md` | `:137-141,737-772,903-928,964-1136` | migration-by-migration dependency records | append N34 row; leaf "current state" summaries must drop the mirrors |
| `AI-CONTEXT/docs/Maps/04_DAPPER_INFRASTRUCTURE.md` | `:347-364,424,467,710-740` | Dapper SQL narratives — several describe the **pre-03A** junction join in `FindByAuthUserIdSql`/bootstrap (`:360-362`) | stale since 03A/03B; refresh with the current SQL shapes |
| `AI-CONTEXT/docs/Maps/15_ADMIN.md` | `:34,60,141,182,226,230-234,269-276,292-293,327,332,421-446,472,528` | Admin write paths, projection, self-lockout — mixes pre-03B (junction subquery, `TemplateProfileStore` sync) and current descriptions | refresh; several entries already stale (03B removed mirror writes) |
| `AI-CONTEXT/docs/Maps/16_USERS_ACCESS.md` | `:39-65,124-144,220-224,280-287,314-320,341-354,377-378,476,538,580` | identity/access model docs — junction as assignment store, `profile_title` NOT NULL+CHECK | refresh to N34 final state |
| `AI-CONTEXT/docs/Maps/18_LOGIN.md` | `:111-114,123,314,381` | login resolution DB reads — shows junction join (pre-03A) | refresh |
| `AI-CONTEXT/docs/Maps/19_APPLICATION.md` | `:444` | **STALE** — describes `TemplateProfileStore.cs` as live ("update `internal_users.profile_title`", "Web bypasses Application") | **already wrong at 03A** (file deleted); correct in the refresh |
| `AI-CONTEXT/docs/Maps/20_WEB.md` | `:245` | header user block shows name + profile | check against current header behavior (presents template name) |
| `AI-CONTEXT/docs/Manual/*` | none found | operational manual has **no table-level mirror references** | no change |
| `AI-CONTEXT/docs/old-design/*` | (historical) | archived design docs | out of scope (historical record) |
| `reports/*` | 03A parity SQL (21), SCHEMA-RAT-01/03A/03B/owner-decisions (historical) | audit/design records | keep verbatim; the **03A live-parity script needs an N34-era revision** (§6.6) |
| `AccessMirrorQuiescenceGuardTests.cs` doc-comment | `:13-15` | allow-list prose "N27…N33" | cosmetic: extend mention to N34 when implemented |

### 3.5 Repository verdict

- **No runtime dependency** on either mirror anywhere in `src/` — enforcement
  is CI-guarded today.
- **No migration or SQL object in the repository** depends on the mirrors
  beyond the historical chain that created/quiesced them (§4.2 replay trace).
- **Test and documentation dependencies exist** but are mechanical re-texts
  (§3.3.2, §3.4).
- The repository therefore **adds no external database dependency** beyond
  what the live audit already measured → **the live audit's no-CASCADE verdict
  stands**.

---

## 4. Fresh-build audit

### 4.1 Method and environment

No local PostgreSQL and no Docker are available in this session, and
`BA_DMO_TEST_DATABASE` is not set, so a literal SQL replay against a scratch
PostgreSQL **cannot** be executed here (and the live Supabase is off-limits by
instruction). The fresh-build audit is therefore delivered in two layers:

1. **Static replay trace** of the migration family (N01→N33, plus the
   hypothetical N34) — a complete, per-migration dependency walk (§4.2),
   the same evidence class the repository's own
   `MigrationDiscoveryTests`/`MigrationArchitectureGuardTests` family relies on.
2. **Executable current-state baseline** — the repository's DB-less suites
   (unit tests, migration discovery/order/content guards, source
   architecture guards) run at N33 head (§4.4). The PG-gated probes
   self-skip without `BA_DMO_TEST_DATABASE`.

The **N34-era verification protocol** (fresh-build replay inside CI + the
`BA_DMO_TEST_DATABASE` probes + post-deploy parity script) is specified in
§6.6 — that is where the literal replay is gated, exactly as 03A/03B ran.

### 4.2 Static replay trace — why the chain stays consistent with N34 appended

Migration runner mechanics (verified in
`src/BA.Dmo.Infrastructure/Persistence/Migrations/MigrationRunner.cs`):
whole-script execution, SHA-256 checksums against `schema_migrations`,
record-after-success, canonical discovery order, fail-fast on checksum
mismatch. N34 is a **new, last file** — it cannot alter the checksum of
N01…N33, and no later migration will reference the dropped objects.

| N | Touches the mirrors? | Concrete dependency direction |
|---|---|---|
| N01 | creates `internal_users.profile_title` (NULL, no CHECK) | origin — nothing depends on the mirror beforehand |
| N02–N26 | **no** | no mirror reference at all (grep-verified) |
| N27 | creates `internal_user_access_templates` (+index+RLS+policy+grants); `profile_title` inference, `SET NOT NULL`, CHECK | creates both objects — self-contained |
| N28–N30 | **no** | — |
| N31 | junction collapse + `ux_internal_user_access_templates_actor`; backfill reads `u.profile_title`; sync writes `profile_title` | reads/writes both mirrors — **precedes** any drop |
| N32 | junction conflict guards + parity; deterministic profile backfill (never copies user profile) | reads junction + `profile_title` — precedes any drop |
| N33 | DROP NOT NULL; junction REVOKE; column-level privilege refactor | last consumer of the mirrors — deliberately leaves them physical |
| **N34** (future) | `DROP TABLE internal_user_access_templates`; drop constraint + `DROP COLUMN profile_title` | runs **last**; every mirror-dependent migration (N27/N31/N32/N33) has already executed |

**Replay proof:** a fresh database executes N01→N33 in canonical order and
terminates (the migration family is complete N01–N33 — asserted by
`MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN33`).
Appending N34 as the 34th file preserves the same property: the objects it
drops exist (created at N27), are fully quiesced by N33 (no privileges), and
carry no incoming FKs from any table the migrations create (live audit:
"CONSTRAINTS FROM OTHER TABLES REFERENCING THIS TABLE — NONE"; repository:
no view/function over them). Nothing in the runtime depends on them (§3.1).
The chain is **append-only and self-consistent with N34 last**.

### 4.3 Consolidated clean-install baseline

- Today the baseline at `database/consolidated_clean_install.sql` **still
  contains both mirrors** (two sections: `internal_users.profile_title` in
  the N01 replica at `:108`, and the full N27 junction/CHECK stanza at
  `:1621-1664`). A fresh install today reproduces them — correct at N33.
- After N34 + the **D-16 baseline refresh** (§6.4) the baseline is regenerated
  to the N34 final state: the two sections are removed, the header is
  corrected, and the clean-install/family equivalence is re-verified. Fresh
  installs then never contain the mirrors — completing D-16
  (owner decision already approved: **D-16 = Option A**).

### 4.4 Executable current-state baseline (N33 head)

Solution build: **succeeded, 0 errors** (5 NU1900 warnings only — NuGet
vulnerability-data fetch unavailable on this offline machine; harmless).
Assemblies run via `dotnet vstest` per assembly (the full `dotnet test`
orchestration is unusable on this host — its MSBuild node fan-out hangs; the
per-assembly runner works and is equivalent for result collection):

| Assembly | Result | Relevance to N34 |
|---|---|---|
| `BA.Dmo.UnitTests.dll` | **Passed! — Failed: 0, Passed: 657, Skipped: 0, Total: 657** | full contract surface green (identity/admin/template services on fakes) |
| `BA.Dmo.IntegrationTests.dll` | **Failed: 1, Passed: 311, Skipped: 0, Total: 312** | see the single failure below |

The single integration failure is
`Access.ShellRoutingTests.Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`
(`:163`): it asserts the CSS class `nav-item-admin` in rendered Admin markup;
the file has **zero** references to either mirror identifier, and the
SCHEMA-RAT-03B plan §7 already declares this exact test as
**owner-declared unrelated debt ("never mixed into schema work")**. It is an
Admin-navigation markup drift, not a schema/authority defect.

All N34-relevant suites inside the integration assembly **passed**:
`Migrations.MigrationDiscoveryTests` (fresh-build family N01…N33 complete +
deterministic order + N28–N33 content guards), `Access.AccessAuthorityGuardTests`
(7 repository-SQL authority facts), `Access.AccessMirrorQuiescenceGuardTests`
(zero `src/` mirror references), `Access.DapperAdminRepositoryProjectionTests`
(authority-join projection), plus the Web/Design guard suites. The
PostgreSQL-gated probes (`RemediationGuardTests`, `ArmazemReturnPostgresTests`,
`JobOnLifecyclePostgresTests`, `RepairAtomicityTests`) self-skip without
`BA_DMO_TEST_DATABASE` and counted as vacuous passes — their N34-era rewrite is
specified in §6.3.

### 4.5 What the fresh-build audit proves / cannot prove here

**Proven repo-side (executable):** family completeness N01…N33, deterministic
discovery/order, per-migration source guards (N28–N33), zero `src/`
references to the mirrors, repository SQL contracts (direct-FK + template
profile authority), and full unit contract coverage.

**Proven by static trace:** the append-only replay property with N34 last
(§4.2), and the exact consolidated-baseline delta (§4.3).

**Cannot be proven in this environment:** literal execution of the N34 DROP
statements against PostgreSQL and the post-drop behavioural probes. Those are
gated in the N34 verification protocol (§6.6) — a disposable
`BA_DMO_TEST_DATABASE` replay plus the live post-deploy parity — exactly as
N32/N33 were proven.

---

## 5. Design consequence — the A/B decision (live audit §C)

The live audit found the **only** dependency on `profile_title` is the
table-owned CHECK `ck_internal_users_functional_profile`, and that the
junction table has no external dependents at all. The N34 design must pick A
(explicit constraint drop first) or B (rely on same-table dependent-object
semantics).

**This report recommends Option A** — explicit, in this order:

```sql
-- §1 junction mirror (table-owned PK/FKs/indexes/policy/default vanish with it)
DROP TABLE IF EXISTS internal_user_access_templates;

-- §2 profile mirror CHECK first (explicit intent; makes the column drop obvious)
ALTER TABLE internal_users
    DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile;

-- §3 profile mirror column (attnum 5; fossil values are discarded by design)
ALTER TABLE internal_users
    DROP COLUMN IF EXISTS profile_title;
```

Why A over B:

1. **Intent is pinned in the migration itself.** N33's legacy is precise
   prose; a bare `DROP COLUMN` that silently drags the CHECK with it
   reproduces the same "implicit semantics" opaqueness the project has been
   removing (N33 §3 is an explicit refactor for exactly this reason).
2. **House style.** N27 already does explicit `DROP CONSTRAINT IF EXISTS …
   ADD CONSTRAINT …`; explicit two-step mirrors it.
3. **Zero cost.** The CHECK is vacuous once the column is gone; dropping it
   first is free and removes any doubt about what the column drop implies.
4. **No CASCADE anywhere.** Live audit + repository audit agree there is no
   external dependent object; `IF EXISTS` guards keep the script idempotent,
   consistent with N27/N31/N33 conventions (whole-script, own transaction,
   no BEGIN/COMMIT, no dynamic discovery).

---

## 6. N34 specification draft — DESIGN ONLY, NOT IMPLEMENTED

No file was created. The following is the reviewed shape of the N34 change
set for owner approval, matching the artifact conventions of N27/N31/N33.

### 6.1 Migration `database/migrations/N34_legacy_access_mirror_removal.sql`

- Header block restating the lineage (N01 origin → N27 contract → N31
  sync/collapse → N32 parity → N33 quiescence → N34 removal).
- §1 `DROP TABLE IF EXISTS internal_user_access_templates;` — carries its PK,
  both FKs, both indexes (incl. `ux_internal_user_access_templates_actor`),
  the inert RLS policy, default, row type, TOAST.
- §2 `ALTER TABLE internal_users DROP CONSTRAINT IF EXISTS
  ck_internal_users_functional_profile;`
- §3 `ALTER TABLE internal_users DROP COLUMN IF EXISTS profile_title;`
- §4 self-documentation block: non-destructive-now-destructive boundary,
  data-loss statement (fossil values discarded; guarded by 03B parity +
  backup), no-CASCADE rationale, and the note that
  `access_template_profiles`, its CHECK and the N31 trigger remain (they are
  the authority, untouched).
- Idempotent: the three statements are `IF EXISTS`-guarded; whole-script in
  its own transaction (no BEGIN/COMMIT).

### 6.2 Application change set

**None.** `src/` has zero mirror references today (§3.1) and the guards keep
it that way. (The `ProfileTitle` view-model/shape slots stay — they are
authority-fed presentation, per D-1/D-2.)

### 6.3 Test change set

1. `RemediationGuardTests.cs` — retire/replace the N32/N33 executed probes
   that name the mirrors (§3.3.2); add N34-era probes: catalog absence
   (`information_schema.tables/columns`), DML on the junction → `42P01`,
   INSERT/SELECT/UPDATE of `profile_title` → `42703`, and `ba_dmo_app`
   canonical-column privileges unchanged. Keep N32/N33 probes **only** where
   the suite can still target a pre-N34 schema, or drop them with a note.
2. PG seed INSERTs — remove `profile_title` from the column list + value in
   `ArmazemReturnPostgresTests.cs:176-177`,
   `JobOnLifecyclePostgresTests.cs:162-163`,
   `RepairAtomicityTests.cs:139-141`.
3. `MigrationDiscoveryTests.cs` — extend the family list to N01…N34; add
   N34 content guards (mirrors dropped, no CASCADE, `IF EXISTS` guards,
   explicit DROP CONSTRAINT before DROP COLUMN).
4. `AccessMirrorQuiescenceGuardTests.cs` doc-comment — extend "N27…N33" to
   "N27…N34" (the guard code itself is unchanged).

### 6.4 D-16 — consolidated clean-install baseline refresh

Regenerate `database/consolidated_clean_install.sql` to the N34 final state:
remove the `profile_title` column from the `internal_users` replica
(`:108`), remove the N27 junction/CHECK stanza (`:1621-1664`), correct the
header, re-run the clean-install ↔ family equivalence protocol (the missing
equivalence report referenced by the header must be produced/tracked).

### 6.5 Documentation refresh

Apply the §3.4 refresh table: Maps 02/03/04/15/16/18/19/20 pass; correct the
stale entries (junction joins, `TemplateProfileStore`, `profile_title`
NOT NULL + CHECK) to the N34 final state.

### 6.6 Verification protocol (N34 era)

1. Fresh-build replay: apply N01→N34 to an empty disposable
   `BA_DMO_TEST_DATABASE`; assert final schema has neither mirror and that
   `schema_migrations` records N01…N34 (runner whole-script semantics).
2. Run the N34-era `RemediationGuardTests` probes (6.3.1) against that
   database, connecting as `ba_dmo_app`.
3. Live post-deploy parity: revise
   `reports/schema_rationalization_03A_live_parity.sql` into an N34 parity
   script — the 03B flip (§1.4 "users without junction rows are the norm")
   becomes "junction rows and profile_title values are impossible (objects
   absent)"; add
   catalog-absence checks (§2.1/§2.2 facts); confirm
   `schema_migrations`/`supabase_migrations` records the removal.
4. Run the full DB-less suite (§4.4) and the grep gate on the deployed tag.
5. Deploy ordering: `migrate` (N34) → deploy → parity (same discipline as
   every prior phase; remember the live project applies migrations via
   Supabase-CLI provenance — land N34 through that path, mirroring the
   N32 application path documented in
   `reports/schema_rationalization_n32_application_path.md`).

---

## 7. Risk register

| Risk | Assessment | Mitigation |
|---|---|---|
| Irreversible data loss (fossil `profile_title` values + junction rows) | MEDIUM — the mirrors are inherently destructive to remove | removal is gated behind 03B live parity + this audit + a pre-N34 backup; junction rows and fossil values are dead by design (N33) |
| Silent privilege regression on `internal_users` after the drop | LOW — N33 column-level grants name explicit columns; dropping `profile_title` does not invalidate the rest | N34-era probe asserts canonical columns keep SELECT/INSERT/UPDATE as `ba_dmo_app` |
| `42P01/42703` in the *test* suite (not product) if §3.3.2 edits are missed | MEDIUM (CI red, not prod) | §6.3 makes the edits part of the N34 change set; grep gate catches strays (`internal_user_access_templates`, `profile_title` outside `database/migrations/N*` + docs) |
| RLS policy residue on a dropped table | none — policy dies with the table (live audit: policy is table-owned) | stated in §6.1 |
| Supabase-CLI provenance divergence (as seen in 03A) | MEDIUM for deploy mechanics only | land N34 through the same path as N32 (`n32_application_path.md`); runner checksums only apply where the Npgsql runner owns provenance |
| Post-N34 docs/guards still describing pre-drop state | LOW | §3.4 + §6.5 refresh; `AccessMirrorQuiescenceGuardTests` still guards `src/` forever |

---

## 8. Explicitly out of scope

- `internal_users.modules_override` (D-11 REMOVE_LATER — separate decision),
  `peso_comparacao_anterior` (D-9), `job_on_revision.image_asset_id` (D-11),
  dormant surfaces (D-7/D-8), audit co-transactionality (D-5/D-13), RLS
  naming (D-15), Job On audit dual-emit (D-5).
- Any re-shaping of `access_template_profiles` / the N31 trigger — the
  authority stays exactly as shipped.
- Implementation of N34 in any form (migration file, test edits, docs,
  baseline) — **explicitly forbidden this turn; design only.**

---

## 9. Status summary

| Item | Status |
|---|---|
| Live DB audit (owner-supplied) | **RECEIVED — authoritative** (no external dependency; A/B decision flagged) |
| Repository audit | **COMPLETE** (§3) — zero runtime refs; mechanical test/doc re-texts; no CASCADE justification |
| Fresh-build audit | **COMPLETE as far as the environment permits** (§4) — replay trace + static baseline; literal SQL replay gated in §6.6 |
| Design consequence | **Option A recommended** (explicit `DROP CONSTRAINT` → `DROP COLUMN`; `DROP TABLE` junction; no CASCADE) — §5 |
| N34 implementation | **NOT PERFORMED** (no file, no DB mutation, no commit) |