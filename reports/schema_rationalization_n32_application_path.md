# SCHEMA-RAT-03A — N32 Application Path: Investigation + Proposed Procedure

> **Type:** READ-ONLY INVESTIGATION + PROPOSAL. Nothing was executed on any
> database; no DDL/DML was run; the repository was not modified (only this
> report was created); no commit. **STOP-gate honored:** per the owner, no
> database write is performed in this step — this report establishes HOW N32
> must be applied and stops there.
>
> **Inputs:** owner-provided live verification (project `bddfhbyrmchktqotpzgb`,
> read-only), repo inspection at `df67e46`, git history, `Dockerfile`, CLI
> source (`MigrateCommand`/`MigrationRunner`/`NpgsqlMigrationScriptGateway`),
> and the documented provenance risk (PA-BK-01).

---

## 1. Confirmed live facts (owner's read-only verifier, 28 Aug 2026)

| Fact | Value |
|------|-------|
| Live Supabase project ref | `bddfhbyrmchktqotpzgb` |
| Live migration provenance table | **`supabase_migrations.schema_migrations`** (Supabase CLI bookkeeping), last row: `20260827150130` / `n31_template_profiles_single_assignment` |
| N32 registration | **ABSENT** — no `n32_access_authority_convergence` entry |
| `internal_users` | 7 |
| `internal_user_access_templates` rows | 7 |
| Users with multiple junction assignments | 0 |
| Direct `template_id` vs junction conflicts | 0 |
| Users missing junction mirror | 0 |
| Templates missing `access_template_profiles` | 0 |
| `profile_title` vs template-profile divergences | 0 |
| Railway app URL | `https://ba-dmo.up.railway.app` (DNS resolves; TLS probe from this sandbox blocked by local `SEC_E_NO_CREDENTIALS` — environment limitation, not an app signal) |

