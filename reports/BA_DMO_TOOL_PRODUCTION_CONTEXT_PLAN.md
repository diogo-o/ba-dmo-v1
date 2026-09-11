# BA-DMO — Tool / Production Context Redesign Plan

> **Status:** OWNER-DIRECTED PLAN — NOT IMPLEMENTED
>
> This document records the current intended redesign around Ferramentas, Job On, Controlo, Peso, Pegamentos, Reparação Interna and Boquilhas so these decisions are not lost before implementation and before any later LIVE → CLEAN rebuild.
>
> This is a planning/authority document. It does **not** mean the current implementation already behaves this way.

---

# 1. Core architectural direction

The central rule is:

> **A Ferramenta is the persistent identity. Production is usage context. Job On is the medium that selects the correct tools for a production. Tool-specific control/repair events stay linked to the tool, while production-wide control information belongs to the Job On/production context.**

The Job On must stop acting as a giant container that duplicates information already owned by Ferramentas or tool-specific control/repair records. At the same time, information whose meaning is inherently about the **whole production and the set of tools used together at that moment** belongs to the Job On/production context.

A critical ownership distinction is now explicit:

- **Peso belongs to the CM that was weighed**, even when the weighing happened during a specific production;
- **Pegamentos belongs to the production/Job On context** when it describes the combined set of tools used in that production;
- **the overall/resume of Controlo belongs to the production/Job On context** when it summarizes several tools used together.

The intended direction is relational:

```text
Ferramenta
   │
   ├── historical tool-specific control records
   │      └── Peso -> CM
   ├── historical notes
   ├── historical internal repairs
   └── historical production usages
            │
            ▼
        Production
            │
            ▼
          Job On
            │
            ├── selected tool set for this production
            ├── production-wide control summary
            └── Pegamentos for this exact tool combination
```

---

# 2. Ferramenta is the persistent entity

A Ferramenta must have a stable internal identity (`tool_id` or equivalent).

Its persistent information includes the real tool identity/context required by the application, such as:

- type (CM, MF, etc.);
- reference;
- lot;
- one or more associated machines/lines;
- established tool classification/context such as `NNPB` or `PS`, where that is a real tool-owned field in the current product;
- other genuinely tool-owned fields already established by the product.

## Important: machines/lines are plural

A tool may be usable/associated with more than one machine/line.

Do **not** model machine/line as a single immutable identity key.

Machine/line associations are operational context and filtering/search information. They must not become an inferred industrial validity blocker unless an explicit owner rule says so.

Conceptually:

```text
Tool
- tool_id
- type
- reference
- lot
- classification/context (e.g. NNPB or PS where applicable)
- machines/lines [0..N]
```

The stable identity is the tool itself, not a concatenated string that assumes exactly one machine.

---

# 3. Explicit Production Tool Usage relation

The system should represent the fact that a particular tool was used in a particular production.

This should be an explicit relation rather than being inferred later from duplicated fields.

Conceptually:

```text
ProductionToolUsage
- id
- production_id
- jobon_id / jobon revision context where applicable
- tool_id
- role_in_jobon / family (CM, MF, BQ, PU, etc.)
- association timestamps / audit as needed
```

This relation is important because the same tool can participate in multiple productions with different companion tools.

Example:

```text
Production 202601
- CM -> Tool A
- MF -> Tool X
- BQ -> Tool B

Production 202602
- CM -> Tool A
- MF -> Tool Y
- BQ -> Tool C
```

The history of Tool A must therefore be able to show that it worked with different companion tools across productions.

Do **not** store "CM A works with MF X" as permanent truth on the tool itself.

The authoritative statement is:

> **In this production, Tool A was used together with this exact set of tools.**

---

# 4. Job On becomes the production medium / context hub

The Job On already contains the correct production-specific selection of tools.

That should be used as the normal medium through which downstream workflows obtain the correct tool context.

Conceptually:

```text
Production
   ↓
Job On
   ├── CM -> tool_id
   ├── MF -> tool_id
   ├── BQ -> tool_id / applicable BQ relation
   ├── PU -> tool_id
   └── ...
```

The Job On should own information whose meaning genuinely belongs to the production instance, including:

- production identity/context;
- planned start/end dates;
- Job On-specific notes;
- verification/revision state;
- tool associations for that production;
- production-wide control summary;
- Pegamentos for the exact production/tool combination;
- generated production documents / document references.

It should **not** duplicate full copies of:

- tool identity data;
- tool history;
- Peso records owned by a CM;
- internal repair history;
- historical tool notes.

The distinction is ownership by meaning:

> **If a value describes one tool, keep it with that tool/event. If it describes the production and the set of tools working together at that instant, keep it with the Job On/production context.**

---

# 5. Controlo links tool-specific records to the tool, with production context

Controlo is the workflow that processes control information, but not every control result has the same ownership.

Normal production flow:

```text
Production
   ↓
Job On
   ↓
Selected Tool(s)
   ↓
Controlo
```

The Job On supplies the already-correct tool selection for that production.

For a tool-specific control record, the resulting record belongs to:

- the tool;
- and the production usage/context in which that control occurred, when applicable.

Conceptually:

```text
ToolControlRecord
- control_id
- tool_id
- production_id (when performed in production context)
- jobon_id / production usage reference where useful
- control type
- measurement/result data
- state/approval data
- notes
- actor
- timestamps
```

Production-wide outcomes produced through Controlo are instead associated to the Job On/production context as defined in Section 6.

## Controlo should reuse tool information instead of asking for it again

Once the user has selected/resolved the tool, Controlo should obtain the tool-owned context directly from that tool rather than forcing repeated manual entry.

At minimum, where those fields genuinely belong to the selected tool, the control workflow should be able to retrieve/display:

- type;
- reference;
- lot;
- `NNPB` / `PS` classification or equivalent established field;
- associated machine(s)/line(s).

Do **not** create a second independently editable copy of these values inside Controlo unless a specific historical snapshot rule requires it.

The normal model should be:

```text
Selected tool_id
      ↓
Tool lookup
      ↓
Type / Reference / Lot / Classification / Machines
      ↓
Control workflow
```

## Control without an existing Job On

A Job On must **not** be structurally required for tool-specific Controlo to exist.

A new tool may require control before a production/Job On has been scheduled.

In that case, Controlo must allow the user to locate/select the tool directly using its real identifying context, without creating:

- a fake production;
- a dummy Job On;
- a temporary fake reference.

Conceptually:

```text
Tool
   ↓
ToolControlRecord
production_id = null until/unless a real production context exists
```

The same control subsystem must therefore support two valid entry paths:

1. **Via Job On / Production** — tool is already selected by the production context.
2. **Direct Tool Selection** — when no production/Job On exists yet.

---

# 6. Ownership split: Peso vs production-wide control summary / Pegamentos

This section is authoritative for the ownership of Peso, Pegamentos and the overall Controlo summary.

## Peso belongs to the CM

Peso uses only the **CM**. Therefore a Peso record belongs to that exact CM, even when the measurement is performed for a specific production.

Conceptually:

```text
PesoRecord
- peso_id
- cm_tool_id
- production_id (when measured for a production)
- jobon_id / ProductionToolUsage reference where useful
- measurement data
- approval/state
- actor
- timestamps
```

This matters because the same CM can be reused with different MF and BQ lots in later productions.

Example:

```text
Production 202601
CM A + MF X + BQ Lote 1
Peso -> CM A

Production 202602
CM A + MF Y + BQ Lote 4
Peso -> CM A
```

The Peso history remains a history of **CM A**, with the production context showing when each measurement occurred.

Changing MF, BQ or their lots does not change the ownership of the Peso record.

Do not attach Peso primarily to the whole Job On/tool combination merely because the measurement happened during a production.

## Pegamentos belongs to the production/tool combination

