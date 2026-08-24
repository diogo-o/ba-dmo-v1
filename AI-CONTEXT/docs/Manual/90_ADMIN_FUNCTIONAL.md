# ADMIN — MODELO FUNCIONAL

---

## Índice

1. [Purpose of Admin](#1-purpose-of-admin)
2. [Is Admin a top-level assignable module?](#2-is-admin-a-top-level-assignable-module)
3. [Who can access Admin](#3-who-can-access-admin)
4. [Admin user / profile behaviour](#4-admin-user--profile-behaviour)
5. [Internal areas / tabs of Admin](#5-internal-areas--tabs-of-admin)
6. [User creation](#6-user-creation)
7. [User editing](#7-user-editing)
8. [User activation / deactivation](#8-user-activation--deactivation)
9. [Profile assignment](#9-profile-assignment)
10. [Module access via associated templates](#10-module-access-via-associated-templates)
11. [Difference between profile and templates](#11-difference-between-profile-and-templates)
12. [Navigation / access effect of template associations](#12-navigation--access-effect-of-template-associations)
13. [What happens when a module is NOT granted](#13-what-happens-when-a-module-is-not-granted)
14. [Can Admin change / reset passwords?](#14-can-admin-change--reset-passwords)
15. [Can Admin change user email / name / profile?](#15-can-admin-change-user-email--name--profile)
16. [Can Admin change which modules a user can access?](#16-can-admin-change-which-modules-a-user-can-access)
17. [Does Admin manage access templates?](#17-does-admin-manage-access-templates)
18. [Are templates current, historical, or superseded?](#18-are-templates-current-historical-or-superseded)
19. [Does Admin manage application / module-catalogue settings?](#19-does-admin-manage-application--module-catalogue-settings)
20. [Audit / history available to Admin](#20-audit--history-available-to-admin)
21. [Admin-specific history / export behaviour](#21-admin-specific-history--export-behaviour)
22. [User-facing labels / profile labels](#22-user-facing-labels--profile-labels)
23. [Creation of the first Admin / bootstrap behaviour](#23-creation-of-the-first-admin--bootstrap-behaviour)
24. [Relationship with authentication (AUTHENTICATION vs ADMIN)](#24-relationship-with-authentication-authentication-vs-admin)
25. [Relationship with Users / Access](#25-relationship-with-users--access)
26. [Relationship with every operational module](#26-relationship-with-every-operational-module)
27. [What Admin OWNS](#27-what-admin-owns)
28. [What Admin does NOT own](#28-what-admin-does-not-own)
29. [Read-only vs editable surfaces](#29-read-only-vs-editable-surfaces)
30. [Important negative rules (Admin-relevant)](#30-important-negative-rules-admin-relevant)
31. [Current vs historical / superseded behaviour](#31-current-vs-historical--superseded-behaviour)
32. [Genuine unresolved Owner questions](#32-genuine-unresolved-owner-questions)
33. [GLOBAL ACCESS MODEL — VERIFICATION](#33-global-access-model--verification)
34. [FINAL OUTPUT](#34-final-output)

---

## 1. Purpose of Admin

ADMIN is the **administration surface of the portal**. Its functional job is to govern **who can use the application and how they behave inside it** by managing:

- **Users** (internal users of the system);
- **Access templates** (a **current, functional, Admin-managed** concept that packages which modules a user can access — see §17/§18);
- **Applications** (the module catalogue / availability / order);
- **Audit** (the global factual history of actions).

FUNCTIONAL RULE: Admin is for **administration of users / profiles / access templates / applications / audit**. It is **not** by itself an operational presence inside the production modules.

---

## 2. Is Admin a top-level assignable module?

**YES.** Admin is a **top-level functional module that can be assigned to a user**. It appears in the list of current top-level assignable modules:

> Job On, Controlo, **Admin**, Ferramentas, Armazém, Boquilhas, Reparação Interna, Reparação Externa, Tampões.

(**História is NOT a top-level module** — see Owner Decision 5 and §18/§21/§26. História is an internal *history tab / area* inside the relevant modules/areas, not an assignable access unit.)

TECHNICAL IMPLEMENTATION EVIDENCE: In the canonical catalogue it is module id `admin`, display "Administração", canonical order **99** (last), initial route `/admin`, capabilities `admin.gerir`, `audit.view`, `audit.export` (`CanonicalModuleCatalog`).

Note on classification: Admin is simultaneously "a top-level assignable module" **and** "a transversal system area". The authority labels it "módulo de topo / área transversal de sistema" — it is a portal-administration surface rather than an operational production module, but it remains assignable (a user may be granted it) and it is one of the current top-level modules.

---

## 3. Who can access Admin

Only users whose **assigned access grants the `admin.gerir` capability** can reach the Admin workspace. On the functional matrix, **Admin is ONLY ADMIN**:

| Module | Admin | Operador / Controlador | Responsável |
|---|---|---|---|
| **Admin** | **YES** | NO | NO |

- A pure Administrator enters directly into `/admin` and stays in the administrative shell.
- Operador / Controlador and Responsável do **not** access Admin (their profile is NO on the Admin row).
- The **Audit** sub-area additionally requires **`audit.view`** (view) and, for export, **`audit.export`**.
- Every Admin service and page re-authorises server-side and **fails closed** (no resolved identity or missing capability ⇒ forbidden). Hiding a button is **not** authorisation.

FUNCTIONAL RULE: any user can authenticate (that is LOGIN's concern), but reaching the Admin module requires the administrative capability `admin.gerir`.

---

## 4. Admin user / profile behaviour

Admin is **one of exactly three functional profiles**:

1. **Admin**
2. **Operador / Controlador**
3. **Responsável**

There is **no fourth profile**, **no read-only profile**, **no management/metrology/consultation profile**. These are the only functional profiles; a technical capability/template does not create another.

FUNCTIONAL RULE — Admin profile specifics:
- The Admin profile is the **administrative** profile; its function is the administration of the portal (users, profiles, templates, applications, audit).
- An Admin is **not implicitly an operational user**. The `admin` profile does **not** grant access to operational modules.
- A pure Admin **should not be automatically converted** into Operador / Controlador or Responsável.
- **A profile never automatically grants modules** — even for Admin, module access depends on the **access templates associated to that user** (Admin gets, and only uses, the Admin module by default). Profile and template association remain separate (see §11).

---

## 5. Internal areas / tabs of Admin

The Admin module has a single landing page plus **four internal sub-areas** (these are areas of the Admin module, **not** separate top-level modules):

| Area | Route | Purpose |
|---|---|---|
| **Landing** | `/admin` | Admin workspace entry (Index; no body model) |
| **Users** | `/admin/users`, `/admin/users/create`, `/admin/users/edit` | user listing, creation, editing, activation, password reset |
| **Templates** | `/admin/templates`, `/admin/templates/edit` | access-template list/create/edit |
| **Applications** | `/admin/applications` | module catalogue (availability + order) |
| **Audit** | `/admin/audit` | global action history query + annual export |

FUNCTIONAL RULE: Users, Templates, Applications and Audit are internal areas **inside the single Admin module** — not separately assignable modules. **Templates is a real, current, functional Admin area** (visible and manageable) — it is **not** optional, not presentation-only, and **not** removable from Admin.

---

## 6. User creation

FUNCTIONAL RULE — the flow is:

> ADMIN → creates/edits the user's/operator's record → associates an email (which creates/associates the user identity) → selects the user's **functional profile** → associates **one or more access templates** to that user → the associated templates determine which modules are visible/accessible to that user.

When the Admin creates a user/operator record they provide:
- **Name / user data** (display name);
- **Email** — associates/creates the **user identity / account**;
- **Functional profile** (one of the three, and a free-text profile title);
- **Active** state (default active);
- **Associated access templates** (one or more).

Behaviour:
- The user identity/account is created/associated via the email; on a **partial failure** the operation is reconciled **idempotently** (no orphan/duplicate mapping).
- A duplicate/conflicting identity is reported (user already registered).
- The **associated templates** determine the modules available to that user.
- **MUST NOT**: show the auth UUID as an "Email" label; expose current passwords.
- Audit event: `admin.user.created` (append-only).

> Note (technical): the Users list currently has a known hardening defect where the auth UUID is shown under an "Email" column (X12) — this is a defect to fix, **not** the intended behaviour (see §30/§31).

---

## 7. User editing

FUNCTIONAL RULE — the Admin can edit a user's:
- **Name** (display name);
- **Email**;
- **Functional profile** (and free-text profile title);
- **Active** state;
- **Associated access templates** — the Admin can **add templates to** a user, **remove templates from** a user, and **change which templates are associated**.

Removing a template association from one user does **not** delete the template globally and does **not** affect other users; the template itself stays reusable in Admin. The **associated templates** determine the modules available to that user (see §16–§18).

Edits are **concurrency-guarded** (the UI expects a matching "last updated" snapshot; a concurrent change is reported as a conflict) so two admins editing the same user cannot silently overwrite each other.
Audit events: `admin.user.updated`, `admin.access_template.updated`, `admin.option.updated`.

---

## 8. User activation / deactivation

FUNCTIONAL RULE — YES, Admin can **activate or deactivate** a user account.

- `active` is a per-user flag. An **inactive** user cannot resolve an identity and is denied access (inactive internal user ⇒ no access).
- Deactivation is recorded (`admin.user.deactivated`) and is append-only audited.
- **Self-lockout guard:** an Admin **cannot** deactivate (or strip admin from) the **last active administrator** — the system counts remaining active admins and rolls back the change if it would leave zero. This prevents locking everyone out of administration.

FUNCTIONAL RULE — separate concepts (do not merge):
- **Account state** (`active`) controls whether the account can authenticate/be used.
- **Profile** controls behaviour inside the modules the user can access.
- **Associated access templates** determine which modules are visible/accessible to that user.
These are three distinct things.

---

## 9. Profile assignment

FUNCTIONAL RULE — when creating/editing a user the Admin assigns the user's **profile** (one of the three: Admin / Operador / Controlador / Responsável) and a **profile title / role** (free text).

- The **profile** determines **HOW the user behaves inside the modules they can access** (the functional variant/experience). The profile is **separate** from the access templates: **assigning or removing templates never changes the user's functional profile**, and the profile never grants modules by itself (see §11). Example: inside Controlo, Operador / Controlador measures/records/submits; Responsável reviews/approves/decides.
- The **profile title** (free text, e.g. "Metrologia", "Chefe", "Engenheiro", "Responsável de qualidade") is a **visual label only** and **never grants permissions**.

---

## 10. Module access via associated templates

FUNCTIONAL RULE (owner-confirmed) — **access is configured per user by associating the appropriate access templates to that user.** Those **template associations determine the modules available to that user**:

- At create/edit time the Admin associates **one or more access templates** to the user (and can later **add or remove** associations).
- The **effective associated templates** determine which modules are visible/accessible.
- The profile does **not** fill in the module list; an Operador / Controlador or Responsável does **not** automatically receive all operational modules.
- This is **NOT** "modules chosen as loose independent checkboxes with templates optional" — the Owner-facing model is **template association per user**.

Example (owner-confirmed): two Operador / Controlador users may end up with different modules — Operador A: Job On, Controlo, Armazém; Operador B: Job On, Boquilhas, Tampões. Each sees/uses **only** the modules granted through their associated access configuration.

---

## 11. Difference between profile and templates

These are **two distinct functional concepts** (owner-confirmed):

| Concept | Answers | Determines |
|---|---|---|
| **Profile** (Admin / Operador / Controlador / Responsável) | *how* the user acts | the functional variant/experience **inside the modules the user can access** |
| **Templates** (associated to the user) | *which* areas are available | **which** modules/areas are visible and accessible to that user |

**A profile never grants modules; associating/removing templates never changes the functional profile.** Do not collapse profile and templates into one concept.

---

## 12. Navigation / access effect of template associations

FUNCTIONAL RULE — the user's **main navigation reflects the modules granted through the user's associated access templates**:
- a **granted** module appears in navigation and is functionally accessible (per profile);
- a **not-granted** module does **not** appear in normal navigation and is **not** functionally accessible.

Inside a granted module, **internal tabs/areas can vary by profile** (two users with the same module granted may see different tabs because their profile gives a different variant). Navigation therefore reflects **access** (modules granted via the associated templates), while internal tabs reflect **profile** within those modules.

TECHNICAL IMPLEMENTATION EVIDENCE: navigation is **derived server-side** from capabilities / effective access (`NavigationService`, `AccessResolver`); the canonical module order renders only authorized modules; the Admin entry requires `admin.gerir` and, when present, is shown/right-aligned.

---

## 13. What happens when a module is NOT granted

FUNCTIONAL RULE — if a module is **not granted through the user's associated access configuration** (the associated templates):
- it does **not** appear in the user's normal navigation;
- it **cannot** be used functionally;
- the user **must not** reach it directly (no shortcut/route that bypasses navigation).

**Non-grant is a functional access barrier — not merely hiding a tab/UI.**

TECHNICAL IMPLEMENTATION EVIDENCE: module/capability authorization handlers check the user's effective grants; not-granted pages are not accessible even by deep link; inactive templates resolve to empty effective access.

---

## 14. Can Admin change / reset passwords?

FUNCTIONAL RULE — Admin can **initiate a password reset** for a user, but:
- the reset **requires explicit confirmation**;
- it **never shows, retrieves, or reveals the current password**;
- it uses the **secure authentication-provider reset flow** (a recovery/reset link is requested from the auth service);
- the operation is **audited** (actor, affected user, timestamp, result) — `admin.password_reset.requested`.

Admin does **not** set/ep display a current password; there is no surface that returns an existing password.

TECHNICAL IMPLEMENTATION EVIDENCE: `AdminUserService.RequestPasswordResetAsync` → `IAdminProvisioningAdapter.RequestPasswordResetAsync` → Supabase privileged `generate_link` (recovery). The password is never persisted/rendered in the UI.

---

## 15. Can Admin change user email / name / profile?

FUNCTIONAL RULE — **YES**:
- **Name** — editable (`display_name`).
- **Email** — editable (via the identity-account provisioning; the email is not stored as the display label, and the auth UUID must not be shown as an "Email" label).
- **Profile / profile title** — editable (functional profile selection + free-text profile title; the title is visual only and never grants permission; changing templates does not change the profile).
- **Active** — editable (see §8).

---

## 16. Can Admin change which modules a user can access?

FUNCTIONAL RULE — **YES**, by managing the user's **template associations**:
- the Admin can **associate (add) templates to** a user;
- the Admin can **remove previously associated templates from** a user;
- the Admin can **change which templates are associated** (swap the set of associated templates).

The **effective associated templates** determine which modules the user can access. Removing a template association from one user does **not** delete the template globally and does not affect other users; the template remains reusable.

This is guarded by grant validation (canonical module/capability rules), by the Job On guard (a non-admin template automatically receives `jobon.view`; an admin template does not), and by the **self-lockout guard** (the user making the change must not remove the last active admin from themselves, if applicable).

---

## 17. Does Admin manage access templates?

**YES — Templates are a CURRENT FUNCTIONAL part of Admin** (Owner Decision 1). They are **not** hidden technical implementation and **must remain** in the Admin functional model.

CURRENT FUNCTIONAL RULE:
- Templates are an **Admin-managed functional concept**, visible and manageable in Admin (a real functional Admin area).
- Admin can **create / edit templates**.
- Admin can **associate templates to users**.
- Admin can **remove template associations from users**.
- Templates **determine module availability** for the users they are associated with.
- Templates are **reusable** — removing an association from one user does **not** change the template nor other users.
- The template association is **per user**.

Template management is validated canonically (grants are normalized/validated against the module catalogue; unknown modules, duplicate entries, and capabilities that don't belong to the granted module are discarded/rejected and reported).

---

## 18. Are templates current, historical, or superseded?

**CURRENT FUNCTIONAL RULE** (Owner Decisions 1–4): **Templates are current and functional.** They are an Admin-managed concept, visible and manageable in Admin; user access is configured by **template association per user**; the associated templates determine effective module visibility/access. Templates are **not** merely hidden technical implementation and are **not** removable from Admin.

- Functional model: **USER → PROFILE + ASSOCIATED TEMPLATES → EFFECTIVE MODULE ACCESS**. The profile determines *how* the user behaves; the associated templates determine *which* modules are visible/accessible.
- The **profile never grants modules**; template association does **not** change the profile.
- The functional contract is **template association per user** — **not** "loose direct module assignment independent of templates".

**Historical / superseded:**
- Treating **templates as merely hidden technical implementation** — superseded.
- Treating the **Templates tab as removable/optional/presentation-only** — superseded (Templates stays a visible, manageable Admin area).
- Treating **module access as loose direct module assignment independent of templates** — superseded; the Owner-facing model is template association per user.
- Old claims that there is **no per-user override** in the implementation — superseded by the live override (a technical detail; the functional model above is unchanged).
- Visual/design claims inside `docs/IMPLEMENTATION_STATE.md` carry the **"DESIGN IMPLEMENTATION RESET — 2026-08-22"** banner and are **historical for visual purposes**; behavioural rules are current in `FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`.

---

## 19. Does Admin manage application / module-catalogue settings?

FUNCTIONAL RULE — **YES**, in the **Applications** area the Admin manages the **module catalogue** (`applications` = application/module list):
- **list the available modules**;
- **change availability** (active) and **order** (display order);
- **associate modules/capabilities to existing access templates**;
- **deactivate instead of delete** where historical records exist.

**Critical boundary (never confuse):**
- **APPLICATIONS** = the catalogue / availability / ordering / configuration of modules. The catalogue/mirror (`module_catalog_mirror`) is **Admin-UI display/ordering only — it NEVER grants access**. Authorisation is always resolved server-side from the in-code catalogue ∩ access grants. Changing availability/order in this area does **not**, by itself, grant or revoke functional access.
- **TEMPLATES ASSOCIATED TO A USER** = determine **which modules the user can access**.
These two concepts must not be merged (Owner Decision "APPLICATIONS AREA").

---

## 20. Audit / history available to Admin

FUNCTIONAL RULE — the **Audit** area reads the **global, single, append-only** action history (`audit_events`). Every authenticated user's relevant business actions become events (user, module, action, entity, UTC timestamp, result). Admin can query this history **by year** and **filter by user, module, action, result, and date range**, with **pagination (20/40/60 rows)**, single-click to select, double-click to open detail. The detail shows only factual data — **no scoring, ranking, productivity, or automatic assessment** is computed or displayed.

Key audit guarantees:
- **append-only**: corrections are new rows; the original fact is never rewritten/deleted (DB trigger blocks UPDATE/DELETE);
- no secrets: events never include passwords, tokens, cookies, credentials, full emails, PDFs, images, or arbitrary payloads;
- backend is the authoritative source; the event is created server-side (ideally same transaction as the action).

Admin-capability split: **`audit.view`** gates viewing; **`audit.export`** gates export (§21).

---

## 21. Admin-specific history / export behaviour

- **Export** is the Admin-specific history behaviour: the Audit area supports **annual authorised export** to **CSV** (`text/csv`, filename `auditoria-{Year}.csv`), gated by **`audit.export`**.
- Export queries the **full** record for the selected filters (unlimited rows) and returns the fact columns (`occurred_at_utc;year;actor_user_id;actor_name;module_id;action_code;entity_type;entity_id;entity_label;result;reason`).
- Admin-specific action codes that Admin itself writes/reads: `admin.user.created`, `admin.user.updated`, `admin.user.deactivated`, `admin.password_reset.requested`, `admin.option.updated`, `admin.access_template.updated`, and the bootstrap event `bootstrap_admin`.
- **História is not a standalone module** (Owner Decision 5). The global audit remains an **Admin/Audit concept**. Relevant modules may expose their **own Histórico tab/area**; do not treat "História" as an independent module that consumes the audit feed.

---

## 22. User-facing labels / profile labels

- **Profiles (functional labels):** **Admin**, **Operador / Controlador**, **Responsável** (exactly three).
- **Profile title / role** is a **free-text label** managed by Admin and shown beside the user's name in the header; examples given in authority: *Metrologia, Chefe, Engenheiro, Responsável de qualidade*. If empty, only the name is shown.
- **MUST NOT / functional rule:** the free-text title is **visual only** and **never grants permission** and **never substitutes** the template, profile, or capabilities. **Never infer authorisation from the header title.**
- Module display labels: Admin area is labelled **"Administração"**; each operational module has its own display label (Job On, Controlo, Ferramentas, Armazém, Boquilhas, Reparação Interna, Reparação Externa, Tampões). **História is not a module** — relevant modules may expose their own **Histórico tab/area** (internal history), which is not an assignable access unit.
- Login: **no manual profile choice** at login (the server decides the landing; operational user → Job On, pure Admin → Admin).

---

## 23. Creation of the first Admin / bootstrap behaviour

FUNCTIONAL RULE — **there is no anonymous/default admin; the first administrator is created only through an explicit bootstrap path** (a CLI command `bootstrap-admin`). The `BootstrapAdminService`:
- creates a **minimal admin access template** (`tpl-bootstrap-admin`) and the **active admin internal user** linked to it, plus a **bootstrap audit event** (`bootstrap_admin`);
- is **idempotent** (if a valid admin already exists, it does nothing / avoids duplicate);
- requires **explicit configuration** (missing config fails validation before any write);
- reconciles partial failures so nothing incomplete is persisted.

FUNCTIONAL vs TECHNICAL: the rule "no default admin; bootstrap only via explicit provisioned configuration" is a **functional/security rule**. The specific Supabase auth-provider mechanics (service-role API calls, `generate_link`) are **technical implementation evidence**, not business rules.

---

## 24. Relationship with authentication (AUTHENTICATION vs ADMIN)

These two must be separated clearly:

- **AUTHENTICATION** = login / session / identity verification. In BA DMO this is the **Login** surface: email + password sign-in via the auth provider, session cookie (`BaDmo.Session`, 8h expiry, HttpOnly/SameSite=Lax), logout, `/no-access`, `/access-denied`. It is a **transversal system area**, not a module, and not Admin.
- **ADMIN** = management of internal users / profiles / access templates / applications / audit. Admin is a module whose UI operates over the shared Users / Access machinery.

Admin **uses** the privileged provisioning part of authentication (creating identity accounts, requesting password resets), but Admin is **not** authentication, and authentication is **not** Admin. Do not infer that because someone can log in they can use Admin — reaching Admin requires the `admin.gerir` capability (see §3).

---

## 25. Relationship with Users / Access

- **Users / Access is a transversal, system-level domain — NOT an assignable operational module.** There is no Users / Access page or module; the Admin pages (`Pages\Admin\Users`) are the **Admin module's** surface.
- The shared Users / Access machinery — `internal_users`, `access_templates`, the module/page/capability catalogues, `AccessResolver`, `CurrentUser`, `NavigationService`, grant normalizer — is the **implementation** that realizes the functional model Admin operates over.
- **Admin owns the administration UI/services/gates** (`AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, `AdminAuthorizationGate`, `IAdminRepository`) that manage all of the above.
- **Functional boundary:** the *template-association rule* ("Admin configures access per user by associating the appropriate access templates; the associated templates determine the modules available to that user; profile selects the variant") is owner-confirmed and is the functional contract; capabilities/overrides are the technical realization.

---

## 26. Relationship with every operational module

Admin's relationship to the operational modules is **governance / access-control**, not operation:

- **Job On, Controlo (Peso/Pegamentos/Resumo/Histórico), Ferramentas, Armazém, Boquilhas, Reparação Interna, Reparação Externa, Tampões** — Admin configures, through the users' **associated access templates**, **which of these modules each user can access** and (via profile) influences the experience inside them. Admin is the surface where template associations are managed.
- **Admin is NOT operational inside them.** On the functional matrix, the Admin profile is **NO** for every operational module; the pure Administrator does **not** receive `jobon.view` nor operational modules and is denied Job On.
- **História is not a module** (Owner Decision 5) and is therefore not an Admin-assigned module. Relevant modules may expose their **own Histórico tab/area**; **global audit** is an Admin/Audit concept (Admin queries it with `audit.view` / exports with `audit.export`).

---

## 27. What Admin OWNS

- **User administration** for internal users (create, edit name/email/title, activate/deactivate, password reset initiation, concurrency-guarded).
- **Access-template management** (create/edit; templates are a current functional Admin concept).
- **Per-user template association** (associating templates to users, removing associations, changing which templates are associated; the associated templates determine the user's module access).
- **Application / module catalogue** (display name, availability, order — **display only, never grants access**).
- **Audit query + annual export** surface over the global `audit_events` (view with `audit.view`, export with `audit.export`).
- **Administrative access governance** — it is the ONLY surface that can administer users/access/configuration.
- **Bootstrap gating** — the only creation path for the first administrator (no default admin).

---

## 28. What Admin does NOT own

- **Operational production data** of every operational module (Job On plans/revisions, Controlo records, Peso/Pegamentos measurements, Ferramentas master records, Armazém stock/movements, Boquilhas movements, Reparação Interna/Externa records, Tampões balances/config).
- **Authentication / identity verification** (that is Login + the auth provider; Admin only consumes privileged provisioning for account create/reset).
- **The global audit events themselves** as a domain owner — Admin *manages/queries* `audit_events` (and exports), but all modules write to the same single append-only table; Admin does not "own" other modules' records, and the append-only table is a global shared fact source.
- **The shared Users / Access catalogue logic** as a functional entity — Users / Access is a transversal system domain that Admin's UI operates over; it is not a module Admin "owns".
- **The category of operational access** — `admin.gerir` qualifies pure admin but is **not** operational access and never implies operational modules.
- The catalogue mirror grants **no access** on its own (Admin changes it without changing functional authorisation).

---

## 29. Read-only vs editable surfaces

| Area | Editable? |
|---|---|
| Users list | Read-only list with per-row actions; create/edit are editable forms |
| User create/edit | **Editable** (name, email, profile, active, associated templates) |
| Templates list | Read-only list with actions |
| Template edit | **Editable** (name, grants, active) — guarded (self-lockout, canonical grants) |
| Applications | **Editable** (availability + order) — display only, never grants access |
| Audit | **Read-only query + export**; no in-place data editing (append-only) |

Every editable Admin surface re-authorises server-side (`AdminAuthorizationGate`) and fails closed; a button being hidden is not authorisation.

---

## 30. Important negative rules (Admin-relevant)

1. **Fail closed** on every Admin gate — no identity or missing capability ⇒ forbidden.
2. **Profile title never grants permission**; never infer authorisation from the header title.
3. **Never show/reveal a current password**; password reset requires explicit confirmation, uses the secure auth flow, and never reveals a password.
4. **Auth UUID must not be labelled "Email"**.
5. **Module not granted through the user's associated templates** ⇒ not in navigation + not functionally accessible + not directly reachable (access barrier, not UI hiding).
6. **Pure Admin does not receive Job On / operational modules**; `admin.gerir` is not operational access.
7. **Profile never auto-grants modules** (all three profiles).
8. **Exactly three profiles** — no fourth, no read-only, no management/metrology/consultation profile.
9. **Append-only audit** — no UPDATE/DELETE of `audit_events`; corrections are new rows.
10. **Self-lockout guard** — the last active admin cannot be deactivated/stripped (would lock everyone out).
11. **No anonymous/default admin** — bootstrap only via explicit CLI/config.
12. **Templates are a current functional Admin concept** — they are visible/manageable and must not be removed; capabilities/overrides are technical realization and are not the Owner-facing mechanism.
13. **Login has no manual role choice** and does not confirm whether a specific email exists.
14. **Catalogue mirror never grants access** — display/order/availability changes don't change authorisation.
15. **Do not treat buttons/hiding as authorisation** — server validates on command/service.
16. **Profiles and templates are separate** — associating/removing templates never changes the functional profile; profile never grants modules.
17. **Removing a template association from a user does not delete the template** — templates are reusable and other users are unaffected.

---

## 31. Current vs historical / superseded behaviour

**Current (authoritative) — verified:**
- Admin is the **top-level administrative module**.
- Internal areas: **Landing / Users / Templates / Applications / Audit**.
- **Templates are functional and visible/manageable** in Admin — not hidden technical implementation, not removable.
- **User access is configured by template associations per user**; the associated templates determine effective module visibility/access.
- **Profile is separate from templates**: profile = how the user behaves (inside the modules they can access); templates = which modules the user can access.
- **Admin is not operational in production modules** (pure Admin isolation: lands on Admin, no operational modules, no `jobon.view`).
- **História is NOT a top-level module** — it is a history tab / internal history area inside the relevant modules/areas, not an assignable access unit.
- Audit is append-only, factual, no scoring/ranking.

**Superseded (Owner Decisions — do not treat as current):**
- Treating **templates as merely hidden technical implementation** — superseded.
- Treating the **Templates tab as removable/optional/presentation-only** — superseded (Templates remains a visible, manageable Admin area).
- Treating **module access as loose direct module assignment independent of templates** — superseded; the Owner-facing model is template association per user.
- Treating **História as a top-level / assignable module** — superseded; História is not a module.
- Old claims that there is **no per-user override** in the implementation — superseded by the live override (technical detail; the functional model above is unchanged).
- Visual/design claims inside `docs/IMPLEMENTATION_STATE.md` carry the **"DESIGN IMPLEMENTATION RESET — 2026-08-22"** banner and are **historical for visual purposes**; behavioural rules are current in `FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`.

**Current known defect (not intended behaviour):** the Admin Users list currently shows the auth UUID under an "Email" column (hardening item **X12**). Intended behaviour is never to show the auth UUID as an Email label — this is a defect to fix, flagged in the functional source of truth §15, not a design decision.

---

## 32. Genuine unresolved Owner questions

There are **no genuine unresolved Owner questions** for Admin functionality.

RESULT:
- No current authoritative source leaves an Admin functional question unresolved; the global module/profile model is closed and owner-confirmed, and the Owner Decisions above (templates functional and visible; profile vs templates separation; template association per user; História not a top-level module) are now explicit Owner rules.
- The items that remain are **classified by the Owner as resolved**, not open questions:
  - Templates are a current functional Admin area (visible, manageable, not removable) — resolved by Owner Decision 1.
  - Profile vs templates separation — resolved by Owner Decision 2.
  - Template association per user as the Owner-facing model — resolved by Owner Decisions 3–4.
  - História is not a top-level module — resolved by Owner Decision 5.
  - X12 (auth UUID shown as Email) → a known hardening defect to fix, not a decision to make.
- Therefore **OPEN_OWNER_QUESTIONS = 0**. No Owner question is asked, per the rule that we only ask when the answer genuinely does not exist, two current authorities conflict, or a source marks it unresolved — none of which applies here.

---

## 33. GLOBAL ACCESS MODEL — VERIFICATION

The review explicitly verifies the current global model against authority and the Owner Decisions:

```
USER
  → has a PROFILE
  → has ASSOCIATED TEMPLATES
  → EFFECTIVE MODULE ACCESS

PROFILE
  = determines HOW the user behaves INSIDE the modules they can access.

ASSOCIATED TEMPLATES
  = determine WHICH modules are visible/accessible to that user.
```

- **Profile does NOT automatically grant modules** — VERIFIED (owner-confirmed; `03_USERS_ACCESS_OPERATIONAL.md` §3, `01_GLOBAL_MODULE_USER_ROLE.md` §3; Owner Decision 2).
- **Template association determines module access — NOT merely UI customization** — VERIFIED (a module not granted through the user's associated access configuration ⇒ no navigation entry + no functional access + no direct deep-link; `03_USERS_ACCESS_OPERATIONAL.md` §4, `02_MODULES_OPERATIONAL.md` §2; Owner Decision 4).
- **If a module is not granted through the user's associated access configuration**: it must not appear in normal navigation and the user must not have functional access — VERIFIED.

**PROFILE MODEL** (against authority — no new profiles invented):
- Expected current functional profiles: **Admin, Responsável, Operador / Controlador**.
- VERIFIED: exactly three; no read-only, no management, no metrology, no fourth operational role (per `01_GLOBAL_MODULE_USER_ROLE.md` §2 and `02_MODULES_OPERATIONAL.md` §14.4–14.5). The term used in authority is **Operador / Controlador**.

**ADMIN BOUNDARY:**
- Admin is **administration of users/access/configuration**.
- VERIFIED: Admin is **not automatically operational** inside production modules (matrix NO for all operational modules; pure Admin denied Job On and does not receive operational modules). Admin **has its own landing page** (`/admin`) and stays in the administrative shell. Admin has **administration + audit access** (audit.view/audit.export), not operational execute permissions.

**USERS / ACCESS — access configuration:**
- Access is configured **per user by associating the appropriate access templates** — VERIFIED (owner-confirmed; Owner Decisions 1, 3, 4). The template associations determine the modules available to that user.
- Profile and template association are **separate** — VERIFIED (Owner Decision 2).
- Templates: **current functional concept** — visible/manageable in Admin; the per-user `modules_override` is TECHNICAL IMPLEMENTATION EVIDENCE only, not the Owner-facing mechanism (Owner Decisions 1, 3).
- Preferred landing/default module: **Job On for operational users; Admin for pure Admin** (no per-user configurable landing for Job On; `PreferredFirstPageId` exists technically but is not consulted in V1 — technical detail).
- Admin is itself an **assignable module** (and a transversal system area) accessed via the `admin.gerir` capability.
- **História is NOT a module** (Owner Decision 5): it is a history tab / internal history area inside the relevant modules/areas — not a top-level logical module, not assignable per user in Admin, not an independent access unit, not owned by Admin. Global audit remains an Admin/Audit concept.

**AUTHENTICATION vs ADMINISTRATION:**
- Separated: Authentication = login/session/identity (Login + auth provider); Administration = management of internal users, profiles, access templates, applications and audit (Admin). Supabase/bootstrap details are classified as TECHNICAL IMPLEMENTATION EVIDENCE, not business rules, except the explicit functional rules (no default admin; bootstrap only explicit; fail-closed; append-only; module-not-granted barrier; profile-title-no-permission).

---

## 34. FINAL OUTPUT

MODULE:
ADMIN

PURPOSE:
Administration of users, profiles, access templates, applications and audit. Admin is the surface that controls who can use the application and how (profile) inside the modules made available through the access templates associated to each user.

PRIMARY USER:
The **Admin** functional profile (a user granted the `admin.gerir` capability). Operador / Controlador and Responsável do not access Admin. Authorised users also need `audit.view` (Audit) and `audit.export` (export).

TOP_LEVEL_MODULE:
YES — `admin` is a canonical top-level, assignable module (display "Administração", route `/admin`); it is also classified as a transversal system area, but it is assignable and one of the current top-level modules.

INTERNAL_AREAS:
Landing · Users · Templates · Applications · Audit. These are internal areas of the single Admin module, not separate modules. Templates is a current, functional, visible/manageable Admin area — not optional.

USER_CREATION:
Admin creates/edits the user/operator record (name/user data, email, functional profile, active state) and associates one or more access templates; the email creates/associates the user identity/account; the associated templates determine the modules available to that user. The Admin can later edit the record, change profile, activate/deactivate, and add/remove/change template associations. Idempotent reconciliation on partial failure. Audit: `admin.user.created`.

PROFILE_ASSIGNMENT:
While creating/editing a user, Admin selects one of exactly three functional profiles (Admin / Operador / Controlador / Responsável) and a free-text profile title. The profile determines how the user behaves inside the modules they can access; the title is visual only and never grants permissions. Profile stays separate from templates.

MODULE_ASSIGNMENT:
Access is configured **per user by associating the appropriate access templates** to that user. Those template associations determine which modules are visible/accessible to that user. The Admin can associate templates, remove associations, and change which templates are associated; removing an association does not delete the template nor affect other users. Profile never auto-grants modules. (Per-user `modules_override` exists in the implementation but is TECHNICAL IMPLEMENTATION EVIDENCE, not the Owner-facing mechanism.)

PROFILE_AND_MODULE_ASSIGNMENT_SEPARATE:
YES — owner-confirmed and verified: **profile** = how the user behaves (experience inside the modules they can access); **templates** = which modules the user can access. The two concepts are kept distinct; associating/removing templates never changes the functional profile.

ADMIN_OPERATIONAL_IN_OTHER_MODULES:
NO. The Admin profile is not operational inside production modules (matrix NO for all operational modules); a pure Admin does not receive `jobon.view` or operational modules, lands on and stays in `/admin`, and has administrative + audit access only. Admin manages access/configuration; it does not perform operational work in the production modules.

OPEN_OWNER_QUESTIONS:
0 — the global module/profile model and the Admin / Users / Access model are owner-confirmed and closed; the Owner Decisions above (templates functional and visible; profile vs templates separation; template association per user; História not a top-level module) are now explicit Owner rules. The only remaining item is the X12 defect (auth UUID shown as Email) to fix, which is not an Owner question.

## Implementation Pointers

### Relevant implementation areas

- Web / Razor: Admin module id `admin` (canonical order 99, route `/admin`); Admin pages under `Pages\Admin\Users` and the Admin areas (Landing / Users / Templates / Applications / Audit); capabilities `admin.gerir`, `audit.view`, `audit.export`.
- Application services/gates: `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, `AdminAuthorizationGate`, `IAdminRepository`, `BootstrapAdminService` (CLI `bootstrap-admin`; template `tpl-bootstrap-admin`); shared Users / Access machinery: `internal_users`, `access_templates.modules` (jsonb), `module_catalog_mirror`, `AccessResolver`, `CurrentUser`, `NavigationService`, grant normalizer.
- Database: migrations N01 (identity; `audit_events` + append-only trigger), N02 (`module_catalog_mirror`), N26 (per-user `modules_override`); audit events `admin.user.created|updated|deactivated`, `admin.password_reset.requested`, `admin.option.updated`, `admin.access_template.updated`, `bootstrap_admin`; annual export CSV `auditoria-{Year}.csv` (text/csv).
- Auth: Supabase auth provider — sign-in/anon vs privileged provisioning (user create / password reset via recovery `generate_link`); session cookie `BaDmo.Session` (8h, HttpOnly/SameSite=Lax); the auth UUID must never be displayed as an Email label (X12).
- Technical map: `maps\15_ADMIN.md`, `maps\16_USERS_ACCESS.md` (verify freshness before use).

### Known implementation gaps

- X12 defect: the Admin Users list currently shows the auth UUID under an "Email" column — must be fixed (never show auth UUID as Email; functional source of truth §15).
- Template association: current `internal_users.template_id` stores a single template per user, while the functional model is one or more associated templates per user — convergence required (see `03_USERS_ACCESS_OPERATIONAL.md`).
- `PreferredFirstPageId` exists technically but is not consulted in V1 (technical detail — landing: Job On for operational users, Admin for pure Admin).
- Per-user `modules_override` (N26) participates in resolution with current replacement semantics; it is implementation evidence, not the Owner-facing mechanism — technical reconciliation item, functional model unchanged.

### Design reference

- `AI-CONTEXT\design-coder\13_ADMIN_01_VISUAL_AUTHORITY_admin.html`
- Login/authentication: `AI-CONTEXT\design-coder\12_LOGIN_01_VISUAL_AUTHORITY_login.html`

### Cross-module dependencies

- All operational modules (template associations determine module visibility/access); Login/auth provider (identity, bootstrap, password reset); História (transversal read of the global `audit_events` — Admin queries/exports; all modules write the same append-only table).
