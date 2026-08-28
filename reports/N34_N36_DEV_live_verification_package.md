# N34–N36 DEV LIVE VERIFICATION PACKAGE

> **Type:** EXECUTABLE VERIFICATION PACKAGE — **PREPARATION ONLY.**
>
> **NO SQL EXECUTION. NO DATABASE MUTATION. NO CODE CHANGES** beyond the
> creation of this report. This document prepares a complete, copy/paste
> executable verification package for applying and validating **N34, N35, N36**
> against **BA-DMO-DEV later**. It is the executor contract for that later
> session — nothing in this file is to be run against any database now.
>
> **Repo:** `diogo-o/ba-dmo-v1`, branch `main`, origin HEAD `8d916cb`
> ("Quiesce legacy access mirrors" — N33) + the Queue A working tree
> (N34/N35/N36 migration files present, uncommitted, statically validated).
>
> **Authoritative inputs used to build this package:**
> 1. `reports/post_codex_database_rationalization_plan.md` (N34/N35/N36 designs
>    §13.1–13.3, live read-only queries §14, clean-install equivalence §15,
>    acceptance criteria §18)
> 2. `reports/schema_rationalization_N34_legacy_mirror_removal_audit.md` (N34
>    design consequence Option A, live audit §2, verification protocol §6.6)
> 3. `reports/post_codex_rationalization_N34_N36_implementation_report.md`
>    (implemented statement sets, consolidated baseline refresh, test changes)
> 4. `reports/controlo_schema_alignment_prebaseline_audit.md` (§15: N34/N35/N36
>    **UNCHANGED** by the Controlo audit)
> 5. `reports/schema_rationalization_03A_live_parity.sql` (labelled
>    zero-rows=PASS parity convention)
>
> **Convention used by every query below** (03A parity style):
> * every query is **SELECT-only** (no DDL, no DML, no functions, no
>   transaction control);
> * a labelled query returning **ZERO rows = PASS**; returning **1+ rows =
>   FAIL** and the rows enumerate the offenders, unless the label
>   explicitly says "informational";
> * every multi-row query has a deterministic `ORDER BY` so outputs can be
>   diffed across runs;
> * run as `postgres` / owner / `ba_dmo_migrate` (catalog + RLS bypass) or, for
>   the few `<ROLE>` behavioural reads, as `ba_dmo_app` (permissive policies).
>
> **Execution gate:** this package is executed ONLY by the executor under the
> §17 Baseline Acceptance Checklist and §16 STOP conditions. The checklist
> must be satisfiable before N34–N36 are considered **LIVE VERIFIED**.

---

## 1. Purpose and Scope

**Purpose.** Provide one self-contained, executable verification package that
a later executor session uses to:

1. prove the DEV target is BA-DMO-DEV and BA-DMO-PROD cannot be addressed by
   accident (pre-flight identity gate);
2. run read-only **pre-checks** proving the live catalog is in the expected
   N33-era state for each migration;
3. apply **N34 → N35 → N36** in the approved order with documented per-step
   expectations, failure modes, atomicity and rollback posture;
4. run read-only **post-checks** proving each migration's final state and
   semantic parity (N36 especially: parity, not just name presence);
5. capture a deterministic **final catalog inventory** (diff-able baseline);
6. run the **clean-install equivalence protocol** (chain N01→N36 vs
   `database/consolidated_clean_install.sql`) on disposable scratch databases;
7. run the **application/test verification sequence** and gate acceptance on
   the recorded pre-live baseline (no regression);
8. hand the whole plan to Codex as an executor/validator with zero
   rediscovery required (§18).

**Scope — exactly N34, N35, N36.**

| # | File | Kind | Destructive |
|---|---|---|---|
| N34 | `database/migrations/N34_legacy_access_mirror_removal.sql` | legacy-mirror removal | **YES** |
| N35 | `database/migrations/N35_index_rationalization.sql` | index hygiene | NO |
| N36 | `database/migrations/N36_ba_dmo_app_access_policy_rename.sql` | RLS policy rename | NO |

**Explicitly NOT in scope (do not implement, do not touch):**

- N37+ (no N37+ migration file exists; `peso_comparacao_anterior`,
  `modules_override`, `image_asset_id`, `contra_costura`, N41/N42 items are
  untouched).
- The optional **BQ-10 CHECK trim** (`'fim'` removal from
  `ck_bq_movements_type`) — owner decision OD-16, **NOT taken**; N35 excludes
  it by design.
- Any application behavior change; any `src/` modification.
- Any new migration file; any edit to N01–N36.
- Unrelated cleanup.
- BA-DMO-PROD in every possible way (§19).

**This package changes nothing.** Its only artifact is this report.

---

## 2. Environment Safety

### 2.1 Targets

| Environment | Identity | Role in this package |
|---|---|---|
| **DEV** | **BA-DMO-DEV** — the Supabase project recorded across audits as **project ref `bddfhbyrmchktqotpzgb`** (N32-era application-path investigation and N34 owner-supplied live audit §2) | **THE ONLY EXECUTION TARGET.** Positive identity re-confirmation is mandatory (§2.3, §16 STOP-1). |
| **PROD** | **BA-DMO-PROD** — distinct project; ref NOT recorded in this repository, by design | **ZERO TOUCH** (§19). |

> ⚠️ `bddfhbyrmchktqotpzgb` is the ref the audits treated as "the live/DEV
> project". **Do not rely on memory:** the executor must positively confirm
> the connection they hold is BA-DMO-DEV **before touching anything** (§2.3).
> If the operator's BA-DMO-DEV ref differs from the recorded one, substitute
> the operator-confirmed DEV ref at execution time — never execute against a
> ref that cannot be mapped to BA-DMO-DEV, and never against a ref that maps
> to BA-DMO-PROD.

### 2.2 Hard rules (binding on the executor)

1. **Read-only until the application gate passes.** Pre-checks (§4/§7/§10)
   are SELECT-only. The first write permitted in the whole sequence is the
   N34 file itself (after its pre-check + backup), inside its own transaction.
2. **Each migration file is applied as ONE unit.** The repository runner
   (`NpgsqlMigrationScriptGateway.ExecuteScriptAsync`) sends each whole file
   as one command inside one transaction — no statement splitting. The
   Supabase-CLI path (`supabase db push`) also runs each file in its own
   transaction. In the Supabase SQL editor, apply **the entire file as one
   batch** (the editor wraps a multi-statement batch in an implicit
   transaction) — never statement-by-statement in autocommit mode.
3. **Provenance discipline (PA-BK-01).** The live DEV DB's migration history
   is owned by **Supabase CLI bookkeeping** (`supabase_migrations.schema_migrations`,
   timestamp keys), NOT the repository runner's `public.schema_migrations`.
   Land N34–N36 through **the Supabase-CLI path that owns the live history**
   (per `reports/schema_rationalization_n32_application_path.md` Option A),
   or a one-shot owner-role transactional apply with a manual bookkeeping row
   (Option B, with explicit owner sign-off). Do NOT run the Npgsql runner on
   the live DB — that creates a provenance split with re-execution risk.
   Verify the exact live column layout of the bookkeeping table read-only
   first (see P0.4/P0.5 in §2.3).
4. **Pre-drop backup before N34.** Data snapshot of the junction table and of
   `internal_users` (fossil `profile_title` values) — commands in §5.4.
5. **Every write is preceded by its pre-check, and every migration is
   followed by its post-check** before the next migration starts
   (§5.6/§8.5/§11.5 checkpoint gates).
6. **No cascading removal anywhere.** N34 contains no CASCADE; the package
   forbids adding any.
7. **STOP beats "continue and inspect later".** Any §16 condition → stop the
   sequence, freeze evidence, report. Never proceed to the next migration
   with a failed post-check.

### 2.3 Pre-flight identity gate (executor, before ANY query)

Run against the connection you hold and record the answers:

```sql
-- P0.1  Which database am I actually connected to? (informational — record it)
SELECT current_database()        AS db,
       current_user              AS role,
       current_setting('server_version') AS pg_version,
       inet_server_addr()        AS server_addr,
       inet_server_port()        AS server_port;
```

```sql
-- P0.2  Supabase project identity if the connect string carries it (informational).
--   The Supabase connection URI embeds the project ref as the host prefix:
--   db.<project-ref>.supabase.co . Record host from your connection string;
--   it MUST equal the operator-confirmed BA-DMO-DEV host.
SELECT 'recorded DEV ref for cross-check: bddfhbyrmchktqotpzgb' AS note;
```

```sql
-- P0.3  Provenance bookkeeping tables present (expect rows for both records;
--       live history lives in supabase_migrations.schema_migrations).
SELECT to_regclass('public.schema_migrations')              AS runner_table,
       to_regclass('supabase_migrations.schema_migrations') AS cli_table;
```

```sql
-- P0.4  Tail of the CLI bookkeeping (expect the N31..N33-era rows; N34/N35/N36
--       ABSENT before execution).  Column names are the live table's — inspect
--       with \d supabase_migrations.schema_migrations first if this errors.
SELECT version, name, applied_at
  FROM supabase_migrations.schema_migrations
 ORDER BY version DESC
 LIMIT 10;
```

```sql
-- P0.5  PASS when: the tail shows N31 (and N32/N33 if applied) and does NOT
--       contain n34/n35/n36 anywhere.
SELECT version, name
  FROM supabase_migrations.schema_migrations
 WHERE name ILIKE '%n3[456]%' OR name ILIKE '%legacy_access_mirror_removal%'
     OR name ILIKE '%index_rationalization%' OR name ILIKE '%policy_rename%';
```

**Identity verdict (mandatory before proceeding):** the connection host =
operator-confirmed BA-DMO-DEV; the recorded DEV ref `bddfhbyrmchktqotpzgb`
matches (or the operator explicitly confirms the substituted ref IS
BA-DMO-DEV); the bookkeeping tail is the N33-era state; **no** N34/N35/N36
record exists. Any deviation → §16 STOP-1.

### 2.4 Connection/role guidance for the queries

- **Catalog queries (§4–§13):** run as `postgres`/owner/service-role on
  BA-DMO-DEV (RLS bypassed for owner roles; catalog always readable).
- **Behavioural read probes that use `SET ROLE ba_dmo_app`** (§12.3): session
  scoped, SELECT-only, always with a matching `RESET ROLE` in the same batch.
- **Disabled here:** every probe that requires `BA_DMO_TEST_DATABASE`
  (executed test probes) is listed by name in §15.4 but not pasted as SQL in
  this package (they live in the repository test suite).

---

## 3. Known Baseline

### 3.1 Repository state (as of this package)

- `database/migrations/` contains exactly **36 files, N01…N36** (33 immutable
  historical + 3 new: N34/N35/N36). No N37+ file exists.
- `database/consolidated_clean_install.sql` describes the **N01→N36 final
  state** (mirrors removed; N35 index deltas; policy renamed; N34-era header).
- `src/` has **zero** references to `internal_user_access_templates` or
  `profile_title` (grep-gate, CI-guarded by `AccessMirrorQuiescenceGuardTests`).
- No `src/` change is required or expected for N34–N36.

### 3.2 Approved statement sets (verbatim, immutable)

**N34** (`N34_legacy_access_mirror_removal.sql`, Option A, no CASCADE):

```sql
DROP TABLE IF EXISTS internal_user_access_templates;
ALTER TABLE internal_users
    DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile;
ALTER TABLE internal_users
    DROP COLUMN IF EXISTS profile_title;
```

**N35** (`N35_index_rationalization.sql`, SAFE NOW I1/I2 only):

```sql
CREATE INDEX IF NOT EXISTS ix_bq_movements_noted_repairer
    ON bq_movements (noted_repairer_id);
DROP INDEX IF EXISTS ix_pegamento_documentos_controlo;
```

