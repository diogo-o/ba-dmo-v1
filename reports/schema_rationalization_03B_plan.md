# SCHEMA-RAT-03B — Kill the Legacy Access Mirrors (Design Plan)

> **Type:** DESIGN / PREPARATION — **GATED**. Per the owner brief, SCHEMA-RAT-03B
> is prepared here but only becomes executable after the SCHEMA-RAT-03A
> post-deploy live parity checks (see
> `reports/schema_rationalization_03A_postdeploy_parity_check.md` +
> `reports/schema_rationalization_03A_live_parity.sql`) come back healthy:
>   1. Railway deployed current main (`df67e46`),
>   2. N32 applied to live Supabase,
>   3. live parity checks all PASS,
>   4. effective access validated (Admin / Operador-Contr / Responsável),
>   5. template replacement + profile propagation confirmed live.
> No file, migration, or database object of 03B is created or modified yet.
> This document defines WHAT must be true and HOW it will be implemented.
>
> **Head the design is based on:** `df67e46` (same as 03A status report).

---

## 1. Objective

After 03B, the two legacy mirrors physically exist but are **completely dead**:

- `internal_user_access_templates` — **no runtime readers, no runtime writers**;
- `internal_users.profile_title` — **no runtime readers, no runtime writers**.

**Hard constraint (owner brief): DO NOT DROP the table or the column yet.**
The destructive removal phase (N33+ per the original naming intent / later
phase) is designed only after 03B is deployed and parity-validated.
`internal_users.modules_override` and all other dormant surfaces are **out of
scope** (their own owner decisions D-11/D-9 etc. stay parked).

The one indispensable schema relaxation: `profile_title` is NOT NULL today
(N27:114), so eliminating its writers is impossible while the column demands a
value on every user insert. 03B therefore **drops NOT NULL** (widening,
non-destructive — the column itself stays) and stops writing it. A NULL
profile_title is unambiguous: the mirror is retired, never a missing value.

---

## 2. Scope basis — the exact writer/reader inventory at df67e46

Verified exhaustively (grep over `src/`), the complete removal set:

| ID | Site | Change in 03B |
|----|------|---------------|
| **W1** | `DapperAdminRepository.CreateInternalUserAsync` (`:165-198`) — junction INSERT + `profile_title` derived from template profile | Remove junction INSERT (CTE tail); remove `profile_title` from the INSERT column list (nullable after N33) |
| **W2** | `DapperAdminRepository.ChangeUserTemplateAsync` (`:271-281`, inside UoW) — junction mirror INSERT | Remove the mirror statement; UoW keeps canonical FK update + self-lockout count |
| **W3** | `DapperInternalUserRepository.CreateBootstrapAdminAsync` — junction INSERT (via `InsertUserTemplateSql :79-87`) + `profile_title` (via `InsertInternalUserSql :64-77`) | Remove both mirror writes; bootstrap keeps template + internal user + audit in one UoW |
| **W4** | `DapperAdminRepository.UpdateTemplateAsync` (`:622-625`) — `UPDATE internal_users SET profile_title` for all template users | Remove the mirror UPDATE; profile authority write stays |
| **R1** | `DapperAdminRepository.UserColumns` (`:41`) + search filter (`:84`) — reads `profile_title` | Read the template-owned profile instead: add `LEFT JOIN access_template_profiles pt ON pt.template_id = u.template_id`; project `pt.functional_profile AS ProfileTitle`; search matches the same joined column. `AdminUserRow.ProfileTitle` then always comes from the authority |
| **R2** | `DapperInternalUserRepository.FindByAuthUserIdSql` (`:28`) — reads `profile_title` into `InternalUserRecord.ProfileTitle` (never used for access; the resolver presents `TemplateName`) | Remove the column read; map `ProfileTitle` to NULL and update the doc comment ("legacy mirror retired") |

Plus the **DB-layer kill switch** carried as migration N33 (§4) so "dead" is
mechanically enforced and provable.

No DB trigger writes the mirrors at runtime (N31's trigger writes only
`access_template_profiles`); N27/N31/N32 mirror writes are migration-time and
remain valid history. Therefore the C# removals + N33 revokes fully quiesce
both mirrors.

---

## 3. Application design decisions (03B code changes)

1. **Single authoritative repository boundary stays.** No new Web-layer SQL is
   introduced (the 03A commit already deleted `TemplateProfileStore`).
2. **`AdminUserRow.ProfileTitle` / `InternalUserRecord.ProfileTitle` public
   shape is preserved** (view models unchanged) — only their provenance
   changes: authority table for Admin pages, NULL for the identity record.
3. **Search UX unchanged:** Admin Users search still matches display name,
   actor id, and now the *template-owned* functional profile.
4. **Bootstrap path unchanged in behavior:** one-shot CLI still creates the
   admin with a guaranteed Admin profile; it simply stops maintaining mirrors.
5. **Deploy ordering (critical):** N33 (relaxes `profile_title` + revokes) must
   be applied by the `migrate` CLI **before** the 03B build's first user
   write — otherwise a user create hits the still-NOT-NULL column. The deploy
   sequence is: `migrate` → deploy → parity → probes (same discipline as every
   prior phase).

---

## 4. Migration N33 — `database/migrations/N33_legacy_access_mirror_quiescence.sql`

Forward-only, idempotent, non-destructive, whole-script in its own
transaction (gateway convention — no explicit BEGIN/COMMIT):

