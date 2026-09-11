# BA-DMO — Tool / Production Context Redesign Plan

> **Status:** OWNER-DIRECTED PLAN — NOT IMPLEMENTED
>
> This document records the current intended redesign around Ferramentas, Job On, Controlo, Peso, Pegamentos and Reparação Interna so these decisions are not lost before implementation and before any later LIVE → CLEAN rebuild.
>
> This is a planning/authority document. It does **not** mean the current implementation already behaves this way.

---

# 1. Core architectural direction

The central rule is:

> **A Ferramenta is the persistent identity. Production is usage context. Job On is the medium that selects the correct tools for a production. Controlo and Reparação Interna record what happened to those tools in that production context.**

The Job On must stop acting as a giant container that duplicates information already owned by Ferramentas, Controlo, Peso, Pegamentos or repair records.

The intended direction is relational:

```text
Ferramenta
   │
   ├── historical control records
   ├── historical notes
   ├── historical internal repairs
   └── historical production usages
            │
            ▼
        Production
            │
            ▼
          Job On
```

The Job On remains the operational production hub, but it should primarily associate the correct tools to the production rather than own duplicated copies of all tool/control information.

---

# 2. Ferramenta is the persistent entity

A Ferramenta must have a stable internal identity (`tool_id` or equivalent).

Its persistent information includes the real tool identity/context required by the application, such as:

- type (CM, MF, etc.);
- reference;
- lot;
- one or more associated machines/lines;
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

Production 202602
- CM -> Tool A
- MF -> Tool Y
```

The history of Tool A must therefore be able to show that it worked with MF X in one production and MF Y in another.

Do **not** store "CM A works with MF X" as permanent truth on the tool itself.

The authoritative statement is:

> **In this production, Tool A was used together with Tool X.**

This enables later traceability without inventing permanent compatibility rules.

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

The Job On should own only information that genuinely belongs to the production sheet itself, for example:

- production identity/context;
- planned start/end dates;
- Job On-specific notes;
- verification/revision state;
- tool associations for that production;
- generated production documents / document references.

It should **not** duplicate full copies of:

- tool identity data;
- tool history;
- Peso records;
- Pegamentos records;
- control history;
- internal repair history;
- historical tool notes.

Those remain in their own domains and are reached through relationships.

---

# 5. Controlo belongs to the tool, with production context

Controlo must record control information against the actual tool.

Normal production flow:

```text
Production
   ↓
Job On
   ↓
Selected Tool
   ↓
Controlo
```

The Job On supplies the already-correct tool selection for that production.

The resulting control record belongs to:

- the tool;
- and the production usage/context in which that control occurred.

Conceptually:

```text
ControlRecord
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

## Control without an existing Job On

A Job On must **not** be structurally required for Controlo to exist.

A new tool may require control before a production/Job On has been scheduled.

In that case, Controlo must allow the user to locate/select the tool directly using its real identifying context, without creating:

- a fake production;
- a dummy Job On;
- a temporary fake reference.

Conceptually:

```text
Tool
   ↓
ControlRecord
production_id = null until/unless a real production context exists
```

The same control subsystem must therefore support two valid entry paths:

1. **Via Job On / Production** — tool is already selected by the production context.
2. **Direct Tool Selection** — when no production/Job On exists yet.

The underlying control record model should remain the same.

---

# 6. Peso and Pegamentos remain owned by Controlo

Peso and Pegamentos information should not be copied into Job On as a second source of truth.

Controlo can be the processing/workflow area for these records.

The tool can expose an aggregated control summary built from its related control records.

Conceptually:

```text
ToolControlSummary
├── Peso
│   ├── has record
│   ├── latest/relevant control
│   ├── result/status
│   ├── date
│   └── production context if any
│
└── Pegamentos
    ├── has record
    ├── latest/relevant control
    ├── result/status
    ├── date
    └── production context if any
```

The UI must show these as separate meaningful fields/sections, not as raw JSON or an opaque generic blob.

The exact document-generation selection rule (for example which approved Peso/Pegamentos record is used for a specific production document) must be explicitly defined during implementation. Do not infer it from "latest record" without authority.

---

# 7. Tool notes must preserve both tool and production context

When a note describes the state/condition/observation of a tool during a production, it must not be stored as a free-floating Job On-only note if its meaning is about that tool.

The system must be able to answer later:

- what notes exist for this exact tool;
- in which production was each note recorded;
- what was recorded about this tool during Production X.

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