**N36** (`N36_ba_dmo_app_access_policy_rename.sql`, D-15 only):

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

### 3.3 Expected N33-era (pre-N34) catalog facts the pre-checks assert

| Object | Expected pre-N34 state (live audit §2) |
|---|---|
| `internal_user_access_templates` | EXISTS; RLS enabled TRUE, FORCE FALSE; `ba_dmo_app` privileges **NONE**; PK `(actor_id, template_id)`; FKs actor→`internal_users`, template→`access_templates`; 3 indexes (pkey, `ix_internal_user_access_templates_template`, `ux_internal_user_access_templates_actor`); 0 triggers; 1 policy `internal_user_access_templates_app_access` (inert); no incoming FK; no external `pg_depend` |
| `internal_users.profile_title` | EXISTS; attnum 5; NULLABLE; no default; sole dependency = CHECK `ck_internal_users_functional_profile` (`profile_title = ANY (ARRAY['Admin','Operador / Controlador','Responsável'])`); `ba_dmo_app` column privileges FALSE |
| `internal_users` canonical columns (post-N33 grants) | `actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc, modules_override` — column-level SELECT/INSERT/UPDATE to `ba_dmo_app` on exactly these 8; table-level DELETE residual from N12's blanket grant is pre-existing and untouched |
| `bq_movements.noted_repairer_id` | EXISTS (`uuid NULL`, N18); **no** index whose leading key is `noted_repairer_id` |
| `pegamento_documentos` | UNIQUE `(pegamento_controlo_id)` constraint index (N14) **and** standalone `ix_pegamento_documentos_controlo` (N14, redundant) |
| `ck_bq_movements_type` | EXISTS; definition includes `'fim'` (BQ-10 untouched; OD-16 not taken) |
| `access_template_profiles` | EXISTS; RLS enabled TRUE, FORCE FALSE; grants `SELECT, INSERT, UPDATE, DELETE` to `ba_dmo_app` (N31); one policy **named `access_template_profiles_app_access`**; none named `ba_dmo_app_access` on this table |
| Final expected inventory after N36 | **60** application tables (61 − junction), **79** indexes, **3** functions, **19** triggers, **60** policies all named `ba_dmo_app_access`, RLS enabled on all 60 |

### 3.4 Pre-live test baseline (no regression acceptable)

| Suite | Baseline |
|---|---|
| Build (`dotnet build BA-DMO.sln -c Debug`) | **PASS**, 0 errors (`-m:1` host workaround; 13 known warnings: 10× NU1900 offline-NuGet, 3× CS8601 pre-existing) |
| Unit (`BA.Dmo.UnitTests`) | **660/660** |
| Integration (`BA.Dmo.IntegrationTests`) | **319 passed / 1 failed** — the sole failure is the pre-existing, owner-declared unrelated `Access.ShellRoutingTests.Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`; never "fixed" in schema work |
| Focused migration/schema guards | **45/45** (filter in §15.3) |
| PG-gated suites | self-skip without `BA_DMO_TEST_DATABASE`; count as vacuous passes until executed (§15.4) |

### 3.5 The three functions and nineteen triggers (name-set invariants)

Functions (3): `ba_dmo_guard_append_only` (N01), `ba_dmo_guard_peso_approved`
(N25), `ba_dmo_ensure_access_template_profile` (N31).

Triggers (19): `trg_audit_events_append_only`, `trg_bq_movements_append_only`,
`trg_bq_lifecycle_history_append_only`, `trg_bq_utilisation_readings_append_only`,
`trg_job_on_audit_event_append_only`, `trg_pegamento_medicoes_append_only`,
`trg_repair_events_append_only`, `trg_warehouse_movements_append_only`,
`trg_tampao_movements_append_only`, `trg_tool_usage_records_append_only`,
`trg_tampao_configuration_notes_append_only`,
`trg_tampao_configuration_machine_event_append_only`,
`trg_controlo_sheet_events_append_only`, `trg_peso_controlos_approved_guard`,
`trg_job_on_revision_append_only`, `trg_job_on_component_append_only`,
`trg_job_on_component_field_append_only`, `trg_job_on_component_row_append_only`,
`trg_access_templates_ensure_profile`.

No N34/N35/N36 statement creates or drops any function or trigger; these
name-sets must be byte-identical before and after.

---

## 4. N34 Pre-Checks

Run **read-only** on BA-DMO-DEV after §2.3 identity gate, before applying N34.
All labelled checks: ZERO rows = PASS unless stated otherwise.

### P4.1 — The three removal targets exist exactly as designed

```sql
-- P4.1.1  Junction table exists (expect one row, non-NULL oid).
SELECT to_regclass('public.internal_user_access_templates') AS junction_oid;

-- P4.1.2  profile_title column exists on internal_users (expect 1 row,
--         attnum 5, attnotnull FALSE).
SELECT a.attname, a.attnum, a.attnotnull, a.attisdropped
  FROM pg_attribute a
 WHERE a.attrelid = 'public.internal_users'::regclass
   AND a.attname = 'profile_title';

-- P4.1.3  The CHECK constraint exists (expect exactly 1 row with the documented
--         definition, NULL-tolerant).
SELECT conname, pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE conrelid = 'public.internal_users'::regclass
   AND conname = 'ck_internal_users_functional_profile';
```

**PASS condition:** all three return the expected single rows. A `to_regclass`
returning NULL, or an `attname` query returning 0 rows, means the live catalog
is NOT the expected N33 state → §16 STOP-3.

### P4.2 — No incoming foreign keys target the junction

```sql
-- P4.2  Incoming FKs to the junction (expect ZERO rows).
SELECT conname, conrelid::regclass::text AS referencing_table
  FROM pg_constraint
 WHERE confrelid = 'public.internal_user_access_templates'::regclass
   AND contype = 'f'
 ORDER BY conname;
```

### P4.3 — No views/matviews depend on the mirror table/column

```sql
-- P4.3  Views / matviews whose definition depends on the junction table OR on
--       internal_users.profile_title (expect ZERO rows).
SELECT DISTINCT dep_ns.nspname AS view_schema,
                dep_cls.relname AS view_name,
                dep_cls.relkind  AS kind   -- 'v' view, 'm' matview
  FROM pg_depend d
  JOIN pg_rewrite r  ON d.objid = r.oid
  JOIN pg_class dep_cls ON r.ev_class = dep_cls.oid
  JOIN pg_namespace dep_ns ON dep_ns.oid = dep_cls.relnamespace
 WHERE d.refobjid = 'public.internal_user_access_templates'::regclass
    OR (d.refobjid = 'public.internal_users'::regclass
        AND d.refobjsubid = (SELECT attnum FROM pg_attribute
                              WHERE attrelid = 'public.internal_users'::regclass
                                AND attname = 'profile_title' AND NOT attisdropped))
 ORDER BY dep_ns.nspname, dep_cls.relname;
```

### P4.4 — No functions/procedures depend on the mirror table/column

```sql
-- P4.4.1  Catalog-level: any pg_proc object depending on either mirror
--         (expect ZERO rows).
SELECT n.nspname AS fn_schema, p.proname
  FROM pg_depend d
  JOIN pg_proc p ON d.objid = p.oid
  JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE d.refobjid = 'public.internal_user_access_templates'::regclass
    OR (d.refobjid = 'public.internal_users'::regclass
        AND d.refobjsubid = (SELECT attnum FROM pg_attribute
                              WHERE attrelid = 'public.internal_users'::regclass
                                AND attname = 'profile_title' AND NOT attisdropped))
 ORDER BY n.nspname, p.proname;
```

```sql
-- P4.4.2  Belt-and-braces text scan of function bodies (expect ZERO rows).
SELECT n.nspname AS fn_schema, p.proname
  FROM pg_proc p
  JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND (p.prosrc ILIKE '%internal_user_access_templates%'
     OR p.prosrc ILIKE '%profile_title%')
 ORDER BY n.nspname, p.proname;
```

### P4.5 — No unexpected pg_depend dependencies

```sql
-- P4.5.1  Junction: every pg_depend row referencing the table must be
--         SELF-owned (deptype 'i'/'a' — the table's own rowtype, constraints,
--         indexes, policy, default, TOAST).  ANY deptype 'n' row = external
--         dependent = FAIL (expect only 'i'/'a' rows).
SELECT d.deptype,
       d.classid::regclass AS dependent_class,
       CASE WHEN d.classid = 'pg_class'::regclass
            THEN d.objid::regclass::text
            WHEN d.classid = 'pg_constraint'::regclass
            THEN (SELECT conname FROM pg_constraint WHERE oid = d.objid)
            WHEN d.classid = 'pg_policy'::regclass
            THEN (SELECT polname FROM pg_policy WHERE oid = d.objid)
            WHEN d.classid = 'pg_attrdef'::regclass
            THEN 'default on table'
            WHEN d.classid = 'pg_type'::regclass
            THEN d.objid::regtype::text
            ELSE d.objid::text END AS dependent_object,
       d.refobjsubid
  FROM pg_depend d
 WHERE d.refobjid = 'public.internal_user_access_templates'::regclass
 ORDER BY d.deptype, d.classid::regclass::text, d.objid;
```

```sql
-- P4.5.2  profile_title column: every pg_depend row referencing the column
--         must be (a) the column's own 'i' row, or (b) exactly ONE deptype 'n'
--         row whose dependent_object = ck_internal_users_functional_profile.
--         Anything else = FAIL.
SELECT d.deptype,
       d.classid::regclass AS dependent_class,
       CASE WHEN d.classid = 'pg_constraint'::regclass
            THEN (SELECT conname FROM pg_constraint WHERE oid = d.objid)
            ELSE d.objid::text END AS dependent_object
  FROM pg_depend d
 WHERE d.refobjid = 'public.internal_users'::regclass
   AND d.refobjsubid = (SELECT attnum FROM pg_attribute
                         WHERE attrelid = 'public.internal_users'::regclass
                           AND attname = 'profile_title' AND NOT attisdropped)
 ORDER BY d.deptype;
```

### P4.6 — ba_dmo_app has the expected pre-N34 privileges/posture

```sql
-- P4.6.1  Role exists, NOLOGIN (expect 1 row, rolcanlogin = FALSE).
SELECT rolname, rolcanlogin, rolconnlimit
  FROM pg_roles
 WHERE rolname = 'ba_dmo_app';

-- P4.6.2  Junction privileges for ba_dmo_app: NONE (expect ZERO rows).
SELECT grantee, privilege_type, is_grantable
  FROM information_schema.role_table_grants
 WHERE table_schema = 'public'
   AND table_name = 'internal_user_access_templates'
   AND grantee = 'ba_dmo_app';

-- P4.6.3  internal_users column-level grants for ba_dmo_app: exactly the 8
--         canonical columns × SELECT/INSERT/UPDATE (24 rows, no profile_title).
SELECT privilege_type, column_name
  FROM information_schema.role_column_grants
 WHERE table_schema = 'public'
   AND table_name = 'internal_users'
   AND grantee = 'ba_dmo_app'
 ORDER BY privilege_type, column_name;
```

**PASS condition (P4.6.3):** the ordered result is exactly the 24-row matrix
{SELECT, INSERT, UPDATE} × {actor_id, auth_user_id, template_id, display_name,
active, created_at_utc, updated_at_utc, modules_override}. ANY row mentioning
`profile_title` → FAIL (§16 STOP-4).

