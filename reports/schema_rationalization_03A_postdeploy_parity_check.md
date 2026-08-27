# SCHEMA-RAT-03A — Post-Deploy Live Parity Check (STATUS REPORT)

> **Type:** READ-ONLY VERIFICATION / STATUS. No source, migration, schema, or
> database object was modified; no DDL/DML executed; no live write performed;
> no commit or push. Artifacts produced: this report and
> `reports/schema_rationalization_03A_live_parity.sql` (read-only script ready
> to run against the live database); the SCHEMA-RAT-03B design is prepared
> separately in `reports/schema_rationalization_03B_plan.md` and is GATED on the
> live checks below (per the owner brief).
>
> **Head verified:** `df67e46c0e39fab8aec63a8ea56475f866466644` ("Converge access
> template and profile authority"), branch `main`, tree clean, in sync with
> `origin/main`. 38 files changed in that commit: N32 added, Web-layer
> `TemplateProfileStore` deleted, services/repositories converged onto the
> D-1/D-2 authority, tests updated.

> **UPDATE (28 Aug 2026, owner-provided live verification, project
> `bddfhbyrmchktqotpzgb`):** live access was supplied through an external
> read-only verifier. **N32 is NOT applied/registered** — the live provenance
> is Supabase-CLI managed (`supabase_migrations.schema_migrations`, last row
> `20260827150130` / `n31_template_profiles_single_assignment`), and there is
> no `n32_access_authority_convergence` entry. Live parity is otherwise clean
> (7 users / 7 junction rows / 0 multi-assignments / 0 direct-vs-junction
> conflicts / 0 missing mirrors / 0 templates missing profiles / 0 profile
> divergences) — clean parity is **not** proof N32 ran. Per the owner, 03B is
> **not** to start; the correct N32 application path is established in
> `reports/schema_rationalization_n32_application_path.md` (continue the
> Supabase-CLI path; do NOT use the Npgsql runner un-reconciled). Chapters
> 3–4 of this report are superseded by that file for the live facts.

---

## 1. Owner-brief checklist — status at a glance

| # | Check | Status from this session |
|---|-------|--------------------------|
| 1 | Railway deployed current main | **BLOCKED** — no Railway CLI/token/project URL available in this session; repo-side condition (HEAD == origin/main == `df67e46`) holds. See §3. |
| 2 | N32 actually applied to live Supabase | **BLOCKED** — no DB connection envelope (`BA_DMO_DB_CONNECTION_STRING`/`DATABASE_URL`/Supabase credentials) in this session. Ready-to-run proof query prepared (§4.1). See §3. |
| 3 | Read-only live parity checks | **BLOCKED** (same credential gap) — the complete script is ready in `reports/schema_rationalization_03A_live_parity.sql` (§1–§2 = the six owner checks; §3 = Admin invariants; §4 = effective-access evidence; §5 = propagation precursors; §6 = owner-executed behavioural probes). |
| 4 | Validate effective access: Admin / Operador-Contr / Responsável | **BLOCKED** on live data; the code-derived expected surface is documented in the SQL script §4 header and in §5 below for review. |
| 5 | Confirm template replacement + template-profile propagation | **BLOCKED** on live writes; both flows are implemented at HEAD (see §6; write probes are owner-executed, protocol in the SQL script §6). |
| — | 03B readiness | Repo-side prerequisite (exact writer/reader inventory) COMPLETE at HEAD; SCHEMA-RAT-03B design prepared and **gated** on checks 1–5 coming back healthy (§7). |

**Gate decision:** per the brief, SCHEMA-RAT-03B is **not** to be considered
green-lit until the live checks pass. Nothing destructive has been started or
designed for execution: the two legacy mirrors
(`internal_user_access_templates`, `internal_users.profile_title`) remain
physically present and, at HEAD, still written one-way by a small, enumerated
set of runtime writers (§6).

---

## 2. What is verified repo-side at df67e46 (no live access needed)

### 2.1 Authority model at HEAD (matches the approved authority)
`internal_users.template_id` → `access_templates` →
`access_template_profiles.functional_profile`; modules from
`access_templates.modules`. Runtime resolution (`IdentityResolutionService`)
never consults the junction or `profile_title`; the shell header presents the
template name, not the mirror.

### 2.2 Build/compile health
`dotnet build BA-DMO.sln -c Debug --no-restore -m:1 -nodeReuse:false
-p:UseSharedCompilation=false` → **0 errors, 41 pre-existing warnings**
(analysis-style xUnit/CS warnings; NU1900 offline vulnerability-data
notifications, environment-caused). Note: the machine's .NET 10 SDK install is
incomplete (two workload-locator SDK folders missing → sln-level `restore`
fails with MSB4276), and the DSH file sandbox denies the test host's
cross-process exit-callback (`Win32Exception(5)`), so `dotnet test` could not
execute in this session. Neither is a repository defect; both are environment
constraints and are logged for the record.

### 2.3 N32 file state
`database/migrations/N32_access_authority_convergence.sql` (149 lines) is
present and immutable-chain compliant (guarded, idempotent, non-destructive,
fail-closed; §1/§2 raise on multi/conflicting junction claims; §3 backfills
missing template profiles with the N31 deterministic default; §4 restates
non-destructive bounds). Deployed publish output carries the migrations
directory (`src/BA.Dmo.Web/bin/Debug/net10.0/database/migrations/*`), so the
`migrate` CLI can run from the deployment.

### 2.4 Writer/reader inventory of the two legacy mirrors at HEAD (exhaustive
grep over `src/`): **this is the exact 03B scope**