**Implication (owner's warning, confirmed):** the clean parity state is
**not** proof N32 ran. It has not been registered/applied. The parity is clean
because the 03A *code path* (deployed via Railway) maintains the mirrors
one-way and N31 left a converged state — N32's guards would currently pass, its
§3 backfill would affect 0 rows, and its only remaining effect is
**registration in the provenance table** plus the fail-closed checks.

---

## 2. How N27/N31 actually reached this Supabase project

1. **The live DB's provenance is Supabase-CLI managed:** the version format
   (`20260827150130` = UTC timestamp) and the table location
   (`supabase_migrations.schema_migrations`) are Supabase CLI's bookkeeping —
   NOT the repository's Npgsql runner, whose table is `public.schema_migrations`
   with `version` = `N01…N32` (created `IF NOT EXISTS` by the gateway).
2. **Corroborating timeline (repo git history + live row):**
   - `9e2569f` "Add template profiles and single assignment model"
     **2026-08-27 14:43 UTC** → created N31.
   - Live row `20260827150130` = **2026-08-27 15:01:30 UTC**, name
     `n31_template_profiles_single_assignment` → N31 was applied to live
     ~18 minutes after the commit, same afternoon.
   - `df67e46` "Converge access template and profile authority"
     **2026-08-27 22:54 UTC** → created N32. **No later live row exists.**
   - Conclusion: every live migration was applied from a **local Supabase CLI
     project owned by the operator** (a `supabase/` tree that is NOT part of
     this repository — repo has no `supabase/`, no `config.toml`, no CI
     workflow; only `database/migrations/*.sql` + the Npgsql runner).
3. **How Railway handles `database/migrations`:** **it does not apply them.**
   - `Dockerfile` ENTRYPOINT → `dotnet BA.Dmo.Web.dll` (web startup only);
   - no `railway.toml`, `Procfile`, or nixpacks config in the repo;
   - migrations are CLI-only (no HTTP migration endpoint; `MigrateCommand`
     exits before web startup). Railway carries the migrations directory in the
     publish output (so the CLI *could* run inside the service) but no deploy
     step invokes it. (Comment in `MigrateCommand.cs` referencing "Render
     pre-deploy" is a stale reference to an earlier provider.)

**Therefore:** the correct, provenance-continuous way to apply N32 to this
project is the **Supabase CLI path that already owns the live history**, not
the repository's Npgsql runner.

---

## 3. Why NOT to run `dotnet BA.Dmo.Web.dll migrate` against live as-is

Running the Npgsql runner would be a **provenance split** with **re-execution
risk**:

1. `public.schema_migrations` has never been used on this live DB (history
   lives in `supabase_migrations.schema_migrations`). The runner would see
   **zero** applied and attempt **N01–N32**.
2. N01–N31 DDL is largely guarded (`IF NOT EXISTS`, `ON CONFLICT DO NOTHING`),
   but several files contain **non-idempotent data mutations** whose re-run
   semantics are not proven (N27 rewrites `access_templates.modules` /
   `profile_title` and materializes `legacy-override-*` templates; N27/N31
   DELETE/backfill the junction; N24/N25/N26 carry data operations). On a
   converged test DB most would be no-ops, but that is not established
   file-by-file, and any surprise is exactly the "unexpected historical
   bookkeeping state" the owner instructed to stop on.
3. Success would leave **two provenance systems** recording the same object
   chain — the documented PA-BK-01 drift — and would record N01–N31 under
   runner versions on top of existing CLI versions.

**Verdict:** do not use the Npgsql runner on this live DB until reconciliation
of the two provenance systems is explicitly agreed (a design decision for the
later destructive phase, not for the N32 step).

---

## 4. Proposed N32 application procedure (NOT executed — awaiting owner)

### Option A — continue the Supabase-CLI path (RECOMMENDED)

Preserves the single provenance system that already owns the live history.

1. In the **local Supabase CLI project** that produced the live history
   (located outside this repo), add the next migration file:
   `supabase/migrations/2026082716xxxx_n32_access_authority_convergence.sql`
   — **timestamp > `20260827150130`**, content = the repo file
   `database/migrations/N32_access_authority_convergence.sql` **unchanged**
   (no edits to N01–N31, no rewrites).
2. If not already linked: `supabase link --project-ref bddfhbyrmchktqotpzgb`
   (requires the project access token / DB password).
3. Apply: `supabase db push` (single maintenance window; each migration runs
   in its own transaction, recorded per CLI bookkeeping).
4. **Expected outcome:** N32's §1/§2 guards PASS (0 multi-assignments, 0
   conflicts — confirmed live), §3 backfill is a no-op (0 templates missing
   profiles), and N32 is recorded as the next row after `20260827150130`.
5. **Verify after (read-only):**
   - `supabase_migrations.schema_migrations` tail contains
     `n32_access_authority_convergence`;
   - re-run `reports/schema_rationalization_03A_live_parity.sql` §1 (§1.1–§1.8
     all 0 rows) and §3 (invariants clean; §3.3 active-admin ≥ 1);
   - counts unchanged: 7 users / 7 junction rows; e.g. direct-vs-junction 0.

### Option B — one-shot transactional apply + manual record (FALLBACK)

Only if the local CLI project is unavailable and the owner accepts operating
on the CLI provenance table by hand on this test-stage DB:

1. Against a privileged (owner/service-role) connection, run the **entire**
   `database/migrations/N32_access_authority_convergence.sql` file as **one
   transaction** (psql `-f` in a `BEGIN`/`COMMIT`, or the Supabase SQL editor
   as the owner role); N32 itself is fail-closed and currently expected to
   succeed with zero DML.
2. Record the migration in the same provenance system it belongs to:
   ```sql
   INSERT INTO supabase_migrations.schema_migrations (version, name)
   VALUES ('2026082716xxxx', 'n32_access_authority_convergence');
   ```
   with `2026082716xxxx > 20260827150130` (version = timestamp, name = the
   lower-cased file name — match the existing row format exactly; verify the
   exact column names of that table first).
3. **Risk note:** this edits CLI bookkeeping by hand; acceptable only because
   the owner classified the environment as testing/development, and only with
   explicit owner sign-off. Do not guess the `schema_migrations` column names —
   inspect the live table first.

### Option C — Npgsql runner (NOT recommended for live)

Deferred: would require first reconciling `public.schema_migrations` with the
CLI history (create + backfill N01–N31 records) and a file-by-file re-run
idempotency assessment of N01–N31. This belongs to the later destructive-phase
design (provenance reconciliation, PA-BK-01), **not** to the N32 step.

---

## 5. What is still needed from the owner to execute

- **For Option A:** access to the local Supabase CLI project (or CLI login
  token + DB credentials); a go-ahead for the write (approval policy: ask).
- **For Option B:** a privileged connection for one transaction + a go-ahead;
  the `supabase_migrations.schema_migrations` column layout (or permission to
  `\d` it read-only first).
- **Railway check #1 (deployed = current main):** please confirm the deployed
  build from the Railway dashboard (deployment for commit `df67e46`), or
  re-run a browser/CLI probe of `https://ba-dmo.up.railway.app` from a normal
  network context — this sandbox's TLS stack cannot complete the handshake.

---

## 6. Gate to SCHEMA-RAT-03B

Per the owner: **do not start 03B yet.** Sequence:

1. N32 applied via Option A (or B with sign-off) and **registered** in
   `supabase_migrations.schema_migrations`;
2. post-N32 read-only parity re-verified (all §1 zero-rows, §3.3 ≥ 1, counts
   unchanged) — the repo's readiness script covers this;
3. Railway-deployed build confirmed on `df67e46`;
4. only then → execute `reports/schema_rationalization_03B_plan.md`
   (kill the mirrors; N33 migration + C# writer removal; no DROPs).

No database write has been performed by this session; N32 remains unapplied
pending the owner's go-ahead.