```sql
-- P4.6.4  No column-level grant references profile_title anywhere (ZERO rows).
SELECT table_schema, table_name, column_name, privilege_type
  FROM information_schema.role_column_grants
 WHERE grantee = 'ba_dmo_app' AND column_name = 'profile_title';

-- P4.6.5  Capture the full ACL picture of the four access-locus tables
--         (informational; re-run unchanged after N34 — see P6.8).
SELECT n.nspname, c.relname, c.relacl::text AS acl
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public'
   AND c.relname IN ('internal_users', 'internal_user_access_templates',
                     'access_templates', 'access_template_profiles')
 ORDER BY c.relname;
```

### P4.7 — Row count of the junction table (informational)

```sql
-- P4.7  Junction mirror row count (informational; value recorded for the
--       backup manifest and the §6.9 data-loss attestation).
SELECT COUNT(*) AS junction_rows FROM public.internal_user_access_templates;
```

### P4.8 — Null/non-null distribution for profile_title (informational)

```sql
-- P4.8.1  Distribution (informational).
SELECT COUNT(*)                                   AS total_users,
       COUNT(profile_title)                       AS non_null_profile_title,
       COUNT(*) - COUNT(profile_title)            AS null_profile_title
  FROM public.internal_users;

-- P4.8.2  Value distribution (informational — the closed domain is enforced by
--         the CHECK; expect only the three values or NULL).
SELECT profile_title, COUNT(*) AS users
  FROM public.internal_users
 GROUP BY profile_title
 ORDER BY profile_title NULLS LAST;
```

### P4.9 — Any unexpected data that would make removal unsafe

```sql
-- P4.9.1  Dangling junction references (should be impossible — hard FKs;
--         expect ZERO rows).
SELECT ut.actor_id, ut.template_id
  FROM public.internal_user_access_templates ut
  LEFT JOIN public.internal_users u ON u.actor_id = ut.actor_id
  LEFT JOIN public.access_templates t ON t.template_id = ut.template_id
 WHERE u.actor_id IS NULL OR t.template_id IS NULL
 ORDER BY ut.actor_id;

-- P4.9.2  Junction self-owned object inventory — expect EXACTLY:
--         3 indexes (pkey, ix_internal_user_access_templates_template,
--         ux_internal_user_access_templates_actor), 0 triggers,
--         1 policy (internal_user_access_templates_app_access). Anything else
--         = unexpected surface = FAIL.
SELECT 'index' AS kind, indexname AS name
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'internal_user_access_templates'
UNION ALL
SELECT 'trigger', tgname
  FROM pg_trigger t
 WHERE t.tgrelid = 'public.internal_user_access_templates'::regclass
   AND NOT t.tgisinternal
UNION ALL
SELECT 'policy', polname
  FROM pg_policy p
 WHERE p.polrelid = 'public.internal_user_access_templates'::regclass
 ORDER BY kind, name;
```

```sql
-- P4.9.3  Any other object anywhere whose name embeds the mirror identifiers
--         (expect ZERO rows — catches stragglers across schemas).
SELECT 'table' AS kind, schemaname AS schema, tablename AS name
  FROM pg_tables
 WHERE tablename ILIKE '%internal_user_access_templates%'
UNION ALL
SELECT 'index', schemaname, indexname
  FROM pg_indexes
 WHERE indexname ILIKE '%internal_user_access_templates%'
UNION ALL
SELECT 'constraint', n.nspname, c.conname
  FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
 WHERE c.conname ILIKE '%internal_user_access_templates%'
    OR c.conname ILIKE '%ck_internal_users_functional_profile%'
ORDER BY kind, schema, name;
```

### P4.10 — Pre-N34 index/function/trigger name-sets (capture for §6.7)

```sql
-- P4.10.1  Full public index set snapshot (capture to file
--          pre_n34_indexes.txt; re-run at P6.7.1 and after N35 for the
--          unrelated-indexes diff).
SELECT tablename, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
 ORDER BY tablename, indexname;
```

```sql
-- P4.10.2  Function name-set snapshot (expect the 3 names of §3.5).
SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS args
  FROM pg_proc p
  JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.prokind = 'f'
 ORDER BY p.proname, args;
```

```sql
-- P4.10.3  Trigger name-set snapshot (expect the 19 names of §3.5).
SELECT event_object_table, trigger_name, action_timing, event_manipulation
  FROM information_schema.triggers
 WHERE trigger_schema = 'public'
 ORDER BY event_object_table, trigger_name;
```

---

## 5. N34 Execution Expectations

### 5.1 Expected starting state

Everything in §4 passes on BA-DMO-DEV; the identity gate (§2.3) passed;
backup §5.4 completed and verified; bookkeeping shows no N34 record
(§2.3 P0.5 = ZERO rows).

### 5.2 Expected final state

- `internal_user_access_templates` gone (table + PK + both FKs + both indexes
  incl. `ux_internal_user_access_templates_actor` + inert policy + `assigned_at_utc`
  default + row type + TOAST).
- `internal_users.profile_title` gone; `ck_internal_users_functional_profile`
  gone.
- `internal_users` keeps exactly its 8 canonical columns; column-level grants
  untouched (N33 grants never named `profile_title`).
- Authority chain untouched: `access_templates`,
  `access_template_profiles` (+ its CHECK + N31 trigger
  `trg_access_templates_ensure_profile`), `internal_users.template_id` FK.
- Zero new objects, zero changes to functions/triggers/other tables.

### 5.3 Atomicity

**YES — fully atomic.** N34 is three statements executed as one whole-file
command inside one transaction (runner: `BeginTransaction`/`CommitAsync`;
Supabase CLI: per-file transaction; SQL-editor batch: implicit transaction).
Either all three take effect or none do. There is no partial state: a failure
at statement 2 rolls back statement 1. `IF EXISTS` guards additionally make
the file idempotent (safe to re-apply verbatim; a re-run is a no-op).

> ⚠️ **Idempotence caveat (must be read — not a failure, but evidence-blind):**
> because of `IF EXISTS`, applying N34 on a database where the mirrors were
> already removed **succeeds silently**. That is why §4 must pass first: it is
> what distinguishes "N34 removed these objects now" from "someone already
> removed them". If §4 passes and the §6 post-checks pass, the attribution is
> proven (pre + post).
>
> ⚠️ **Failure mode 42P01 note:** the FKs from the junction to
> `internal_users`/`access_templates` are dropped as part of `DROP TABLE`; the
> explicit `DROP CONSTRAINT … ck_internal_users_functional_profile` precedes
> `DROP COLUMN profile_title`. If the live DB ever held an *additional*
> dependent object that §4.3–§4.5 missed, PostgreSQL raises
> `dependent objects still exist` → the whole transaction rolls back → §16
> STOP-2/STOP-3. Good: it is designed to fail closed, never to CASCADE.

### 5.4 Pre-drop backup (execution-time; the rollback/recovery contract)

N34 is **one-way (destructive)**. Fossil `profile_title` values and junction
rows are discarded **by design** (dead since N33 — zero runtime readers/
writers, post-03B parity). Rollback is therefore **recorded-restore, not
schema-restore**: there is no in-place rollback SQL that restores the dropped
objects safely, and none is invented here. The recovery contract is:

```bash
# EXECUTION-TIME — run BEFORE applying N34, on BA-DMO-DEV, owner role.
# Data manifests for forensic reconstruction (schema is reconstructible from
# immutable N27/N31/N32/N33 history; DATA is not).
pg_dump "$BA_DMO_DEV_CONNECTION" \
  --data-only --table=public.internal_user_access_templates \
  --file=backup_YYYYMMDD_HHMM_n34_junction_data.sql
pg_dump "$BA_DMO_DEV_CONNECTION" \
  --data-only --table=public.internal_users \
  --file=backup_YYYYMMDD_HHMM_n34_internal_users_data.sql
# If column-fidelity of the fossil values is wanted verbatim:
pg_dump "$BA_DMO_DEV_CONNECTION" \
  --data-only --column-inserts --table=public.internal_users \
  --file=backup_YYYYMMDD_HHMM_n34_internal_users_column_inserts.sql
# Verify the manifests are non-empty of errors and store them outside the DB.
```

**Explicit rollback statement (no invented SQL):**
1. Restoring the **data** = `psql -f` the manifests above into a database
   where N27/N31/N32/N33 schema objects exist (a freshly replayed chain up to
   N33 reproduces the exact pre-N34 objects).
2. There is **no safe checkpoint-restore inside a live transaction** for DROP
   TABLE / DROP COLUMN — do not manufacture one.
3. The N34-era parity gates (§6) plus the backups are the accepted recovery
   contract (rationalization plan §17.5; N34 audit §6.6).

### 5.5 Failure modes (N34)

| Mode | Symptom | Consequence | Handling |
|---|---|---|---|
| Pre-checks (§4) negative | any P4.x FAIL | never reached — execution blocked | §16 STOP-2/STOP-3/STOP-4 |
| Unexpected live dependent object | `dependent objects still exist` (2BP01) → whole file rolls back | no partial removal | STOP, fingerprint the dependent object, do not CASCADE, reconcile with the N34 owner |
| Lock contention | `ALTER TABLE internal_users` waits on AccessExclusiveLock | stall, not failure | apply in a maintenance window; no concurrent writers expected on NOLOGIN technical role |
| Permission denied | 42501 | rollback | run as postgres/owner (never ba_dmo_app); land via the provenance path §2.2.3 |
| Bookkeeping write fails after success | migration applied but unrecorded | provenance gap | reconcile record manually per §2.2.3 Option B discipline; treat as STOP until recorded |

### 5.6 Checkpoint gate before N35

Do NOT start N35 until **all** §6 post-checks pass **and** the bookkeeping
record for N34 exists (P6.10). A single failed post-check = §16 STOP.

---

## 6. N34 Post-Checks

Run read-only on BA-DMO-DEV immediately after N34 applies. ZERO rows = PASS.

### P6.1 — The junction table no longer exists

```sql
-- P6.1  Junction absent (expect NULL oid).
SELECT to_regclass('public.internal_user_access_templates') AS junction_oid;
```

```sql
-- P6.1.2  Nothing anywhere still references the name (expect ZERO rows).
SELECT 'index' AS kind, schemaname AS s, indexname AS name
  FROM pg_indexes WHERE indexname ILIKE '%internal_user_access_templates%'
UNION ALL
SELECT 'constraint', n.nspname, conname
  FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
 WHERE conname ILIKE '%internal_user_access_templates%'
ORDER BY kind, s, name;
```

### P6.2 — profile_title no longer exists (column + CHECK)

```sql
-- P6.2.1  Column absent (expect ZERO rows).
SELECT a.attname
  FROM pg_attribute a
 WHERE a.attrelid = 'public.internal_users'::regclass
   AND a.attname = 'profile_title'
   AND NOT a.attisdropped;

-- P6.2.2  CHECK absent (expect ZERO rows).
SELECT conname
  FROM pg_constraint
 WHERE conrelid = 'public.internal_users'::regclass
   AND conname = 'ck_internal_users_functional_profile';

-- P6.2.3  internal_users now exposes exactly the 8 canonical columns,
--         ordered (expect 8 rows in this order):
--         actor_id, auth_user_id, template_id, display_name, active,
--         created_at_utc, updated_at_utc, modules_override.
SELECT a.attname
  FROM pg_attribute a
 WHERE a.attrelid = 'public.internal_users'::regclass
   AND a.attnum > 0 AND NOT a.attisdropped
 ORDER BY a.attnum;
```

### P6.3 — No remaining policy/grant/dependency references

```sql
-- P6.3.1  No policy named after the removed mirror (expect ZERO rows).
SELECT n.nspname, p.polname, p.polrelid::regclass::text AS on_table
  FROM pg_policy p
  JOIN pg_class c ON c.oid = p.polrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE p.polname ILIKE '%internal_user_access_templates%';

-- P6.3.2  No grant anywhere names profile_title (expect ZERO rows).
SELECT table_schema, table_name, column_name, grantee, privilege_type
  FROM information_schema.role_column_grants
 WHERE column_name = 'profile_title';
```