Do not duplicate the same note into multiple independent stores.

---

# 8. Reparação Interna follows the same Job On -> Tool context path

Reparação Interna must also associate its records directly with the actual tool and the production in which that tool was being used.

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

They do not independently re-select an arbitrary warehouse tool when the production already established the correct one.

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

This relation should also eliminate the current class of errors where RI history copies the reference into `lote` instead of resolving the actual tool/lot.

---

# 9. Historical context becomes much richer

With explicit ProductionToolUsage + tool-linked events, a tool history can reconstruct the actual context of every production in which that tool participated.

For a CM, the system should eventually be able to show, per production:

- production number;
- Job On context;
- MF used alongside it;
- other associated tools as relevant;
- control records;
- Peso/Pegamentos summary where applicable;
- internal repairs;
- notes;
- generated production documents.

This is historical context, not an inference engine.

The application may show that Tool A was paired with Tool X in one production and Tool Y in another. It must not conclude that either pairing is universally valid/invalid unless an explicit owner rule exists.

---

# 10. Loading/performance direction

This redesign should reduce the amount of information Job On must load eagerly.

The Job On can load its own production sheet and tool associations first.

Related information can be loaded by relationship when required, for example:

- tool control summary;
- full tool control history;
- internal repair history;
- historical notes;
- production pairing history.

Do not make the Job On preload every historical record for every associated tool merely because those records are accessible from the Job On.

Accessibility through the production hub does not imply eager duplication/loading.

---

# 11. Document generation direction

Job On will remain the place from which production documents are accessible by production.

However, document generation should aggregate authoritative data from the actual sources rather than relying on copied Job On fields.

Conceptually:

```text
DocumentGenerationContext
├── Production
├── Job On
├── ProductionToolUsage
├── Tools
├── relevant Control records
│   ├── Peso
│   └── Pegamentos
├── production/tool notes
├── other document-required data
└── output document metadata
```

The generated PDF is the historical snapshot of what was emitted/sent to production at that point in time.

The database should not need to duplicate all source information into Job On merely to preserve a document snapshot.

Before this area is considered ready for a later CLEAN rebuild, implementation must explicitly verify and define:

- which control/Peso/Pegamentos record feeds each production document;
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

Do not leave PDF backup/storage behavior implicit.

---

# 12. Job On editing/revision implications

Because Job On becomes primarily a production-context/tool-association hub, Job On editing must explicitly define what happens when an associated tool changes.

Example:

```text
Production 202601 initially:
CM -> Tool A
MF -> Tool X

Later corrected/revised:
CM -> Tool A
MF -> Tool Y
```

The system must preserve enough history to know what associations existed for the relevant production/revision/document emission.

Do not silently rewrite historical context that was already used for control, repair or issued documents.

The implementation plan must therefore resolve:

- whether ProductionToolUsage is revisioned/effective-dated;
- how tool changes are audited;
- whether existing control/repair records remain tied to the original usage/context;
- how generated documents preserve the association snapshot used at generation time.

This is a required design decision before destructive schema simplification or LIVE → CLEAN rebuild.

---

# 13. What must NOT be done

Do not solve this redesign by:

- copying tool reference/lot/machine fields into every module as independent truth;
- making Job On own Peso/Pegamentos data;
- forcing Controlo to require a fake Job On;
- making RI select arbitrary warehouse tools when production context already selected the correct tool;
- treating machine/line as exactly one permanent value per tool;
- storing permanent CM↔MF compatibility inferred from historical pairings;
- loading all historical tool data eagerly every time Job On opens;
- using raw JSON as the user-facing control summary;
- deleting current data/schema before migration behavior is proven.

---

# 14. Proposed implementation plan

This is intentionally staged. Do not implement everything in one blind migration.

## Phase A — Inspect current LIVE relationships only

Inspect the current running behavior and current schema/code paths specifically for:

- current tool identity;
- how Job On stores CM/MF/BQ/PU/etc.;
- how machine/line associations are represented;
- how Peso and Pegamentos are stored;
- how Controlo finds its current Job On/tool context;
- how RI stores reference/lot/production context;
- how notes are persisted;
- how production documents currently obtain Peso/Pegamentos/tool data;
- how generated PDFs are stored/backed up locally today.

This is a targeted implementation prerequisite, **not** a general legacy audit.

Do not treat old tests/reports/migrations as requirements merely because they exist.

## Phase B — Define new relational contracts

Define the minimum contracts required for:

- Tool identity;
- Tool ↔ Machines/Lines (0..N);
- ProductionToolUsage;
- Tool-linked control records;
- Tool + production-linked notes;
- Tool + production-linked internal repair records;
- document generation context.

Do not yet delete legacy structures.

## Phase C — Implement ProductionToolUsage

Make Job On tool selection produce/use explicit production-tool associations.

Acceptance criteria:

- each selected tool has stable `tool_id`;
- production knows which tool fulfilled each Job On family/role;
- same tool can appear across multiple productions;
- historical production pairings can be reconstructed.

## Phase D — Rewire Controlo

Controlo must support:

1. production/Job On entry path -> receives the Job On-selected tool;
2. direct tool entry path -> allows control before a production exists.

Store the control against the tool and, when applicable, the production context.

Peso/Pegamentos remain owned by Controlo and are exposed through a structured tool summary.

## Phase E — Rewire Reparação Interna

RI receives the CM/MF tool from the active Job On/production context and stores repair data against that tool + production.

Acceptance criteria:

- no independent arbitrary warehouse selection for the production flow;
- real lot comes from/resolves through the tool identity;
- historical RI records can be viewed by tool and by production;
- current `lote = reference` class of bug is impossible in the new relation model.

## Phase F — Tool notes / production observations

Move tool-specific observations to explicit tool-linked records with production context when applicable.

Do not convert genuinely Job On-wide notes into tool notes. Ownership must follow meaning.

## Phase G — Job On query/load simplification

Remove the need for Job On to eagerly carry duplicated tool/control data.

Use relationship-based summary/detail queries.

Measure query count/payload and avoid N+1 behavior while still not loading unnecessary full histories.

## Phase H — Document generation contract

Before changing PDF output behavior, document and test the exact source of every required field.

Define:

- source records;
- state/approval selection;
- production/tool context;
- snapshot/revision rules;
- filesystem/backup conventions;
- reprint behavior.

Then implement the generator against authoritative sources and persist generated-document metadata/snapshot references.

## Phase I — Migration/backfill strategy

Only after the new model works for new records:

- decide what historical associations can be backfilled safely;
- preserve unknown values as unknown rather than inventing associations;
- do not fabricate machine/tool/production links from weak guesses;
- maintain audit/history where historical reconstruction is possible.

## Phase J — Validation before CLEAN reset

Before a later LIVE → CLEAN rebuild, validate at least:

- one tool used across multiple productions;
- one CM paired with different MF tools across productions;
- tool with multiple machines/lines;
- control created through Job On;
- control created before any Job On exists;
- Peso control and Pegamentos control retrieval by tool;
- RI record associated to Job On-selected CM/MF;
- tool note associated to tool + production;
- Job On opens without loading unnecessary full histories;
- document generation pulls the correct data;
- generated PDF remains traceable to production/revision/source context;
- local PDF backup/retrieval works as intended.

---

# 15. Key owner decisions recorded here

These decisions are the reason for this plan and must not be lost during implementation:

1. A tool is persistent independently of Job On.
2. A tool can be associated with more than one machine/line.
3. Job On selects the correct tools for a production and acts as the normal medium/context for downstream production workflows.
4. Job On should associate tools, not duplicate all tool/control data.
5. ProductionToolUsage should preserve which exact tools worked together in each production.
6. Controlo is associated to the tool and, when applicable, to the production in which the tool was used.
7. Controlo must also work for a tool that does not yet have a scheduled production/Job On.
8. Peso and Pegamentos remain owned by Controlo; Job On does not need duplicate copies.
9. Tool control summaries should expose meaningful separated fields/sections for Peso/Pegamentos, not opaque raw blobs.
10. Tool-specific notes must be attributable to the exact tool and production context where applicable.
11. Reparação Interna obtains the required CM/MF tool from the Job On production context in the same general way as Controlo.
12. RI records belong to the tool + production context, not to copied reference/lot text.
13. Historical pairing changes (for example same CM with different MF in different productions) must remain queryable.
14. Historical pairings are context, not permanent inferred compatibility rules.
15. Job On should not eagerly load all information merely because it is accessible from the production hub.
16. Production documents aggregate from authoritative sources; the generated PDF is the snapshot that must remain traceable/versioned/backed up.
17. Job On editing/tool changes must preserve historical associations already used by control, repairs and generated documents.

---

# 16. Implementation status

**Current status:** `PLANNED / NOT IMPLEMENTED`

Do not mark this plan completed until the behavior has been validated against the real application and production data paths.