| # | Site (file:line at HEAD) | Writes to | Notes |
|---|--------------------------|-----------|-------|
| W1 | `DapperAdminRepository.CreateInternalUserAsync` (`:165-198`) | junction INSERT + `profile_title` (derived from template profile in the same statement) | user create flow |
| W2 | `DapperAdminRepository.ChangeUserTemplateAsync` (`:271-281` inside UoW) | junction mirror INSERT | template replacement flow |
| W3 | `DapperInternalUserRepository.CreateBootstrapAdminAsync` (`:205-210`, `InsertUserTemplateSql :79-87`; `InsertInternalUserSql :64-77` also writes `profile_title`) | junction INSERT + `profile_title` | bootstrap-admin CLI |
| W4 | `DapperAdminRepository.UpdateTemplateAsync` (`:622-625`) | `UPDATE internal_users SET profile_title` for all users of the template (same transaction as profile upsert) | template-profile propagation |
| R1 | `DapperAdminRepository.UserColumns` (`:41`) + search filter (`:84`) | reads `profile_title` | Admin Users page projection/search (presentation) |
| R2 | `DapperInternalUserRepository.FindByAuthUserIdSql` (`:28`) | reads `profile_title` into `InternalUserRecord.ProfileTitle` | carried; NOT used by identity resolution (template name is presented instead) — a dead compatibility read |
| R3 | `internal_user_access_templates` **SELECTs** | **ZERO** in `src/` (verified) | junction has no runtime reader |

No DB trigger writes either mirror at runtime (N31's trigger writes only the
authority table `access_template_profiles`; N27/N31/N32 mirror writes are
migration-time). Hence: **5 runtime mirror-write sites (W1–W4, with
W1/W3 each touching both mirrors) + 2 dead/presentational profile_title read
sites (R1/R2) + 0 junction readers** = the complete 03B removal scope.

---

## 3. Live verification — what is missing (credential gap) and exact evidence needed

Neither the Railway deployment status nor the live Supabase schema is reachable
from this session: no `BA_DMO_DB_CONNECTION_STRING`/`DATABASE_URL`, no
Supabase URL/anon/service-role vars in the environment, no local PostgreSQL
listener, no Railway CLI/token, no `~/.railway`, no repo/deploy URL in history,
and the GitHub repo exposes no deployments. To complete checks 1–5 the owner
(approval policy: ask) must provide **one** of:

1. **Read-only live DB access** — e.g. a Supabase connection string for a
   read-only/service-role context (or run the parity script themselves and
   paste the output). Everything in
   `reports/schema_rationalization_03A_live_parity.sql` then completes checks
   2–4 in one pass.
2. **The deployed app URL** (Railway service URL) — enables probing the live
   deployment (login + admin surface) to confirm the deployed build and to run
   the §6 behavioural probes.
3. **Migrate-CLI execution note:** N32 is applied only when someone runs
   `dotnet BA.Dmo.Web.dll migrate` (server-side env:
   `BA_DMO_DB_CONNECTION_STRING` preferred, `DATABASE_URL` fallback) against
   the live DB — the Dockerfile entrypoint starts the web app only and the
   pipeline has no migration step in-repo. If the owner has not run `migrate`
   since 27 Aug 2026, N32 is very likely NOT applied yet; the parity script's
   §0.1/§0.2 queries settle it.

### 4.1 The single decisive N32-applied query (part of the script; shown here)
```sql
SELECT version, filename, applied_at, execution_time_ms
  FROM schema_migrations
 WHERE filename IN ('N31_template_profiles_single_assignment.sql',
                    'N32_access_authority_convergence.sql')
 ORDER BY version;
```

---

## 5. Expected effective-access surface at HEAD (code-derived, for owner review)

Derived from `AccessResolver.Resolve` + `ProjectProfileCapabilities` (df67e46).
DB-side inputs come from §4 of the parity script:

| Profile | Template module surface (must hold) | Capabilities projected by the resolver |
|---------|-------------------------------------|----------------------------------------|
| **Admin** | admin module ONLY (invariant also enforced in `AdminTemplateService.ValidateProfileModuleRule`) | `admin.gerir`, `audit.view`, `audit.export`; no História |
| **Operador / Controlador** | any non-admin assignable modules; `controlo` expands to `peso`+`pegamentos`; História added if any module | `jobon.view`, `jobon.confirmar` (if jobon); `controlo.view`, `controlo.edit`, `controlo.submit` (if controlo); **no** `peso.aprovar` / `jobon.edit` / `jobon.configure` / `ferramentas.configure` |
| **Responsável** | same module derivation | additionally `jobon.edit`, `jobon.configure` (if jobon); `ferramentas.configure` (if ferramentas); `controlo.review` + `peso.aprovar` (if controlo) |

---

## 6. Template replacement / profile-propagation flows at HEAD (implemented)

- **Replacement (D-2):** `AdminUserService.ChangeTemplateAsync` →
  `DapperAdminRepository.ChangeUserTemplateAsync` — canonical FK replaced +
  junction mirror re-derived in ONE UoW; access does not accumulate (resolver
  sees only the new template). Covered by `IdentityResolutionServiceTests` /
  `AdminUserServiceTests` / `AccessAuthorityGuardTests` at HEAD.
- **Profile propagation (D-1):** `AdminTemplateService.UpdateAsync` →
  `DapperAdminRepository.UpdateTemplateAsync` — profile upserted (authority) +
  `profile_title` re-derived for every user of the template in ONE transaction
  (`updated_at_utc` of users untouched). User-level `profile_title` edit removed
  (`AdminUserService.UpdateUserAsync` writes identity/display only).
- **User create (D-2):** `CreateInternalUserAsync` derives both mirrors from
  the template in the same statement.
- Behavioural (write) confirmation on the live app = owner-executed probes A–C
  in `reports/schema_rationalization_03A_live_parity.sql` §6 (each reversible).

---

## 7. Next step

1. Provide access per §3 (option 1 is sufficient for checks 2–4; option 2 +
   the §6 probes complete check 5; option 3 explains how N32 reaches the DB).
2. Run `reports/schema_rationalization_03A_live_parity.sql`; every §1–§3 check
   must return 0 rows (except §3.3 active-admin ≥ 1).
3. If healthy → execute SCHEMA-RAT-03B per `reports/schema_rationalization_03B_plan.md`.

Known unrelated debt (out of scope, per brief): ShellRoutingTests Scenario7
Admin-navigation drift — not mixed into this work.