```sql
-- P6.3.3  Dependency-residue text scan: no view definition or function body
--         anywhere still references either mirror identifier (expect ZERO
--         rows). PostgreSQL removes pg_depend rows with their objects, so
--         catalog absence (P6.1/P6.2) plus this text scan is the strongest
--         executable dependency-residue evidence available.
SELECT 'view' AS kind, v.schemaname AS s, v.viewname AS name, v.definition AS body
  FROM pg_views v
 WHERE v.schemaname = 'public'
   AND (v.definition ILIKE '%internal_user_access_templates%'
     OR v.definition ILIKE '%profile_title%')
UNION ALL
SELECT 'function', n.nspname, p.proname, p.prosrc
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND (p.prosrc ILIKE '%internal_user_access_templates%'
     OR p.prosrc ILIKE '%profile_title%')
ORDER BY kind, s, name;
```

### P6.4 — Access authority remains represented by the canonical structures

```sql
-- P6.4.1  Canonical authority objects present (expect 4 rows, all non-NULL):
--         access_templates, access_template_profiles, internal_users,
--         and the internal_users.template_id FK.
SELECT 'access_templates'            AS object,
       to_regclass('public.access_templates')            AS present
UNION ALL SELECT 'access_template_profiles',
       to_regclass('public.access_template_profiles')
UNION ALL SELECT 'internal_users',
       to_regclass('public.internal_users')
UNION ALL SELECT 'template_id FK',
       (SELECT c.conname
          FROM pg_constraint c,
               unnest(c.conkey) WITH ORDINALITY k(attnum, ord)
         WHERE c.conrelid = 'public.internal_users'::regclass
           AND c.contype = 'f'
           AND c.confrelid = 'public.access_templates'::regclass
           AND k.ord = 1
           AND k.attnum = (SELECT a.attnum FROM pg_attribute a
                            WHERE a.attrelid = 'public.internal_users'::regclass
                              AND a.attname = 'template_id'));
```

```sql
-- P6.4.2  N31 authority intact: profile CHECK + ensure trigger + grants
--         (expect 1 CHECK row, 1 trigger row, 1 policy row named
--         ba_dmo_app_access — this table is pre-N36, so expect
--         access_template_profiles_app_access for now — and the 4 grants).
SELECT 'check' AS kind, c.conname AS name
  FROM pg_constraint c
 WHERE c.conrelid = 'public.access_template_profiles'::regclass
   AND c.contype = 'c'
UNION ALL
SELECT 'trigger', tgname
  FROM pg_trigger t
 WHERE t.tgrelid = 'public.access_template_profiles'::regclass
   AND NOT t.tgisinternal
UNION ALL
SELECT 'policy', polname
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
UNION ALL
SELECT 'grant', privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema = 'public' AND table_name = 'access_template_profiles'
   AND grantee = 'ba_dmo_app'
ORDER BY kind, name;
```

### P6.5 — Effective-access evidence (authority survives the mirror drop)

```sql
-- P6.5.1  Every user's effective-access inputs (review table; 03A §4.1
--         pattern — no mirror columns involved).
SELECT u.actor_id, u.display_name, u.active AS user_active, u.template_id,
       t.name AS template_name, t.active AS template_active,
       COALESCE(p.functional_profile, '<MISSING>') AS functional_profile
  FROM public.internal_users u
  JOIN public.access_templates t ON t.template_id = u.template_id
  LEFT JOIN public.access_template_profiles p ON p.template_id = t.template_id
 ORDER BY p.functional_profile NULLS LAST, u.display_name, u.actor_id;
```

```sql
-- P6.5.2  At least one active admin path must survive (self-lockout guard,
--         03A §3.3). Expect >= 1 — a 0 here is CRITICAL and stops the run.
SELECT COUNT(*) AS active_admin_paths
  FROM public.internal_users u
  JOIN public.access_templates t ON t.template_id = u.template_id
  JOIN public.access_template_profiles p ON p.template_id = t.template_id
 WHERE u.active AND p.functional_profile = 'Admin'
   AND t.active AND t.modules @> '[{"moduleId":"admin"}]'::jsonb;
```

### P6.6 — Canonical-column privileges unchanged for ba_dmo_app

```sql
-- P6.6  Re-run P4.6.3 verbatim: the 24-row matrix must be byte-identical to
--       the pre-N34 capture. Additionally assert the has_column_privilege
--       matrix (expect 24 'yes' rows for SELECT/INSERT/UPDATE × 8 columns).
SELECT privilege_type, column_name
  FROM information_schema.role_column_grants
 WHERE table_schema = 'public' AND table_name = 'internal_users'
   AND grantee = 'ba_dmo_app'
 ORDER BY privilege_type, column_name;
```

### P6.7 — No unexpected object was removed (diff against pre-N34 snapshots)

```sql
-- P6.7.1  Re-run P4.10.1 (index set). Expected delta vs pre_n34_indexes.txt:
--         exactly −3 rows, all junction-owned, all dying with the table:
--         internal_user_access_templates_pkey,
--         ix_internal_user_access_templates_template,
--         ux_internal_user_access_templates_actor.
--         Nothing else may change. Identical rows elsewhere = PASS.
SELECT tablename, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
 ORDER BY tablename, indexname;
```

```sql
-- P6.7.2  Functions re-run of P4.10.2 — expect the same 3 names.
SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS args
  FROM pg_proc p
  JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.prokind = 'f'
 ORDER BY p.proname, args;
```

```sql
-- P6.7.3  Triggers re-run of P4.10.3 — expect the same 19 names.
SELECT event_object_table, trigger_name
  FROM information_schema.triggers
 WHERE trigger_schema = 'public'
 ORDER BY event_object_table, trigger_name;
```

### P6.8 — Grant/ACL posture unchanged

```sql
-- P6.8  Re-run P4.6.5 (ACL capture on the four access-locus tables). Expected:
--       internal_user_access_templates row GONE; the other three byte-identical.
SELECT n.nspname, c.relname, c.relacl::text AS acl
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public'
   AND c.relname IN ('internal_users', 'internal_user_access_templates',
                     'access_templates', 'access_template_profiles')
 ORDER BY c.relname;
```

### P6.9 — Data-loss attestation (informational)

```sql
-- P6.9.1  Users still reference valid templates (zero rows = clean; the
--         authority chain is intact).
SELECT u.actor_id, u.template_id
  FROM public.internal_users u
  LEFT JOIN public.access_templates t ON t.template_id = u.template_id
 WHERE t.template_id IS NULL
 ORDER BY u.actor_id;
```

```sql
-- P6.9.2  Record the post-N34 internal_users count next to the P4.7/P4.8
--         captures in the execution report (informational).
SELECT COUNT(*) AS post_n34_user_count FROM public.internal_users;
```

### P6.10 — Migration bookkeeping

```sql
-- P6.10  N34 recorded in the provenance table the live DB owns
--        (expect >= 1 row for the n34 file; use the CLI table if that is the
--        live owner — P0.3 tells you which one exists/owns history).
SELECT version, filename, sha256, applied_at
  FROM public.schema_migrations
 WHERE filename = 'N34_legacy_access_mirror_removal.sql'
UNION ALL
SELECT version, name, NULL::text, applied_at
  FROM supabase_migrations.schema_migrations
 WHERE name ILIKE '%n34%' OR name ILIKE '%legacy_access_mirror_removal%';
```

> **Checkpoint gate (before N35):** P6.1–P6.8 all PASS, P6.10 records N34.
> Only then continue.

---

## 7. N35 Pre-Checks

Run read-only on BA-DMO-DEV after §6 passes, before applying N35.

### P7.1 — ix_bq_movements_noted_repairer does not already exist

```sql
-- P7.1  Target index absent (expect ZERO rows).
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'bq_movements'
   AND indexname = 'ix_bq_movements_noted_repairer';
```

### P7.2 — ix_pegamento_documentos_controlo exists (the redundant removal target)

```sql
-- P7.2  Redundant index present (expect exactly 1 row, btree on
--       pegamento_controlo_id).
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'pegamento_documentos'
   AND indexname = 'ix_pegamento_documentos_controlo';
```

### P7.3 — Current definitions of both tables and their index sets

```sql
-- P7.3.1  bq_movements / pegamento_documentos columns (informational —
--         record for the execution report).
SELECT table_name, column_name, data_type, is_nullable, column_default
  FROM information_schema.columns
 WHERE table_schema = 'public'
   AND table_name IN ('bq_movements', 'pegamento_documentos')
 ORDER BY table_name, ordinal_position;
```

```sql
-- P7.3.2  bq_movements current index set (no noted_repairer index today;
--         used for the post-N35 unrelated-indexes diff).
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'bq_movements'
 ORDER BY indexname;
```

```sql
-- P7.3.3  pegamento_documentos current index set — expect the UNIQUE
--         constraint index (pegamento_controlo_id) AND the redundant
--         ix_pegamento_documentos_controlo.
SELECT indexname, indexdef, indexdef LIKE '%UNIQUE%' AS is_unique
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'pegamento_documentos'
 ORDER BY indexname;
```

```sql
-- P7.3.4  The 1:1 UNIQUE constraint definition (survives N35; capture for
--         comparison at P9.4).
SELECT conname, pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE conrelid = 'public.pegamento_documentos'::regclass
   AND contype = 'u'
 ORDER BY conname;
```

### P7.4 — No unexpected index already covers the same keys/predicate

```sql
-- P7.4.1  Any index on bq_movements whose LEADING key column is
--         noted_repairer_id (full-prefix redundancy check — expect ZERO rows).
SELECT i.indexrelid::regclass::text AS index_name,
       pg_get_indexdef(i.indexrelid) AS definition,
       pg_get_expr(i.indpred, i.indrelid) AS predicate
  FROM pg_index i
  JOIN pg_class c ON c.oid = i.indrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname = 'bq_movements'
   AND (i.indkey::int2[])[1] = (SELECT attnum FROM pg_attribute
                                 WHERE attrelid = c.oid
                                   AND attname = 'noted_repairer_id'
                                   AND NOT attisdropped);
```

```sql
-- P7.4.2  Full-prefix: ANY index whose first N key columns BIND
--         noted_repairer_id as column 1 (same as P7.4.1 — kept separate for
--         readability) PLUS informational listing of every bq_movements index
--         key (to eyeball no exotic prefix covers it).
SELECT i.indexrelid::regclass::text AS index_name,
       (SELECT string_agg(a.attname, ', ' ORDER BY k.ord)
          FROM unnest(i.indkey::int2[]) WITH ORDINALITY k(attnum, ord)
          JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = k.attnum
        ) AS key_columns,
       pg_get_expr(i.indpred, i.indrelid) AS predicate
  FROM pg_index i
  JOIN pg_class c ON c.oid = i.indrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname = 'bq_movements'
 ORDER BY 1;
```

**PASS condition:** P7.4.1 returns ZERO rows **and** P7.4.2 shows no composite
whose first column is `noted_repairer_id` and no partial index with that
leading key.

### P7.5 — Full public index-set capture (for the "no unrelated indexes changed" diff)

```sql
-- P7.5  Capture to file pre_n35_indexes.txt (same query as P4.10.1;
--       post-N35 P9.5 diff allows exactly +ix_bq_movements_noted_repairer,
--       −ix_pegamento_documentos_controlo).
SELECT tablename, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
 ORDER BY tablename, indexname;
```

### P7.6 — BQ-10 CHECK captured (unchanged proof for P9.6)

