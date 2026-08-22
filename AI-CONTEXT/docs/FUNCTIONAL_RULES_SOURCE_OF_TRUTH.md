# BA DMO — Functional Rules & Owner Decisions (source of truth)

> This concise document preserves the unique **functional/business rules and owner
> decisions** that previously lived inside `IMPLEMENTATION_STATE.md`. It is a concise
> source of truth; the full functional/technical history remains available in
> `docs/IMPLEMENTATION_STATE.md` (which carries the `DESIGN IMPLEMENTATION RESET —
> 2026-08-22` banner, so its visual/design claims are historical only).
>
> It is a **source-of-truth reference for requirements**, NOT a completion/parity
> claim and NOT a visual audit. Treat it as the authoritative statement of the
> module boundaries and business rules below. Where any of these items conflict with
> the authoritative design mockups / handoffs in `design/portal-dmo-design-final/`,
> the design authority governs presentation; these rules govern **behavioral
> invariants, ownership boundaries, and owner decisions**.
>
> These facts are **current requirements**, not historical reports. Visual parity of
> the shipped UI against the design must still be independently audited.

---

## 1. Overall architecture (authoritative)

- Clean architecture, Razor Pages (ASP.NET Core `net10.0`), Npgsql + Dapper,
  PostgreSQL/Supabase. Projects: `BA.Dmo.Domain`, `BA.Dmo.Application`,
  `BA.Dmo.Infrastructure`, `BA.Dmo.Web`; tests: `BA.Dmo.UnitTests`,
  `BA.Dmo.IntegrationTests`.
- Canonical dependency direction: Application→Domain; Infrastructure→Application+Domain;
  Web→Application+Infrastructure; UnitTests→Domain+Application; IntegrationTests→Web+Infrastructure.
- Modules: Job On, Peso, Pegamentos, Ferramentas, Armazém, Tampões, Reparação Interna,
  Reparação Externa, História, Boquilhas, Controlo, Admin, Login.
- Sole canonical cross-module append-only fact source: `audit_events` (N01).

## 2. History / immutability (global)

- Corrections are always NEW rows; the original fact never disappears
  (GLM-DATA-07). A Peso/Pegamentos/Reparação-Interna record stays pinned to the exact
  `job_on_revision_id` under which it was performed; later Job On revisions must not
  reinterpret that historical context.
- Canonical direction: `Job On revision → production context → dependent record
  (Peso control / Pegamentos / Reparação Interna)`.
- No scoring / rankings / performance-judgement logic.

## 3. Job On (U-13/related)

- Revisions are immutable; the graph (revision + components + fields + rows +
  verifications) must be persisted and rehydrated atomically. **Known historical gap
  (see AUDIT-REVIEWED / refine-v1):** `GetByIdAsync` originally did not hydrate
  `Components`/`Verifications`; this must be fixed so PDF, duplication, Peso context
  and the "Confirmar" tab read a fully-hydrated aggregate.
- Universally the landing: calendar + production list; deterministic machine/line→
  colour mapping (`B1..C3 → b1..c3 → --dmo-line-*`); the colour identifies the
  MACHINE/LINE, never a semantic status.
- Per-user "current open Job On" via `jobon_user_current` (N24).

## 4. Boquilhas (U-19) — confirmed rules

- Owns its own `bq_*` schema (N03) — NOT Ferramentas CM/MF `tool_lotes` identity.
  Boquilhas is NOT modelled as the CM/MF batch-repair flow (02_DEC AB-03).
- **20→25 excess-return rule:** `matched = min(return, repair)`; unmatched →
  `exceptional_received_qty` + open `bq_discrepancy`; never a hard block and never
  auto-added to production. NO `AllowUnmatched` hard block (UD-08/UD-09).
- Repairer vocabulary is the canonical `repairers` / `line_repairer_defaults`
  (`tool_type='BQ'`) — reused, not duplicated.
