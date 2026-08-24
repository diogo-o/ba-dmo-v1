# BA DMO — FUNCTIONAL INDEX

STATUS: CLEANED FUNCTIONAL REFERENCE SET — OWNER-REVIEWED

## 1. Purpose

This folder contains the **normalized functional model set** of the BA DMO application, used as the **pre-manual functional reference**.

This is:

- **functional documentation** — the functional model, rules, boundaries, ownership and workflows as confirmed/classified;
- **not implementation documentation** — implementation behaviour is treated as evidence, never as functional authority;
- **not technical mapping** — no technical map, schema or code-level mapping is produced here;
- **not the final manual** — this is the source set from which the final manual will be produced in a later step.

Priority rule: **Owner-confirmed functional rules take priority over stale implementation evidence.**

## 2. Principal Reading Order

### A. Global / Transversal Models

1. `01_GLOBAL_MODULE_USER_ROLE.md`
2. `02_MODULES_OPERATIONAL.md`
3. `03_USERS_ACCESS_OPERATIONAL.md`

### B. Functional Modules

4. `10_JOB_ON_FUNCTIONAL.md`
5. `20_CONTROLO_FUNCTIONAL.md`
6. `30_FERRAMENTAS_FUNCTIONAL.md`
7. `40_ARMAZEM_FUNCTIONAL.md`
8. `50_BOQUILHAS_FUNCTIONAL.md`
9. `60_REPARACAO_INTERNA_FUNCTIONAL.md`
10. `70_REPARACAO_EXTERNA_FUNCTIONAL.md`
11. `80_TAMPOES_FUNCTIONAL.md`
12. `90_ADMIN_FUNCTIONAL.md`

### C. Supporting / Special

13. `99_DESIGN_LABORATORIO.md`

## 3. Index Table

| Order | File | Classification | Purpose |
|---|---|---|---|
| 1 | `01_GLOBAL_MODULE_USER_ROLE.md` | TRANSVERSAL GLOBAL MODEL | Modelo global módulo × perfil (Admin, Operador / Controlador, Responsável) e atribuição individual de módulos; classificação do que é / não é módulo. |
| 2 | `02_MODULES_OPERATIONAL.md` | TRANSVERSAL MODULE STRUCTURE / OPERATIONAL MODEL | Explicação transversal de como os módulos funcionam: módulo, áreas internas, workflows, variantes, acesso, contexto e navegação. |
| 3 | `03_USERS_ACCESS_OPERATIONAL.md` | TRANSVERSAL USERS / ACCESS MODEL — NOT A MODULE | Atribuição de módulos aos utilizadores; perfil vs módulos atribuídos; comportamento quando um módulo não está atribuído. |
| 4 | `10_JOB_ON_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional do Job On — contexto de produção/planeamento, entrada na aplicação e relação com os restantes domínios. |
| 5 | `20_CONTROLO_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional do Controlo — Peso, Pegamentos, Resumo / Folha de Controlo, Histórico (áreas internas), decisões e documentos. |
| 6 | `30_FERRAMENTAS_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional das Ferramentas — master das ferramentas CM/MF/BQ/PU/CS, estado técnico e fronteiras. |
| 7 | `40_ARMAZEM_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional do Armazém — localização física, movimentos (entrada/saída/saídas programadas) e ownership. |
| 8 | `50_BOQUILHAS_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional das Boquilhas — registo dos movimentos de reparação externa de BQ. |
| 9 | `60_REPARACAO_INTERNA_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional da Reparação Interna — registo operacional de reparação de CM/MF em produção. |
| 10 | `70_REPARACAO_EXTERNA_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional da Reparação Externa — batches de reparação externa, estados, reparadores e fluxo operacional. |
| 11 | `80_TAMPOES_FUNCTIONAL.md` | TOP-LEVEL FUNCTIONAL MODULE | Modelo funcional dos Tampões — configuração técnica e gestão de quantidades TP. |
| 12 | `90_ADMIN_FUNCTIONAL.md` | TOP-LEVEL ADMINISTRATIVE MODULE | Modelo funcional do Admin — utilizadores, templates de acesso, aplicações e auditoria; decisões Owner 1–5. |
| 13 | `99_DESIGN_LABORATORIO.md` | SUPPORTING / DESIGN REFERENCE | Manual/referência transversal da aplicação — shell, design system, integração de módulos e superfícies transversais; superfície de sistema, não módulo de negócio. |

## 4. Global Classification Rules

**USERS / ACCESS**
=
transversal access model
**NOT** a top-level module.

**HISTÓRIA**
=
**NOT** a top-level module.
History is an internal history tab / area inside relevant modules.

**CONTROLO**
=
one top-level module.

Inside Controlo:

- Peso
- Pegamentos
- Resumo
- Histórico

are internal areas.

**Comparação** is a workflow/type inside **Peso**, not a module.

**LOGIN / AUTHENTICATION**
=
transversal system area, not a module, if referenced.

**DESIGN LABORATÓRIO**
=
supporting/special area, not part of the normal operational-module reading sequence unless its current file (`99_DESIGN_LABORATORIO.md`) explicitly establishes otherwise.

---

*Canonical entry point for the functional set. This index is structural only — it adds no functional rule.*