```sql
-- P7.6  ck_bq_movements_type definition BEFORE N35 — the 'fim' value MUST
--       remain present after N35 (BQ-10 excluded; OD-16 not taken).
SELECT conname, pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE conrelid = 'public.bq_movements'::regclass
   AND conname = 'ck_bq_movements_type';
```

---

## 8. N35 Execution Expectations

### 8.1 Expected starting state

§6 pass; §7 pre-checks pass; bookkeeping shows N34 recorded and no N35 record.

### 8.2 Expected final state

- `ix_bq_movements_noted_repairer` EXISTS: plain btree, single column
  `bq_movements(noted_repairer_id)`, no predicate, no uniqueness.
- `ix_pegamento_documentos_controlo` GONE.
- `pegamento_documentos` keeps its UNIQUE `(pegamento_controlo_id)`
  constraint index (the redundant standalone index was the only removal).
- No other index, table, column, constraint, function, trigger or policy
  changes. `ck_bq_movements_type` byte-identical.
- Zero data movement, zero locks beyond the index objects.

### 8.3 Atomicity

**YES — fully atomic** (same whole-file/one-transaction semantics as §5.3).
CREATE INDEX + DROP INDEX either both commit or both roll back. `IF EXISTS`
guards make the file idempotent.

> ⚠️ Same evidence-blind idempotence caveat as N34: pre-checks §7 are what
> prove the add/drop actually happened during N35.

### 8.4 Failure modes (N35)

| Mode | Symptom | Consequence | Handling |
|---|---|---|---|
| Target index already exists with a conflicting definition | `IF EXISTS` makes CREATE silent; the catalog shows a different definition | N35 "succeeds" with the wrong shape | P7.1/P7.4 are the guard — if they passed, a conflicting index cannot pre-exist; if P9.1 shows a wrong definition → STOP, fingerprint |
| Lock contention on `bq_movements` | non-concurrent CREATE INDEX takes AccessExclusiveLock | stall on large tables | maintenance window; do not switch to CONCURRENTLY (would break the runner's one-transaction model); movement table is append-only and small |
| Permission denied | 42501 | rollback | owner role |
| Bookkeeping write fails after success | applied, unrecorded | provenance gap | §5.6-style reconciliation; STOP until recorded |

Rollback: N35 is **fully reversible in place**:
`DROP INDEX ix_bq_movements_noted_repairer;` and
`CREATE INDEX ix_pegamento_documentos_controlo ON pegamento_documentos (pegamento_controlo_id);`
(N14:20-21 exact shape). Zero behavior impact.

### 8.5 Checkpoint gate before N36

Do NOT start N36 until all §9 post-checks pass and N35 is recorded. One
failed post-check = §16 STOP.

---

## 9. N35 Post-Checks

Run read-only on BA-DMO-DEV immediately after N35 applies. ZERO rows = PASS.

### P9.1 — ix_bq_movements_noted_repairer exists with the exact intended definition

```sql
-- P9.1  Exact index definition (expect exactly
--       'CREATE INDEX ix_bq_movements_noted_repairer ON public.bq_movements
--               USING btree (noted_repairer_id)'
--       — single column, btree, no predicate).
SELECT indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'bq_movements'
   AND indexname = 'ix_bq_movements_noted_repairer';
```

```sql
-- P9.1.2  Structural proof: plain (non-unique, non-PK) index on exactly
--         noted_repairer_id, no predicate (expect 1 row: unique=f, primary=f,
--         predicate=NULL, key='noted_repairer_id').
SELECT c.relname AS table_name,
       i.indexrelid::regclass::text AS index_name,
       i.indisunique, i.indisprimary,
       (SELECT string_agg(a.attname, ', ' ORDER BY k.ord)
          FROM unnest(i.indkey::int2[]) WITH ORDINALITY k(attnum, ord)
          JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = k.attnum) AS key_columns,
       pg_get_expr(i.indpred, i.indrelid) AS predicate
  FROM pg_index i
  JOIN pg_class c ON c.oid = i.indrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public'
   AND i.indexrelid = 'public.ix_bq_movements_noted_repairer'::regclass;
```

### P9.2 — ix_pegamento_documentos_controlo no longer exists

```sql
-- P9.2  Redundant index absent (expect ZERO rows).
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'pegamento_documentos'
   AND indexname = 'ix_pegamento_documentos_controlo';
```

### P9.3 — The UNIQUE constraint index survives

```sql
-- P9.3  pegamento_documentos index set = exactly the UNIQUE constraint index
--       (expect 1 row; indexdef contains 'CREATE UNIQUE INDEX').
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public' AND tablename = 'pegamento_documentos'
 ORDER BY indexname;
```

```sql
-- P9.3.2  Constraint recorded, unchanged (re-run of P7.3.4; byte-identical).
SELECT conname, pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE conrelid = 'public.pegamento_documentos'::regclass
   AND contype = 'u'
 ORDER BY conname;
```

### P9.4 — No unrelated indexes were changed (two-run diff)

**Protocol:** re-run P7.5 → `post_n35_indexes.txt`; `diff pre_n35_indexes.txt
post_n35_indexes.txt` must contain **exactly**:
- `+` `bq_movements|ix_bq_movements_noted_repairer|CREATE INDEX …`
- `−` `pegamento_documentos|ix_pegamento_documentos_controlo|CREATE INDEX …`

Any other line = unrelated index change = **FAIL** (§16 STOP).

```sql
-- P9.4  Post-N35 full index set (feed the diff).
SELECT tablename, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
 ORDER BY tablename, indexname;
```

### P9.5 — BQ-10 CHECK remains untouched

```sql
-- P9.5  ck_bq_movements_type AFTER N35 — must be byte-identical to the P7.6
--       capture (and still contain 'fim').
SELECT conname, pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE conrelid = 'public.bq_movements'::regclass
   AND conname = 'ck_bq_movements_type';
```

**PASS condition:** P7.6 definition == P9.5 definition (string-equal; the
`'fim'` literal is still in the list).

### P9.6 — Auxiliary invariants (functions, triggers, tables)

```sql
-- P9.6.1  Functions (expect the same 3) and triggers (expect the same 19):
SELECT 'fn' AS kind, p.proname AS name
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.prokind = 'f'
UNION ALL
SELECT 'tg', tgname
  FROM pg_trigger t
 WHERE NOT t.tgisinternal
   AND t.tgrelid IN (SELECT c.oid FROM pg_class c JOIN pg_namespace n
                       ON n.oid = c.relnamespace WHERE n.nspname = 'public')
ORDER BY kind, name;
```

```sql
-- P9.6.2  RLS still enabled on every app table (expect ZERO rows = none
--         disabled; 60 tables).
SELECT c.relname
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
   AND NOT c.relrowsecurity
 ORDER BY c.relname;
```

### P9.7 — Migration bookkeeping

```sql
-- P9.7  N35 recorded (expect >= 1 row; same dual-table pattern as P6.10).
SELECT version, filename, sha256, applied_at
  FROM public.schema_migrations
 WHERE filename = 'N35_index_rationalization.sql'
UNION ALL
SELECT version, name, NULL::text, applied_at
  FROM supabase_migrations.schema_migrations
 WHERE name ILIKE '%n35%' OR name ILIKE '%index_rationalization%';
```

> **Checkpoint gate (before N36):** P9.1–P9.6 all PASS, P9.7 records N35.
> Only then continue.

---

## 10. N36 Pre-Checks

Run read-only on BA-DMO-DEV after §9 passes, before applying N36.

### P10.1 — The divergent policy name exists on access_template_profiles

```sql
-- P10.1  access_template_profiles_app_access present (expect exactly 1 row).
SELECT p.polname, p.polrelid::regclass::text AS on_table
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'access_template_profiles_app_access';
```

### P10.2 — ba_dmo_app_access does NOT yet exist on this table

```sql
-- P10.2  Convention name ABSENT on access_template_profiles (expect ZERO rows).
SELECT p.polname
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

### P10.3 — Capture the exact current policy definition (semantic-parity anchor)

```sql
-- P10.3  Full definition of the divergent policy. Expected:
--        polname        = access_template_profiles_app_access
--        polcmd         = *                 (FOR ALL)
--        roles          = ba_dmo_app
--        using_expr     = true
--        check_expr     = true
SELECT p.polname,
       p.polcmd,
       ARRAY(SELECT r.rolname FROM pg_roles r WHERE r.oid = ANY(p.polroles))
         AS roles,
       pg_get_expr(p.polqual, p.polrelid)      AS using_expr,
       pg_get_expr(p.polwithcheck, p.polrelid) AS check_expr
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass;
```

### P10.4 — Capture RLS enabled/forced state of the target table

```sql
-- P10.4  RLS posture of access_template_profiles: expect relrowsecurity = t,
--        relforcerowsecurity = f.
SELECT c.relname,
       c.relrowsecurity     AS rls_enabled,
       c.relforcerowsecurity AS rls_forced
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname = 'access_template_profiles';
```

### P10.5 — Capture grants on access_template_profiles (unchanged proof)

```sql
-- P10.5  ba_dmo_app grants on the table (expect SELECT, INSERT, UPDATE,
--        DELETE — 4 rows; grantor normalized away).
SELECT DISTINCT grantee, privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema = 'public' AND table_name = 'access_template_profiles'
   AND grantee = 'ba_dmo_app'
 ORDER BY privilege_type;
```

### P10.6 — Capture the global policy inventory (pre-N36 snapshot)

```sql
-- P10.6  Expected: 60 application tables each with exactly 1 policy; 59 rows
--        named ba_dmo_app_access + 1 row access_template_profiles_app_access.
SELECT c.relname AS table_name, p.polname
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
  JOIN pg_policy p ON p.polrelid = c.oid
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname;
```

```sql
-- P10.6.2  RLS posture of every app table (informational; expect 60 rows
--          rls_enabled = t, none forced).
SELECT c.relname, c.relrowsecurity AS rls_enabled, c.relforcerowsecurity AS rls_forced
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname;
```

### P10.7 — Policy name references outside the catalog (grep gate, repo-side)

```bash
# P10.7  Repository guard — run from the repo root on the tag being deployed:
grep -rn "access_template_profiles_app_access" src/ || echo "ZERO src references"
# Expect ZERO matches in src/ (no runtime code names policies — pre-verified).
```

---

## 11. N36 Execution Expectations

### 11.1 Expected starting state

§9 passes; §10 pre-checks pass; bookkeeping shows N35 recorded and no N36
record.

### 11.2 Expected final state

- On `access_template_profiles`: exactly **one** policy, named
  `ba_dmo_app_access`, with semantics **byte-for-byte identical** to the N31
  original (`FOR ALL TO ba_dmo_app USING (TRUE) WITH CHECK (TRUE)`).
- The name `access_template_profiles_app_access` is gone everywhere.
- Grants on the table unchanged (SELECT/INSERT/UPDATE/DELETE to `ba_dmo_app`).
- RLS enabled unchanged; forced unchanged (false).
- Every application table now carries exactly one policy and every policy is
  named `ba_dmo_app_access` (60 tables / 60 policies / 1 name).
- No other object changes; zero data movement.

### 11.3 Atomicity

**YES — fully atomic** (whole-file/one-transaction). The drop + create either
both commit or both roll back. `DROP POLICY IF EXISTS` on both names makes the
file idempotent for pre-N36 and already-renamed databases.

### 11.4 Failure modes (N36)

| Mode | Symptom | Consequence | Handling |
|---|---|---|---|
| Policy drop fails (permission) | 42501 | rollback | run as the policy-owning role (owner) |
| A conflicting `ba_dmo_app_access` pre-exists with DIFFERENT semantics on this table | `DROP POLICY IF EXISTS ba_dmo_app_access` removes it, then CREATE installs the canonical body | P10.2 would have caught presence; if pre-check passed, no conflict existed; if the post body differs from the P10.3 anchor → STOP (semantic drift) |
| Policy body text drift in the file (should never happen — file is immutable, hash-pinned) | final body ≠ anchor | STOP-8/STOP-9, do not "fix forward" | compare P10.3 vs P12.2 |

Rollback: N36 is **fully reversible in place** (cosmetic):

```sql
-- Rollback ONLY (not part of the approved forward sequence):
DROP POLICY IF EXISTS ba_dmo_app_access ON access_template_profiles;
CREATE POLICY access_template_profiles_app_access
    ON access_template_profiles
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);
```

This restores the exact N31-era name with identical semantics; RLS and grants
are untouched by the rollback.

### 11.5 Checkpoint gate before the final inventory

Do NOT start §13 inventory, §14 equivalence, or §15 tests until all §12
post-checks pass and N36 is recorded. One failed post-check = §16 STOP.

---

## 12. N36 Post-Checks

Run read-only on BA-DMO-DEV immediately after N36 applies. ZERO rows = PASS.
**The goal is semantic parity, not merely name presence.**

### P12.1 — ba_dmo_app_access exists; divergent name gone

```sql
-- P12.1.1  Convention policy present on access_template_profiles
--          (expect exactly 1 row).
SELECT p.polname
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';

