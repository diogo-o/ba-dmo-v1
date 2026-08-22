# Package validation

## Overall result

**PASS WITH DISCLOSED SOURCE GAPS.** All DES-001 through DES-018 have a folder, README, exact task extraction and acceptance file. Every canonical visual explicitly named by the final plan is present. Two generic/legacy references are missing and are not silently substituted.

| DES task | Module | Canonical authority | Cited handoffs/briefs | Owner decision | README/task/acceptance | Result |
|---|---|---|---|---|---|---|
| DES-001 | 10_FOUNDATION | PASS — CSS/docs | PASS | N/A | PASS | PASS |
| DES-002 | 11_SHELL | PASS — global system and canonical headers contract | PASS | N/A | PASS | PASS |
| DES-003 | 12_LOGIN | PASS | PASS | N/A | PASS | PASS |
| DES-004 | 13_ADMIN | PASS | PASS | N/A | PASS | PASS |
| DES-005 | 20_JOB_ON | PASS — main + print | PASS — four briefs/contracts | PASS — shared documents + resolved article image | PASS | PASS |
| DES-006 | 21_CONTROLO | PASS | PASS | PASS — shared-production decision | PASS | PASS |
| DES-007 | 22_PESO_OPERADOR | PASS — operator + print | PASS | PASS — comparison/shared production | PASS | PASS |
| DES-008 | 23_PESO_RESPONSAVEL | PASS | PASS | PASS — comparison | PASS | PASS |
| DES-009 | 24_PEGAMENTOS | PASS | PASS — handoff + snapshot | N/A | PASS | PASS |
| DES-010 | 30_FERRAMENTAS | PASS | MISSING — DES-010 says registration/verification briefs, but only registration brief is named in §2 and retained | PASS — Q-001 resolved locally | PASS | MISSING |
| DES-011 | 31_BOQUILHAS | PASS | PASS | N/A | PASS | PASS |
| DES-012 | 32_ARMAZEM | PASS | PASS | PASS — SAP alert relationship | PASS | PASS |
| DES-013 | 33_TAMPOES | PASS | PASS | N/A | PASS | PASS |
| DES-014 | 34_REPARACAO_INTERNA | PASS | PASS | PASS — CM/MF only | PASS | PASS |
| DES-015 | 35_REPARACAO_EXTERNA | PASS — moldes.html | PASS — lifecycle + brief | PASS — functional SOT A–G | PASS | PASS |
| DES-016 | 36_HISTORIA | PASS | PASS | N/A | PASS | PASS |
| DES-017 | 90_DESIGN_LAB | PASS — docs are plan authority; review HTML supporting | PASS | N/A | PASS | PASS |
| DES-018 | 99_FINAL_ACCEPTANCE | PASS — all local authorities | PASS | PASS — all resolved/local decisions | PASS | PASS |

## Variant classification

| Source | Classification | Result and use |
|---|---|---|
| `tampoes.html` | CANONICAL | Copied as the sole Tampões visual authority. |
| `tampoes-v38-standalone.html` | SUPERSEDED | Standalone historical variant; not copied and explicitly forbidden in the README. |
| `moldes.html` | CANONICAL | Copied as Reparação Externa primary visual authority. |
| `reparacao-externa-v1.html` | SUPPORTING | Copied as lifecycle reference only; cannot override `moldes.html` or functional SOT. |
| `reparacao-v2.html` | SUPERSEDED | Copied only as `99_DO_NOT_IMPLEMENT_...` for explicit comparison/provenance. |

## Missing and ambiguous sources

- **MISSING:** `job-on-v48-folha-producao.html` is named by the retained package README/HANDOFF_INDEX but is absent. It is not named as the final plan's canonical Job On visual; no substitute was made. The final plan-backed `job-on.html` and four-page print authority are present.
- **MISSING / AMBIGUOUS GENERIC REFERENCE:** DES-010 says “registration/verification briefs”, while Authority §2 names only `FERRAMENTAS_REGISTO_DESIGN_BRIEF.md`. No separate Ferramentas verification brief exists in the retained current design package. No similarly named file was substituted.
- No unresolved Q-001/Q-002 remains: both resolutions are copied locally into the affected modules.

## Safety and invariant audit

- PASS — all 18 DES tasks represented.
- PASS — Boquilhas and Ferramentas are separate.
- PASS — Peso Operator and Responsible are separate.
- PASS — RI is CM/MF only; BQ is forbidden; `5447T173` is surfaced intact.
- PASS — Controlo exact current-open Job On, one context, no second selector/calendar, free mode surfaced.
- PASS — SAP utilisation is active, manual-only, never calculated; future automation out of scope; Armazém alert consumption documented.
- PASS — Job On article image is reference-owned and limited to the required print sheet.
- PASS — every folder has one deterministic read order and one explicit visual/design authority.
- PASS — all new material is under `design-coder`; no application, database, schema or migration destination was written.

