# BA DMO — Technical Mapping Index

## Repository Identity

| Item | Value |
|---|---|
| Repository | `D:\BA-DMO` (git worktree root) |
| Solution | `BA-DMO.sln` |
| Branch | `main` |
| HEAD | `847830824262bc42aadfc9a34d9c4d9bdc058baf` — "Render one persistent Admin navigation" (2026-08-27 16:27:58 +0100) |
| Target framework | `net10.0` for every project (`Directory.Build.props`), `LangVersion latest`, nullable enabled, `pt-PT` neutral language |
| Container | `Dockerfile` — SDK 10.0 build of `src\BA.Dmo.Web`, `aspnet:10.0` runtime, `ASPNETCORE_URLS=http://0.0.0.0:10000` |

### Solution / project overview

| Project | Path | Role |
|---|---|---|
| `BA.Dmo.Domain` | `src\BA.Dmo.Domain\` | Domain entities, value objects, rules, shared kernel + access catalogs |
| `BA.Dmo.Application` | `src\BA.Dmo.Application\` | Application services, ports/contracts, gates, models; shared Access/Identity/Persistence/Shell |
| `BA.Dmo.Infrastructure` | `src\BA.Dmo.Infrastructure\` | Dapper repositories/lookups, unit-of-work factories, auth adapters, PDF renderers, migration runner |
| `BA.Dmo.Web` | `src\BA.Dmo.Web\` | Razor Pages, PageModels, minimal API endpoints (`Program.cs`), shell/identity/authorization, CLI commands, static assets |
| `BA.Dmo.UnitTests` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\` | Domain + Application unit tests |
| `BA.Dmo.IntegrationTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\` | Web + Infrastructure contract/guard tests |
| `BA.Dmo.VisualHost` | `AI-CONTEXT\docs\tests\BA.Dmo.VisualHost\` | Visual host harness (`Program.cs`) |

> **Test root note:** the test projects physically live under `AI-CONTEXT\docs\tests\` (registered in `BA-DMO.sln` under the `tests` solution folder). Maps in this folder use that actual root — do not reintroduce a repo-root `tests\` path.

### Current layer structure

```
D:\BA-DMO
├── BA-DMO.sln
├── Directory.Build.props  (net10.0, nullable, pt-PT)
├── Dockerfile
├── database\
│   ├── migrations\N01_identity.sql … N31_template_profiles_single_assignment.sql
│   └── consolidated_clean_install.sql
├── src\
│   ├── BA.Dmo.Domain\        (Modules\* + Shared\Access + Shared\Kernel)
│   ├── BA.Dmo.Application\   (Modules\* incl. Admin + Historia; Shared\{Access,Identity,Persistence,Shell})
│   ├── BA.Dmo.Infrastructure\(Access Dapper\*, Auth\*, Identity\*, Persistence\{Migrations, unit-of-work})
│   └── BA.Dmo.Web\           (Pages\{Admin,Auth,modules…,Shared}, Shell\, Identity\, Authorization\, Cli\, wwwroot\{styles,scripts,assets})
└── AI-CONTEXT\
    └── docs\
        ├── Maps\             (this folder — 00_INDEX … 20_WEB)
        └── tests\            (BA.Dmo.{UnitTests,IntegrationTests,VisualHost})