1. **Relax the mirror**: `ALTER TABLE internal_users
   ALTER COLUMN profile_title DROP NOT NULL;` (existing rows keep their fossil
   values; new rows are NULL; CHECK constraint is NULL-tolerant and may stay —
   it is inert on NULL).
2. **Junction kill switch** (guarded DO block for role existence, then plain
   REVOKEs — idempotent):
   `REVOKE ALL PRIVILEGES ON TABLE internal_user_access_templates FROM ba_dmo_app;`
   — no runtime reader or writer can touch the junction anymore. (Migration
   owner / Supabase admin role is unaffected, so future migrations and the
   N31/N32-style guards still run.)
3. **profile_title kill switch** (privilege REFACTOR — the originally planned
   column-level REVOKE alone cannot close the hole: `ba_dmo_app` holds
   TABLE-LEVEL `SELECT`/`INSERT`/`UPDATE` on `internal_users`, and
   table-level grants imply access to every column, `profile_title`
   included; table-level `INSERT` would also still permit writing the
   retired mirror). Actual N33 §3:
   - `REVOKE SELECT, INSERT, UPDATE ON internal_users FROM ba_dmo_app;`
   - then re-grant the same three privileges at COLUMN level for every
     current `internal_users` column EXCEPT `profile_title` — explicit
     list, no dynamic discovery, no `profile_title` grant through any path:
     `GRANT SELECT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc, modules_override) ON internal_users TO ba_dmo_app;`
     (likewise `INSERT` and `UPDATE`);
   - `DELETE` remains untouched (table-level, exactly as before);
   - result: `profile_title` `SELECT`/`INSERT`/`UPDATE` are inaccessible to
     `ba_dmo_app` (any residual read/write/insert of the column fails
     loudly with a permissions error instead of silently maintaining the
     mirror), while canonical columns keep their privileges.
4. **Self-documentation block** restating the non-destructive bounds (mirrors
   stay physical; N33+ removal phase is separate; N31/N32 guards remain
   migration-only readers).
5. **No drops, no renames, no data rewrites anywhere.**

Rationale for revokes (vs code-only): the project's fail-closed philosophy —
if a future change reintroduces a mirror touch, it breaks loudly (500 /
permission denied) in the first test, not silently.

---

## 5. Regression and guard tests (03B)

- **Contract tests (fake repository, no PG):** update
  `AdminUserServiceTests` / `AdminTemplateServiceTests` /
  `IdentityResolutionServiceTests` / `AccessAuthorityGuardTests` /
  `FakeAdminRepository` to the new contract (no mirror side effects).
- **Architecture guard test (grep-based, mirror of the existing
  `MigrationArchitectureGuardTests`):** assert **zero** source references to
  `internal_user_access_templates` and `internal_users.profile_title`
  (allow-list: `database/migrations/N27*.sql`…`N33*.sql` + doc comments), so
  the "dead mirror" state is enforced in CI forever.
- **Live-PG guard tests (`RemediationGuardTests` pattern, env-gated on
  `BA_DMO_TEST_DATABASE`):** after N33, connecting as `ba_dmo_app`:
  INSERT into the junction → permission denied; UPDATE
  `internal_users.profile_title` → permission denied; INSERT into
  `internal_users` with no `profile_title` → succeeds (nullable).

---

## 6. Post-03B verification protocol (parity-validated gate)

Run against the deployed live DB after 03B:

1. Re-run `reports/schema_rationalization_03A_live_parity.sql` (extended for
   03B):
   - §1.4 expectation **flips**: junction rows for users are no longer
     maintained; the check becomes "users without junction rows are the norm"
     and the junction row count should freeze (monotone, unchanged);
   - new check: `profile_title` is NULL for all rows created after 03B and the
     column itself is untouched by any write;
   - new check: `role_table_grants`/`information_schema.role_column_grants`
     show no `ba_dmo_app` privilege on the junction or on `profile_title`;
   - §3.3 (active admin path ≥ 1) still holds.
2. Re-run behavioural probes A–C (SQL script §6): template change, profile
   change, user create — all succeed, mirrors untouched.
3. `schema_migrations` contains `N33_legacy_access_mirror_quiescence.sql`.
4. Grep gate (§5) passes on the deployed tag.

**Acceptance:** all of the above green ⇒ 03B complete ⇒ then (and only then)
design the destructive N33+ removal phase + D-16 consolidated-baseline refresh.

---

## 7. Explicitly out of scope for 03B

- Drops/renames of `internal_user_access_templates` or
  `internal_users.profile_title` (destructive phase, later).
- `internal_users.modules_override` (D-11), `peso_comparacao_anterior` (D-9),
  `job_on_revision.image_asset_id` (D-11), dormant surfaces (D-7/D-8),
  audit co-transactionality (D-5/D-13), RLS naming (D-15), consolidated
  baseline refresh (D-16), Job On audit dual-emit (D-5).
- ShellRoutingTests Scenario7 Admin-navigation drift (owner-declared unrelated
  debt — never mixed into schema work).

---

## 8. Current status of this plan

**PREPARED — NOT EXECUTED.** Execution is blocked on the SCHEMA-RAT-03A live
checks (credential gap documented in the 03A status report §3). The owner must
provide live-DB access (or run the readiness script and share output) before
this plan turns into the N33 migration + code changes.