- Reference regex `^[A-Z][0-9]{3}$`; dynamic lines B1–C3.
- Owner decisions: **D1** — Reparação-Externa BQ workflow NOT activated/redesigned in
  Boquilhas U-19 (existing `repair_exit_items.bq_lote_id` hook stays for a later pass);
  **D2** — no live Job On → Boquilhas lookup; immutable Job On/BQ snapshots remain the
  default historical integration.

## 5. Ferramentas (U-12)

- CM/MF references, lotes, per-lote verificações, SAP utilisation readings
  (append-only `tool_usage_records`, N19), rule lookup feeding Job On.
- R003 SAP utilisation (`% use`, manual) — backend + endpoints exist; UI is an
  **owner-decision** item (not invented).

## 6. Reparação Externa (U-15) — owner decisions A–G

- **A:** BQ functional repair deferred (not in U-15); Boquilhas tab exists but holds no
  fake BQ behavior.
- **B:** Armazém remains the SOLE owner of `warehouse_stock`/`warehouse_movements` and
  physical release/re-occupation; U-15 consumes the Armazém-owned port, never writes
  Armazém tables.
- **C:** any confirmation that changes BOTH repair-cycle state AND warehouse physical
  state (pickup, return) runs in ONE Dapper unit of work.
- **D:** no physical effect is inferred; only explicit persisted confirmations move tools.
- **E:** `Cancelado` is schema/status-compat only; functional CancelarLista deferred.
- **F:** duplicate-item-in-open-exit is a hard Application/domain rule.
- **G:** non-returning-close / destination / other GLM-RE-12 rules safe-deferred.

## 7. Armazém (U-14) — owner decisions

- Ferramentas owns read-only `IFerramentasIdentityLookup`; Armazém owns
  `IToolIdentityResolver`. Two-different-references warning (no silent normalization);
  `fora` derived, never persisted; 4-digit positions.
- Occupation 1:1 — a location may not hold two active tools. **Known hardening item**
  (AUDIT-REVIEWED / refine-v1): the check+INSERT is not atomic (TOCTOU); requires
  `SELECT ... FOR UPDATE` or `ON CONFLICT`.
- Programmed external-repair exits and BQ are out of Armazém U-14 scope.

## 8. Tampões (U-17)

- Owns saldos, movements, configurations, settings. Job On is only an OPTIONAL read-only
  production link in planning, never mutated. Actor server-derived; every change is a NEW
  append-only movement; balances derived from facts.
- Balance/quantity updates must be **atomic deltas or row-locked** — known historical
  lost-update risk (`SetSaldoAsync` absolute rewrite) requires delta/`FOR UPDATE`
  (AUDIT-REVIEWED / refine-v1).
- `planear != reservar` (planning does not reserve stock).

## 9. Reparação Interna (U-16) + R009 / R015

- Owns `internal_repair_records` (write) + repair_events scope interna.
- **Definitive scope correction (2026-08-22): Reparação Interna repairs only CM and
  MF. BQ is not repairable, selectable, or processed in Reparação Interna.** Boquilhas
  use their own separate external-repair flow (external repairers plus dedicated entry
  and exit registration).
- The production/reference context must always show and preserve the **complete
  reference**, including the Boquilhas identifier — for example `5447T173`, never
  truncated to `5447`. Here `CM 5447`, `MF 5447`, and `BQ T173` identify the complete
  production reference; showing `T173` is context/identification only and does not mean
  that BQ is repaired internally. Any historical statement that BQ is recordable as a
  Reparação Interna type is superseded by this rule.
- Production activation = most recent start date at 09:00 local factory, no end-date
  test, line-scoped, deterministic. Repeated CM/MF numbers are valid occurrences (never
  deduplicated). **NO operational hard blocks.**