Pegamentos represents a result of a **specific set of tools working together**. Its meaning is therefore production-wide, not intrinsic to one CM, MF or BQ.

Conceptually:

```text
JobOn / Production 202601
├── CM -> Tool A
├── MF -> Tool X
├── BQ -> Tool B / Lote context
└── Pegamentos
     └── values/results for this exact production/tool set
```

If one of the tools changes in another production, that is a different production/tool combination and its Pegamentos must remain separate.

## Overall/resume of Controlo also belongs to the production/tool combination

When the Controlo summary combines information about several tools used together, the summary belongs to the Job On/production context.

Conceptually:

```text
JobOn / Production 202601
├── ProductionToolUsage
│   ├── CM -> Tool A
│   ├── MF -> Tool X
│   ├── BQ -> Tool B
│   └── ...
│
├── ProductionControlSummary
│   ├── overall control/result fields
│   └── references to underlying records when useful
│
└── Pegamentos
    └── values/results for this exact tool combination
```

The same CM may later appear in another production with a different MF or BQ lot. The previous Pegamentos and previous overall Controlo summary must remain attached to the **original production/tool combination**, not become permanent properties of the CM.

Tool history may show that a production containing that tool had a certain Pegamentos/summary result, but that is a related production event rather than tool-owned data.

---

# 7. Tool notes must preserve both tool and production context

When a note describes the state/condition/observation of a tool during a production, it must be attributable to that exact tool and production context.

Conceptually:

```text
ToolNote
- note_id
- tool_id
- production_id (when applicable)
- jobon_id / production usage reference where useful
- note
- actor
- created_at
```

This gives both historical views:

```text
Tool history -> all notes across productions
```

and:

```text
Production history -> notes for each tool used in that production
```

Genuinely production-wide notes remain Job On/production notes and must not be forced into a tool record.

---

# 8. Reparação Interna follows the same Job On -> Tool context path

Reparação Interna must associate its records directly with the actual tool and the production in which that tool was being used.

Normal flow:

```text
Production
   ↓
Job On
   ↓
Selected CM/MF tool
   ↓
Reparação Interna
```

The important rule is:

> **Controlo and Reparação Interna both retrieve the required tool from the Job On in the same way when operating inside a production context.**

The resulting RI record belongs to the tool and its production context.

Conceptually:

```text
InternalRepair
- repair_id
- tool_id
- production_id
- jobon_id / production usage reference where useful
- repairer
- intervention / repair data
- state
- notes
- dates
- actor
```

Reparação Interna remains:

- CM/MF only;
- internal/company repair during production;
- distinct from CM/MF external programmed repair handled through Armazém;
- distinct from Boquilhas repair/tracking.

---

# 9. Boquilhas repair/history should use the same production-context linking pattern

Boquilhas keeps its own operational model and **must preserve individual BQ tracking**. It must not be collapsed into the CM/MF lot-level repair model.

When a Boquilhas repair/event is associated with a production, the repair record belongs to the real BQ identity/record and is linked to the production in which that BQ/lot was used.

The repair record must be able to resolve/display:

- repairer;
- production;
- reference;
- lot;
- machine/line context;
- the specific BQ identity/tracking record where applicable;
- repair dates/state/notes already belonging to the Boquilhas workflow.

Conceptually:

```text
BqRepairRecord
- repair_id
- bq/tool identity reference
- production_id (when associated to a production)
- jobon_id / ProductionToolUsage reference where useful
- repairer
- repair state/data
- notes
- dates
- actor
```

Do not duplicate `reference`, `lot` and `machine` as new independent truths merely because they are displayed on the repair record.

---

# 10. Historical context becomes richer without confusing ownership

With explicit ProductionToolUsage + tool-linked events + production-wide control records, history can reconstruct both levels correctly.

For a CM, the system should eventually be able to show, per production:

- production number;
- Job On context;
- MF used alongside it;
- BQ/lot used alongside it;
- other associated tools as relevant;
- Peso records owned by that CM;
- production-wide control summary for that production;
- Pegamentos for that production/tool combination;
- internal repairs;
- notes;
- generated production documents.

The important distinction is:

- Peso is reached directly through the CM, with production context;
- production-wide summary/Pegamentos are reached through the production in which the CM participated.

For Boquilhas, history should likewise show the production context of relevant repair/tracking events without losing individual BQ granularity.

Historical pairings are context, not inferred compatibility rules.

---

# 11. Loading/performance direction

This redesign should reduce the amount of information Job On must load eagerly while still allowing the Job On to own lightweight production-wide records.

The Job On can load its own sheet, production tool associations, and only the summary information required for the normal view.

Detailed related information can be loaded when required, for example:

- CM-specific Peso/control detail/history;
- internal repair history;
- Boquilhas repair/history context;
- historical tool notes;
- production pairing history.

Do not make the Job On preload every historical record for every associated tool merely because those records are accessible from the Job On.

---

# 12. Document generation direction

Job On remains the place from which production documents are accessible by production.

Document generation should aggregate authoritative data from the actual owners of each field.

Conceptually:

```text
DocumentGenerationContext
├── Production
├── Job On
│   ├── ProductionControlSummary
│   └── Pegamentos for this production/tool set
├── ProductionToolUsage
├── Tools
│   └── CM
│       └── relevant Peso record(s)
├── production/tool notes
├── other document-required data
└── output document metadata
```

The generated PDF is the historical snapshot of what was emitted/sent to production at that point in time.

Before this area is considered ready for a later CLEAN rebuild, implementation must explicitly verify and define:

- which CM Peso record feeds each production document;
- which Job On production-wide summary/Pegamentos values feed each document;
- approval/state requirements for document data;
- document revision behavior after Job On/tool/control edits;
- PDF naming convention;
- generation timestamp;
- production association;
- Job On/revision association;
- reprint/reissue behavior;
- local backup/archive directory structure;
- whether generated PDFs are immutable or versioned;
- how the application finds previously generated PDFs.

---

# 13. Job On editing/revision implications

Because Job On represents the production-context/tool combination, editing must explicitly define what happens when an associated tool changes.

Example:

```text
Production 202601 initially:
CM -> Tool A
MF -> Tool X
BQ -> Tool B

Later corrected/revised:
CM -> Tool A
MF -> Tool Y
BQ -> Tool C
```

The system must preserve enough history to know what associations existed for the relevant production/revision/document emission and which combination a production-wide Controlo/Pegamentos result belongs to.

Peso for CM A remains linked to CM A, while its `production_id`/usage context identifies which production the measurement came from.

Do not silently rewrite historical context already used for control, repair, Pegamentos, summary or issued documents.

The implementation plan must therefore resolve:

- whether ProductionToolUsage is revisioned/effective-dated;
- how tool changes are audited;
- whether existing tool-specific control/repair/Peso records remain tied to the original usage/context;
- how production-wide summary/Pegamentos remain tied to the correct tool set/revision;
- how generated documents preserve the association snapshot used at generation time.

---

# 14. What must NOT be done

Do not solve this redesign by:

- copying tool reference/lot/machine/classification fields into every module as independent truth;
- attaching Peso to the full Job On/tool set when the Peso measurement is specifically about the CM;
- attaching production-wide Pegamentos or multi-tool control summary permanently to one tool;
- duplicating the same production-wide summary independently on every tool;
- forcing Controlo to require a fake Job On for a tool-specific pre-production control;
- forcing the user to retype tool-owned fields in Controlo after the tool is already known;
- making RI select arbitrary warehouse tools when production context already selected the correct tool;
- treating machine/line as exactly one permanent value per tool;
- storing permanent CM↔MF compatibility inferred from historical pairings;
- collapsing Boquilhas individual tracking into the CM/MF lot repair model;
- loading all historical tool data eagerly every time Job On opens;
- deleting current data/schema before migration behavior is proven.