-- P12.1.2  Divergent policy name gone everywhere (expect ZERO rows).
SELECT n.nspname, c.relname, p.polname
  FROM pg_policy p
  JOIN pg_class c ON c.oid = p.polrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE p.polname = 'access_template_profiles_app_access';
```

### P12.2 — Policy command is unchanged (FOR ALL)

```sql
-- P12.2  polcmd of the surviving policy (expect '*' = FOR ALL: SELECT+INSERT+
--        UPDATE+DELETE in one policy; byte-equal to the P10.3 capture).
SELECT p.polcmd
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

### P12.3 — Roles are unchanged

```sql
-- P12.3  Role set (expect exactly {ba_dmo_app}).
SELECT ARRAY(SELECT r.rolname FROM pg_roles r WHERE r.oid = ANY(p.polroles))
         AS roles
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

### P12.4 — USING expression is unchanged

```sql
-- P12.4  pg_get_expr(polqual) (expect 'true' — byte-equal to P10.3 using_expr).
SELECT pg_get_expr(p.polqual, p.polrelid) AS using_expr
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

### P12.5 — WITH CHECK expression is unchanged

```sql
-- P12.5  pg_get_expr(polwithcheck) (expect 'true' — byte-equal to P10.3
--        check_expr).
SELECT pg_get_expr(p.polwithcheck, p.polrelid) AS check_expr
  FROM pg_policy p
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

### P12.6 — RLS posture unchanged

```sql
-- P12.6  relrowsecurity = t, relforcerowsecurity = f — byte-equal to P10.4.
SELECT c.relname, c.relrowsecurity AS rls_enabled, c.relforcerowsecurity AS rls_forced
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname = 'access_template_profiles';
```

### P12.7 — Grants are unchanged

```sql
-- P12.7  Re-run P10.5 — must be byte-identical (SELECT, INSERT, UPDATE,
--        DELETE to ba_dmo_app; grantor normalized away).
SELECT DISTINCT grantee, privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema = 'public' AND table_name = 'access_template_profiles'
   AND grantee = 'ba_dmo_app'
 ORDER BY privilege_type;
```

### P12.8 — Global policy inventory: one name, one policy per table

```sql
-- P12.8.1  Policy inventory (expect 60 rows, every polname =
--          ba_dmo_app_access; nothing else).
SELECT c.relname AS table_name, p.polname
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
  JOIN pg_policy p ON p.polrelid = c.oid
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname;
```

```sql
-- P12.8.2  Any divergent policy name anywhere (expect ZERO rows).
SELECT DISTINCT p.polname
  FROM pg_policy p
  JOIN pg_class c ON c.oid = p.polrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname <> 'schema_migrations'
   AND p.polname <> 'ba_dmo_app_access';
```

```sql
-- P12.8.3  Exactly ONE policy per application table (expect ZERO rows =
--          no table with 0 or 2+ policies).
SELECT c.relname, COUNT(p.polname) AS policy_count
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
  LEFT JOIN pg_policy p ON p.polrelid = c.oid
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 GROUP BY c.relname
HAVING COUNT(p.polname) <> 1
 ORDER BY c.relname;
```

### P12.9 — Semantic parity attestation (the N36 proof)

```sql
-- P12.9  THE parity query: the surviving policy's (table, polcmd, roles,
--        using_expr, check_expr) must be EXACTLY
--        ('access_template_profiles', '*', '{ba_dmo_app}', 'true', 'true')
--        — the same tuple captured at P10.3 for the old name.
SELECT c.relname AS table_name,
       (SELECT rolname FROM pg_roles WHERE oid = p.polroles[1]) AS sole_role,
       p.polcmd,
       pg_get_expr(p.polqual, p.polrelid) AS using_expr,
       pg_get_expr(p.polwithcheck, p.polrelid) AS check_expr
  FROM pg_policy p
  JOIN pg_class c ON c.oid = p.polrelid
 WHERE p.polrelid = 'public.access_template_profiles'::regclass
   AND p.polname = 'ba_dmo_app_access';
```

**Expected row:** `access_template_profiles | ba_dmo_app | * | true | true`.
Any deviation = semantic drift = §16 STOP-7 (a rename must NEVER change
permission semantics).

### P12.10 — Optional session-scoped behavioural read probe (SELECT-only)

```sql
-- P12.10  Behavioural confirmation that the policy is permissive and active
--         for ba_dmo_app: session-scoped SET ROLE (no data change), SELECT
--         COUNT, then RESET ROLE in the SAME batch. The count must equal the
--         post-N34 template-profile count (P6.4.2 context implies > 0).
SET ROLE ba_dmo_app;
SELECT COUNT(*) AS visible_template_profiles FROM public.access_template_profiles;
RESET ROLE;
```

> This is read-only (session role switch is not DML). If your identity-gate
> role cannot `SET ROLE` to `ba_dmo_app`, treat this probe as skipped, not
> failed — the catalog parity (P12.9) is the authoritative proof.

### P12.11 — Migration bookkeeping

```sql
-- P12.11  N36 recorded (expect >= 1 row; dual-table pattern of P6.10).
SELECT version, filename, sha256, applied_at
  FROM public.schema_migrations
 WHERE filename = 'N36_ba_dmo_app_access_policy_rename.sql'
UNION ALL
SELECT version, name, NULL::text, applied_at
  FROM supabase_migrations.schema_migrations
 WHERE name ILIKE '%n36%' OR name ILIKE '%policy_rename%';
```

> **Checkpoint gate:** P12.1–P12.9 all PASS, P12.11 records N36. Only then
> proceed to §13.

---

## 13. Final Catalog Inventory Queries

Capture the complete post-N36 catalog on BA-DMO-DEV with deterministic
ordering so outputs can be archived and diffed against the clean-install
equivalence run (§14) and future baselines. **Expected final counts:**

| Artifact | Expected |
|---|---|
| Application tables (public, excl. `schema_migrations`) | **60** |
| Indexes | **79** |
| Functions | **3** |
| Triggers | **19** |
| RLS-enabled tables | **60** (0 disabled) |
| Policies | **60**, all named `ba_dmo_app_access` |
| Roles/grants | `ba_dmo_app` technical role; grants per §13.9 |

### 13.1 Counts (headline)

```sql
-- 13.1.1  Table count (expect 60).
SELECT COUNT(*) AS app_table_count
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations';
```

```sql
-- 13.1.2  Index count (expect 79).
SELECT COUNT(*) AS index_count
  FROM pg_indexes WHERE schemaname = 'public';
```

```sql
-- 13.1.3  Function count (expect 3).
SELECT COUNT(*) AS function_count
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.prokind = 'f';
```

```sql
-- 13.1.4  Trigger count (expect 19).
SELECT COUNT(*) AS trigger_count
  FROM information_schema.triggers WHERE trigger_schema = 'public';
```

```sql
-- 13.1.5  Policy count (expect 60) and name uniformity (expect 1 distinct
--         name, ba_dmo_app_access).
SELECT COUNT(*) AS policy_count,
       COUNT(DISTINCT p.polname) AS distinct_policy_names
  FROM pg_policy p
  JOIN pg_class c ON c.oid = p.polrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname <> 'schema_migrations';
```

### 13.2 Tables + columns (types, nullability, defaults, identity)

```sql
-- 13.2  Full table/column inventory (deterministic order: table, attnum).
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
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname, a.attnum;
```

### 13.3 Primary keys

```sql
-- 13.3  PKs (contype 'p'), deterministic order.
SELECT conrelid::regclass::text AS table_name,
       conname,
       pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE connamespace = 'public'::regnamespace AND contype = 'p'
 ORDER BY 1, 2;
```

### 13.4 Foreign keys

```sql
-- 13.4  FKs (contype 'f'), deterministic order.
SELECT conrelid::regclass::text AS table_name,
       conname,
       pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE connamespace = 'public'::regnamespace AND contype = 'f'
 ORDER BY 1, 2;
```

### 13.5 Unique constraints

```sql
-- 13.5  Uniques (contype 'u').
SELECT conrelid::regclass::text AS table_name,
       conname,
       pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE connamespace = 'public'::regnamespace AND contype = 'u'
 ORDER BY 1, 2;
```

### 13.6 CHECK constraints

```sql
-- 13.6  Checks (contype 'c'), including ck_bq_movements_type (BQ-10) and the
--       absence of ck_internal_users_functional_profile.
SELECT conrelid::regclass::text AS table_name,
       conname,
       pg_get_constraintdef(oid) AS definition
  FROM pg_constraint
 WHERE connamespace = 'public'::regnamespace AND contype = 'c'
 ORDER BY 1, 2;
```

### 13.7 Indexes

```sql
-- 13.7  Full index inventory (79 rows), deterministic order.
SELECT tablename, indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
 ORDER BY tablename, indexname;
```

### 13.8 Functions

```sql
-- 13.8  Functions (3 rows), deterministic order.
SELECT p.proname,
       pg_get_function_identity_arguments(p.oid) AS args,
       pg_get_function_result(p.oid) AS result,
       p.prosrc
  FROM pg_proc p
  JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.prokind = 'f'
 ORDER BY p.proname, args;
```

### 13.9 Triggers

```sql
-- 13.9  Triggers (19 rows), deterministic order (timing + event folded into
--       one ordering key).
SELECT event_object_table,
       trigger_name,
       action_timing,
       event_manipulation,
       action_statement
  FROM information_schema.triggers
 WHERE trigger_schema = 'public'
 ORDER BY event_object_table, trigger_name;
```

### 13.10 RLS-enabled tables

```sql
-- 13.10  RLS posture per table (expect 60 rows, all rls_enabled = t,
--        rls_forced = f).
SELECT c.relname,
       c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS rls_forced
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname;
```

```sql
-- 13.10.2  Any table with RLS disabled (expect ZERO rows).
SELECT c.relname
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations' AND NOT c.relrowsecurity
 ORDER BY c.relname;
```

### 13.11 Policies

```sql
-- 13.11  Every policy with its semantic body (60 rows, all ba_dmo_app_access).
SELECT c.relname AS table_name,
       p.polname,
       p.polcmd,
       ARRAY(SELECT r.rolname FROM pg_roles r WHERE r.oid = ANY(p.polroles)) AS roles,
       pg_get_expr(p.polqual, p.polrelid)      AS using_expr,
       pg_get_expr(p.polwithcheck, p.polrelid) AS check_expr
  FROM pg_class c
  JOIN pg_namespace n ON n.oid = c.relnamespace
  JOIN pg_policy p ON p.polrelid = c.oid
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 ORDER BY c.relname;
```

### 13.12 Grants

```sql
-- 13.12.1  Table-level grants for ba_dmo_app (grantor normalized away;
--          grants to postgres/service_role excluded — compare on the
--          application role).
SELECT DISTINCT table_name, privilege_type
  FROM information_schema.role_table_grants
 WHERE table_schema = 'public' AND grantee = 'ba_dmo_app'
   AND table_name <> 'schema_migrations'
 ORDER BY table_name, privilege_type;
