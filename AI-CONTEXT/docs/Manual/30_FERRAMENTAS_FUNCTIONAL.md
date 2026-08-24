# FERRAMENTAS — MODELO FUNCIONAL

OPEN OWNER QUESTIONS: 6

---

## Índice

1. [Ferramentas — Visão Geral](#1-ferramentas--visão-geral)
2. [Conceitos Fundamentais](#2-conceitos-fundamentais)
3. [Tipos de Ferramenta](#3-tipos-de-ferramenta)
4. [Ficha da Ferramenta](#4-ficha-da-ferramenta)
5. [Estado Técnico](#5-estado-técnico)
6. [Utilização / SAP](#6-utilização--sap)
7. [Verificações](#7-verificações)
8. [Papéis e Permissões](#8-papéis-e-permissões)
9. [Ferramentas ↔ Armazém](#9-ferramentas--armazém)
10. [Ferramentas ↔ Job On](#10-ferramentas--job-on)
11. [Ferramentas ↔ Reparação](#11-ferramentas--reparação)
12. [Ferramentas ↔ Boquilhas](#12-ferramentas--boquilhas)
13. [Ferramentas ↔ Controlo](#13-ferramentas--controlo)
14. [Histórico / Auditoria](#14-histórico--auditoria)
15. [Exemplos](#15-exemplos)
16. [Questões Funcionais Abertas](#16-questões-funcionais-abertas)

---

## Scope / boundary note

The tool families **CM (Contra Molde), MF (Molde Final), BQ, PU and CS** are tools and belong to **Ferramentas**, the master/tooling module. This clarification's functional Ferramentas explanation is **primarily focused on CM and MF master data**, because those are the Ferramentas master-data concepts being documented here in detail; exact family-specific fields for all families remain documented/validated separately.

- **BQ (Boquilha)** is a tool whose **master belongs to Ferramentas**. The separate **Boquilhas** module **only records the movements related to BQ external repair**, and does not own the BQ master.
- BQ may still appear in shared production / Job On / Armazém context, and in historical or current technical/implementation evidence. That production/technical presence does not change that Ferramentas owns the BQ master.
- **BQ is never a Reparação Interna tool.** Reparação Interna applies to CM and MF only.
- **PU and CS are tools** of Ferramentas. They may carry production-specific configuration in Job On for one exact production; that does not make them "not tools". CM, MF, BQ, PU and CS are the tool families; exact family-specific fields remain documented/validated separately.

---

## 1. FERRAMENTAS — VISÃO GERAL

### 1.1 What Ferramentas is

**Ferramentas** is the master/tooling register for the mould tooling used in production. The tool families **CM (Contra Molde), MF (Molde Final), BQ, PU and CS** are tools and belong to Ferramentas; this clarification's functional detail is focused on **CM** and **MF**, while exact family-specific fields are documented/validated separately.

The module header names it **"Configuração mestre de Contra Moldes e Moldes Finais"** — the master configuration of Contra Moldes (CM) and Moldes Finais (MF).

Within the CM/MF model documented here, Ferramentas owns the registered master information and configuration associated with those tools, including their references, Lots, relevant per-Lot configuration, and usage information where defined.

### 1.2 Core rule — Ferramentas registers what users enter

> **Ferramentas is NOT a decision engine.** It does NOT:
>
> - infer the correct tool;
> - infer CM/MF relations automatically;
> - infer machine compatibility from codes;
> - automatically choose tooling;
> - create unnecessary technical identities.
>
> It stores and exposes the registered CM/MF information. Only data explicitly registered, or explicitly defined by a business rule, should be persisted or interpreted.

### 1.3 Module boundaries — what each domain owns

The boundaries between **Ferramentas**, **Job On**, **Armazém**, and **Reparação** must NOT be mixed.

| Domain | Owns | Does NOT own |
| --- | --- | --- |
| **FERRAMENTAS** | Stable registered tooling information for the tool families (CM, MF, BQ, PU, CS): tool identity, Lot, Machine/Lines, Owner/Plant, technical state, % usage, verification configuration, other actual registered master/detail fields. | Physical location/movements, production-specific Job On fields, repair events/history, automatic tooling decisions. |
| **JOB ON** | Production + reference-specific configuration and snapshots: selected CM+Lot, MF+Lot, BQ+Lot, FF, Calibres, Pinças, other production fields, and snapshots/details of the selected tooling as used in that production. | Master tool registration, repair-history ownership, warehouse movements. |
| **ARMAZÉM** | Physical location and movements: Entrada/Saída, destination, current physical/operational whereabouts. | Tool master-data ownership, technical-state ownership, repair records. |
| **REPARAÇÃO INTERNA / EXTERNA** | Repair records/history and repair events. | Tool master registration, production snapshots, warehouse location ownership. |

---

## 2. CONCEITOS FUNDAMENTAIS

### 2.1 Product Reference

The **Product Reference** identifies the product internally. A numeric code such as `5447` is the internal identification of the product. For a given bottle / jar / product for a customer, that number is registered internally and becomes the product's internal identification.

The **numeric part** of the Product Reference corresponds to the internal identification used for the **MF / final product shape**. Because the MF molds / creates the final physical shape of the product, the internal numeric identification used in the Product Reference is the MF identification. The associated CM is tooling, but its own internal number may be equal to or different from the MF number; **the CM number does not redefine the Product Reference**.

### 2.2 Marisa type and the complete Reference

`T173` identifies the **TYPE OF MARISA**.

**Marisa (short explanation):** Marisa is the neck/finish geometry of the bottle or jar made by the Boquilha — the area where the cap/closure screws on or fits/snaps on. (BQ is a tool whose master belongs to Ferramentas; the Boquilhas module only records BQ external-repair movements. This is only the explanation needed to read `T173`.)

Complete product Reference = numeric internal product identification + Marisa type. Combining both gives the complete production/product reference:

```
5447 + T173 = 5447T173
```

where `5447` = internal product identification (MF-based) and `T173` = Marisa / neck-finish type identification.

### 2.3 Terminology / distinct concepts

The following terms are distinct and must not all be called simply "Reference".

| Term | Meaning | Example |
| --- | --- | --- |
| Product Reference | identifies the product internally | `5447T173` |
| Internal numeric product identification | numeric, MF-based product identity | `5447` |
| Marisa type | neck/finish type made by the Boquilha | `T173` |
| MF internal identification | numeric identification that provides the numeric basis of the Product Reference | `5447` |
| CM internal identification | may be the same as MF or may differ; never redefines the Product Reference | `5447` or another number |

Do not conflate: product Reference · CM internal number · MF internal number · Marisa type.

### 2.4 Lot

A **Lot** is a replacement/replenishment of the same tool/reference over time, due to wear/degradation. A new Lot is not production, not fabrication, not a new product, and not a new Reference — it remains the same tool/reference (e.g. `CM 5447 — Lot 1 → Lot 2 → Lot 3` are all `CM 5447`, not different References). 

The lot numbering/sequence rules remain open (see §16).

### 2.5 Machine / Line

**Machine / Line** is user-registered data associated with the tool/Lot. A tool/Lot may carry one or multiple Machine/Line associations (e.g. `B1` or `B1` + `C3`). It is editable where permitted. Machine/Line does NOT create a per-machine Ferramentas entity and does NOT require a composite identity; there is no automatic compatibility inference. The **Responsável** chooses the correct tool/Lot. 

---

## 3. TIPOS DE FERRAMENTA

### 3.1 CM — Contra Molde

**CM = Contra Molde** (counter-mould). It is a distinct tooling type, never fused with MF. `MP` is a legacy import alias of CM, not a second family ("CM é a designação canónica — MP é apenas alias legado de importação", visual authority).

The CM is a distinct tooling type with its own registered internal identification (e.g. `CM 7080`). This number may coincide with the MF numeric identification, or it may differ.

- It is **not** a mandatory rule that the CM internal code equals the product Reference numeric code. The product Reference numeric part is based on the MF / final product internal identification, not on the CM number.
- The CM number does **not** redefine the product Reference, even when it differs from the MF number.

**Reference / Lot / Machine.** Each tool type carries its own registered data: Reference, Lot (a replacement/replenishment instance of the same tool — see §2.4), and Machine/Line (see §2.5).

A new CM Lot is a replacement/replenishment of the same CM tool/reference — it remains the same CM (e.g. `CM 5447 — Lot 1 → Lot 2 → Lot 3` are all `CM 5447`, not different References).

### 3.2 MF — Molde Final

**MF = Molde Final** (final mould). Distinct tooling type, never fused with CM. It has its own registered internal identification (e.g. `MF 7080`).

The MF is the mould that molds / creates the final physical shape of the product. Because of that, its internal identification provides the numeric product identification used in the product Reference. This is why the product Reference remains based on the MF even where the associated CM uses another number.

The **MF side** (Job On print: "Lado do Molde Final") groups MF, fundo final, BQ (Boquilha), AN (Anilha), CS (C. de Sopro), and PI (Pinças). This positions MF as the mould half/assembly alongside CM's side.

**Reference / Lot / Machine:** same register model as CM — Lot + Machine. The MF internal identification is the numeric basis of the product Reference, independent of whether the CM uses the same or a different number.

A new MF Lot is a replacement/replenishment of the same MF tool/reference — it remains the same MF. The Lot concept applies to CM and MF alike and does not merge them.

### 3.3 CM vs MF

| Aspect | CM | MF |
| --- | --- | --- |
| Full name | Contra Molde (counter-mould) | Molde Final (final mould) |
| Legacy alias | `MP` (import alias of CM, never a second family) | none |
| Job On side | Lado do Contra-Molde (CM/MP, TP, PU, ARR) | Lado do Molde Final (MF, fundo final, BQ, AN, CS, PI) |
| Domain type | must remain separate | must remain separate |
| Reference scope | own internal identification; may equal or differ from MF; never redefines product Reference | internal identification is the numeric basis of the product Reference |
| Also selected in Job On with | BQ | BQ |
| Repair type in Reparação Interna | repairable (CM) | repairable (MF) |

The system never merges CM and MF; they keep separate identities and histories even when a pair shares the same internal number. Their numbers may coincide or differ; this is separate from the product Reference, whose numeric part comes from the MF identification (see §2).

### 3.4 BQ, PU, CS and other tool families

- **BQ, PU and CS are tools** and belong to **Ferramentas** (the master/tooling module), together with CM and MF. **BQ** is one of these tool families; the separate **Boquilhas** module only **records the movements related to BQ external repair** and does not own the BQ master. Its appearance in Job On, Armazém, or Controlo is production/operational context; the BQ master remains a Ferramentas concept.
- **BQ is never a Reparação Interna tool.**
- Exact family-specific fields for CM, MF, BQ, PU and CS remain documented/validated separately.

---

## 4. FICHA DA FERRAMENTA

### 4.1 What belongs to Ferramentas

Ferramentas owns the stable registered information of the tool. Examples include, where applicable:

- Type: CM / MF
- internal tool identification
- Lot
- Machine/Lines
- Owner/Plant
- technical state
- latest % usage
- verification configuration
- other actual master/detail fields that are really registered

> **IMPORTANT:** Do NOT import fields from printed production sheets merely because they appear beside CM or MF. Only fields that genuinely belong to the tool register belong to Ferramentas.

### 4.2 What Ferramentas does NOT own

Ferramentas does NOT own:

- Warehouse physical location
- Warehouse movements
- production-specific Job On fields
- Calibres
- Pinças
- FF as a production-specific Job On value
- Job On production snapshots
- repair events/history
- automatic tooling decisions

Ferramentas does not own physical location/movements/destination (Armazém) or repair records/events (Reparação). The tool detail may expose repair information/history read-only (owned by Reparação) without duplicating it as an independently editable Ferramentas record (see §11).

### 4.3 Print-sheet fields are not automatically Ferramentas master data

Appearing on a Job On production sheet does NOT make a field Ferramentas master data.

- The CM operational card shows `Referência`, `Lote`, `Diâm. exterior`, `Folgas`, `Uso %`, and notes.
  - **Ferramentas-registered data (source master: Ferramentas):** `Referência`, `Lote`, `Uso %`. These may also be displayed/snapshotted on the Job On print.
  - **Print / production evidence — NOT Ferramentas master data:** `Diâm. exterior`, `Folgas`. Their presence on the print proves they exist in the production document/context — it does NOT prove they are fields registered in Ferramentas.
- The MF operational card shows `Referência`, `Lote`, `Fundo final`, `Folgas`, `Utilização/Uso %`, and notes.
  - **Ferramentas-registered data:** `Referência`, `Lote`, `Utilização/Uso %`.
  - **Print / production evidence — NOT Ferramentas master data:** `Fundo final`, `Folgas`. FF = Fundo Final belongs to the Job On production/reference context (see §10), not Ferramentas master data.

### 4.4 Registered per-lot data

The register keeps per-lot information, including where applicable `Lote`, Machine/Lines (see §2.5), technical state, `% usage`, and verification configuration. A reference can hold many lots. New lot via "Novo lote a partir deste" copies configuration only (never occurrences/checks/history) and keeps master identity read-only.

Other per-lot fields — such as `Processo` (NNPB/PS), `quantidade`, `Nome/número do desenho`, `revisão` — are CURRENT DESIGN / IMPLEMENTATION EVIDENCE, not yet owner-confirmed functional Ferramentas fields. Whether these (or any other fields) are genuinely registered in Ferramentas remains an open question (see §16). Processes NNPB/PS belong to the lot (Peso flow), not the reference. These fields do not redefine what a Lot is (see §2.4 and §3).

### 4.5 Tool identity model — registration, not inference

> **OWNER RULE — REGISTRATION, NOT INFERENCE.** Ferramentas records the CM/MF information entered by users and exposes that information to the rest of the application. The system does not need to infer an additional tooling identity, nor decide operational tooling, from Reference, Lot, Machine, or their combination.

`TYPE + REFERENCE + LOT + MACHINE/LINE` is a set of **registered operational data / context** that lets the **Responsável** recognise the correct tooling. It is a display/selection context for the **Responsável** in Job On — it is NOT a requirement for separate Ferramentas master-register records per machine. The same Reference and Lot may be registered with multiple Machine/Lines (e.g. `CM 5447`, Lot 3, Machines: `B1, C3`). Ferramentas must faithfully register and expose the Machine/Line values as entered by users.

Machine/Line is registered information associated with the tool/Lot. One tool/Lot may carry one or multiple Machine/Line associations. Machine/Line does NOT create a separate domain identity and does NOT imply a separate Ferramentas record per line.

This is NOT an automatic selection rule. The application only needs to display the registered data; the **Responsável** knows the correct tool and chooses it in Job On. No machine→tool inference.

This does NOT establish any requirement that Ferramentas create or persist a special domain entity whose identity key is `TYPE+REFERENCE+LOT+MACHINE`. No requirement is established for: a separate per-machine entity; a composite technical identity; one persisted tool option per machine; or a schema redesign solely to materialise that combination. Machine is data entered/registered, not an automatic decision rule.

---

## 5. ESTADO TÉCNICO

### 5.1 Known technical states

The known technical states are: **Novo**, **Reparado**, **Por reparar**, **Sucatado**. (Historically recovered; labels confirmed by Armazém and Job On current design.)

### 5.2 Technical state vs operational state

Keep **technical state** and **operational / physical state** separate. Do NOT collapse technical condition and physical whereabouts into one enum/model.

- **TECHNICAL STATE** belongs to the tool information (Ferramentas). Known states: Novo; Reparado; Por reparar; Sucatado.
- Do **NOT** add "Em produção" as a technical condition.
- **"Em produção"** is an OPERATIONAL / PHYSICAL STATE derived from or represented by movement/location context. Operational examples: Em armazém; Em produção; Em reparação / enviado para reparação; other movement destinations where defined.
- The technical state is editable tool information (master edit — see §8 and §9); the operational state is owned by Armazém via movements/destination (see §9).

### 5.3 Technical state authority

The tool detail/master record is authoritative for technical state. The **Responsável** may change the technical state from the tool ficha/detail. Known states are **Novo**, **Reparado**, **Por reparar**, **Sucatado** (see §5.1).

This is RESOLVED: the screen/event is the tool ficha/detail.

Do NOT infer automatic state transitions from Warehouse movements.

The exact repair → technical-state flow (including what happens to the technical state when a repair is completed) remains open (see §16).

### 5.4 Sucatado

**Sucatado** is primarily a visible warning/state indicating that the lot should not normally be used in production. It is **NOT a hard block**.

Reason: registration mistakes can happen and must remain correctable. Therefore:

- the tool can show Technical State = `Sucatado`;
- users can clearly see that it should not be used;
- the application must not permanently hard-block correction/recovery because of that state.

### 5.5 Sucatado vs Saída para Sucata

Keep these separate.

- Technical state: `Sucatado`
- Physical movement: `Saída → Sucata`

A lot may be marked `Sucatado` while still physically present in the Warehouse. Only the actual `Saída → Sucata` records that it physically left for scrap.

After `Saída → Sucata`:

- it must no longer appear as active stock/location;
- its tool record/history remains preserved;
- the system must show that it already left for scrap.

Do NOT delete its history.

### 5.6 Error correction — Scrap

The **Operador** may accidentally create `Saída → Sucata` for the wrong tool / by mistake. The **Responsável** must be able to correct that operational error.

The correction must preserve history/audit. Do NOT silently delete the original event. Record conceptually: original event; correction/cancellation; who corrected; when; reason where required. Do not invent DB schema.

---

## 6. UTILIZAÇÃO / SAP

Utilisation `% use` is manually read from SAP and manually entered; the application never calculates it. The latest value belongs to the tool/Lot context in Ferramentas, not a Job On / Armazém fact. Where utilisation readings are recorded historically, previous readings must be preserved rather than silently overwritten.

The latest `% usage` may need to be updated while the tool is already stored in the Warehouse (example: `CM 5447`, Lot 3, position `3113`, usage `54%`). The appropriate permitted user may update the usage information without requiring the tool to leave the Warehouse first. The previously established rule is preserved: `% usage` is information entered from SAP / registered by the user — the application does not calculate it automatically. Where utilisation readings are recorded historically, previous readings must be preserved rather than silently rewritten.

Ownership: usage belongs to the lot, not the reference master. Job On consumes it read-only. Manual only; future SAP automation is out of scope.

### 6.1 Legacy SAP fields

Fields such as `sap_start`, `sap_end`, `value_added`, `value_cumulative` are **historical implementation evidence only**. The current owner-validated functional requirement is centered on `% usage`. Do NOT treat those legacy fields as required functional data unless the owner later confirms them. They remain legacy/unconfirmed.

---

## 7. VERIFICAÇÕES

Rules are configured in the lot card **Verificações** tab (owned by Ferramentas); Job On only presents/confirms generated occurrences. Rule fields: text, frequency (`uma_vez_no_lote` / `por_fabrico`), active, creator/author + timestamp, origin when copied.

**Rule ownership.** A rule belongs to a **lot**, not the reference. Editing applies to the future and never rewrites occurrences/history.

**Copied to new lots.** Duplicating a Lot copies verification rule configuration. It never copies occurrences/checks/history.

- CURRENT DESIGN: the design states that only active rules are copied.
- OWNER VALIDATION: STILL OPEN — whether disabled/inactive verification rules are also copied has not been confirmed by the owner. This remains a genuine functional open question (see §16).

**Frequency semantics.** `uma_vez_no_lote` stays pending until first check (reset by Chefe re-opens); `por_fabrico` creates an occurrence per new Job On. V1 has no free condition builders (no %-life/date/state/reference-text conditions).

**CM vs MF.** No separate modelling; both types use the same per-lot rule model. BQ is out of scope — rules here concern CM/MF tooling.

---

## 8. PAPÉIS E PERMISSÕES

### 8.1 Responsável

The **Responsável** may: search/consult tools; open the tool detail; create new tool records; edit the editable master/detail information of the tool. This includes, where applicable: `% usage`; Machine/Lines; Owner/Plant; technical state; other tool fields defined as editable.

The detail remains editable even while the tool is physically stored in a Warehouse position (see §9). The **Responsável** controls master-data edits and changes technical state from the tool ficha/detail (see §5.3).

### 8.2 Operador

The **Operador** may:

- search tools;
- consult tool information;
- open the detailed tool page;
- create/register Entrada movements;
- create/register Saída movements;
- define the operational destination of a Saída;
- supply the information required by the movement;
- correct an operational record when an error was made, subject to audit/history (see §8.3).

When Reparação is selected as the Saída destination and the relevant repair flow uses a repairer: provide a repairer dropdown; record the selected repairer (see §11).

The **Operador** must NOT have unrestricted permission to change arbitrary master tool information merely because the detail page is visible.

### 8.3 Operator correction vs master edit (audit)

Preserve the distinction:

- **OPERADOR CORRECTION** = correcting an operational record because information was entered incorrectly.
- **RESPONSÁVEL MASTER EDIT** = deliberately changing the registered master/detail information of the tool.

Do NOT treat these as the same permission. An operator correction must preserve audit/history. Do NOT silently overwrite historical facts. At minimum the conceptual audit must preserve enough information to know: what was corrected; previous value/state where applicable; corrected value; who corrected it; when; reason where the business flow requires it. Do not invent an exact DB schema.

---

## 9. FERRAMENTAS ↔ ARMAZÉM

### 9.1 Ownership split

- **Ferramentas owns:** master/technical information (identity, technical name, drawing, compatibility, life/usage, technical state). Data ownership is at the Ferramentas source of truth.
- **Armazém owns:** physical position/location and movements (Entrada/Saída), destination, and current physical/operational whereabouts.

Armazém does NOT own Ferramentas master data. However, a page reached through Armazém may expose and, for authorised **Responsável** users, edit Ferramentas-owned data at its source of truth. **UI entry point does not change data ownership.** There must remain one source of truth for tool information.

Physical state concepts (position, in production, away for repair, returned, movement, availability) are Armazém-owned, presented to Job On for planning only. They are NOT Ferramentas attributes.

### 9.2 Tool detail remains editable while stored

A tool record does NOT become frozen because the tool is physically stored in the Warehouse. Example: position `3113` — `CM 5447`, Lot 3, machines `B1`, technical state `Por reparar`, usage `54%` — the tool may be stored at `3113` and its tool information may still need to change later.

The tool detail must therefore remain accessible and editable according to permissions. Examples of information that may change: `% usage`; Machine/Line associations; Owner/Plant (e.g. `MG`, `LE`, etc.); technical state; other editable master/detail fields defined for the tool.

Owner/Plant is editable tool information. Do NOT hardcode the full allowed owner list unless current authority defines it. The important rule: the permitted user must be able to correct/change the registered Owner/Plant value.

Do NOT infer that Armazém owns these fields. They remain tool/Ferramentas information.

### 9.3 Armazém search → tool detail

When searching/browsing a tool in Armazém, the user must be able to open the associated tool detail. Example: position `3113` → `CM 5447` → Lot 3 → `B1` → `Por reparar` → `54% Uso`; clicking the tool/reference opens a detailed tool page.

That page combines useful information for the user, such as: Type; tool/internal identification; Lot; Machine/Lines; Owner/Plant; technical state; latest % usage; current physical location; last repairer; repair history; movement/history information where appropriate.

IMPORTANT: this does NOT mean Armazém owns all those fields. The UI may expose them together while their source ownership remains separated (see §9.1). Do not define exact UI layout — functional behavior only.

### 9.4 Creating a new tool record from Armazém (+ Criar novo)

Armazém must provide a "+ Criar novo" flow for a tool record that does not yet exist. Example user flow: Criar novo — Type: `CM`; tool/internal identification: `5447`; Lot: `3`; Machines: `B1` and/or `C3`; Owner: `MG`; Technical state: `Novo`; Position: `3115`.

Conceptually preserve the ownership split:

1. create/register the CM/MF tool information (Ferramentas);
2. register its physical Entrada/location in Armazém.

The user may experience this as one workflow. Do NOT create a duplicate Warehouse-owned tool master.

### 9.5 Armazém role split — Operador vs Responsável

Armazém must be split operationally between **Operador** and **Responsável** (see §8). This mirrors the role-separation idea already used elsewhere, but do NOT blindly copy Controlo permissions.

### 9.6 Saída — operational destination

OWNER-CONFIRMED examples: `Saída → Produção`; `Saída → Reparação`; `Saída → Sucata`. These are operational/movement facts.

Do NOT automatically equate them with technical-state changes:

- `Saída → Produção` does NOT mean a technical state of "Em produção" — because "Em produção" is not a technical state (see §5.2).
- `Saída → Reparação` does not automatically establish a specific Ferramentas technical-state transition unless separately defined (still open — see §16).

Record the physical/operational event first.

---

## 10. FERRAMENTAS ↔ JOB ON

### 10.1 Tool selection and snapshots

Ferramentas supplies registered tool information to Job On: Reference, Lot, technical name, technical state, usage %, and allowed lines/machine. Job On does not ask Ferramentas to decide the correct tool — the **Responsável** chooses the tooling (`Tipo + Referência + Lote + Máquina`).

Job On presents the registered tooling information relevant to the production context so the **Responsável** can select the intended tool/Lot. Ferramentas does not decide the correct tooling.

Job On keeps the production/revision snapshot/history of the tooling as used in that production. It reads live tool state but never overwrites the tool master. Later Ferramentas edits do not rewrite historical Job On data (see §10.5).

When Job On loads/selects or displays a tool, useful supporting information may include the last repairer / repair information. This is read-only context for Job On. Job On does NOT own or edit repair history; Ferramentas/repair sources remain authoritative for that information. (Recorded here only — Job On is not redesigned by this clarification.)

### 10.2 Production-specific data belongs to Job On

Production-specific fields belong to the Job On production/revision context. Example production: `202601`, Product Reference: `5447T173`.

The fields and technical details that are specific to PRODUCTION + PRODUCT REFERENCE belong to the Job On production/revision context. This includes, where applicable:

- Calibres
- Pinças
- FF / Fundo Final (see §10.3)
- production-specific component fields
- specific technical values used on the Job On sheets
- details/snapshots of the selected tooling as used in that production

Do NOT move these into Ferramentas master data merely because they appear on the print sheets.

### 10.3 FF — Fundo Final

FF = Fundo Final. FF is associated with the MF side of the production/tooling context. However: do NOT treat FF as a Ferramentas master-data field unless there is explicit evidence that users actually register it in the Ferramentas database. For the current functional model: FF used on Job On/print sheets should be treated as production/reference-specific data belonging to the Job On context.

### 10.4 Tool vs Production — core principle

- **FERRAMENTAS** describes the registered tool.
- **JOB ON** describes how that tool and the other components are used in a specific production/reference.

Example: JOB ON — Production `202601`, Reference `5447T173` may contain: selected CM + Lot; selected MF + Lot; selected BQ + Lot; FF; Calibres; Pinças; other production fields; snapshots/details of each selected tool/component.

These values belong to that exact production/revision. They must not automatically become live master data in Ferramentas.

### 10.5 Job On snapshot rule

Ferramentas provides the current registered tool information. When a tool is selected for a Job On production, Job On may store the necessary snapshot/details for that production. Later edits to Ferramentas must NOT silently rewrite historical Job On production data. Do not define implementation details here.

---

## 11. FERRAMENTAS ↔ REPARAÇÃO

### 11.1 Repairer directory / configuration

There must be a registered repairer directory/configuration. The system must provide a configuration page where repairers can be created/added by name. This repairer source is reused by repair flows. Do NOT model repairers as arbitrary free text in each repair record.

### 11.2 Reparação Interna relationship

Reparação Interna (RI) applies to CM and MF only. **BQ is never a Reparação Interna tool** (BQ uses its own external-repair flow).

RI belongs to the exact production / Job On context where applicable and does not reconstruct tooling. BQ may appear in the production identification context only (e.g. the complete product Reference `5447T173` = MF/product numeric identification `5447` + Marisa type `T173`), read-only — that does not mean BQ is repaired internally.

Repair records/events and history belong to **Reparação**; the tool state (if any) belongs to **Ferramentas**. How a tool becomes repairable, and the repair effect on technical condition (for example, whether a repaired CM/MF becomes `Reparado`), remain open / NOT FOUND — owner validation required (see §16). 

### 11.3 Repairer, repair history, last repairer

**Repairer** When the applicable repair movement/workflow requires a repairer, the system must record which repairer is associated with that repair. The user must be able to select the repairer from the registered repairer dropdown (see §11.1).

**Repair history** A tool must retain repair history. When a tool has been repaired, later consultation must allow the user to understand its repair history. The detailed tool view should expose the repair information/history relevant to that tool/lot. Ownership is preserved: REPARAÇÃO owns the actual repair records/events; FERRAMENTAS / tool detail may expose/read those records as related information. Do NOT duplicate the repair event as an independently editable Ferramentas record.

**Last repairer** The system must retain and show who repaired the tool most recently. Example detail:

```
CM 5447 — Lot 3
Machines: B1, C3
Technical state: Reparado
Usage: 54%
Location: 3113

Último reparador: <repairer>
Histórico de reparação: <repair records>
```

"Último reparador" must be derived from / backed by the repair history. It must NOT be an unrelated free-text field that can drift away from the actual history. Do not invent the exact query/storage implementation.

### 11.4 CM/MF external repairer rule

For CM/MF external repair:

- repairer is selected from the registered repairer dropdown;
- when creating a new repair record, preselect the LAST REPAIRER known for that tool;
- the user may change the selected repairer before saving.

Example:

- `CM 5447 — Lot 3`, Last repairer: `ABC Moldes`
- Create repair record → Repairer: `[ABC Moldes ▼]`
- User may change to: `[XYZ Tols]`
- After saving that repair record, the new repairer becomes the most recent repairer for future suggestions.

Reason for last-repairer default (operational): users often create repair records for many references/tools at once (e.g. 15 references to send for repair). Repeatedly selecting the same repairer is unnecessary work. Therefore CM/MF external repair pre-fills the last known repairer while still allowing manual change.

---

## 12. FERRAMENTAS ↔ BOQUILHAS

BQ is a tool whose **master belongs to Ferramentas**. The separate **Boquilhas** module only **records the movements related to BQ external repair**. This section records only the cross-module distinction needed here; it does not expand Boquilhas internals.

**Boquilhas repairer association:**

- repairers can be associated with Machine/Lines;
- one repairer may work with multiple lines;
- when sending a BQ/Marisa type from a line to repair, the configured repairer can be automatically suggested/associated.

Example: `T173`, Line `B1` → repairer associated with `B1`.

This differs from CM/MF external repair, where the default is the last known repairer with manual override (see §11.4).

---

## 13. FERRAMENTAS ↔ CONTROLO

Two distinct concepts:

- **PRODUCTION TOOLING (Case A)** — the CM/MF/BQ selected in the Job On, inherited by Controlo as context, never reconstructed.
- **CONTROL SUBJECT (Case B)** — a tool/lot being inspected in Controlo. Controlo may identify another valid lot (e.g. a newly arrived lot to be controlled before being selected in a Job On) as the subject of a Controlo record.

Controlo uses the tooling planned in the Job On as context, and, where applicable, may also control another valid/new Lot (Case B). **Case B does NOT change the Job On planned tooling.**

What Ferramentas data Controlo needs to identify a lot: the lot identifier and read-only reference/lot/technical-name identity.

---

## 14. HISTÓRICO / AUDITORIA

Approximate owner of each history. The exact persistence model is not business truth here — do not turn implementation audit structures into business rules.

| History | Owner |
| --- | --- |
| Tool master changes | Ferramentas (preserved where defined) |
| Lot history (including duplications) | Ferramentas (preserved where defined) |
| Usage history | Ferramentas (must be preserved) |
| Technical-condition history | Ferramentas (preserved where defined) |
| Verification-rule history + occurrence confirmations | rules = Ferramentas; occurrences confirmed in Job On |
| SAP utilisation history | Ferramentas (must be preserved) |
| Repair history (Reparação Interna + Externa) | Reparação; exposed read-only in the Ferramentas tool detail — never duplicated as an editable Ferramentas record |
| Warehouse movement history | Armazém |
| Operator-correction audit (operational records corrected after an input error) | owning module of the corrected record (conceptual minimum — what / previous / corrected / who / when / reason — see §8.3) |
| Job On snapshot/revision history | Job On (revisions/snapshots preserved) |
| Controlo document/snapshot history | Controlo |

Ferramentas is not the owner of warehouse-movement, repair, production-snapshot, or Controlo histories, even where it provides the tool identity those records reference.

Relevant corrections must remain auditable, and technical/master change history should be preserved where defined — without inventing the exact persistence model.

---

## 15. EXEMPLOS

These examples illustrate behaviour already supported by the functional content. They do not create new business rules.

### Example A — Product Reference

```
PRODUCT: Bottle X
Internal product / MF identification: 5447
Marisa type: T173
Complete product Reference: 5447T173
```

Possible tooling (internal numbers):

```
MF 5447      — or —      MF 5447
CM 5447                  CM <different internal number>
```

In both cases: Product Reference = `5447T173` (the numeric part stays based on the MF identification). No specific "different" CM number is invented here; it is shown only as a clearly labelled hypothetical.

### Example B — Lot progression (replacement/replenishment)

```
CM 5447 — Lot 1 — Machines: B1
CM 5447 — Lot 2 — Machines: B1
CM 5447 — Lot 3 — Machines: B1, C3
```

All continue to represent the same registered tool/reference `CM 5447`. The Lot number distinguishes the replacements of that same tool. The Lot numbers 1/2/3 illustrate the operational concept only — no starting value, sequencing, gaps, or uniqueness rule is established by this example.

### Example C — Tool ficha/detail while stored

```
CM 5447 — Lot 3
Machines: B1, C3
Technical state: Reparado
Usage: 54%
Location: 3113

Último reparador: <repairer>
Histórico de reparação: <repair records>
```

The ficha remains editable according to permissions even while physically stored (see §9.2). Repair history is exposed read-only and owned by Reparação (see §11.3).

### Example D — Job On production selection

```
JOB ON — Production 202601, Reference 5447T173
selected CM + Lot
selected MF + Lot
selected BQ + Lot
FF
Calibres
Pinças
other production fields
snapshots/details of each selected tool/component
```

These values belong to that production/revision and do not become live master data in Ferramentas (see §10.4). Later Ferramentas edits do not rewrite this historical Job On data (see §10.5).

### Example E — Sucatado vs Saída → Sucata

A lot may show Technical State = `Sucatado` while still physically present in the Warehouse. Only an actual `Saída → Sucata` records that it physically left for scrap. After that movement it no longer appears as active stock/location, but its tool record/history remains preserved (see §5.4–5.5).

---

## 16. QUESTÕES FUNCIONAIS ABERTAS

The following business/functional questions remain open and require owner input. They are intentionally not answered here.

1. **LOT NUMBER** — Is there a business rule for visible Lot numbering? Must it start at 1? Must it be sequential? Are gaps allowed? What is its uniqueness scope?
2. **TECHNICAL STATE CHANGE — REASON** — Is a reason mandatory when the **Responsável** changes technical state?
3. **REPAIR / TECHNICAL-STATE FLOW** — Who changes a tool to `Por reparar`? Is that always a manual **Responsável** action from the tool ficha/detail? When a repair is completed, what happens to the technical state? Does the tool remain `Por reparar` until the **Responsável** manually changes it to `Reparado`? Or should repair completion cause another defined functional transition?
4. **VERIFICATION DUPLICATION** — When "Novo lote a partir deste" is used, are inactive/disabled verification rules copied too? (Current design says active-only; owner has not confirmed.)
5. **ACTUAL FERRAMENTAS MASTER FIELDS** — Are there any real Ferramentas master fields beyond the already confirmed core set (Type, internal identification, Lot, Machine/Lines, Owner/Plant, technical state, % usage, verification configuration) that users actually register? Do NOT ask about print-sheet fields merely because they appear in Job On printouts.
6. **ENTRADA ESTADO SYNCHRONISATION** — Whether Armazém Entrada's `Estado` (`Reparado | Por reparar | Novo`) synchronises to the tool domain technical condition or remains movement-only context.

---

## Implementation Pointers

### Relevant implementation areas

- Domain/master: tool Type (CM/MF; `MP` = legacy import alias of CM, never a second family), Lot, Machine/Line associations (e.g. `B1` or `B1` + `C3`), Owner/Plant, technical state (`Novo` / `Por reparar` / `Reparado` / `Sucatado`, …), `% usage`, verification rules (fields: text, frequency `uma_vez_no_lote` / `por_fabrico`, active, creator + timestamp, origin when copied).
- Application: "Novo lote a partir deste" copies configuration only (never occurrences/checks/history); verification occurrences confirmed from Job On; legacy SAP fields `sap_start`, `sap_end`, `value_added`, `value_cumulative` = historical implementation evidence only — do not treat as required functional data (current requirement is `% usage`).
- Web / Razor: tool ficha/detail remains accessible and editable while the tool is stored (Armazém context); Armazém search → tool detail; "+ Criar novo" flow from Armazém; repairer associations (directory/line).
- Technical map: `maps\08_FERRAMENTAS.md` (verify freshness before use).

### Known implementation gaps

- None verified in this document set; §16 open questions (lot numbering, technical-state change reason, repair/technical-state flow, verification duplication, actual master fields, Entrada `Estado` synchronisation) affect implementation decisions.

### Design reference

- `AI-CONTEXT\design-coder\30_FERRAMENTAS_01_VISUAL_AUTHORITY_ferramentas.html`

### Cross-module dependencies

- Armazém (localização/movimentos; ferramenta armazenada permanece editável); Job On (seleção de tooling, snapshots, dados do print); Reparação Interna/Externa (registos de reparação; diretório canónico de reparadores partilhado); Boquilhas (master BQ = Ferramentas; Boquilhas regista movimentos de reparação externa de BQ); Controlo (usa ferramentas no contexto do controlo).