---

# 15. Proposed implementation plan

This is intentionally staged. Do not implement everything in one blind migration.

## Phase A — Inspect current LIVE relationships only

Inspect the current running behavior and current schema/code paths specifically for:

- current tool identity;
- how Job On stores CM/MF/BQ/PU/etc.;
- how machine/line associations are represented;
- where `NNPB` / `PS` or equivalent classification is currently stored and whether it is genuinely tool-owned;
- how Peso is currently stored and how directly it can be tied to the CM;
- how Pegamentos is currently stored and how it represents the multi-tool production setup;
- how the current Controlo summary is assembled and which fields span several tools;
- how Controlo finds its current Job On/tool context;
- which tool-owned fields Controlo currently asks the user to re-enter;
- how RI stores reference/lot/production context;
- how Boquilhas repair records currently store repairer/reference/lot/machine/production context;
- how notes are persisted;
- how production documents currently obtain control/Peso/Pegamentos/tool data;
- how generated PDFs are stored/backed up locally today.

This is a targeted implementation prerequisite, **not** a general legacy audit.

## Phase B — Define new relational contracts

Define the minimum contracts required for:

- Tool identity;
- Tool ↔ Machines/Lines (0..N);
- tool-owned classification/context fields that Controlo can reuse;
- ProductionToolUsage;
- CM-linked Peso records with optional production context;
- other Tool-linked control records;
- Job On / production-wide control summary;
- Job On / production-wide Pegamentos;
- Tool + production-linked notes;
- Tool + production-linked internal repair records;
- BQ individual identity/tracking + production-linked repair records;
- document generation context.

## Phase C — Implement ProductionToolUsage

Make Job On tool selection produce/use explicit production-tool associations.

Acceptance criteria:

- each selected tool has stable `tool_id`;
- production knows which tool fulfilled each Job On family/role;
- same tool can appear across multiple productions;
- historical production pairings can be reconstructed.

## Phase D — Rewire Controlo with explicit ownership split

Controlo must support:

1. production/Job On entry path -> receives the Job On-selected tool(s);
2. direct tool entry path -> allows tool-specific control before a production exists.

Once a tool is selected, Controlo must reuse authoritative tool information instead of asking the user to type it again.

Ownership rules:

- Peso -> exact CM, with production context when applicable;
- other tool-specific results -> exact tool, with production context when applicable;
- Pegamentos -> Job On/production/tool combination;
- overall/resume of Controlo spanning several tools -> Job On/production/tool combination.

Acceptance criteria include:

- type resolved from tool;
- reference resolved from tool;
- lot resolved from tool;
- NNPB/PS or equivalent established classification resolved from tool where applicable;
- one-or-more associated machines/lines available from the tool;
- Peso record resolves to one exact CM;
- changing MF/BQ in another production does not move/re-own historical Peso away from the CM;
- production-wide summary remains tied to the exact production/tool set;
- Pegamentos remains tied to the exact production/tool set;
- a tool-specific pre-production control remains possible without a Job On.

## Phase E — Rewire Reparação Interna

RI receives the CM/MF tool from the active Job On/production context and stores repair data against that tool + production.

Acceptance criteria:

- real lot resolves through the tool identity;
- historical RI records can be viewed by tool and by production;
- current `lote = reference` class of bug is impossible in the new relation model.

## Phase F — Rewire Boquilhas repair production context

Preserve the existing individual BQ tracking model, but make repair history able to link to the production in which the BQ/lot was used.

Acceptance criteria:

- repairer is explicit;
- production association is explicit when applicable;
- reference/lot/machine context resolves from the real BQ/tool + production relation;
- individual BQ identity remains available;
- Boquilhas repair remains independent when no production context exists.

## Phase G — Tool notes / production observations

Store tool-specific observations against the tool + production context when applicable. Keep genuinely production-wide notes at Job On/production level.

## Phase H — Job On query/load simplification