```

Module folders in source: `Armazem`, `Boquilhas`, `Controlo`, `Ferramentas`, `JobOn`, `Pegamentos`, `Peso`, `ReparacaoExterna`, `ReparacaoInterna`, `Tampoes` (Domain); Application adds `Admin` and `Historia` (no Domain module exists for Admin or História — see 01_DOMAIN.md, 14_HISTORIA.md, 15_ADMIN.md).

## Purpose

This folder (`maps\`) contains BA DMO technical maps.

Each map is a technical inventory/navigation document for its declared scope. Each map answers:

- What exists in this scope?
- What is it technically?
- Where is it located?
- What relevant members/structure does it contain?
- What direct references exist within that scope?

The INDEX is the registry for filenames, order, status, scope and execution history.

## Mapping Rule

- Mapping = technical inventory + location.
- Explain only what exists in the target scope.
- Point to exact files / folders / objects.
- Include relevant members, identifiers, methods, enums, states, columns, constraints, queries, tests, etc. only when they belong to the target map's scope.
- Include direct references visible inside that same scope.
- Do not explain end-to-end workflows.
- Do not merge or explain layers as an end-to-end flow.
- Do not explain how another map/layer resolves the current one.
- Do not include Design/SOT functional interpretation.
- Do not include reconciliation/gaps/fixes.
- Manuals will later combine multiple mappings to explain how the application works.

## Taxonomy

The map architecture separately registers three non-interchangeable categories:

1. **Canonical Functional Module** — a top-level application functional module (utility/use-case surface). Exactly 10.
2. **Transversal / System Surface** — a shared system-level surface (Users/Access, Design Laboratório, Login) that is not a canonical functional module. Exactly 3.
3. **Global Technical Layer** — a transversal technical-layer map covering a slice across modules (Domain, Database, Migrations, Dapper/Infrastructure, Tests, Application, Web). Exactly 7.

These categories are NOT interchangeable and are NOT merged.

### Canonical Functional Module Rule

A canonical functional module is a top-level application module with its own functional scope. Only these 10 are canonical functional modules:

1. Job On
2. Controlo
3. Ferramentas
4. Armazém
5. Boquilhas
6. Reparação Interna
7. Reparação Externa
8. Tampões
9. História
10. Admin

The following are **transversal / system surfaces**, NOT canonical functional modules:

- Users / Access
- Design Laboratório
- Login

> **Access-catalog note (source-grounded):** `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` registers 12 module entries, of which 10 are the canonical modules above and two — `peso` and `pegamentos` — are non-assignable technical entries kept as children of the single Controlo area (`AreaChildren[controlo] = [peso, pegamentos]`). `historia` is also registered as non-assignable in that catalog. Assignability rules live in 16_USERS_ACCESS.md; the registry here keeps the 10-module canonical list.

### Internal-Area Rule

Entries listed under `Internal Areas` are technical areas contained inside the canonical module. They do NOT:

- become canonical modules;
- receive an independent top-level module order;
- appear separately in the Canonical Module Registry;
- alter the canonical module count.

Dedicated technical submaps may exist later if complexity justifies them, but they remain subordinate to the canonical module.

### User-Surface Rule

Entries listed under `User Surfaces` represent source-visible variants of the same canonical module for different user profiles. They may differ in source-visible:

- UI;
- controls;
- actions;
- routes;
- capabilities;
- components;
- validation;
- decision surfaces.

They do NOT become separate modules. They remain inside the same canonical module map.

### Role/User-Surface Subdivision (source-grounded)

Role/user-surface subdivision is only applied inside a map when current source actually distinguishes the surfaces. Do NOT require artificial duplication.

Example: If a Controlo area has identical source for both profiles, map the shared technical surface once. If source distinguishes Operador/Controlador and Responsável, inventory them separately inside `07_CONTROLO.md`.

Canonical module names/order are registry metadata only; technical map contents are grounded in each map's current technical source.

## Canonical Module Registry

| Order | Module | Internal Areas | User Surfaces | Map | Status |
|---:|---|---|---|---|---|
| 1 | Job On | — | Shared | `06_JOB_ON.md` | COMPLETE |
| 2 | Controlo | Peso · Pegamentos · Resumo do Controlo · Histórico do Controlo | Operador/Controlador · Responsável | `07_CONTROLO.md` | COMPLETE |
| 3 | Ferramentas | — | Shared | `08_FERRAMENTAS.md` | COMPLETE |
| 4 | Armazém | — | Operador · Responsável | `09_ARMAZEM.md` | COMPLETE |
| 5 | Boquilhas | — | Shared | `10_BOQUILHAS.md` | COMPLETE |
| 6 | Reparação Interna | — | Shared | `11_REPARACAO_INTERNA.md` | COMPLETE |
| 7 | Reparação Externa | — | Shared | `12_REPARACAO_EXTERNA.md` | COMPLETE |
| 8 | Tampões | — | Shared | `13_TAMPOES.md` | COMPLETE |
| 9 | História | — | Shared | `14_HISTORIA.md` | COMPLETE |
| 10 | Admin | — | Admin | `15_ADMIN.md` | COMPLETE |

Statuses refreshed 2026-08-27 at HEAD `8478308` (see Execution Log).

### Controlo — Canonical Structure

**CONTROLO** is one canonical module.

Internal areas:

- Peso
- Pegamentos
- Resumo do Controlo
- Histórico do Controlo

User surfaces:

- Operador / Controlador
- Responsável

Peso, Pegamentos, Resumo and Histórico do Controlo must NOT appear as top-level modules. Operador/Controlador and Responsável must NOT appear as modules or internal areas.

`Comparação` is a **workflow / record type inside Peso** — it is NOT a module and NOT an internal area. It is a record-type value (`PesoRecordType.Comparacao`) managed within the Peso internal area.

#### Controlo Conceptual Tree

```
CONTROLO
├── Shared / module-level
├── Peso
│   ├── Operador/Controlador
│   └── Responsável
├── Pegamentos
│   ├── Operador/Controlador
│   └── Responsável
├── Resumo do Controlo
│   ├── Operador/Controlador
│   └── Responsável
└── Histórico do Controlo
```

This tree describes INDEX structure only. It does NOT imply every area necessarily has both role variants in source. Role subdivisions are mapped only where current source actually distinguishes them.

### Armazém — Canonical Structure

**ARMAZÉM** is one canonical module.

User surfaces:

- Operador
- Responsável

Conceptual structure:

```
ARMAZÉM
├── Shared / module-level
├── Operador
└── Responsável
```

These are user surfaces of the same module. Do NOT create `ARMAZEM_OPERADOR.md` or `ARMAZEM_RESPONSAVEL.md` unless a future explicit decision changes the mapping strategy.

### Technical Submaps

Possible internal-area technical submaps:

```
CONTROLO_PESO.md
CONTROLO_PEGAMENTOS.md
CONTROLO_RESUMO.md
CONTROLO_HISTORICO.md
```

These are optional technical submaps. They are NOT canonical module maps. They do NOT appear in the top-level canonical Module Registry. Do NOT create them in this task.

## Transversal / System Surface Registry

| Order | Surface | Internal Areas | User Surfaces | Map | Status |
|---:|---|---|---|---|---|
| 16 | Users / Access | — | None | `16_USERS_ACCESS.md` | COMPLETE |
| 17 | Design Laboratório | — | Shared | `17_DESIGN_LABORATORIO.md` | COMPLETE |
| 18 | Login | — | Shared | `18_LOGIN.md` | COMPLETE |

**Classification:**

| Surface | Classification |
|---|---|
| Users / Access | TRANSVERSAL / SYSTEM SURFACE — NOT a canonical functional module |
| Design Laboratório | TRANSVERSAL / SYSTEM SURFACE, technical design-system laboratory surface — NOT a canonical functional module |
| Login | TRANSVERSAL / SYSTEM SURFACE, application authentication surface — NOT a canonical functional module |

## Global Technical Layers

Global technical-layer maps provide transversal technical navigation across the technical effort. There are exactly 7:

| Map | Technical Area | Status | ID |
|---|---|---|---|
| `01_DOMAIN.md` | Domain | COMPLETE | MAP-01 |
| `02_DATABASE.md` | Database | COMPLETE | MAP-02 |
| `03_MIGRATIONS.md` | Migrations | COMPLETE | MAP-03 |
| `04_DAPPER_INFRASTRUCTURE.md` | Dapper / Infrastructure | COMPLETE | MAP-04 |
| `05_TESTS.md` | Tests | COMPLETE | MAP-05 |
| `19_APPLICATION.md` | Application | COMPLETE | MAP-19 |
| `20_WEB.md` | Web | COMPLETE | MAP-20 |

These complement the module/surface maps; they do NOT replace them.

Scope pointers:

- **01_DOMAIN.md** — domain folders, types, entities, aggregate roots, value objects, enums/states, identifiers, methods/rules/invariants, direct Domain references, exact Domain source locations.
- **02_DATABASE.md** — tables, columns, PKs, FKs, UNIQUE/CHECK constraints, indexes, functions, triggers, direct database relationships, exact SQL locations.
- **03_MIGRATIONS.md** — migration files (`database\migrations\N01…N31`), order, objects/statements introduced or altered by each migration, constraints/indexes/functions/triggers contained in migrations, `schema_migrations` bookkeeping mechanism, exact migration file locations. No application behavior.
- **04_DAPPER_INFRASTRUCTURE.md** — repository implementations, Dapper classes, embedded SQL, connection/transaction helpers, hydration/mapping code, relevant methods, exact Infrastructure locations. No full business workflow.
- **05_TESTS.md** — test projects/folders (`AI-CONTEXT\docs\tests\`), test classes, fixtures, helpers, test infrastructure, key test methods/categories, direct target under test where visible, exact test locations. No coverage-gap analysis.
- **19_APPLICATION.md** — Application project inventory, shared Application, module application objects, services, interfaces/ports/contracts, models/DTOs/projections, validators/parsers/gates, exact locations, direct Application references.
- **20_WEB.md** — Razor Pages, PageModels, routes/endpoints, authorization/identity/session Web objects, shared shell/navigation, static assets, module Web areas, exact locations, direct Web references.

## Quick Routing Table

| Need to inspect/change | Start with |
|---|---|
| Identity/access/templates | `16_USERS_ACCESS.md` |
| Admin | `15_ADMIN.md` |
| Login/session/auth adapter | `18_LOGIN.md` |
| Migrations | `03_MIGRATIONS.md` |
| Current DB model | `02_DATABASE.md` |
| Dapper persistence | `04_DAPPER_INFRASTRUCTURE.md` |
| Domain types/rules | `01_DOMAIN.md` |
| Application services/ports | `19_APPLICATION.md` |
| Web/layout/navigation | `20_WEB.md` |
| Tests | `05_TESTS.md` |
| Job On / História | `06_JOB_ON.md` / `14_HISTORIA.md` |
| Controlo (Peso · Pegamentos) | `07_CONTROLO.md` |
| Ferramentas / Armazém | `08_FERRAMENTAS.md` / `09_ARMAZEM.md` |
| Boquilhas / Reparação Interna | `10_BOQUILHAS.md` / `11_REPARACAO_INTERNA.md` |
| Reparação Externa / Tampões | `12_REPARACAO_EXTERNA.md` / `13_TAMPOES.md` |
| Design system surface | `17_DESIGN_LABORATORIO.md` |

## Map Links & Descriptions

Every map below links to the shared technical layers (01–05, 19, 20) and to the related module/surface maps; this INDEX is the top-level entry point.

| Map | Short description |
|---|---|
| `00_INDEX.md` | Map-of-maps: identity, taxonomy, registries, routing, cross-links, execution log |
| `01_DOMAIN.md` | Domain layer: entities, value objects, enums/states, rules, shared kernel/access catalogs, ownership by module |
| `02_DATABASE.md` | Final DB model from the migration chain: every table, constraints, indexes, triggers, RLS, Dapper consumers |
| `03_MIGRATIONS.md` | Migration family N01–N31 + consolidated install; per-migration DDL/DML; runner/bookkeeping (`schema_migrations`, SHA-256) |
| `04_DAPPER_INFRASTRUCTURE.md` | Every Dapper repository/lookup/unit-of-work/PDF renderer: tables read/written, transactions, consumers |
| `05_TESTS.md` | Test inventory: UnitTests, IntegrationTests, VisualHost; coverage areas per module |
| `06_JOB_ON.md` | Job On vertical slice (incl. revisions, verification, PDF, reference images, user-current context) |
| `07_CONTROLO.md` | Controlo vertical slice incl. Peso and Pegamentos internal areas (folha de controlo, peso, pegamentos, PDFs) |
| `08_FERRAMENTAS.md` | Ferramentas vertical slice (references, lotes, check rules, usage records) |
| `09_ARMAZEM.md` | Armazém vertical slice (locations, stock, movements; Operador/Responsável surfaces) |
| `10_BOQUILHAS.md` | Boquilhas vertical slice (lotes, traces, movements, discrepancies, repairers) |
| `11_REPARACAO_INTERNA.md` | Reparação Interna vertical slice (records, context, CM/MF-only, Job On active-context dependency) |
| `12_REPARACAO_EXTERNA.md` | Reparação Externa vertical slice (repairers, exits, tool-piece resolution, Armazém movements) |
| `13_TAMPOES.md` | Tampões vertical slice (configurations, fields, balances, movements, machines) |
| `14_HISTORIA.md` | História vertical slice (cross-module history surface; no dedicated Domain module) |
| `15_ADMIN.md` | Admin vertical slice (landing, Utilizadores, Templates, Aplicações, Auditoria; template↔profile↔modules) |
| `16_USERS_ACCESS.md` | Access architecture end-to-end (identity → template → profile → modules → capabilities → shell → URL auth); single-template model |
| `17_DESIGN_LABORATORIO.md` | Design system surface (`/design-laboratorio` laboratory page + design guard tests) |
| `18_LOGIN.md` | Login surface (route, auth adapter, session/cookie, identity resolution, redirects, failure states) |
| `19_APPLICATION.md` | Application contracts/services/ports/models/gates by module and shared area |
| `20_WEB.md` | Web structure: Razor Pages, layout, shell/Admin navigation authorities, endpoints, static assets |

## Mapping Sequence

The mapping sequence is separated into three clearly separated groups.

### A. GLOBAL TECHNICAL LAYERS

1. Domain
2. Database
3. Migrations
4. Dapper / Infrastructure
5. Tests
19. Application
20. Web

### B. CANONICAL FUNCTIONAL MODULE SEQUENCE

1. Job On
2. Controlo
3. Ferramentas
4. Armazém
5. Boquilhas
6. Reparação Interna
7. Reparação Externa
8. Tampões
9. História
10. Admin

> Peso and Pegamentos are absent from the sequence of canonical top-level modules — they are internal
> areas of the CONTROLO module. Dedicated technical detail for these internal areas, if ever mapped, is
> covered as technical submaps, not as canonical top-level modules.

### C. TRANSVERSAL / SYSTEM SURFACES

16. Users / Access
17. Design Laboratório
18. Login

> Orders 16–18 continue the MAP-ID numbering but are a separate category; they do NOT continue the
> canonical functional module sequence (B). The canonical module sequence contains exactly 10 modules.

## Status Model

| Status | Meaning |
|---|---|
| NOT STARTED | No fresh map file exists under `maps\`. |
| IN PROGRESS | A fresh map has been started but is incomplete or still being verified. |
| COMPLETE | The fresh map exists and has been verified for its declared technical scope. |
| REVERIFY | A map exists but current source has changed since it was verified; it must be re-verified before being relied on. |

Rules:

- COMPLETE means the fresh map exists and has been verified for its declared technical scope. It does NOT mean the workflow has been fully explained.
- A file existing does NOT automatically mean COMPLETE.

## Map Contract

Every map must contain only what is useful for navigation in its declared scope.

Minimum expected structure:

1. Scope
2. Inventory
3. Technical objects/types
4. Relevant members/structure
5. Direct references within scope
6. Exact locations
7. Sources verified

Optional sections may be used when useful: identifiers, enums/states, constraints, indexes, functions/triggers, queries, fixtures, helpers. Do NOT require irrelevant sections.

Do NOT require: main data flow, technical gaps, functional rules, reconciliation, cross-layer explanation.

A mapping is not an audit. If a technical object is absent and that absence is useful for navigation, it may simply say: "No dedicated Domain type found." or "No table with this name exists." — do not turn that into an issue analysis.

### Module Layer-Coverage Table Rule (canonical module maps)

Every canonical functional module map (orders 1–10) should expose a compact technical layer-coverage table:

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES/NO | ... |
| Application | YES/NO | ... |
| Infrastructure | YES/NO | ... |
| Web | YES/NO | ... |
| Database | YES/NO | ... |
| Tests | YES/NO | ... |

Clarification:

- This is **technical navigation only**; it does **not** explain workflow.
- `Present = NO` is a valid value (the module has no dedicated objects in that layer).
- Shared-only dependencies may be noted mechanically but are not the module's own layer objects.
- Exact locations should be concise (folder/file patterns), not exhaustive enumerations.
- Build the table only from information already verified inside that module map; do not invent locations.

If a canonical module map already has a Layer Summary that contains the same layer/location information, it may be normalized to satisfy the contract without duplicating unnecessary tables.

## Classification Labels

When a map encounters a suspicious structure, it uses ONLY these labels (with evidence):

- CONFIRMED CURRENT
- INTENTIONAL NORMALIZATION
- POTENTIAL OVERLAP — NEEDS AUDIT
- SCHEMA DRIFT — NEEDS AUDIT
- MIGRATION DRIFT — NEEDS AUDIT
- LEGACY CANDIDATE — NEEDS AUDIT
- ORPHAN CANDIDATE — NEEDS AUDIT
- UNKNOWN / OWNER DECISION REQUIRED

Problems discovered during mapping are recorded in the relevant map as `NEEDS REVIEW` (with evidence). This task never implements fixes.

## Execution Log

| Map | Status | Last verified |
|---|---|---|
| 01_DOMAIN.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 02_DATABASE.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 03_MIGRATIONS.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 04_DAPPER_INFRASTRUCTURE.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 05_TESTS.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 06_JOB_ON.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 07_CONTROLO.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 08_FERRAMENTAS.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 09_ARMAZEM.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 10_BOQUILHAS.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 11_REPARACAO_INTERNA.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 12_REPARACAO_EXTERNA.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 13_TAMPOES.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 14_HISTORIA.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 15_ADMIN.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 16_USERS_ACCESS.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 17_DESIGN_LABORATORIO.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 18_LOGIN.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 19_APPLICATION.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |
| 20_WEB.md | COMPLETE | 2026-08-27 — reconciled at HEAD 8478308 |

Index history:

- MAP-00 — INDEX created.
- MAP-01 to MAP-05 — transversal maps completed and verified.
- MAP-06 — 06_JOB_ON.md completed and final verified.
- MAP-07 — 07_CONTROLO.md completed and final verified.
- MAP-08 — 08_FERRAMENTAS.md completed and final verified.
- MAP-09 — 09_ARMAZEM.md completed and final verified.
- MAP-10 — 10_BOQUILHAS.md completed and final verified.
- MAP-11 — 11_REPARACAO_INTERNA.md completed and final verified.
- MAP-12 — 12_REPARACAO_EXTERNA.md completed and final verified. User Surface set to Shared from source.
- MAP-13 — 13_TAMPOES.md completed and final verified.
- MAP-14 — 14_HISTORIA.md completed and final verified. User Surface set to Shared from source.
- MAP-15 — 15_ADMIN.md completed and final verified.
- MAP-16 — 16_USERS_ACCESS.md completed and final verified. User Surface set to None from source.
- MAP-17 — 17_DESIGN_LABORATORIO.md completed and final verified. User Surface set to Shared from source.
- MAP-18 — 18_LOGIN.md completed and final verified. User Surface set to Shared from source.
- INDEX structure refined: canonical modules, internal areas and user surfaces are now represented separately; Controlo contains Peso/Pegamentos/Resumo/Histórico, and Controlo/Armazém expose profile-dependent user surfaces. Completed map IDs (MAP-01 to MAP-12) remain unchanged.
- MAP-19 — APPLICATION transversal technical map completed and final verified.
- MAP-20 — WEB transversal technical map completed and final verified.
- INDEX taxonomy normalized: 10 canonical functional modules separated from 3 transversal/system surfaces.
- 2026-08-27 — REFRESH PASS at HEAD `8478308`: all maps re-reconciled IN PLACE against current source. Key reconciliation points: migrations N28–N31 added (reparação interna CM/MF-only, JobOn reference images, covering index, template profiles single assignment); Admin/Access template-profile + single-assignment model (N31, `access_template_profiles`, one effective template per user); persistent Admin navigation (`_AdminNav` rendered once from `_Layout`); test-root paths corrected to `AI-CONTEXT\docs\tests\`; test inventory refreshed (UnitTests/IntegrationTests/VisualHost); Dapper/Application/Domain/Web inventories re-verified and `Sources Verified` sections updated. No source/database/test code modified.

## Current Pointer

Global technical layers:

COMPLETE (Domain, Database, Migrations, Dapper/Infrastructure, Tests, Application, Web) — reconciled 2026-08-27

Canonical functional modules:

COMPLETE (Job On, Controlo, Ferramentas, Armazém, Boquilhas, Reparação Interna, Reparação Externa, Tampões, História, Admin) — reconciled 2026-08-27

Transversal / system surfaces:

COMPLETE (Users / Access, Design Laboratório, Login) — reconciled 2026-08-27

Last completed technical maps:

19_APPLICATION.md — MAP-19 — COMPLETE
20_WEB.md — MAP-20 — COMPLETE

Next:

FINAL GLOBAL MAP REVIEW — normalized architecture / completeness