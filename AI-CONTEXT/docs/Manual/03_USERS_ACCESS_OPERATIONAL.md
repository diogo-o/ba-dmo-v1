# USERS / ACCESS — MODELO FUNCIONAL TRANSVERSAL

OPEN OWNER QUESTIONS: 0

---

## Índice

1. [Principle](#1-principle)
2. [Profile vs assigned modules (two distinct concepts)](#2-profile-vs-assigned-modules-two-distinct-concepts)
3. [Assigning a module to a user — role does NOT imply all modules](#3-assigning-a-module-to-a-user--role-does-not-imply-all-modules)
4. [Behaviour when a module is NOT assigned](#4-behaviour-when-a-module-is-not-assigned)
5. [Worked example — two Operadores with different modules](#5-worked-example--two-operadores-with-different-modules)
6. [Profile decides the variant *inside* an assigned module](#6-profile-decides-the-variant-inside-an-assigned-module)
7. [Summary of the owner-confirmed functional model](#7-summary-of-the-owner-confirmed-functional-model)
8. [Relationship to the transversal module model](#8-relationship-to-the-transversal-module-model)

## USERS / ACCESS — ATRIBUIÇÃO DE MÓDULOS AOS UTILIZADORES

## 1. Principle

Os **módulos acessíveis** por cada utilizador são definidos **individualmente** no painel de **Administração**.

Ao **criar ou editar** um utilizador, o **Admin seleciona os módulos** aos quais esse utilizador terá acesso.

## 2. Profile vs assigned modules (two distinct concepts)

O **perfil** do utilizador e a **atribuição de módulos** são conceitos **distintos**:

- o **PERFIL** define **como** o utilizador atua **dentro** dos módulos;
- os **MÓDULOS ATRIBUÍDOS** definem **quais áreas** da aplicação estão disponíveis para esse utilizador.

| Conceito | Responde a | Determina |
|---|---|---|
| **Perfil** (Admin / Operador / Controlador / Responsável) | como o utilizador atua | a variante funcional EXPERIÊNCIA dentro de um módulo atribuído |
| **Módulos atribuídos** | quais áreas estão disponíveis | **quais** módulos / áreas o utilizador pode entrar e usar |

## 3. Assigning a module to a user — role does NOT imply all modules

Um utilizador **Operador / Controlador** **não recebe automaticamente** acesso a todos os módulos operacionais.

Um utilizador **Responsável** também **não recebe automaticamente** acesso a todos os módulos.

O **perfil** não preenche, por si só, a lista de módulos de nenhum utilizador. Os módulos são **escolhidos individualmente**
pelo Admin ao criar/editar o utilizador.

## 4. Behaviour when a module is NOT assigned

Se um módulo não estiver atribuído ao utilizador:

- **não aparece** na sua navegação normal;
- **não** pode ser utilizado funcionalmente;
- o utilizador **não deve conseguir aceder-lhe diretamente** (sem atalho/rota que contorne a navegação).

A não-atribuição é, portanto, uma barreira de **acesso funcional**, não apenas uma ocultação de tab/UI.

## 5. Worked example — two Operadores with different modules

**Operador A:**
- Job On
- Controlo
- Armazém

**Operador B:**
- Job On
- Boquilhas
- Tampões

Apesar de **ambos** terem o perfil **Operador / Controlador**, cada um **vê e utiliza apenas os módulos que lhe foram
atribuídos**. Operador A não vê/acessa Boquilhas nem Tampões; Operador B não vê/acessa Controlo nem Armazém.

## 6. Profile decides the variant *inside* an assigned module

Dentro de um módulo **atribuído**, o **perfil** determina a **variante funcional** apresentada.

Exemplo no **CONTROLO**:

- **Operador / Controlador** → medição, registo, OK/NOK técnico e submissão;
- **Responsável** → revisão, aprovação, rejeição e decisão.

A **atribuição do módulo** determina se o utilizador entra no CONTROLO.
O **perfil** determina o que o utilizador pode **fazer** dentro do CONTROLO.

## 7. Summary of the owner-confirmed functional model

| Regra | Afirmação |
|---|---|
| Quem define os módulos | Admin, ao criar/editar cada utilizador |
| Âmbito | individual por utilizador (não implícito pelo perfil) |
| Perfil | determina como o utilizador atua dentro de um módulo atribuído |
| Módulos atribuídos | determinam quais áreas o utilizador pode entrar/usar |
| Operador / Controlador | NÃO recebe automaticamente todos os módulos operacionais |
| Responsável | NÃO recebe automaticamente todos os módulos |
| Módulo não atribuído | não aparece na navegação; não usável; não acessível diretamente |
| Dentro de módulo atribuído | perfil define a variante / experiência |

---

## 8. Relationship to the transversal module model

This clarifies the **module assignment** concept of the GLOBAL MODULE / USER ROLE MODEL:

- **MODULE ACCESS** = which modules the user is allowed to enter (assigned modules).
- **ROLE / PROFILE** = which experience/actions the user gets inside an assigned module.
- These remain two separate concepts (kept distinct from **active production context**, e.g. which Job On is loaded).

A regra funcional de atribuição acima ("o Admin atribui módulos individualmente; o perfil seleciona a variante") é o
modelo funcional resolvido. Os templates de acesso geridos no Admin são a forma funcional através da qual os módulos
são associados ao utilizador; os mecanismos técnicos por baixo dessa atribuição são matéria de mapeamento técnico,
não deste modelo funcional.

---

## Implementation Pointers

### FUNCTIONAL MODEL (owner-confirmed — do not change)

- Modules are assigned **individually per user** by the Admin; profile ≠ assigned modules.
- A not-assigned module → not in navigation, not functional, not reachable by direct route/shortcut (functional access barrier, not UI hiding).
- Access templates managed in Admin are the functional form through which modules are associated with the user.

### CURRENT IMPLEMENTATION (from implementation evidence in the Admin / Users / Access documentation)

- `internal_users.template_id` stores a **single template per user**; `access_templates.modules` (jsonb) holds `[{moduleId, capabilities[]}]`.
- Optional per-user `modules_override` (migration N26): when present it participates in resolution with **current replacement semantics**.
- Profile is currently represented as free-text/display-oriented (a profile title that never grants permissions) rather than a first-class enforced three-profile model.
- Effective access / navigation / direct-URL denial is resolved **server-side** from capabilities and effective access (AccessResolver, NavigationService).

### Known implementation gaps

- Functional model: **one or more associated templates per user**. Current implementation: **single `template_id` per user**. The association model must converge to one-or-more **without changing the functional rule**.
- `modules_override` semantics and the free-text profile representation are technical reconciliation items; effective access / navigation / direct-URL enforcement must converge to the final functional model.

### Design reference

- Admin visual authority: `AI-CONTEXT\design-coder\13_ADMIN_01_VISUAL_AUTHORITY_admin.html`.
- Login/authentication surface: `AI-CONTEXT\design-coder\12_LOGIN_01_VISUAL_AUTHORITY_login.html`.

### Cross-module dependencies

- Admin (template/assignment management); every operational module (visibility and functional access follow template associations).