Remove the need for Job On to eagerly carry duplicated tool histories. Keep only its own production-wide data and lightweight summaries needed for the normal production view.

Measure query count/payload and avoid N+1 behavior.

## Phase I — Document generation contract

Document and test the exact owner/source of every required field before changing PDF output behavior.

Define source records, state/approval selection, production/tool context, snapshot/revision rules, filesystem/backup conventions and reprint behavior.

## Phase J — Migration/backfill strategy

Only after the new model works for new records:

- decide what historical associations can be backfilled safely;
- preserve unknown values as unknown rather than inventing associations;
- do not fabricate machine/tool/production links from weak guesses;
- maintain audit/history where historical reconstruction is possible.

## Phase K — Validation before CLEAN reset

Before a later LIVE → CLEAN rebuild, validate at least:

- one tool used across multiple productions;
- one CM paired with different MF tools across productions;
- one CM paired with different BQ lots across productions;
- tool with multiple machines/lines;
- Controlo auto-populates/reuses tool type/reference/lot/classification/machines rather than requiring duplicate entry;
- Peso created through Job On and stored against the exact CM;
- Peso created for a CM before any Job On exists where that workflow is valid;
- same CM retains its Peso history across productions with different MF/BQ combinations;
- production-wide control summary tied to one exact production/tool set;
- Pegamentos tied to one exact production/tool set;
- RI record associated to Job On-selected CM/MF;
- BQ repair record associated to repairer + production + real BQ/reference/lot/machine context;
- tool note associated to tool + production;
- Job On opens without loading unnecessary full histories;
- document generation pulls the correct CM Peso and correct production-wide summary/Pegamentos;
- generated PDF remains traceable to production/revision/source context;
- local PDF backup/retrieval works as intended.

---

# 16. Key owner decisions recorded here

These decisions are the reason for this plan and must not be lost during implementation:

1. A tool is persistent independently of Job On.
2. A tool can be associated with more than one machine/line.
3. Tool-owned information such as type, reference, lot, machines/lines and established classification such as NNPB/PS should be reusable by Controlo instead of repeatedly re-entered.
4. Job On selects the correct tools for a production and acts as the normal medium/context for downstream production workflows.
5. ProductionToolUsage preserves which exact tools worked together in each production.
6. **Peso belongs to the exact CM being weighed.** Production/Job On is context, not the owner of Peso.
7. The same CM can retain its Peso history even when later productions use different MF, BQ or lots.
8. Tool-specific Controlo must also work for a tool that does not yet have a scheduled production/Job On where the workflow requires it.
9. **The overall/resume of Controlo that spans several tools belongs to the Job On/production context.**
10. **Pegamentos belongs to the Job On/production context because it represents the specific set of tools used together in that production.**
11. Production-wide summary/Pegamentos must not become permanent properties of one tool merely because that tool participated in the production.
12. Tool-specific notes must be attributable to the exact tool and production context where applicable; production-wide notes stay with Job On.
13. Reparação Interna obtains the required CM/MF tool from the Job On production context in the same general way as Controlo.
14. RI records belong to the tool + production context, not to copied reference/lot text.
15. Boquilhas keeps individual tracking, but BQ repair history can associate the repairer/event with the production and real reference/lot/machine context where applicable.
16. Historical pairing changes (for example same CM with different MF or BQ lots in different productions) remain queryable.
17. Historical pairings are context, not permanent inferred compatibility rules.
18. Job On should not eagerly load all detailed historical information merely because it is accessible from the production hub.
19. Production documents aggregate from the authoritative owner of each field; the generated PDF is the historical snapshot that remains traceable/versioned/backed up.
20. Job On editing/tool changes must preserve historical associations already used by Peso, control, repairs, production-wide Pegamentos/summary and generated documents.

---

# 17. Implementation status

**Current status:** `PLANNED / NOT IMPLEMENTED`

Do not mark this plan completed until the behavior has been validated against the real application and production data paths.