```

```sql
-- 13.12.2  Column-level grants for ba_dmo_app (internal_users presents the
--          24-row canonical matrix; anything else is suspect).
SELECT table_name, column_name, privilege_type
  FROM information_schema.role_column_grants
 WHERE table_schema = 'public' AND grantee = 'ba_dmo_app'
   AND table_name <> 'schema_migrations'
 ORDER BY table_name, column_name, privilege_type;
```

### 13.13 Deviance guards (one-query summary)

```sql
-- 13.13  Any deviance from the approved final state (expect ZERO rows across
--        the three unions): RLS disabled · policy name not the convention ·
--        tables with != 1 policy.
SELECT 'rls_disabled' AS deviance, c.relname AS object
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations' AND NOT c.relrowsecurity
UNION ALL
SELECT 'nonconvention_policy', p.polname
  FROM pg_policy p JOIN pg_class c ON c.oid = p.polrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relname <> 'schema_migrations'
   AND p.polname <> 'ba_dmo_app_access'
UNION ALL
SELECT 'policy_count_ne_1', c.relname
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  LEFT JOIN pg_policy p ON p.polrelid = c.oid
 WHERE n.nspname = 'public' AND c.relkind = 'r'
   AND c.relname <> 'schema_migrations'
 GROUP BY c.relname
HAVING COUNT(p.polname) <> 1
ORDER BY 1, 2;
```

---

## 14. Clean-Install Equivalence Protocol

**Goal (not executed now):** prove the two build paths produce
*structurally equivalent* final databases at the N36 end state:

- **Path A — chain:** empty scratch DB → apply `database/migrations/N01…N36`
  in canonical order (runner whole-script semantics, or
  `psql -f` per file in order).
- **Path B — consolidated:** empty scratch DB → execute
  `database/consolidated_clean_install.sql` in one pass.

### 14.1 Procedure (execution-time; disposable databases only)

```bash
# 1. Scratch A
createdb ba_dmo_equiv_a
# apply N01..N36 in order (migrate CLI or per-file psql -f)
# 2. Scratch B
createdb ba_dmo_equiv_b
psql -f database/consolidated_clean_install.sql ba_dmo_equiv_b
# 3. Run the canonical snapshot set (§13.1–§13.12 — the SAME queries) on both
#    databases; redirect each to snapshot_a.sql / snapshot_b.sql
# 4. Normalize (below) -> diff -u snapshot_a.sql snapshot_b.sql
```

> On Supabase-hosted scratch space the consolidated file's guarded role/default
> privilege statements become NOTICEs — that is by design (file header
> documents it). Ownership/ACL of `schema_migrations` may differ by execution
> role — allow-list item 1/5 below.

### 14.2 Canonical comparison queries

Use **exactly** the §13.2–§13.12 query set (tables+columns+types+nullability+
defaults, PKs, FKs, uniques, CHECKs, indexes, functions, triggers, RLS,
policies, grants) on both paths. They are already deterministically ordered.

### 14.3 Normalization rules (kill false diffs)

1. **Exclude `schema_migrations` rows** from every §13 query (done by
   construction) — its *content* differs by design (Path A: 36 records;
   Path B: none/`consolidated_clean_install`). Only its **existence and RLS
   posture** are compared.
2. **Grantor column dropped** from all grant queries (`DISTINCT grantee,
   privilege_type`) — grantor may be `postgres` vs `ba_dmo_migrate`.
3. **Ownership/ACL (`relacl`), OIDs, relfilenode, stats — never compared.**
   `pg_get_*` output, `format_type`, and `pg_get_constraintdef/IndexDef` are
   already canonical text; do not re-format them.
4. **Boolean representation:** keep PostgreSQL canonical `t`/`f`; do not
   convert in one path only.
5. **Default expressions:** `pg_get_expr(d.adbin,...)` is canonical on both
   paths even when the source file spelled `now()` vs `CURRENT_TIMESTAMP`.
6. **Invariant-custom casing:** do NOT lowercase `pg_get_*` text — the
   canonical renderers are shared; a diff there is a real diff.
7. **Trailing whitespace / line endings:** strip before diff
   (`dos2unix` / `sed 's/[[:space:]]*$//'`).

### 14.4 Verdict definitions

| Verdict | Condition |
|---|---|
| **PASS** | After normalization + documented allow-list, `diff -u` between Path A and Path B snapshots is **empty**. |
| **EXPECTED_DIFFERENCE** | Every diff line maps to a documented allow-list item: (1) `schema_migrations` content; (2) `GRANT USAGE ON SCHEMA public` (N01) intentionally absent from Path B; (3) `ALTER DEFAULT PRIVILEGES` (N01) comment-kept on Path B; (4) N27/N28/N29/N32 reconciliation DML not reproduced on Path B (no-ops on an empty DB — final catalog unaffected); (5) ownership/ACL of `schema_migrations`/`ba_dmo_migrate` objects by execution role. These are EXPECTED and do not block. |
| **FAIL** | Any diff line **outside** the allow-list. STOP: identify chain-vs-file drift, fix on the **consolidated file side only** (migrations N01–N36 are immutable), re-run the protocol. Do not ship the next migration. |

### 14.5 Additional checks on Path A (behavioral coverage)

The PG-gated test suites (`RemediationGuardTests` N34/N35/N36 probes,
`*PostgresTests`, `RepairAtomicityTests`) run against Path A's database
(`BA_DMO_TEST_DATABASE`); Path B's object identity is covered by the catalog
diff.

### 14.6 Cadence

Run once per destructive checkpoint. **This package mandates the N36 run** on
disposable scratch databases. Do not perform the comparison on BA-DMO-DEV
itself (or anywhere near PROD); scratch DBs only. **NOT EXECUTED NOW.**

---

## 15. Application/Test Verification

Run on the **repository tree at the deployed N36 tag** after DEV migration
execution completes, on the local dev machine (no DB needed for 15.1–15.3;
PG-gated suites need `BA_DMO_TEST_DATABASE`).

### 15.1 Solution build

```bash
dotnet build BA-DMO.sln -c Debug -m:1
```

- **Expected:** PASS, 0 errors. (Host workaround: multi-node MSBuild fan-out
  hangs on this machine — `-m:1` produces an identical result.) Known
  warnings: 10× NU1900 (offline NuGet vulnerability-data fetch, pre-existing),
  3× CS8601 in `AdminUserListResetTests` (pre-existing, untouched).

### 15.2 Full unit suite

```bash
dotnet vstest AI-CONTEXT/docs/tests/BA.Dmo.UnitTests/bin/Debug/net10.0/BA.Dmo.UnitTests.dll
```

- **Expected:** Failed: 0, Passed: **660/660**, Skipped: 0.

### 15.3 Full integration suite

```bash
dotnet vstest AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/bin/Debug/net10.0/BA.Dmo.IntegrationTests.dll
```

- **Expected:** **319 passed / 1 failed**, and the one failure is exactly
  `Access.ShellRoutingTests.Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn`
  (pre-existing, owner-declared unrelated debt — never "fixed" in schema
  work). Any NEW or different failure = STOP (no regression acceptable).
- PG-gated suites self-skip without `BA_DMO_TEST_DATABASE` and count as
  vacuous passes at this stage(→ §15.4).

### 15.4 Focused migration guards + PostgreSQL schema guards

```bash
# 15.4.1  DB-less focused guards (expect 45/45):
dotnet vstest AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/bin/Debug/net10.0/BA.Dmo.IntegrationTests.dll \
  --TestCaseFilter:"MigrationDiscoveryTests | AccessMirrorQuiescenceGuardTests | AccessAuthorityGuardTests | DapperAdminRepositoryProjectionTests | RemediationGuardTests"
```

- **Expected:** **45/45 PASSED**, including:
  - `MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN36`
    (exact 36-file family), `N34_RemovesBothLegacyAccessMirrors_Explicitly_NoCascade`,
    `N35_AddsBqRepairerIndex_AndDropsRedundantPegamentoDocumentosIndex`,
    `N36_UnifiesPolicyNaming_WithIdenticalSemantics`;
  - `AccessMirrorQuiescenceGuardTests` (zero `src/` mirror references);
  - `AccessAuthorityGuardTests` (7 repository-SQL authority facts);
  - `DapperAdminRepositoryProjectionTests` (authority-join projection);
  - `RemediationGuardTests` — vacuous passes without a DB here; executed for
    real in 15.4.2.

```bash
# 15.4.2  PG-gated executed probes — requires BA_DMO_TEST_DATABASE pointing at
#         the migrated N01→N36 database (Path A of §14, or a disposable copy of
#         the migrated DEV schema, NEVER DEV itself for writes; these probes
#         insert on fresh GUID keys and rely on teardown):
$env:BA_DMO_TEST_DATABASE = "<connection string of the migrated scratch DB>"
dotnet vstest AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/bin/Debug/net10.0/BA.Dmo.IntegrationTests.dll \
  --TestCaseFilter:"RemediationGuardTests | ArmazemReturnPostgresTests | JobOnLifecyclePostgresTests | RepairAtomicityTests | PegamentoPersistencePostgresTests"