- Correction to a new line recalibrates the automatic production context to the NEW
  line (explicit override wins; no-production new line persists a clean null context —
  never the old line's context, never a block). Job On untouched. Original row preserved.

## 10. Controlo (R010 / R012)

- **R010 Folha de Controlo:** production-level control summary sheet INSIDE the Controlo
  area (not a new module). Anchored to `job_on_id` + exact `job_on_revision_id`;
  snapshots the production's MP_CM/MF/BQ components at creation; per-component OK/NOK +
  observation + manual MCaliper link; workflow draft → submitted → approved/rejected with
  reopen (append-only history). Capabilities on Controlo area (N23).
- **R012 Unified Production Workspace:** active-production card binds all tabs
  (Resumo/Peso/Comparação/Pegamentos/Histórico) to the same production; consumes the
  R011 per-user current-open Job On. No second calendar; no re-selection per tab;
  free-mode consultation without a fake production.

## 11. Peso (functional rules — see also LEGACY_PESO_VERIFIED_BEHAVIOR.md)

- Pinned to the exact Job On revision; `job_on_revision_id` authoritative.
- Measurement calculations are C# server-side only (GLM-PESO-05):
  `glass_weight = (capacity + volume_neck - volume_pu) * process_value`;
  water-density lookup 5–35 °C; pairing by reading/table position — CM number is an
  identifier, NOT the pairing key.
- NNPB/PS configurable values; every saved control preserves the exact value used.

## 12. Pegamentos (U-11)

- Pinned to exact `job_on_revision_id` (immutable by construction); CM/BQ/MF inherited,
  never reselectable; append-only measurements computed server-side.
- Tolerance check ±0.20 (boundary = `Exceeded`), C# only.
- PDF generated server-side from the frozen snapshot; final document persisted exactly
  once (`ON CONFLICT`), filename `Pegamentos_{producao}_{referencia}_{maquina}_relatorio.pdf`;
  closed control cannot silently replace its final document.

## 13. História (U-18)

- Transversal READ module (`historia`, `/historia`), reads `audit_events` (N01) read-only,
  no new table, no writes. TD-24: a user sees only events of modules their active template
  grants (`user.Modules ∩ origin modules`); admin events only with `audit.view`.

## 14. Admin / Login / Identity (R014)

- Capability-driven access; grants in `access_templates.modules` (jsonb); users link via
  `internal_users.template_id`; per-user optional `modules_override` (N26). Gates fail
  closed. `admin.gerir` qualifies pure admin. No anonymous/default admin; bootstrap admin
  only via the explicit CLI. User creation reconciles partial-failure idempotently
  (no orphan/duplicate mapping).

## 15. Known hardening items still required (functional/security, not visual)

These genuine defects/requirements are documented in detail in `AUDIT-REVIEWED.md` and
the `refine-v1.md` backlog (removed from AI-CONTEXT active status but not deleted from
the repo). Highlights that must remain requirements:
- JobOn aggregate hydration (C2) + transactional save/duplication (C3).
- `reparacao-externa.js` const-redeclaration SyntaxError (C1).
- PDF interpolation escaping in `JobOnPdfRenderer` (A2) + valid PDF escapes (L5).
- Armazém 1:1 TOCTOU (A3); Tampões lost-update (A4).
- RLS coverage for post-N12 tables (A5 — addressed in N25).
- Enum string-vs-number JS comparisons in Peso / Reparação Interna (M5/X5).
- `esc()` missing in `jobon.js` (M6/X6); `peso.js` innerHTML XSS (X4).
- Auth hardening: rate-limit login, cookie `Secure` always, HSTS, persist DataProtection,
  concrete `AllowedHosts` (X7).
- Admin Users list shows auth UUID under an "Email" column (X12).

---
*End of extracted functional-rules source of truth. Origin of facts:
`docs/IMPLEMENTATION_STATE.md` (full functional/technical history, carrying the
`DESIGN IMPLEMENTATION RESET — 2026-08-22` banner; its visual claims are historical).*
