# BA DMO — MODULES OPERATIONAL MODEL

OPEN OWNER QUESTIONS: NONE

---

## Índice

1. [O Que É um Módulo (WHAT IS A MODULE)](#1-o-que-é-um-módulo-what-is-a-module)
2. [Atribuição de Módulos a Utilizadores (MODULE ASSIGNMENT TO USERS)](#2-atribuição-de-módulos-a-utilizadores-module-assignment-to-users)
3. [Papel / Perfil Dentro do Módulo (ROLE / PROFILE INSIDE A MODULE)](#3-papel--perfil-dentro-do-módulo-role--profile-inside-a-module)
4. [Variantes de Módulo (MODULE VARIANTS)](#4-variantes-de-módulo-module-variants)
5. [Áreas Internas (INTERNAL AREAS)](#5-áreas-internas-internal-areas)
6. [Workflows / Tipos de Registo (WORKFLOWS / RECORD TYPES)](#6-workflows--tipos-de-registo-workflows--record-types)
7. [Entrada na Aplicação — Job On (APPLICATION ENTRY — JOB ON)](#7-entrada-na-aplicação--job-on-application-entry--job-on)
8. [Contexto de Job On Ativo (ACTIVE JOB ON CONTEXT)](#8-contexto-de-job-on-ativo-active-job-on-context)
9. [Acesso vs Contexto (ACCESS VS CONTEXT)](#9-acesso-vs-contexto-access-vs-context)
10. [Navegação (NAVIGATION)](#10-navegação-navigation)
11. [Relação com o Admin (ADMIN RELATIONSHIP)](#11-relação-com-o-admin-admin-relationship)
12. [Terminologia — Módulo / Área / Workflow / Variante](#12-terminologia--módulo--área--workflow--variante)
13. [Módulos Atuais como Exemplos (USE CURRENT MODULES AS EXAMPLES)](#13-módulos-atuais-como-exemplos-use-current-modules-as-examples)
14. [Questões Transversais — Resolvidas no Modelo Global (RESOLVED BY THE GLOBAL MODULE / USER ROLE MODEL)](#14-questões-transversais--resolvidas-no-modelo-global-resolved-by-the-global-module--user-role-model)
- [SUMMARY (compact)](#summary-compact)

## 1. O QUE É UM MÓDULO (WHAT IS A MODULE)

A **module** is a **logical functional access unit** in the application.

A module represents an **area of the system that can be assigned to a user**. It is the unit through which
a user is given (or denied) access to a functional area of the application. Assigning a module to a user is
what determines whether that user can enter and work with that area.

This is an important classification rule, because not everything that looks like a separate screen is a
separate module. Do **not** classify something as a module merely because it has:

- its own page;
- its own tab;
- its own technical namespace;
- its own service;
- its own workflow.

The classification must follow the **intended functional model**, not the technical presence of a screen or a
namespace. A page, a tab, a service or a workflow can exist **inside** a module without being a module itself.
What makes something a module is that it is a **logical access unit assignable to a user**.

### Confirmed transversal principle

> **MODULE** = logical access unit that can be assigned to a user.

This transversal principle is not exclusive to any single module. It is the shared baseline that every
module explanation in the application must satisfy.

---

## 2. ATRIBUIÇÃO DE MÓDULOS A UTILIZADORES (MODULE ASSIGNMENT TO USERS)

When creating or editing a user in the Admin panel, the administrator must be able to choose **which
application modules that user can access**.

The functional model is:

```
USER
  -> assigned modules
  -> accessible application areas
```

Behaviour when a module is **NOT assigned** to the user:

- the module **does not appear** in that user's normal navigation/tabs;
- the user **does not have functional access** to that module.

Module assignment is therefore an **access-control concept**, not merely a UI preference (not just "which
tabs are visible"). It determines whether the user is functionally allowed to enter and use an area of the
application for that user.

This principle applies to the application modules in general, not only to one screen. The common examples
used across the captured clarifications (Controlo, Armazém) both follow it.

---

## 3. PAPEL / PERFIL DENTRO DO MÓDULO (ROLE / PROFILE INSIDE A MODULE)

Having access to a module **does not necessarily mean that every user sees the same interface or the same
actions**.

Inside a module, the user's **functional role / profile** can determine:

- which **interface variant** is shown;
- which **actions** are available;
- which actions are **hidden**;
- whether the user **measures / records**;
- whether the user **reviews / approves**;
- whether the user has **more or fewer options**.

A difference in role/profile that changes the experience **does NOT automatically create a separate module**.
Two different experiences of the same assigned module are still the same module.

This is a second, separate concept from module assignment (see §9). It is about the experience **inside** an
assigned module, not about whether the user can enter the module at all.

---

## 4. VARIANTES DE MÓDULO (MODULE VARIANTS)

A module can have **role-dependent variants**. A variant is a different experience of the **same** assigned
module, shown to the user according to role/profile.

### Example — CONTROLO

**CONTROLO** is **one logical module**.

- **CONTROLO + Operador / Controlador** → the operational **measurement / recording** experience.
- **CONTROLO + Responsável** → the **review / approval / decision** experience.

These are **different experiences of the same module**. They must **not** be treated as two separately
assignable modules. The assignable logical module is CONTROLO; the role selects which variant of that module
the user gets.

This "assigned module + functional role → variant/experience" pattern is **not exclusive to Controlo**. Other
modules can present different functional experiences depending on the user's role.

### Example — ARMAZÉM

**Armazém** is another known example where the **same logical module** can have **different experiences**
depending on the user's role (for example an Operador experience and a Responsável experience). Its detailed
workflow is **not explained here** — it belongs to the Armazém module explanation. It is mentioned only to
establish the general design pattern: role-dependent module variants are a transversal characteristic, not a
Controlo exception.

---

## 5. ÁREAS INTERNAS (INTERNAL AREAS)

A module may contain **internal functional areas**. An internal area is **not automatically another module**.

### Example — CONTROLO

**CONTROLO** is the module. Inside Controlo there are internal functional areas:

- **Peso**
- **Pegamentos**
- **Resumo / Folha de Controlo**
- **Histórico do Controlo**

These belong **inside Controlo**. They must **not** be independently classified as top-level modules unless the
owner explicitly defines them that way.

Consequence: **Peso is not a top-level module; Pegamentos is not a top-level module; Resumo is not a top-level
module; Histórico do Controlo is not a top-level module.** They are internal areas of the single Controlo
module.

---

## 6. WORKFLOWS / TIPOS DE REGISTO (WORKFLOWS / RECORD TYPES)

A workflow or record type inside an internal area is **also not a module**.

### Example — CONTROLO / PESO

```
CONTROLO
  -> Peso  (internal area)
     -> Controlo inicial
     -> Comparação
```

**Comparação** is a **workflow / type of record inside Peso**. It is not:

- a separate module;
- a top-level Controlo area;
- a separately assignable access unit.

It is a kind of operation/record that happens inside the Peso area of Controlo. The same rule applies to the
smaller workflow/record concepts inside other modules: a workflow or record type is not, by itself, a module.

---

## 7. ENTRADA NA APLICAÇÃO — JOB ON (APPLICATION ENTRY — JOB ON)

All operational users start the application on the **Job On** page.

The **Job On page is the common application entry point** (the common landing page of all authenticated
operational users). It presents:

- the **production calendar / planning**;
- **upcoming / current productions**;
- **selectable Job Ons**;
- **details of the selected production/day**.

Job On is the central **production / planning context** of the application. It is the common starting surface —
not merely a dependency of Controlo. It holds the production planning, exposes planned productions, identifies
the exact production context (Reference, Production, Machine/Line), holds the exact revision, and provides that
production context to the operational modules.

Two important clarifications:

1. **Not every application module depends on an active Job On.** The Job On page is the common starting
   surface, but whether a given module depends on a loaded Job On must be explained **per module**.
2. The **pure Admin exception**: the pure Administrator is the exception — the Administrator enters directly
   into the Admin area and does not enter the operational modules. For **operational** users, Job On is the
   common landing page.

---

## 8. CONTEXTO DE JOB ON ATIVO (ACTIVE JOB ON CONTEXT)

Some modules / functional areas use the **Job On selected on the common Job On page** as their **active
production context**.

**Controlo** is a confirmed example.

Functional model:

```
Job On page
  -> user selects / loads a Job On
  -> Controlo receives that active production context
  -> Peso, Pegamentos, Resumo / Folha de Controlo and Controlo history
     use that same production context
```

Controlo consumes the exact Job On and exact revision and inherits the exact planned tooling/lots from Job On
(in the confirmed Case A it inherits the exact CM/MF/BQ lots; it does not reconstruct or independently
re-select the production tooling).

The message/state

> **"Nenhum Job On carregado"**

belongs to modules/areas that **require an active production context**. It indicates that the module currently
has no production to work against. It must **NOT automatically be applied to every application module** — only
to production-dependent modules/areas that genuinely need an active Job On context.

---

## 9. ACESSO VS CONTEXTO (ACCESS VS CONTEXT)

These three concepts are different and must be kept separate:

| Concept | Means |
| --- | --- |
| **MODULE ACCESS** | whether the user is **allowed to enter** that module. |
| **ROLE / PROFILE** | which **experience / actions** the user gets **inside** the module. |
| **ACTIVE JOB ON CONTEXT** | which **production** a production-dependent module is currently working with. |

### Example

A user may have **Controlo assigned** but **no Job On currently loaded**.

That means:

- the user has **permission to access** Controlo (module access);
- but Controlo currently has **no production context** (no active Job On).

Lack of context must **not** be confused with lack of module permission. "Nenhum Job On carregado" is a context
condition, not a statement that the user is not allowed to enter the module. The user still has the module;
the module simply has no production loaded at the moment.

---

## 10. NAVEGAÇÃO (NAVIGATION)

The user's **main navigation** should reflect the **modules assigned to that user**.

- **Assigned module** → available in navigation where appropriate.
- **Unassigned module** → not shown as an available application area.

Inside a module, **internal tabs / areas can vary according to role/profile**. Do not assume that all users must
see the same tabs. Two users who both have the same module assigned may see different internal tabs because
their role/profile gives them a different variant of that module.

Navigation therefore reflects **access** (assigned modules), and **internal tabs reflect role/profile** within
the assigned modules.

---

## 11. RELAÇÃO COM O ADMIN (ADMIN RELATIONSHIP)

**Admin user management is where module access is assigned.**

The functional explanation establishes:

```
Administrator creates / edits user
  -> chooses accessible modules
  -> user sees only relevant assigned application modules
  -> role / profile determines the experience inside those modules where applicable
```

The Administrator, when creating or editing a user, chooses which application modules that user may access.
The user then sees only the assigned application modules in their navigation, and — where the module has
role-dependent variants — the user's role/profile determines which experience of each assigned module is shown.

The functional model is: the Administrator assigns modules to a user; that assignment determines the user's
functional access; the role/profile determines the experience where applicable. The technical mechanism that
realizes this assignment is not part of this functional model. Access templates, managed in the Admin module,
are the functional vehicle through which modules are associated to a user; no technical template or capability
creates a functional profile or module by itself.

---

## 12. TERMINOLOGIA — MÓDULO / ÁREA / WORKFLOW / VARIANTE

Define clearly:

| Term | Meaning |
| --- | --- |
| **MODULE** | A logical access unit assignable to a user. |
| **INTERNAL AREA / VERTENTE** | A functional area contained inside a module. |
| **WORKFLOW / RECORD TYPE** | A process or type of operation inside an area. |
| **ROLE / FUNCTIONAL PROFILE** | The responsibility / type of user. |
| **MODULE VARIANT / EXPERIENCE** | The interface / workflow shown inside an assigned module according to role/profile. |
| **ACTIVE PRODUCTION CONTEXT** | The selected Job On / revision used by modules that depend on production context. |

These six terms are the vocabulary of the transversal module model. Every module explanation should use them
consistently and keep the boundaries between them clear.

---

## 13. MÓDULOS ATUAIS COMO EXEMPLOS (USE CURRENT MODULES AS EXAMPLES)

Only examples already supported by existing functional clarifications are used.

### CONTROLO — main worked example

- one module;
- **Peso / Pegamentos / Resumo / Histórico** internal areas;
- **Operador / Responsável** variants;
- **depends on the selected Job On production context** (active production context).
- **Comparação** = a workflow/type of record inside Peso, not a module.

CONTROLO is the clearest and most completely validated example of the full model: one assignable module,
internal areas, a workload inside an area, role-dependent variants, and dependence on the active production
context.

### ARMAZÉM

- another module known to have **role-dependent behavior**;
- its full workflow is **not** explained here (belongs to the Armazém module explanation).

Armazém is used only to show that role-dependent module variants are a general design pattern.

### JOB ON

- common application entry point / landing page for operational users;
- central **production / planning context**;
- **not merely a dependency of Controlo** — it is the common starting surface and the production/planning hub.

### What is deliberately NOT classified

No classification is invented for modules that have not yet been functionally validated. Where a module's
classification is not yet confirmed, that is recorded as a genuine open question rather than assumed.

---

## 14. QUESTÕES TRANSVERSAIS — RESOLVIDAS NO MODELO GLOBAL (RESOLVED BY THE GLOBAL MODULE / USER ROLE MODEL)

The following transversal questions were previously listed as unresolved. They are now **closed** by the
owner-confirmed global module / user role model and the owner-confirmed module-assignment rule, and are
recorded here as resolved so that they are not re-opened as genuine questions.

1. **Which current application areas are truly top-level modules?**
   **RESOLVED.** The current top-level assignable modules are: Job On, Controlo, Ferramentas, Armazém,
   Boquilhas, Reparação Interna, Reparação Externa, Tampões and Admin. Peso, Pegamentos,
   Resumo / Folha de Controlo, Histórico do Controlo and Comparação are not top-level modules. História is
   not a module — it is a transversal read surface for audit events, and the internal Histórico tabs of the
   modules are internal areas.

2. **Is any current navigation item incorrectly classified as a module?**
   **RESOLVED.** Peso, Pegamentos, Resumo / Folha de Controlo and Histórico do Controlo are internal areas of
   the single Controlo module; Comparação is a Peso workflow/record type. Navigation/tabs reflect modules
   assigned to the user; a module not assigned is absent from the user's navigation and not functionally
   accessible.

3. **Is all module access individually assignable in Admin?**
   **RESOLVED.** Modules are assigned **individually per user** in the Admin panel when creating/editing a
   user. The profile never automatically grants modules.

4. **Can some roles have read-only variants?**
   **CLOSED — NO READ-ONLY PROFILE.** There is no read-only functional profile and no management / metrology /
   consultation profile. A user with an assigned module experiences one of the existing three profiles'
   behaviour inside it; a profile does not automatically grant modules.

5. **How should additional profiles beyond Operador / Responsável behave where not yet defined?**
   **CLOSED — EXACTLY THREE FUNCTIONAL PROFILES.** There are exactly three functional profiles: Admin,
   Operador / Controlador and Responsável. No fourth profile exists.

These items remain **closed** and are not re-opened in this transversal model.

---

## SUMMARY (compact)

A module is a logical functional access unit assignable to a user. The Administrator assigns modules to users
when creating/editing a user; a user sees and has functional access only to the assigned modules. Inside an
assigned module, the user's role/profile determines the variant/experience (more/fewer/different options,
measure-record vs review-approve). A module can contain internal areas; an internal area is not a module. A
workflow/record type inside an area is not a module. Controlo is the main worked example (one module; Peso,
Pegamentos, Resumo, Histórico internal; Comparação a Peso workflow; Operador/Responsável variants; depends on
the active Job On context). Armazém is a second known example of role-dependent variants (workflow not
explained here). Job On is the common application entry point and central production/planning context — it is
not merely a Controlo dependency. Module access, role/profile, and active production context are three separate
concepts; "Nenhum Job On carregado" is a context condition, not a lack of module permission. The former
transversal open questions (final module list, possible misclassifications, individual assignability, read-only
variants, additional profiles) are now resolved/closed by the owner-confirmed global model (§14).

## Implementation Pointers

### Relevant implementation areas

- Application: module assignment per user is realised through Admin (access templates) — see `03_USERS_ACCESS_OPERATIONAL.md` and `90_ADMIN_FUNCTIONAL.md`.
- Web / Razor: navigation shows only assigned modules; "Nenhum Job On carregado" is a context condition, not a permission failure (state/message, not an error).
- Technical map: `maps\16_USERS_ACCESS.md`, `maps\19_APPLICATION.md` (verify freshness before use).

### Known implementation gaps

- None verified in this document set.

### Design reference

- Transversal shell/navigation/tabs/side panel: `99_DESIGN_LABORATORIO.md`.

### Cross-module dependencies

- Controlo (Peso/Pegamentos/Resumo/Histórico as internal areas of one module); Job On (entry point + production/planning context); Armazém (role-dependent variants example).