```

- **Expected probes green:**
  - `RemediationGuardTests.N34_JunctionTable_IsAbsent_AndAnyDmlRaises42P01`
  - `RemediationGuardTests.N34_ProfileTitleColumn_IsAbsent_AndAnyDmlRaises42703`
  - `RemediationGuardTests.N34_CanonicalColumnPrivileges_AreUnchanged_ForBaDmoApp`
  - `RemediationGuardTests.N34_NewUserRows_AreInsertable_OnThePostRemovalSchema`
  - `RemediationGuardTests.N35_BqMovementsRepairerIndex_Exists_AndRedundantPegamentoIndex_IsGone`
  - `RemediationGuardTests.N36_PolicyInventory_IsUniform_BaDmoAppAccess_Only`
  - `RemediationGuardTests.N32_ProfileBackfill_UsesDeterministicDefault_NotUserProfileTitle`
  - PG seed suites (`ArmazemReturnPostgresTests`, `JobOnLifecyclePostgresTests`,
    `RepairAtomicityTests`) — seeds no longer reference `profile_title`.

### 15.5 Static gates (re-run on the deployed tag, repo root)

```bash
grep -rn "internal_user_access_templates\|profile_title" src/   # expect 0 matches
grep -rln "CASCADE" database/migrations/                        # only pre-existing
#   N06/N23/N31 ON DELETE CASCADE FKs (documented DO-NOT-TOUCH) — none in N34-N36
sha256sum database/migrations/N*.sql > predeploy_migration_hashes.txt   # BEFORE
sha256sum database/migrations/N*.sql > postdeploy_migration_hashes.txt  # AFTER
diff predeploy_migration_hashes.txt postdeploy_migration_hashes.txt     # must be EMPTY
```

### 15.6 Known pre-live baseline (no regression acceptable)

| Suite | Pre-live baseline | Pass rule after N34–N36 execution |
|---|---|---|
| Build | **PASS** (0 errors, `-m:1`) | PASS |
| Unit | **660/660** | ≥ 660 passed, 0 failed |
| Integration | **319 passed / 1 failed** (`ShellRoutingTests.Scenario7`) | same counts, same single failure |
| Focused migration/schema | **45/45** | 45/45 |
| PG-gated (executed) | not yet executed (vacuous) | green when executed against a migrated scratch DB |

---

## 16. STOP Conditions

**STOP the sequence immediately** (do not "continue and inspect later"; do not
proceed to the next migration; freeze all evidence; report) when ANY of the
following is true:

1. **STOP-1 — Target cannot be proven DEV.** The connection host/ref cannot be
   positively confirmed as BA-DMO-DEV, or the operator-confirmed DEV ref
   mismatches the recorded `bddfhbyrmchktqotpzgb` without explicit operator
   substitution, or **PROD credentials/ref are detected as the active target**
   in any connection string, environment variable, or CLI project.
2. **STOP-2 — Any dependency on an N34 removal target discovered live.** Any
   incoming FK, view/matview, function dependency, or external `pg_depend`
   row referencing `internal_user_access_templates` or
   `internal_users.profile_title` that §4 did not allow.
3. **STOP-3 — Pre-check catalog does not match the expected N33 state.** Any
   P4/P7/P10 labelled check FAILs (e.g., a removal target already missing
   before N34, a target index already present with a different definition,
   RLS/grants off expectation). Note the evidence-blind idempotence hazard
   (§5.3/§8.3): "success" with failing pre-checks still means STOP.
4. **STOP-4 — Unexpected policy/grant difference.** Any grant matrix departure
   from the captured pre-state (P4.6.3, P6.6, P10.5, P12.7) or any policy
   body/semantics deviation (P10.3 vs P12.9).
5. **STOP-5 — Any migration partially fails or fails to record.** A failed
   statement inside a migration file (transaction rollback), a migration that
   applied but was not recorded in bookkeeping, or any bookkeeping anomaly
   (runner vs CLI provenance split without owner sign-off).
6. **STOP-6 — Catalog post-check differs from the approved design.** Any
   §6/§9/§12 post-check FAIL; any §13 expected count mismatch (60/79/3/19/60);
   any §13.13 deviance row; any index/function/trigger/table change beyond the
   documented final state.
7. **STOP-7 — N36 semantic drift.** The renamed policy's (table, command,
   roles, USING, WITH CHECK) tuple differs from the pre-capture (P10.3) in
   ANY component — a rename must never alter permission semantics.
8. **STOP-8 — File immutability breach.** `sha256sum` of any N01–N36 file
   differs between the pre- and post-deployment captures; or `git status`
   shows edits inside `database/migrations/` during the run.
9. **STOP-9 — Consolidated-baseline drift.** Clean-install equivalence (§14)
   reports a diff outside the documented allow-list; or `git status` shows
   `database/consolidated_clean_install.sql` modified during the run.
10. **STOP-10 — Test regression.** Build fails; unit < 660 passed or any
    failure; integration shows a new/different failure beyond
    `ShellRoutingTests.Scenario7`; focused guards < 45/45; any executed
    PG-gated probe fails.
11. **STOP-11 — Any N34-era probe finds the removed objects reachable at
    runtime** (e.g., PG-gated `42P01/42703` probes do not raise, or catalog
    absence probes return rows — meaning the appointment of the drop is not
    proven).

Every STOP requires: immediate halt, evidence archive (query outputs,
connection string metadata redacted of secrets), and a written
`N34_N36_DEV_live_execution_report.md` section explaining the condition.

---

## 17. Baseline Acceptance Checklist

**ALL boxes must be satisfied before N34–N36 are considered LIVE VERIFIED.**

- [ ] **Target verified as BA-DMO-DEV** (identity gate §2.3; connection host
      + operator confirmation; recorded ref `bddfhbyrmchktqotpzgb` mapped or
      operator-substituted)
- [ ] **PROD project ID/ref explicitly differs** — recorded in the execution
      report; PROD unreachable from every connection string used (§19)
- [ ] N34 pre-checks pass (§4, incl. backup manifest §5.4 verified)
- [ ] N34 applies successfully (bookkeeping records it: P6.10)
- [ ] N34 post-checks pass (§6, incl. canonical-column privilege matrix)
- [ ] N35 pre-checks pass (§7, incl. no leading-key redundancy for
      `noted_repairer_id`)
- [ ] N35 applies successfully (bookkeeping records it: P9.7)
- [ ] N35 post-checks pass (§9, incl. BQ-10 / unrelated-indexes untouched)
- [ ] N36 pre-checks pass (§10, incl. semantic anchor P10.3 captured)
- [ ] N36 applies successfully (bookkeeping records it: P12.11)
- [ ] N36 post-checks pass (§12, incl. semantic parity P12.9)
- [ ] **RLS parity passes** (P10.4 vs P12.6; P9.6.2 / §13.10: 60 tables RLS on)
- [ ] **Grant parity passes** (P10.5 vs P12.7; P4.6.3 vs P6.6; §13.12)
- [ ] Catalog inventory matches expectations (§13: 60/79/3/19/60; §13.13 clean)
- [ ] Migrations N01–N36 remain immutable after execution (sha256 diff empty)
- [ ] Consolidated clean install matches final intended state (§14 PASS or
      EXPECTED_DIFFERENCE only)
- [ ] Application build passes (15.1)
- [ ] Unit suite passes (15.2: 660/660)
- [ ] Integration suite has no new failures (15.3: 319/1, same single failure)
- [ ] ShellRoutingTests.Scenario7 remains the only known unrelated failure
- [ ] Focused migration/schema guards 45/45 (15.4.1)
- [ ] PG-gated N34/N35/N36 probes green against a migrated scratch DB (15.4.2)
- [ ] **PROD untouched** (§19 — no connection string, no CLI project, no
      query, no test ever addressed BA-DMO-PROD)
- [ ] Execution report written (`reports/N34_N36_DEV_live_execution_report.md`,
      §18.8)

---

## 18. Codex Execution Handoff

> Hand this section to Codex as the FULL executor contract. It is
> self-contained; no rediscovery of project architecture is required. Codex
> is an **executor/validator of an already-approved plan** — it must NOT
> redesign, rationalize, re-audit, or extend anything.

### 18.1 Exact target

- **DEV:** BA-DMO-DEV — Supabase project ref recorded as
  `bddfhbyrmchktqotpzgb` **must be positively confirmed** (or operator-
  substituted for the true DEV ref) before anything else.

### 18.2 Explicit prohibition

- **BA-DMO-PROD: ZERO TOUCH.** Do not create, open, ping, or use any PROD
  connection. Do not set PROD in any env var. If a connection string cannot be
  proven DEV, do not execute (§16 STOP-1).

### 18.3 Pre-check sequence (read-only first)

1. Identity gate: §2.3 (P0.1–P0.5).
2. N34 pre-checks: §4 (P4.1–P4.10) + pre-drop backups §5.4.
3. N35 pre-checks: §7 (P7.1–P7.6) incl. `pre_n35_indexes.txt` capture.
4. N36 pre-checks: §10 (P10.1–P10.7) incl. semantic anchor P10.3.
5. Hash captures: 15.5 `predeploy_migration_hashes.txt`.
6. Any §16 condition → STOP and report; do not continue.

### 18.4 Migration sequence

1. **N34** `database/migrations/N34_legacy_access_mirror_removal.sql` — one
   file, one transaction (CLI path per §2.2.3, or owner-role one-batch apply).
2. **Post-N34 checks §6** (P6.1–P6.10); gate §5.6.
3. **N35** `database/migrations/N35_index_rationalization.sql` — same
   discipline; post-checks §9; gate §8.5.
4. **N36** `database/migrations/N36_ba_dmo_app_access_policy_rename.sql` —
   same discipline; post-checks §12; gate §11.5.

No other SQL. No reordering. No edits to any migration file.

### 18.5 Post-check sequence

1. Final catalog inventory §13 (archive output).
2. Clean-install equivalence §14 on disposable scratch DBs (PASS or
   EXPECTED_DIFFERENCE only).
3. Post-deploy hashes §15.5 (`diff pre/post` empty).

### 18.6 Test sequence

1. `dotnet build BA-DMO.sln -c Debug -m:1` → PASS.
2. Unit 15.2 → 660/660.
3. Integration 15.3 → 319/1 (Scenario7 only).
4. Focused guards 15.4.1 → 45/45.
5. PG-gated probes 15.4.2 against migrated scratch DB (`BA_DMO_TEST_DATABASE`)
   → green.
6. Static gates §15.5.

### 18.7 STOP conditions

§16 conditions 1–11 verbatim. On STOP: halt, archive evidence, report; never
"continue and inspect later".

### 18.8 Expected results and report path

- Expected: all pre/post checks PASS, bookkeeping records N34/N35/N36, final
  inventory 60/79/3/19/60 with a single policy name, hashes unchanged, tests
  at baseline, PROD untouched.
- **Create after execution:**
  `reports/N34_N36_DEV_live_execution_report.md` — containing: identity-gate
  evidence; every pre/post check output (or its file path); backup manifest
  hashes; bookkeeping rows; the §17 checklist with all boxes ticked + evidence
  pointers; any STOP conditions triggered (none expected); PROD no-touch
  attestation.

---

## 19. PROD Explicit No-Touch Statement

- **BA-DMO-PROD is out of scope, off-limits, and untouched** by this package
  and by every session that executes it.
- No connection string, Supabase CLI project, environment variable, or test
  fixture in this package references BA-DMO-PROD's project ref (it is not
  recorded in this repository, by design).
- The executor must verify - before running anything - that no shell variable,
  `.env`, or stored connection targets PROD; §2.3 P0.1/P0.2 evidence must be
  recorded per run.
- No query, migration, backup, or test may be pointed at PROD under any
  circumstance, including as a "dry run" or "comparison".
- The only permitted database targets are BA-DMO-DEV (per this package) and
  disposable scratch databases (§14).
- **Attestation required in the execution report (§18.8):** "No operation in
  this run addressed BA-DMO-PROD."

---

## 20. Remaining Verification After This Package

The following remain **LIVE VERIFICATION REQUIRED** items that this PREPARATION
package cannot close by itself — they are the explicit follow-ups for the
executor and owner:

1. **DEV execution itself** — the whole §4–§12 sequence against BA-DMO-DEV
   (this package is the contract for it).
2. **Fresh-chain replay proof** — N01→N36 on an empty disposable database with
   runner whole-script semantics; `schema_migrations` records the full 36-file
   family in order (§14 Path A).
3. **Two-path equivalence execution** — the §14 protocol on real PostgreSQL
   (statically approximated today; the live diff is still pending).
4. **PG-gated suite execution** — `RemediationGuardTests` N34/N35/N36 probes
   and the PG seed suites run against a migrated scratch DB (§15.4.2).
5. **Live post-deploy parity script revision** — extend
   `reports/schema_rationalization_03A_live_parity.sql` into the N34-era
   script (mirror parities become object-absence checks; keep verbatim
   history) and run it on BA-DMO-DEV (N34 audit §6.6.3).
6. **Effective-access validation on the deployed DEV build** — Admin /
   Operador-Controlador / Responsável each land on their expected surface
   (03A §4 pattern) after the mirror drop.
7. **Supabase-CLI provenance continuity** — N34/N35/N36 records present in
   `supabase_migrations.schema_migrations` with timestamps after the N33-era
   tail; no runner/CLI provenance split without owner sign-off.
8. **Production parity planning** — when the owner eventually ships to PROD,
   the §17 plan deploy-order discipline (backup → migrate → deploy → parity)
   applies; out of scope for this DEV verification package.
9. **Regression re-run cadence** — any later migration wave (N37+) must
   re-run §13 inventory + §16 STOP conditions against this N36 archive.

**Nothing in §20 is performed now. This package performs no execution.**

— End of package.