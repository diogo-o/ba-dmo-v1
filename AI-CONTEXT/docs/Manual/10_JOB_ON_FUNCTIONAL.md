# JOB ON — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: 7 (+ 3 design clarification items)

---

## Índice

1. [Job On — Visão Geral](#s1)
2. [Conceitos Fundamentais](#s2)
3. [What Job On Owns](#s3)
4. [Tooling Selection](#s4)
5. [Responsável vs Operador](#s5)
6. [Production-Specific Tooling Data](#s6)
7. [Verificações](#s7)
8. [Job On ↔ Ferramentas](#s8)
9. [Job On ↔ Armazém](#s9)
10. [Job On ↔ Controlo](#s10)
11. [Job On ↔ Peso](#s11)
12. [Job On ↔ Reparação](#s12)
13. [Job On ↔ Boquilhas](#s13)
14. [Printing / Job On Documents](#s14)
15. [History / Revisions](#s15)
16. [Exemplos](#s16)
17. [Questões Funcionais Abertas](#s17)

---

## Scope / boundary note

Job On is the **production/planning context** of BA DMO. This clarification focuses on that role and on how Job On relates to the surrounding domains.

- Job On does **not** own Ferramentas master data, Armazém movements, or repair history. It consumes and displays information from those domains.
- Tooling in Job On planning concerns **CM**, **MF**, and **BQ**, plus other planned components. **BQ is a tool whose master belongs to Ferramentas**; the separate **Boquilhas** module only records the movements related to BQ external repair. Job On selects/uses **BQ + Lot** for the production but does not own the BQ master.
- **BQ is never a Reparação Interna tool.** Reparação Interna applies to CM and MF only. BQ repair handling belongs to the Boquilhas external-repair lifecycle.

<a id="s1"></a>
## 1. JOB ON — VISÃO GERAL

Job On is the **central production/planning context** of BA DMO. Operationally it is a **hub**, not merely a technical spreadsheet. The "sheet" is one representation of the Job On.

Job On:

- creates and holds the production planning;
- exposes planned productions through its calendar;
- identifies the exact production context (Reference, Production, Machine/Line);
- holds the exact production revision/snapshot;
- identifies the exact tools/lots planned for that production;
- provides this production context to the operational modules;
- receives/links the information those modules record about that production;
- contains the production verification/check items;
- preserves the history of the production context;
- drives the production documents/printing.

### Central-tracking rule

There must **NOT** be separate general production/tool-lot tracking systems re-created in every operational module. The central production/tooling context is Job On. Operational modules consume this exact context, record their own facts/results, and do not independently reconstruct the production.

### Conceptual topology

The following represents operational information flow, **not** database ownership:

```
                    JOB ON
              central production hub
            planning / calendar / context
                       |
       +---------------+---------------+
       |               |               |
       v               v               v
   CONTROLO     REPARAÇÃO INTERNA   other consumers
       |
       +-- PESO
       |
       +-- PEGAMENTOS
```

Supporting tooling information feeds Job On:

```
FERRAMENTAS / BOQUILHAS / ARMAZÉM
                  |
                  v
                JOB ON
```

### Calendar and planning

The Job On calendar is the central planner/locator for productions. From the calendar users can locate a production, open its Job On, see its production context, see the exact planned tooling, see relevant operational verification state, and navigate to information associated with that production.

Preserved structural behaviour:

- A click on a day selects it; a double-click opens the Job On sheet view.
- Past days show References with registered in/out movements; the present day shows that day's records; a future day offers "Criar Job On para este dia" (the selected day becomes the new Job On's date).
- Changing month does not auto-select a day.
- The calendar queries registered facts; it never infers entries/exits from the absence of a Job On.
- After a Job On is created/persisted it appears automatically; a date change updates the event via the same stable Job On identity (never a duplicate copy).
- The machine/line colour identifies the machine/line, never a semantic status.

Therefore Job On is both the **planning entry point** and the **production context hub**. The calendar is not a separate tracking system — it is the scheduling face of the Job On hub.

---

<a id="s2"></a>
## 2. CONCEITOS FUNDAMENTAIS

### 2.1 Production

A production is a planned fabrication event identified within Job On. It carries the context needed to run and to understand that production: the product being made, the production identifier, and the operating Machine/Line.

### 2.2 Product Reference

The **Product Reference** identifies the product internally (summary; the full model is documented in the Ferramentas clarification).

- The numeric part of the Product Reference is the internal product identification and is **MF-based** (the MF molds the final product shape).
- The **Marisa type** is a separate part identifying the neck/finish type made by the Boquilha.
- Example: **`5447T173`** = `5447` (MF-based numeric identification) + `T173` (Marisa type).

Do not conflate: CM internal number · MF internal number · Product Reference · Marisa type.

### 2.3 Production + Reference context

Data that is specific to one exact Production + Product Reference belongs to the Job On context. Job On holds the editable production-decision snapshot for that context: header context, planned tooling/components, typed fields, repeatable rows, verifications, and notes.

### 2.4 Job On revision / historical context

A Job On **revision** is a historical version/snapshot of the Job On for the same production at a point in time. It does not create or replace the Production; it preserves how that Production's Job On context was recorded at that moment.

- The **Production** remains the main production context/entity.
- A revision does NOT create or replace a Production.
- Multiple revisions may belong to the same Production.

Job On must preserve historical production/revision context: past production data must not be silently rewritten, and corrections must not reinterpret historical context (see §15).

CURRENT DESIGN: every save creates a new revision; older revisions remain exactly as saved; corrections always create new rows. This preserves the historical production/context snapshot.

### 2.5 Tooling context

Job On planning identifies the exact planned tool/lot for the main tooling. The tooling context may include: Type; Reference / internal identification; Lot; Machine/Line; technical state; % usage; location/availability context where useful; last repairer / repair context where useful. Machine/Line is useful registered context, not an automatic decision rule (see §4).

---

<a id="s3"></a>
## 3. WHAT JOB ON OWNS

Job On owns **production/reference-specific configuration and data**. This may include, where applicable:

- selected CM + Lot;
- selected MF + Lot;
- selected BQ + Lot;
- FF / Fundo Final;
- Calibres;
- Pinças;
- PU production-specific configuration (§6.1);
- CS production-specific configuration (§6.1);
- TP / Tampão production-specific configuration (§6.1);
- other production-specific fields and planned components;
- details/snapshots required for that production/revision.

> **Critical rule:** Data specific to the exact Production + Product Reference belongs to the Job On context. Do NOT automatically move production-sheet fields into Ferramentas master data.

### Job On owns

- the stable Job On identity and its revisions;
- the full editable production-decision snapshot (context, fields, rows, quantities, notes);
- chosen tool/lot selections plus readable revision snapshots of what was decided for the production;
- verification occurrences for the production (materialized state, confirming user, date/time);
- its own audit/history of the production context;
- the production document/print projection surface (produced from its snapshot).

### Job On does NOT own

- master tooling data (Ferramentas domain, including the BQ master; Boquilhas only registers BQ repair-related movements);
- warehouse physical state, presence, location, and movements (Armazém);
- Controlo control results (Controlo module);
- Peso / Pegamentos results (internal areas of the Controlo module);
- Reparação Interna repair records (Reparação Interna module);
- the article image itself (belongs to the master reference); Job On consumes the reference image, it is not per-revision-owned;
- any master modification — editing a Job On snapshot never edits a master sheet, state, life, position, or history of a tool.

---

<a id="s4"></a>
## 4. TOOLING SELECTION

> **The Responsável chooses the tooling.** Job On presents registered context; the application does not need to infer the correct tool.

Job On uses its production context to **filter** the tooling options presented to the Responsável. The relevant selection context includes:

- Production
- Product Reference
- Machine / Line

Job On uses its Product Reference and Machine/Line context to filter the registered tooling options presented to the Responsável. For example, for a Job On on line `B3`, the CM selector should present only the registered CM/tool-Lot options relevant to that Reference and registered for `B3`. The same principle applies to the other tooling selectors where applicable (MF, BQ). The registered data must support this filtering, without treating Reference + Lot + Machine/Line as a new composite tool identity.

Important distinction:

- **Machine/Line is registered tool/Lot information.**
- The registered tooling data must support filtering by the Job On's Product Reference and Machine/Line.
- This is a **selection/filtering rule**, not a new domain identity.
- `CM + Lot + Machine` is **NOT** a composite business identity.
- Job On does not need to infer tooling relationships — there is **no automatic CM↔MF inference**.
- The application does not automatically determine compatibility or choose the tooling.
- The **Responsável** still makes the final selection from the filtered options.

For CM/MF/BQ, the selected tool/lot is registered against the authoritative tooling registers. Job On stores the selected tool/lot plus a readable snapshot of the value decided for that production. Operational modules inherit this exact planned tooling context.

Tooling context may include:

- Type
- Reference / internal identification
- Lot
- Machine/Line
- technical state
- % usage
- location/availability context where useful
- last repairer / repair context where useful

---

<a id="s5"></a>
## 5. RESPONSÁVEL VS OPERADOR

- Only the **Responsável** edits Job On production configuration/tooling.
- The **Operador** reads/consults Job On.
- The **Operador** performs manual verification/check confirmation where applicable (see §7).

Do not invent additional edit permissions. If any permission is not confirmed, keep it open (see §17).

---

<a id="s6"></a>
## 6. PRODUCTION-SPECIFIC TOOLING DATA

The boundary between the two domains:

- **FERRAMENTAS** = stable registered/master tool information.
- **JOB ON** = how selected tooling/components are used in one exact production/reference.

> **Later edits in Ferramentas must NOT silently rewrite historical Job On production/revision data.** Job On may preserve the relevant production/revision snapshot/details.

FF belongs to the MF side of the production/tooling context. FF / Calibres / Pinças are Job On production/reference data unless explicit evidence proves otherwise.

### 6.1 Production-specific configuration — PU / CS / TP (owner clarification)

Owner-confirmed rule: **PU / CS / TP are production-specific configuration fields of the Job On.**

- **PU and CS** are pieces shown/configured in the production context.
- **TP/Tampão** here is the **production-specific tooling/configuration item** of Job On (see the terminology warning below — do NOT merge with the Peso Tampão/calote value).
- **Pinças, Calibres** and other equivalent production-specific fields are also maintained in Job On.

**Current source (manual, not Armazém):** PU, CS and TP currently do **NOT** have their own registration/integration in Armazém. Because of that, they are currently **configured MANUALLY in Job On** by the **Responsável** — as part of the production-specific Job On configuration. The same applies to Pinças, Calibres and the other equivalent production-specific fields maintained in Job On.

> **Ownership.** This is **JOB ON production-specific configuration**. **JOB ON owns:** PU production-specific configuration · CS production-specific configuration · TP/Tampão production-specific configuration · Pinças · Calibres · equivalent manually maintained production-specific fields. **ARMAZÉM currently does NOT own/provide PU, CS or TP as registered entities.** Other modules may **consume** this Job On snapshot/context, but they do **not** own the production configuration.

**New Job On:** the Responsável **fills/reviews** the production-specific values as necessary (PU, CS, TP and the equivalent manual production configuration such as Pinças and Calibres).

**Duplicated Job On:** these production-specific values are **inherited/copied** from the duplicated Job On; the **Responsável reviews them** and **changes only what differs** for the new production.

**Normal usage pattern:** this manual configuration is generally entered **mainly on the first relevant Job On/reference**; subsequent Job Ons are **mostly created by duplication**.

**Important — copied values are NOT immutable defaults:** duplication means **copying the production-specific configuration into the new Job On as a starting point**. It does **not** mean permanent defaults, immutable values, or automatic correctness for the new production. The **Responsável may alter** the copied values whenever the new production requires different values.

> **Terminology warning — TP/Tampão vs Peso Tampão/calote:** the **TP/Tampão in Job On** is the production-specific tooling/configuration item; the **Tampão/calote in Peso** is a **calculated informational technical value** (formula `π × s² × (3r − s) / 3`, not part of the main Peso calculation). These are **distinct concepts** and must **not** be merged.

> **OWNER DECISION REQUIRED — conflito preservado (quarentena transversal):** a regra acima (PU / CS / TP como configuração específica de produção do Job On) conflita com `80_TAMPOES_FUNCTIONAL.md`, que fecha Tampões como módulo autónomo — "Sem relação com Job On", "Envia dados para Job On: NÃO", "Consome contexto de Job On: NÃO" (§2, §13, §16). Ambas as afirmações são preservadas integralmente; a sua reconciliação é decisão do Owner e **não** é resolvida neste conjunto documental.

---

<a id="s7"></a>
## 7. VERIFICAÇÕES

The established ownership split:

- Verification **rule/configuration** belongs to the **Lot in Ferramentas**.
- **Job On** materializes/presents the occurrences relevant to production.
- The **Operador** manually confirms/checks them.
- Duplicating a Lot copies configuration, not previous occurrences/history.

Inside a planned production, Job On contains operational verification/check occurrences. Users can confirm them. When confirmed, the system records the verification occurrence, its confirmed status, the authenticated application user, and the confirmation date/time, in the context of the relevant Job On production.

The user identity comes from the authenticated application session / current-user context. It must not be manually chosen by the browser/user. The UI keeps visible who confirmed and when they confirmed.

Example:

```
MF · Confirmar folga
Confirmada
João Silva · 17/08/2026 14:32
```

These confirmations form part of the production history.

**Clear distinction:**

- **NOTAS** = free-text production information.
- **VERIFICAÇÕES** = attributed operational confirmations with state + user + timestamp.

Preserved verification mechanics:

- Confirmation is exclusively manual in V1; it is never inferred from warehouse movements, repair, technical state, usage %, or elapsed time.
- Frequencies V1: `uma_vez_no_lote` and `por_fabrico` (current design).
- Occurrences originate from rules configured on the tool/lot's own sheet (Verificações tab), not inside Job On; Job On materializes and confirms them.
- Duplicating a Job On does not copy old checks; it generates the new production's occurrences.

CURRENT DESIGN: confirmations are recorded in the context of the relevant Job On/revision and remain part of the history; confirmed occurrences and prior confirmations/resets are not silently rewritten; duplicating a Job On generates the new production's occurrences.

Reset/reopen behaviour and actor rules are not promoted to owner-confirmed truth here.

---

<a id="s8"></a>
## 8. JOB ON ↔ FERRAMENTAS

Functional relationship only:

- **Ferramentas supplies registered tooling context.**
- **Job On does not own Ferramentas master data.**
- Job On uses selected tooling in production context.
- Later Ferramentas changes do not rewrite historical Job On production data (see §6).

Ferramentas' registered tool/Lot information — including the Machine/Line associations registered on each tool/Lot — is what allows Job On to filter the tooling options presented to the Responsável by the Job On's Product Reference and Machine/Line (see §4). Filtering narrows the options; it does not choose the tool. Job On does not ask Ferramentas to decide the correct tool — the Responsável makes the final selection.

Job On may display tooling information read-only where useful: reference, lot, technical name, technical state, usage %, allowed lines/machine. This does not duplicate the full Ferramentas operational model.

`% usage` belongs to the tool/Lot context in Ferramentas (manually read from SAP and manually entered). Job On consumes it read-only where useful (see also §17 for the remaining snapshot question).

---

<a id="s9"></a>
## 9. JOB ON ↔ ARMAZÉM

- **Armazém owns physical location and movements.**
- Job On may display location/availability context for planning.
- **Selecting tooling in Job On is not itself a warehouse movement.** Do not infer physical movements from Job On selection unless explicitly defined.

During planning, Job On needs the real tooling situation to judge whether the planned tools are suitable/available. Where supported, this includes: current position/location; whether present; current state; availability; awaiting repair; repaired; relevant utilisation information. Job On consumes this information for planning; it does not create a warehouse movement merely by selecting a tool.

The tooling options filter itself is based on registered tool/Lot information (Reference + Machine/Line — see §4); Armazém provides complementary location/availability context for that planning.

---

<a id="s10"></a>
## 10. JOB ON ↔ CONTROLO

Controlo is an **independent functional module**; it is not owned by Job On and is not the same module as Job On. However, its production context comes from Job On.

Preserved boundary:

- **Job On asks:** Which tools/Lots are planned for production?
- **Controlo asks:** Which tool/Lot is being controlled?

**CASE A:** Controlo controls tooling already planned in Job On and can receive that context automatically. When a Controlo record is created this way, it uses the exact Job On, the relevant Job On version/context for that production, and the exact CM / MF / BQ lots from Job On. In Case A these lots are not reselected independently in Controlo — Controlo inherits the tooling that was already filtered and selected in Job On.

**CASE B:** Controlo may control another valid/new Lot even if it is not selected in Job On.

> **Critical:** Selecting another control subject in Case B does NOT alter Job On production tooling. Do not mix these two concepts.

Controlo records the control results for that production. No second general lot-tracking system is created inside Controlo.

**PU/CS consumer boundary (owner clarification):** The Resumo / Folha de Controlo may evaluate **PU and CS** (part of the five Resumo pieces CM/BQ/MF/PU/CS), but **PU/CS come from the exact Job On production/revision context** — not from Armazém. **Controlo does not independently create/select/maintain** those production values; it consumes the Job On snapshot/context (§6.1).

```
JOB ON     = authoritative production/tooling context
CONTROLO   = control records/results for that context
```

---

<a id="s11"></a>
## 11. JOB ON ↔ PESO

- Peso operates within the production context inherited through Job On/Controlo as defined by the design.
- It is associated with the exact Job On production context.
- It uses inherited production/tool context where applicable.
- **For Peso, the functional tooling context is CM + Lot inherited from Job On.** The broader production context may include CM, MF, BQ, but do not make Peso depend on unrelated tooling types.
- Peso does not independently reconstruct production/tooling identity.

**Pegamentos** operates within the same production workspace. It works against the same exact Job On context and inherits the exact CM / MF / BQ production tooling context. CM / MF / BQ must not be independently reselected where the design forbids it.

Do not invent additional Peso or Pegamentos rules.

---

<a id="s12"></a>
## 12. JOB ON ↔ REPARAÇÃO

Confirmed relationships only:

- **Repair history belongs to Reparação.**
- Job On may display last repairer / repair information read-only where useful.
- **Job On does not own/edit repair history.**
- **Reparação Interna concerns CM/MF only.**
- **BQ is never a Reparação Interna tool.** BQ may appear only as production/reference context.

Topology: there is **no** Controlo → Reparação Interna relationship. Both are independent modules downstream of Job On, both consuming the same upstream production context.

```
               JOB ON
               /    \
              v      v
       CONTROLO    REPARAÇÃO INTERNA
```

Reparação Interna records what operators repaired DURING that production. Repair records remain associated with the correct Job On, production, reference, revision/context, and line/machine where applicable.

---

<a id="s13"></a>
## 13. JOB ON ↔ BOQUILHAS

Cross-module boundary needed here:

- **BQ is a tool whose master belongs to Ferramentas.** Boquilhas only registers the movements related to BQ external repair.
- Job On may select/use **BQ + Lot** as production tooling context (Job On selects BQ + Lot but does not own the BQ master).
- Where applicable, the BQ selector follows the same filtering principle as CM/MF: Job On uses its Product Reference and Machine/Line context to filter the registered BQ/Lot options presented to the Responsável (see §4). BQ master data remains owned by Ferramentas.
- BQ is inherited by Controlo from Job On exactly like the exact CM/MF lot context.
- Job On or Ferramentas do **not** own each other's scopes: Job On selects/uses BQ + Lot for the production; the BQ master belongs to Ferramentas.

Do not describe BQ as outside the tooling/production ecosystem. BQ participates normally in production planning, Armazém/warehouse context, and as the exact BQ lot for a Job On production. The key difference is repair ownership: CM/MF may use Reparação Interna; BQ never does.

---

<a id="s14"></a>
## 14. PRINTING / JOB ON DOCUMENTS

Job On drives the production documents/printing. Printed documents represent the relevant production/reference context.

The known production sheets include:

- Ficha de Artigo;
- Job-On Moldes;
- Trabalho de Equipa;
- the required duplicate/variant sheet where applicable.

Printed sheets are operational documents and must reflect the exact production/reference revision context. Do not invent fields from screenshots, and do not remove fields merely because they seem visually redundant.

If some sheet-field ownership is uncertain, keep it as print/production evidence; do not automatically classify it as Ferramentas master data.

---

<a id="s15"></a>
## 15. HISTORY / REVISIONS

Functionally:

- Job On production configuration must preserve historical/revision context.
- Later master-data changes must not silently alter past production records.
- Historical production data must not be silently rewritten.
- Printing should represent the relevant production/revision state.
- Corrections must preserve what was recorded before; historical context is never reinterpreted.

CURRENT DESIGN: every save creates a new revision; older revisions remain exactly as saved; corrections always create new rows. Historical context is preserved and never reinterpreted.

---

<a id="s16"></a>
## 16. EXEMPLOS

These examples illustrate existing rules only; they do not create new business rules.

### Example A — Production + tooling selection

```
Production 202601
Product Reference 5447T173

selected:
  CM + Lot
  MF + Lot
  BQ + Lot
  FF
  Calibres
  Pinças
  PU          (production-specific configuration — manual, §6.1)
  CS          (production-specific configuration — manual, §6.1)
  TP / Tampão (production-specific configuration — manual, §6.1)
```

These values belong to that exact production/revision. They are Job On production/reference data and do not automatically become live master data in Ferramentas. Later Ferramentas edits do not rewrite this historical Job On data. PU / CS / TP are configured manually in Job On by the Responsável because they are not currently registered in Armazém (§6.1); when a Job On is duplicated, these production-specific values are copied/inherited and the Responsável reviews and may change what differs (§6.1).

### Example B — Controlo Case A vs Case B

```
CASE A:
  Controlo controls the CM / MF / BQ lots already planned in Job On 202601.
  It receives that context automatically; lots are not reselected.

CASE B:
  Controlo controls a newly arrived Lot that is not selected in Job On 202601.
  This creates a control record for that Lot but does NOT alter
  the tooling planned in Job On 202601.
```

### Example C — Tooling filter by Machine/Line

```
Job On: Production 202601, Reference 5447T173, Machine B3

Responsável opens the CM selector.
Job On uses Reference 5447T173 + Machine B3 to filter the registered
CM/tool-Lot options and presents only the options relevant to that
Reference and registered for B3.

The Responsável then selects the intended CM + Lot from those filtered options.
```

Filtering narrows the options; it does not choose the tool, and it does not create a composite tool identity.

---

<a id="s17"></a>
## 17. QUESTÕES FUNCIONAIS ABERTAS

Genuine unresolved questions, classified. Questions already owner-confirmed are integrated into the main explanation, not listed here.

**A. OWNER DECISION REQUIRED (business questions):**

1. **Family siglas** — Official/displayed meaning (expansion) of the family siglas (MP/CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO) and the final list of required fields per family.
2. **Job On life-cycle statuses** — The real set of Job On life-cycle statuses (draft / planned / in-fabrication / closed / cancelled?).
3. **"Novo em branco" template** — Which families/fields are required to appear in a "Novo em branco" Job On template.
4. **Duplicar anterior** — The canonical ordering ("anterior") used by Duplicar anterior and the minimal information that lets a user identify a historical Job On in the duplication list.
5. **Verification completion/cancellation** — Whether verifications need priority, completion comment, or cancellation (reset/correção on an already-closed Job On; whether confirmation requires a comment; whether Apagar requires a reason; behaviour of already-created pendings when a rule is disabled).
6. **Stock vs quantity** — The business meaning of stock and quantity in machine / needed on the family cards, where this affects user workflow.
7. **Tipo vs Processo** — The business relationship between Tipo and Processo on the Job On, if this is a real business distinction.

**B. DESIGN CLARIFICATION / RECONCILIATION:**

8. **% usage snapshot in Job On** — Ferramentas ownership of `% usage` is resolved (Ferramentas is the source/master of current usage information; Job On consumes it read-only). The remaining open part is whether Job On keeps a production/revision snapshot of the usage value, and if yes, at what functional moment/context that snapshot represents the value. Do not reopen Ferramentas ownership of `% usage`.
9. **Calendar past-day movements** — The source of the calendar past-day in/out movements shown by the Job On calendar.
10. **Selector lot eligibility** — Which lot states are eligible in the CM/MF/BQ selector (active / available / historical).

---

## Implementation Pointers

### Relevant implementation areas

- Application: Job On is the production/planning context provider; tooling selection filters by Product Reference + Machine/Line (CM/MF/BQ selectors) — registered tooling data must support that filtering (see `30_FERRAMENTAS_FUNCTIONAL.md`).
- Application: verification occurrences generated from Ferramentas rule configuration — V1 frequencies `uma_vez_no_lote` and `por_fabrico` (rules owned by Ferramentas; Job On presents/confirms generated occurrences).
- Application: tooling/usage data consumed read-only (`% usage` owned by Ferramentas; whether Job On keeps a production/revision snapshot of the value is an open item — §17).
- Printing: Job On print document (production document / print projection surface).
- Technical map: `maps\06_JOB_ON.md` (verify freshness before use).

### Known implementation gaps

- No verified gap recorded in this document set. §17 lists open functional/design questions that should be resolved before finalising the affected code (e.g. `% usage` snapshot moment, calendar past-day movement source, selector lot eligibility).

### Design reference

- `AI-CONTEXT\design-coder\20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html`
- Print: `AI-CONTEXT\design-coder\20_JOB_ON_02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html`

### Cross-module dependencies

- Ferramentas (tooling master, `% usage`, verification rules); Armazém (stock/location context; selection itself creates no movement); Controlo (consumes Job On context); Reparação Interna (resolves its production context from Job On); Boquilhas (BQ external-repair movements; BQ master in Ferramentas).