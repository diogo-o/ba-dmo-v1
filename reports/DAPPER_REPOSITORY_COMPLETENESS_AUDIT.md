# Dapper / Repository Completeness Audit

**Date:** Current audit pass
**Mode:** READ-ONLY — no application code, tests, schema, migrations, or Git were modified.
**Workspace:** `D:\BA-DMO-RECOVERY`
**Application:** `D:\BA-DMO-RECOVERY\AI-CONTEXT\app`
**Functional authority:** `AI-CONTEXT\docs\FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`
**Design package:** `AI-CONTEXT\design-coder\**` (final implementation requirements)
**Prior DB audit:** `reports\DESIGN_PLAN_DATABASE_SUPPORT_AUDIT.md`
**Companion schema audit:** this report confirms/extends the prior database-support audit at the **Application-contract ↔ Infrastructure-Dapper** layer.

---

## 0. Scope and method

Authority order applied: **1)** functional rules → **2)** design-coder → **3)** Application contracts/services → **4)** Infrastructure/Dapper code → **5)** DB support audit (verification only).

The prior database-support audit already established that **the schema (N01–N26) supports 13 of 16 modules immediately** and has essentially **one genuine schema-level dependency** (Job On master reference + reference-scoped image, Q-002) plus a **known schema/rule divergence** (Reparação Interna `tool_type` CHECK widened to include BQ by N22). This audit answers the question that audit does **not**: *are the Application persistence ports and their Dapper implementations complete enough to actually drive the final design?*

Every repository interface in `BA.Dmo.Application\Modules\**\I*Repository.cs` is implemented by a concrete `Dapper*Repository` in `BA.Dmo.Infrastructure\Access`, and **every interface is registered** in `BA.Dmo.Web\Program.cs` (lines 127–269). There is **no `NotImplementedException`, no `NotSupportedException`, no TODO/FIXME placeholder** anywhere in the application (verified by grep). All Dapper SQL is parameterized and cancellation tokens are propagated throughout. The remaining findings below are therefore about **aggregate hydration, atomicity, and design-read projections**, not about stubbed/missing contract methods — with the single decisive exception of Job On.

---

## 1. Module-by-module report

### 1.1 Admin / Auth (DES-004)

- **APPLICATION CONTRACTS:** `IAdminRepository`, `IInternalUserRepository`, auth adapters.
- **DAPPER IMPLEMENTATIONS:** `DapperAdminRepository`, `DapperInternalUserRepository`.
- **SERVICES USING THEM:** `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, `IdentityResolutionService`.
- **READ METHODS:** `ListUsersAsync`, `GetUserAsync`, `ListTemplatesAsync`, `GetTemplateAsync`, `QueryAuditAsync` (paged; `PageSize<=0` = export), `CountActiveAdminsAsync`.
- **WRITE METHODS:** `CreateInternalUserAsync` (idempotent-safe), `UpdateUserAsync`, `ChangeUserTemplateAsync`, `SetUserActiveAsync`, `SetUserModulesOverrideAsync` (N26), `CreateTemplateAsync`, `UpdateTemplateAsync`, `InsertAuditEventAsync`.
- **TRANSACTION / UOW SUPPORT:** guarded writes run inside **one** `DapperUnitOfWork`; self-lockout invariant (`CountActiveAdminsOnAsync` = 0 → `LockoutViolationException` → rollback) is validated **in the same transaction**; optimistic concurrency via `updated_at` + `ConcurrencyGuard`. `SetUserModulesOverrideAsync` deliberately does **not** apply the admins-count guard (module grants still resolve through the template) — correct.
- **HISTORY / APPEND-ONLY:** `audit_events` insert; user creation reconciles partial failure idempotently.
- **DESIGN DATA SURFACES:** Users / Templates / Applications / Audit workspace fully served. The `X12` cosmetic gap ("auth UUID under an Email column") is confirmed as a **read/DTO label** gap only — the value exists.
- **MISSING / INCOMPLETE METHODS:** none.
- **QUERY/DTO GAPS:** cosmetic `X12` display label only (data present).
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE** (one cosmetic read-DTO label).
- **CODEX:** A (safe).

### 1.2 Job On (DES-005) — CRITICAL

- **APPLICATION CONTRACTS:** `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnImageProvider`.
- **DAPPER IMPLEMENTATIONS:** `DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `JobOnPdfRenderer`, `FileSystemJobOnImageProvider`.
- **SERVICES USING THEM:** `JobOnService`, `JobOnPdfService`.

#### AGGREGATE HYDRATION — **INCOMPLETE (blocking)**
`DapperJobOnRepository.GetByIdAsync` (lines 62–107) loads the header row and **revisions only** (`GetRevisionsAsyncInternal`). It does **not** load `job_on_component`, `job_on_component_field`, `job_on_component_row`, or `job_on_verification_occurrence`. Every hydrated `JobOnRevision` therefore has `Components = null` / `Verifications = null` (`JobOnRevision.cs` init-properties default null), **even when rows exist in persistence** unless explicitly assembled elsewhere.

Concrete consequences (all verified by reading consumers):
1. `JobOnService.SaveRevisionAsync` reads `jobOn.CurrentRevision` for typed snapshot columns only — it writes the new revision with `Components` from the **request**, not the aggregate, so saves are not broken for writes *per se*.
2. **`JobOnService.DuplicateAsync` (lines 131–161) loses the entire component/field/CAL-row/verification graph.** It calls `GetByIdAsync(sourceId)`, then `source.CurrentRevision.Components` (empty) → `CopyRevisionForDuplication` copies nothing → the duplicate Job On has a header revision with **no components/fields/rows/verifications**. Design requires full duplication.
3. **`JobOnPdfService.BuildPdfData` (lines 60–114) reads `revision.Components`.** With empty components the 4-page print renders the header/snapshot but **no tool sections (CM/MF/BQ/PU/…), no fields, no CAL rows, no verifications**.
4. **`JobOn\Index.cshtml.cs` (line 124)** reads `CurrentRevision.Verifications` — the "Confirmar" tab shows **no verifications** even when `job_on_verification_occurrence` rows exist.

This is exactly the acknowledged historical gap in `FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md §3` ("`GetByIdAsync` originally did not hydrate `Components`/`Verifications`; this must be fixed ..."). A repository replacing the closed‑incomplete hydration is required.

#### TRANSACTION / UOW — **NOT ATOMIC (blocking)**
- `SaveRevisionAsync` (lines 220–231) calls, **each on its own connection / implicit autocommit**: `InsertRevisionAsync` → `InsertComponentsAsync` → per-component `InsertFieldsAsync`/`InsertRowsAsync`/`InsertVerificationsAsync` → `UpdateCurrentRevisionAsync` → `InsertAuditEventAsync`. **No shared unit of work.**
- If any child insert (or the `current_revision_id` update) fails part-way, a revision and a subset of children are persisted while `current_revision_id` is **not** advanced → **partially persisted / inconsistent current revision**.
- `DuplicateAsync` (lines 144–164) is equally non-atomic across many independent connections.
- Contrast: the **image** mutation `InsertImageMutationAsync` (lines 514–593) **is** atomic via `DapperUnitOfWork.RunAsync` (revision + current-revision update + audit in one transaction). The image path is correct; the **normal save and duplication paths are not**.
- Contradicts the functional requirement "the graph (revision + components + fields + rows + verifications) must be persisted and rehydrated atomically" and "Job On current revision cannot become partially persisted".

#### WRITE PATHS
create **OK**; duplicate **BROKEN** (loses children + non-atomic); save-new-revision **non-atomic** (children + current-revision not bound in one tx); verification confirmation (`UpdateVerificationStatusAsync`) **OK**; set/update current-open user context (`IJobOnUserContextRepository`) **OK**; print/image path **partially broken** (image atomic; printed tool content empty due to hydration).

#### QUERY / DTO GAPS
`GetHistoricalProductionsAsync` returns the summary projection for the calendar/list (references `jc.reference_snapshot` from the current-revision component — a minor note: reference is derived from a component which, if components are incomplete for historical rows, may be null); the **live-tool-state decorator** (current location/status from Ferramentas/Armazém) is a design read/DTO gap (data exists).

#### SCHEMA DEPENDENCY
For DES-005 the **master article/reference + reference-scoped image (Q-002)** remains a genuine schema dependency (see prior DB audit §2.4 A/B). The repository cannot satisfy Q-002 on its own.

#### CLASSIFICATION
**BROKEN / INCOMPLETE IMPLEMENTATION** (aggregate hydration + transactional save/duplication). This is the single highest-risk module for Codex.

#### CODEX
**C** (must fix before DES-005 can function) for hydration + atomic save/duplication; **D** for the Q-002 image surface (schema).

### 1.3 Controlo (DES-006)

- **APPLICATION CONTRACTS:** `IControloSheetRepository`, `IControloProductionContextLookup`.
- **DAPPER IMPLEMENTATIONS:** `DapperControloSheetRepository`, `DapperControloProductionContextLookup`.
- **SERVICES USING THEM:** `ControloSheetService`.
- **READ METHODS:** `GetByIdAsync` (full sheet + items + events), `GetForProductionAsync` (create-or-load by jobOn + optional revision), `ListByProductionAsync`, `ListAsync` (from/to/machine/jobOn/status) for free-mode.
- **WRITE METHODS:** `InsertAsync` (sheet + items atomic in one UoW), `UpdateAsync` (header + item control-fields in one UoW), `InsertEventAsync` (append-only history).
- **TRANSACTION / UOW:** `InsertAsync` and status/update writes run in a **single unit of work** (via `IRepairUnitOfWorkFactory` → `DapperUnitOfWork`); commit-once/rollback-on-failure. **ATOMIC.**
- **AGGREGATE HYDRATION:** reads hydrate header + **items** + **full event history** (`LoadItemsAndEventsAsync`). **COMPLETE.**
- **EXACT REVISION BINDING:** `job_on_id` + `job_on_revision_id` are FK NOT NULL, pinned on write and preserved on read (both `GetForProductionAsync` revision filter and header columns). **BOUND.**
- **DESIGN DATA SURFACES:** draft→submitted→approved/rejected with reopen; per-component OK/NOK + observation + MCaliper link; Resumo tab bound to current-open Job On (R011).
- **QUERY/DTO GAPS:** none; DTOs carry header + items + events. Free-mode read endpoints exist and are wired.
- **SCHEMA DEPENDENCY:** none blocking; optional additive per-result MCaliper-link history ledger (clean-baseline).
- **CLASSIFICATION:** **COMPLETE.**
- **CODEX:** A (safe).

### 1.4 Peso (DES-007 / DES-008)

- **APPLICATION CONTRACTS:** `IPesoRepository`, `IPdfRenderer`.
- **DAPPER IMPLEMENTATIONS:** `DapperPesoRepository`, `PesoSingleFilePdfRenderer`.
- **SERVICES USING THEM:** `PesoService`.
- **READ METHODS:** references/lotes/controls (`GetControlByIdAsync` hydrates control + `peso_leituras` + all jsonb snapshots), `GetApprovedControlsForJobOnAsync`, `GetPreviousApprovedAsync`, `GetRecordDatesAsync`, `GetSettingAsync`.
- **WRITE METHODS:** create/update reference+lote+control, `DeleteControlAsync`, `SaveDayApprovalAsync`, `SaveSettingAsync`, `InsertAuditEventAsync`.
- **TRANSACTION / UOW:** `CreateControlAsync` and `UpdateControlAsync` wrap control + readings (+ audit) in **one** `DapperUnitOfWork`. **ATOMIC.**
- **SERVER-SIDE CALCULATION:** `BuildMeasurementsSnapshot` (and the service `WeightCalculator`, density 5–35 °C lookup) computes `Peso do vidro`, averages and persists them. **CONFIRMED server-side.**
- **AGGREGATE HYDRATION:** `GetControlByIdAsync` returns control + `Leituras` + raw `ComparisonDecisionsJson`/`PreviousControlJson`/`ApprovalLogJson`. The service deserializes `comparison_decisions` into `List<PesoComparisonCmDecision>` (PesoService lines 440, 615) and builds per-CM `PesoCmComparisonRow` by loading the previous control and matching by CM (lines 689, 727–779). **Previous-comparison data IS readable after persistence** (via `GetPreviousApprovedAsync` + `GetControlByIdAsync` of the previous control + service-level CM matching).
- **WRITE PATHS:** save readings, submit, approve/reject, comparison decisions, day approval, settings — all wired to real endpoints/queries.
- **QUERY / DTO GAPS:** comparison pairing is stored as **jsonb** (`comparison_decisions`) and rehydrated by deserializing at the service layer; the repository does not expose a typed per-CM pair projection. This is a **read/DTO** consideration — the data is fully present and readable. Optional clean-baseline: promote to a relational per-CM pair table.
- **SCHEMA DEPENDENCY:** none blocking.
- **CLASSIFICATION:** **COMPLETE** (jsonb comparison pairing = non-blocking; possible clean-baseline relational promotion).
- **CODEX:** B (small read/DTO if a typed comparison DTO is desired; otherwise A).

### 1.5 Pegamentos (DES-009)

- **APPLICATION CONTRACTS:** `IPegamentoRepository`, `IJobOnProductionContextLookup`.
- **DAPPER IMPLEMENTATIONS:** `DapperPegamentoRepository`, `DapperJobOnProductionContextLookup`, `PegamentoPdfRenderer`.
- **SERVICES USING THEM:** `PegamentoService`, `PegamentoPdfService`.
- **READ METHODS:** `GetByIdAsync` (control + measurements, recomputing Ovalizacao/Media/ToleranceStatus server-side from persisted nominal + tolerance), `GetByRevisionAsync`, `GetByJobOnAsync`, `SearchAsync`, `GetMeasurementsAsync`, `GetDocumentAsync`.
- **WRITE METHODS:** `CreateAsync`, `AddMeasurementAsync` (append-only), `UpdateAsync` (tolerance/status/notas only — snapshots frozen), `UpsertDocumentAsync`.
- **TRANSACTION / UOW:** single-table writes; control insert is a single statement. No multi-table write needs a cross-domain UoW here (inheritance is read from the pinned revision via the lookup). Adequate.
- **AGGREGATE HYDRATION:** detail read (`GetByIdAsync`) fully hydrates control + measurements. **COMPLETE for detail.** List reads (`GetByRevisionAsync`/`GetByJobOnAsync`/`SearchAsync`) return controls **without** measurements (deliberate list optimization) — a caller needing per-tool status must call `GetByIdAsync`.
- **INHERITANCE / REVISION BINDING:** `job_on_id` + `job_on_revision_id` FK NOT NULL; CM/BQ/MF snapshots are inherited from the pinned revision via the production-context lookup and stored, never reselectable. **BOUND.**
- **SINGLE-DOCUMENT GUARANTEE:** `UpsertDocumentAsync` uses `UNIQUE(pegamento_controlo_id)` + `ON CONFLICT DO UPDATE` — never creates a duplicate row. **Watch item:** `ON CONFLICT DO UPDATE` will silently overwrite the metadata if invoked twice at the app layer (the "persist exactly once" rule is guarded by the service workflow, not by the Dapper upsert). Low risk; the actual PDF file on disk is the immutable artifact.
- **QUERY / DTO GAPS:** minor — bound-context read for creation (inherited CM/BQ/MF) is served by the lookup; a small display DTO for human-readable context is optional. List views without measurement summaries may want a read DTO.
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE** (minor list/read-DTO + upsert-double-invoke watch items).
- **CODEX:** B (small read/DTO).

### 1.6 Ferramentas (DES-010)

- **APPLICATION CONTRACTS:** `IFerramentasRepository`, `IFerramentasIdentityLookup`, `IFerramentasRuleLookup`, `IFerramentasPieceLookup`.
- **DAPPER IMPLEMENTATIONS:** `DapperFerramentasRepository`, `DapperFerramentasIdentityLookup`, `DapperFerramentasRuleLookup`, `DapperFerramentasPieceLookup`.
- **SERVICES USING THEM:** `FerramentasService`.
- **READ METHODS:** references (search), lotes, pieces, check rules, occurrences, utilisation readings.
- **WRITE METHODS:** create/update reference, create/update lote, register/update piece, add/update/toggle/delete(check = soft deactivate)/copy check rule, `RecordUtilisationReadingAsync` (append-only, manual SAP %), `CreateReferenceWithFirstLoteAsync` (**atomic** — reference + first lote in one UoW).
- **TRANSACTION / UOW:** atomic reference+lote creation; lot duplication + rule copy atomic. Balance-scale multi-writes are not required elsewhere.
- **AGGREGATE HYDRATION:** piecemeal — `GetReferenceByIdAsync` does not load lotes; `GetLoteByIdAsync` does not auto-load pieces/rules/occurrences/usage. Each is separately readable via dedicated methods. This matches the five-tab workspace (each tab loads its data). **Adequate**, not a blocking incomplete aggregate.
- **CURRENT STATE / LOCATION READ GAP (confirmed):** the Ferramentas read model does **not** expose the tool's current warehouse location/status (active `warehouse_stock` occupation via `tool_lote_id`). No Ferramentas method returns it; the identity lookups carry reference/lot/type only. The data exists in `warehouse_stock`. **Pure query/DTO gap** for DES-010 (and for the Job On live-tool decorator).
- **SAP UTILISATION:** manual append-only (R003/Q-001) — supported. `MapPiece` hardcodes `Status="operational"` alongside the real `Condition` (minor redundancy, cosmetic).
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE WITH READ/DTO GAP** (current-location/status projection).
- **CODEX:** B (small read DTO/query; data exists).

### 1.7 Armazém (DES-012)

- **APPLICATION CONTRACTS:** `IArmazemRepository`, `IArmazemRepairMovementPort`, `IToolIdentityResolver`.
- **DAPPER IMPLEMENTATIONS:** `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`, `FerramentasArmazemToolIdentityResolver`.
- **SERVICES USING THEM:** `ArmazemService` (+ consumed by Reparação Externa).
- **READ METHODS:** locations, active/all stock by location or tool, movement history.
- **WRITE METHODS (each atomic in one UoW):** `RegisterEntradaAsync` (stock + movement), `RegisterSaidaAsync` (release + movement, `ConcurrencyGuard`), `ReplaceOccupationAsync` (release + occupy + two movements).
- **AGGREGATE HYDRATION:** stock rows are simple; movement history is complete. **COMPLETE.**
- **1:1 OCCUPATION (hardening item A3):** `RegisterEntradaAsync` is a plain INSERT; the "location already occupied" check runs in the service before the call (check-then-insert → **TOCTOU**). The DB partial unique index `uq_warehouse_stock_active_occupation(location, tool_lote) WHERE released IS NULL` enforces only "one active occupation **of a given tool_lote** at a location", **not** "one active tool overall at a location". The 1:1 cross-tool invariant is therefore **not guaranteed under concurrency** by either the app or the DB constraint. Confirmed hardening gap (A3) — app-level `SELECT … FOR UPDATE` / `ON CONFLICT` recommended.
- **SAP-UTILISATION ALERT READ (DES-012):** the Armazém read surface does not project latest `percent_used` from `tool_usage_records` per lot. The data exists (Ferramentas-owned); a read/DTO is required for the alert card.
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE WITH READ/DTO GAP** (alert projection) + a confirmed app-atomicity hardening item (A3).
- **CODEX:** B (read/DTO for alert) ; the 1:1 TOCTOU is an app/Dapper atomicity fix (B), not schema.

### 1.8 Boquilhas (DES-011)

- **APPLICATION CONTRACTS:** `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory`.
- **DAPPER IMPLEMENTATIONS:** `DapperBoquilhasRepository`, `DapperBoquilhasUnitOfWorkFactory`.
- **SERVICES USING THEM:** `BoquilhasService`.
- **READ METHODS:** lots (by id / by reference+batch / list / count), traces (by id / active-for-lote / last / in-uow-for-movement), movements (by trace / by lote / aggregate / count / voided-ids), discrepancies, repairers/line-defaults, utilisation reading, audit.
- **WRITE METHODS (all participate in the shared UoW):** `CreateLoteAsync`, `UpdateLoteAsync`, `UpdateLifecycleStateAsync`, `InsertLifecycleEventAsync`, `CreateTraceAsync`, `CloseTraceAsync`, `ReopenTraceAsync`, `AppendReopenHistoryAsync`, `InsertMovementAsync`, `VoidMovementAsync`, `InsertUtilisationReadingAsync`, `InsertDiscrepancyAsync`/`UpdateDiscrepancyAsync`, repairers, `InsertAuditEventAsync`.
- **TRANSACTION / UOW:** multi-row flows (lot+trace+movement+audit; movement dispatch; discrepancy; void) go through `IBoquilhasUnitOfWorkFactory` → `DapperUnitOfWork` and commit/roll back **together**. **ATOMIC.**
- **AGGREGATE HYDRATION:** lot reads return the lot row; trace/movements/discrepancies/lifecycle/utilisation are loaded independently by the service/UI (Registo page tabs). All data is readable via dedicated methods. **Adequate.**
- **20→25 RULE:** movements carry `exceptional_received_qty` and open `bq_discrepancy` (non-blocking, no `AllowUnmatched` hard block) — consistent with the rule; the discrepancy read/write path is present.
- **REPAIRER VOCABULARY:** canonical `repairers`/`line_repairer_defaults` (`tool_type='BQ'`) reused, not duplicated.
- **SCHEMA DEPENDENCY:** none. Owner D1/D2 respected (no live Job On lookup; BQ external-flow hook present-but-unexercised).
- **CLASSIFICATION:** **COMPLETE.**
- **CODEX:** A (safe).

### 1.9 Tampões (DES-013)

- **APPLICATION CONTRACTS:** `ITampaoRepository`, `ITampoesUnitOfWorkFactory`.
- **DAPPER IMPLEMENTATIONS:** `DapperTampaoRepository`, `DapperTampoesUnitOfWorkFactory`.
- **SERVICES USING THEM:** `TampaoService`.
- **READ METHODS:** field defs/values, configurations (by key/id/list/by-machine), saldo, movements, machines, machine events, notes, planos.
- **WRITE METHODS (in shared UoW):** `CreateConfigurationAsync` (config + saldo), `SetSaldoAsync`, `InsertMovementAsync`, `ReplaceConfigurationMachinesAsync`, `InsertMachineEventAsync`, `AddConfigurationNoteAsync`, `CreatePlanoAsync`, `CancelPlanoAsync`, audit.
- **TRANSACTION / UOW:** balance transformations (movement + saldo + audit) are atomic in one UoW; config creation creates config+saldo atomically. **ATOMIC** as a unit.
- **LOST-UPDATE HARDENING (item A4 — confirmed gap):** `SetSaldoAsync` (DapperTampaoRepository lines 221–229) performs an **absolute rewrite**: `ON CONFLICT DO UPDATE SET enchidos = @Enchidos, por_encher = @PorEncher`, fed by `GetSaldoInTransactionAsync`, which does a plain `SELECT … WHERE` with **no `FOR UPDATE`**. Under concurrent balance transformations the read-compute-write is not row-locked and not a delta → **lost-update risk**, exactly the functional hardening item A4. The schema supports atomic deltas / row-locks; the repository should switch to delta/`FOR UPDATE`.
- **AGGREGATE HYDRATION:** configurations/planos are simple; movements/notes/events are separately readable. Saldo is returned by dedicated lookup. **Adequate.**
- **PLANNING:** non-reserving (planear ≠ reservar). Job On link is optional read-only plain uuid; never mutated.
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE** with a confirmed Dapper atomicity hardening item (A4).
- **CODEX:** B (delta/FOR UPDATE in `SetSaldoAsync`).

### 1.10 Reparação Interna (DES-014)

- **APPLICATION CONTRACTS:** `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`.
- **DAPPER IMPLEMENTATIONS:** `DapperReparacaoInternaRepository`, `DapperJobOnActiveContextLookup`.
- **SERVICES USING THEM:** `ReparacaoInternaService`.
- **READ METHODS:** `GetByIdAsync`, `GetChainRootAsync` (leftmost original), `GetChainAsync` (ordered correction chain), `ListAsync` (from/to/line/jobOn/type/number/operator/onlyCorrected).
- **WRITE METHODS:** `InsertAsync` (primary/CM·MF record + repair_event + audit in **one UoW**), `InsertRepairEventAsync`, `InsertAuditEventAsync`.
- **TRANSACTION / UOW:** primary register and correction each run in one `DapperUnitOfWork` (record + repair_event + audit). **ATOMIC.**
- **CORRECTION CHAIN:** new rows with `correction_of_id`, original preserved; chain reads implemented. **COMPLETE.**
- **COMPLETE REFERENCE:** `production_code`/`reference`/`lot_id` + `job_on_revision_id` anchor preserve the complete reference (e.g. `5447T173`); reads surface it. **PRESERVED.**
- **CRITICAL — BQ APPLICATION-CONTRACT CONTRADICTION (confirmed, extends prior DB audit R1):** contrary to the surgical app-gating assumption in the prior DB audit, the **Application/Domain layer still actively models BQ as a valid internal repair type**:
  1. `InternalRepairToolType` domain enum **includes `BQ`** (`InternalRepairToolType.cs` lines 10–15; codec docstring: "BQ is a third recordable type").
  2. `ReparacaoInternaService.ResolveEffectiveLotIdAsync` has explicit BQ handling (`if (type == InternalRepairToolType.BQ) …context.BqLotIds…`, line 386).
  3. `ReparacaoInternaService` correction line-recalibration handles `request.ToolType == InternalRepairToolType.BQ` (line 333).
  4. **HTTP surface** `Program.cs` `ParseInternalToolType` maps `"BQ"` → `InternalRepairToolType.BQ` (line 1058).
  This **contradicts** `FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md §9` ("BQ is not repairable, selectable, or processed in Reparação Interna") and the design owner-decision `34_REPARACAO_INTERNA\03_OWNER_DECISION_CM_MF_ONLY.md` ("BQ não é um tipo de Reparação interna"). The Dapper repository is **faithful** (it persists the tool type the domain passes), so this is an **Application-contract / Domain gap**, not a repository gap — but it **blocks DES-014 as designed**. BQ must be stripped from the enum/codec/service/HTTP surface and kept **context read-only only**.
- **SCHEMA DEPENDENCY:** the N22 `tool_type CHECK ('CM','MF','BQ')` widening remains a schema contradiction to revert at the clean baseline (prior audit R1). App/domain must also stop emitting BQ.
- **CLASSIFICATION:** repository **COMPLETE**; but **Application contract / Domain carries a confirmed BQ contradiction** that must be fixed for DES-014.
- **CODEX:** **C** for the BQ stripping (app contract / domain / HTTP); the DDL revert is **D/E** clean-baseline.

### 1.11 Reparação Externa (DES-015)

- **APPLICATION CONTRACTS:** `IRepairRepository`, `IRepairUnitOfWorkFactory`, `IArmazemRepairMovementPort`, `IToolPieceResolver`, `IFerramentasPieceLookup`.
- **DAPPER IMPLEMENTATIONS:** `DapperRepairRepository`, `DapperRepairUnitOfWorkFactory`, `DapperArmazemRepairMovementRepository`, `FerramentasRepairToolPieceResolver`.
- **SERVICES USING THEM:** `ReparacaoExternaService`.
- **READ METHODS:** exits (list/detail), exit items, repairers, line defaults, repairer repair-types, duplicate-open-item check.
- **WRITE METHODS:** create exit (with repairer snapshot), add/delete item, confirm pickup/return, update exit status, insert repair event, repairer CRUD, `SetRepairerRepairTypesAsync` (R004), line defaults, audit.
- **TRANSACTION / UOW — owner C **CONFIRMED ATOMIC**:** `ConfirmPickupAsync` (service lines 202–258) and `ConfirmReturnAsync` (262–321) each begin **one** `uow`, then run the Armazém physical movement (`_armazemRepair.ConfirmPickupAsync/ConfirmReturnAsync` on the **same** `uow`) AND the repair-cycle write (`_repository.ConfirmItemPickedAsync/ConfirmItemReturnedAsync` + `UpdateExitStatusAsync` + `InsertRepairEventAsync` on the same `uow`), then `uow.CommitAsync` once. **Repair-cycle + warehouse physical state commit/roll back together.** Owner B (Armazém sole owner of warehouse tables, consumed via the port, never written directly) is respected.
- **DUPLICATE OPEN-ITEM (owner F):** `ExistsItemInOpenExitAsync` is called in `AddItemCoreAsync` before insert (service line 126). **ENFORCED** (app layer, per rule).
- **AGGREGATE HYDRATION:** exit detail built by service combining `GetExitByIdAsync` + `GetExitItemsAsync` + piece-picker + repairer snapshot. **COMPLETE.**
- **OWNER A/E/G:** BQ deferred (empty surface), Cancelado status-compat, non-returning-close safe-deferred.
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE.**
- **CODEX:** A (safe).

### 1.12 História (DES-016)

- **APPLICATION CONTRACTS:** `IHistoriaRepository`, `HistoriaAuthorizationGate`.
- **DAPPER IMPLEMENTATIONS:** `DapperHistoriaRepository`.
- **SERVICES USING THEM:** `HistoriaService`.
- **READ METHODS:** `QueryAsync` (grouped-by-entity, paged, stable ordering over `audit_events`), `QueryFlatAsync` (flat detail/JSON path). **READ-ONLY only.**
- **CANONICAL SOURCE:** reads **only** `audit_events` (N01); no writes, no universal business-history table.
- **VISIBILITY (TD-24):** `BuildWhere` filters `module_id = ANY(@VisibleModules)` where `VisibleModules` = granted origin modules **+** `admin` only when the identity holds `audit.view`. Correctly applied server-side.
- **FILTERS:** query/entity-type/entity-id/module/action/actor/result/from/to + pagination. **COMPLETE** for DES-016.
- **SCHEMA DEPENDENCY:** none.
- **CLASSIFICATION:** **COMPLETE.**
- **CODEX:** A (safe).

---

## 2. Contract ↔ implementation match (aggregate summary)

Every `I*Repository` below is implemented by a concrete `Dapper*Repository`, registered in `Program.cs`, with every method implemented (verified; no `NotImplementedException`/placeholder). Status legend: **OK** / **MISSING** / **PARTIAL** / **SUSPICIOUS** / **DEAD**.

| CONTRACT | IMPLEMENTATION | METHOD COVERAGE | STATUS | NOTES |
|---|---|---|---|---|
| `IAdminRepository` | `DapperAdminRepository` | all (users/templates/audit/guarded writes/self-lockout) | **OK** | guarded + atomic lockout; X12 cosmetic DTO |
| `IInternalUserRepository` | `DapperInternalUserRepository` | all | **OK** | |
| `IJobOnRepository` | `DapperJobOnRepository` | all | **PARTIAL** | **hydration missing** (no components/fields/rows/verifications); save/duplicate not atomic; image mutation atomic |
| `IJobOnUserContextRepository` | `DapperJobOnUserContextRepository` | all | **OK** | R011 |
| `IControloSheetRepository` | `DapperControloSheetRepository` | all | **OK** | full hydration; atomic |
| `IPesoRepository` | `DapperPesoRepository` | all | **OK** | control+readings atomic; comparison jsonb read at service |
| `IPegamentoRepository` | `DapperPegamentoRepository` | all | **OK** | list reads omit measurements; doc fetched separately |
| `IFerramentasRepository` | `DapperFerramentasRepository` | all | **OK** | current-location/status read missing (DTO gap) |
| `IArmazemRepository` | `DapperArmazemRepository` | all | **OK** | 1:1 TOCTOU; usage-alert read missing |
| `IArmazemRepairMovementPort` | `DapperArmazemRepairMovementRepository` | all | **OK** | shares UoW with repair |
| `IBoquilhasRepository` | `DapperBoquilhasRepository` | all | **OK** | atomic via UoW factory |
| `ITampaoRepository` | `DapperTampaoRepository` | all | **OK** | SetSaldo absolute rewrite (A4) |
| `IReparacaoInternaRepository` | `DapperReparacaoInternaRepository` | all | **OK** | chain reads; atomic; but domain/codec/HTTP still expose BQ |
| `IRepairRepository` | `DapperRepairRepository` | all | **OK** | one-UoW pickup/return with Armazém |
| `IHistoriaRepository` | `DapperHistoriaRepository` | all | **OK** | audit_events read-only + TD-24 |
| Lookups (Ferramentas/JobOn/Controlo/ReparacaoInterna/Piece/ProductionContext) | all `Dapper*Lookup*` | all | **OK** | read-only; registered |

**No DEAD / DUPLICATED persistence path** for the same operation. The only duplicate concept is the historical **verification-occurrence split** (`tool_check_occurrences` N04 vs `job_on_verification_occurrence` N05) — a clean-baseline reconciliation item (prior audit R5), not a live competing read path.

---

## 3. Aggregate hydration — detailed verdicts

| Aggregate | Hydrated children on read? | Verdict |
|---|---|---|
| **Job On** | revisions loaded; **components/fields/CAL rows/verifications NOT loaded** | **INCOMPLETE (blocking)** |
| ControloFolha | header + items + full event history | COMPLETE |
| PesoControl | control + leituras + raw snapshots (typed compare at service) | COMPLETE |
| PegamentoControlo | control + measurements (detail); lists omit measurements | COMPLETE (detail), list DTO note |
| ToolReference/Lote | piecemeal (separate methods each) | ADEQUATE |
| WarehouseStock | simple row; movement history complete | COMPLETE |
| BqLote/Trace | piecemeal (service composes) | ADEQUATE |
| TampaoConfiguration | piecemeal (saldo/movements/notes/machines separate) | ADEQUATE |
| InternalRepairRecord | chain root/chain reads present | COMPLETE |
| RepairExit | service composes exit + items + piece + snapshot | COMPLETE |
| História | groups complete (all events of paged groups) | COMPLETE |

---

## 4. Transaction / UoW check

| Operation | Atomic? | Evidence |
|---|---|---|
| Job On save revision (+children+current-revision) | **NOT ATOMIC** | `SaveRevisionAsync` sequential independent connections |
| Job On duplication | **NOT ATOMIC** | `DuplicateAsync` many connections; **also loses children** |
| Job On image mutation | **ATOMIC** | `InsertImageMutationAsync` one UoW |
| Controlo create/update + items + events | ATOMIC | one `DapperUnitOfWork` |
| Peso control + readings + audit | ATOMIC | `DapperUnitOfWork` in create/update |
| Ferramentas reference+lote | ATOMIC | `CreateReferenceWithFirstLoteAsync` |
| Armazém stock + movement (entrada/saída/substituir) | ATOMIC | each in one UoW; Substitution release+occupy+2 movements atomic |
| Boquilhas lot+trace+movement+audit / void / discrepancy | ATOMIC | shared UoW factory |
| Tampões balance transformation | ATOMIC unit | movement + saldo + audit in one UoW; **saldo write is absolute rewrite w/o FOR UPDATE → lost-update under concurrency (A4)** |
| R.Interna register + event + audit | ATOMIC | one UoW |
| R.Externa pickup/return + Armazém movement | **ATOMIC** | one shared `uow` across repair repo + Armazém port (owner C) |
| Admin guarded writes + self-lockout | ATOMIC | one UoW + in-tx admins count |

---

## 5. Write-path check

| Module action | Real path? | Module action | Real path? |
|---|---|---|---|
| Job On create | ✅ | Job On save-new-revision | ⚠️ non-atomic |
| Job On duplicate | ❌ **loses children; non-atomic** | Job On verification confirm | ✅ |
| Job On current-open context | ✅ | Job On print/image | ⚠️ image atomic; print content empty (hydration) |
| Controlo create/edit/submit/approve/reject/reopen | ✅ all | Peso save/submit/approve/reject/decisions/day | ✅ all |
| Pegamentos create (from context) | ✅ | Pegamentos measure / finalize / document-once | ✅ (doc upsert double-invoke watch) |
| Ferramentas create ref/first-lot | ✅ | Ferramentas duplicate/create lot, check rules | ✅ |
| Ferramentas manual SAP utilisation | ✅ | Armazém entrada/saída/movement/correction | ✅ |
| Boquilhas lot/trace/movement/discrepancy/lifecycle/repairer | ✅ all | Tampões config/saldo/machine/planning/notes | ✅ all |
| R.Interna CM·MF record / correction | ✅ (BQ must be stripped) | R.Externa exit/item/pickup/return/close/event | ✅ all |

**MISSING WRITE PATH confirmed:** none new beyond **Job On duplication** (children) — the only functional write path that does not achieve its intended effect.

---

## 6. Design surface → repository map (key DES tasks)

| DES | UI region / action | Required persistence | Application service | Repository method | Available? | Complete? | New DTO/query? |
|---|---|---|---|---|---|---|---|
| 005 | Job On sheet/edit/Confirmar/print | hydrated component/field/row/verification graph | `JobOnService`/`JobOnPdfService` | `GetByIdAsync` | ❌ | ❌ **child collections empty** | hydration fix |
| 005 | Duplicate Job On | copy components/fields/rows/verifications | `JobOnService.DuplicateAsync` | `GetByIdAsync`+inserts | ❌ | ❌ **none copied** | hydration fix |
| 005 | Image surface (Q-002) | reference-scoped image | `JobOnService` image ops | `InsertImageMutationAsync` | ✅ (revision-scoped) | ⚠️ | **schema** (master reference) |
| 006 | Controlo Resumo / free-mode | sheet+items+events by jobOn/revision/from-to | `ControloSheetService` | `GetForProductionAsync`/`ListAsync` | ✅ | ✅ | none |
| 007/008 | Peso per-CM comparison | previous-approved + per-CM pairings | `PesoService` | `GetPreviousApprovedAsync`+`GetControlByIdAsync` | ✅ | ✅ (jsonb read at service) | optional typed DTO |
| 009 | Pegamentos bound context / doc | inherited CM/BQ/MF + document-once | `PegamentoService`/`PegamentoPdfService` | `GetByIdAsync`/lookup/`UpsertDocumentAsync` | ✅ | ✅ | minor display DTO |
| 010 | Ferramentas current state/location | active warehouse occupation per tool | `FerramentasService` | (none exposes it) | ❌ | ❌ | **query/DTO** (data exists) |
| 010 | Ferramentas Utilização (Q-001) | manual append-only usage | `FerramentasService` | `Record/ListUtilisationReadingsAsync` | ✅ | ✅ | none |
| 012 | Armazém SAP-usage alert | latest percent per lot | `ArmazemService` | (none exposes it) | ❌ | ❌ | **query/DTO** (data exists) |
| 011 | Boquilhas lot/movement/discrepancy | BQ schema facts | `BoquilhasService` | lot/trace/movement/discrepancy reads | ✅ | ✅ | none |
| 013 | Tampões balance/machine/planning | saldo/movements/machines/planos | `TampaoService` | `GetSaldoByConfigurationAsync`/`List…` | ✅ | ✅ (A4 hardening) | none |
| 014 | R.Interna CM·MF only | tool_type/chain/complete reference | `ReparacaoInternaService` | `InsertAsync`/`GetChain*`/`ListAsync` | ✅ | ✅ (repo) | **app/domain must strip BQ** |
| 015 | R.Externa exit/pickup/return | exit/items/one-UoW + Armazém | `ReparacaoExternaService` | coordinated UoW methods | ✅ | ✅ | none |
| 016 | História timeline/filter | audit_events grouping/filters | `HistoriaService` | `QueryAsync`/`QueryFlatAsync` | ✅ | ✅ | none |

---

## 7. DIRECT-SQL / bypass check

- **Razor Pages / controllers:** no direct SQL. The only raw `IDbConnectionFactory` usage in Web is the `BootstrapAdminCommand` CLI, which constructs a repository (no raw SQL). **SAFE.**
- **Services bypassing repositories:** none found — all module services go through their `I*Repository` ports.
- **Duplicate Dapper implementations for the same concept:** none live. The historical verification-occurrence duplication (`tool_check_occurrences` N04 vs `job_on_verification_occurrence` N05) is a **clean-baseline** reconciliation (prior audit R5), not an active competing read path.
- **Old/unused path still registered:** none.

---

## 8. Risk register (maximum 20; only real risks)

| Risk-ID | Severity | Module | Finding | Evidence | Impact | Fix category |
|---|---|---|---|---|---|---|
| R-001 | **CRITICAL** | Job On | `GetByIdAsync` does not hydrate components/fields/CAL rows/verifications → PDF, duplication, edit and Confirmar see empty children | `DapperJobOnRepository.GetByIdAsync` (only revisions) → `JobOnRevision.Components/Verifications` null | DES-005 (and Peso context) read empty aggregates after reload | **C must-fix-before-DES** |
| R-002 | **CRITICAL** | Job On | `DuplicateAsync` copies **zero** components/fields/rows/verifications (reads empty current revision) and is non-atomic | `JobOnService.DuplicateAsync` + `CopyRevisionForDuplication` over empty `Components` | Duplicated Job On loses all tool/verification content | **C must-fix** |
| R-003 | **CRITICAL** | Job On | Job On save revision is **not transactional** (revision + children + current_revision_id each on separate connections) | `JobOnService.SaveRevisionAsync` (independent repo calls) | Current revision can be partially persisted on mid-failure | **C (transaction/UoW fix)** |
| R-004 | **CRITICAL** | R.Interna | Application contract/domain/HTTP still model **BQ** as a valid internal repair type, contradicting settled CM/MF-only rule and DES-014 owner decision | `InternalRepairToolType.BQ`, service lines 333/386, `Program.cs:1058` | DES-014 cannot deliver BQ-excluded UI/behavior as specified | **C (app contract/domain fix)** |
| R-005 | **HIGH** | Job On | 4-page PDF generated from unhydrated aggregate → prints header/notes but no tool sections/CAL/verifications | `JobOnPdfService.BuildPdfData` over `revision.Components` | Print output incomplete | **C** |
| R-006 | **MEDIUM** | Tampões | `SetSaldoAsync` absolute rewrite without `FOR UPDATE`/delta → lost update under concurrent balance operations | `DapperTampaoRepository:221-229` + `GetSaldoInTransactionAsync` plain select | Balance drift under concurrency; violates atomic-delta rule | **B (Dapper fix)** |
| R-007 | **MEDIUM** | Armazém | 1:1 occupation is check-then-insert (TOCTOU); partial unique index on `(location, tool_lote)` does not enforce cross-tool 1:1 | `RegisterEntradaAsync` plain INSERT + service pre-check | Two different tools could occupy one location under race; violates 1:1 | **B (Dapper fix)** |
| R-008 | **LOW/MEDIUM** | Ferramentas | Current warehouse location/status of a tool not exposed in Ferramentas read model | No Ferramentas method returns active `warehouse_stock`; identity lookups lack it | DES-010 current-location surface + Job On live-tool decorator | **B (read/DTO)** |
| R-009 | **LOW/MEDIUM** | Armazém | Latest SAP utilisation `%` per lot not projected for the alert card | Armazém read surface only movements/stock | DES-012 alert card cannot render from Armazém alone | **B (read/DTO)** |
| R-010 | **LOW** | Peso | Comparison pairing persisted as jsonb; repository returns raw jsonb (service deserializes); no typed per-CM pair projection | `DapperPesoRepository.GetControlByIdAsync` (`ComparisonDecisionsJson`); service lines 440/615 | Clean-baseline; data readable today | **E / optional B** |
| R-011 | **LOW** | Pegamentos | List reads omit measurements; document metadata fetched separately; `UpsertDocumentAsync` uses `ON CONFLICT DO UPDATE` (double-invoke could overwrite metadata row) | `MapControlRow` empty measurements; multi-call document path | Minor read/DTO + workflow guard | **B/E** |
| R-012 | **LOW** | Job On | Schema depends on master article/reference + reference-scoped image (Q-002); cannot be satisfied by repository alone | DB audit §2.4 A/B | DES-005 image surface | **D (schema)** |
| R-013 | **LOW** | R.Interna | N22 `tool_type CHECK ('CM','MF','BQ')` widens the settled CM/MF-only rule (schema contradiction) | DB audit R1; migration N22 | Clean-baseline DDL revert; app must also stop emitting BQ | **D/E** |
| R-014 | **LOW** | Job On / Verification | Verification-occurrence concept exists in both `tool_check_occurrences` (N04) and `job_on_verification_occurrence` (N05) | DB audit R5 | Historical split; reconcile at clean baseline | **E** |

---

## 9. Query/DTO vs Dapper-fix vs schema classification of every finding

| Finding | Classification |
|---|---|
| Job On incomplete aggregate hydration | **DAPPER IMPLEMENTATION GAP** (must fix) |
| Job On save/duplicate non-atomic | **TRANSACTION/UOW GAP** (must fix) |
| Job On duplication loses children | **DAPPER IMPLEMENTATION GAP / APPLICATION GAP** (must fix) |
| R.Interna BQ still modeled | **APPLICATION CONTRACT GAP** (must fix) |
| Tampões lost update (A4) | **DAPPER IMPLEMENTATION GAP** (fix during DES) |
| Armazém 1:1 TOCTOU (A3) | **DAPPER IMPLEMENTATION GAP** (fix during DES) |
| Ferramentas current-location/status | **READ DTO / QUERY GAP** (data exists) |
| Armazém SAP-usage alert | **READ DTO / QUERY GAP** (data exists) |
| Peso comparison pairing (jsonb) | **READ DTO / QUERY GAP → CLEAN-BASELINE TECH DEBT** (readable today) |
| Pegamentos list measurements / doc | **READ DTO / QUERY GAP** (minor) |
| Admin X12 label | **UI ONLY / READ DTO** (cosmetic) |
| Controlo MCaliper-link ledger | **CLEAN-BASELINE TECH DEBT** (optional additive) |
| Job On master reference + reference image (Q-002) | **SCHEMA DEPENDENCY** |
| R.Interna N22 CHECK revert | **SCHEMA DEPENDENCY / CLEAN-BASELINE** |
| Verification occurrence duplicate (N04/N05) | **CLEAN-BASELINE TECH DEBT** |

---

## 10. Module matrix

| MODULE | APP CONTRACT | DAPPER IMPL | READ COMPLETE | WRITE COMPLETE | AGGREGATE COMPLETE | UOW/TRANSACTION | DTO GAP | SCHEMA DEP | CODEX BLOCKED | FINAL STATUS |
|---|---|---|---|---|---|---|---|---|---|---|
| Admin / Auth | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC | X12 label | – | No | **COMPLETE** |
| Job On | ✅ | ✅ (impl) | ❌ | ⚠️ | **❌** | **NOT ATOMIC** | live-tool decorator | Q-002 image | **Yes** | **BROKEN / INCOMPLETE** |
| Controlo | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC | – | – | No | **COMPLETE** |
| Peso | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC | jsonb compare read (optional typed) | – | No | **COMPLETE** |
| Pegamentos | ✅ | ✅ | ✅ (detail) | ✅ | ✅ | OK | list measurements / minor | – | No | **COMPLETE** |
| Ferramentas | ✅ | ✅ | ⚠️ | ✅ | ✅ | ATOMIC | **current-location/status** | – | No | **COMPLETE W/ READ DTO GAP** |
| Armazém | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC (1:1 TOCTOU) | **SAP-usage alert** | – | No (app fix) | **COMPLETE W/ READ DTO GAP + app hardening** |
| Boquilhas | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC | – | – | No | **COMPLETE** |
| Tampões | ✅ | ✅ | ✅ | ✅ | ✅ | ATOMIC (lost-update fix) | – | – | No (app fix) | **COMPLETE (A4 hardening)** |
| R.Interna | ⚠️ (BQ modeled) | ✅ | ✅ | ✅ | ✅ | ATOMIC | – | **N22 CHECK revert** | **Yes (BQ)** | **COMPLETE repo; APPLICATION CONTRACT GAP (BQ)** |
| R.Externa | ✅ | ✅ | ✅ | ✅ | ✅ | **ATOMIC (owner C)** | – | – | No | **COMPLETE** |
| História | ✅ | ✅ | ✅ | n/a (read-only) | ✅ | n/a | – | – | No | **COMPLETE** |

---

## 11. CODEX IMPACT

- **A. SAFE FOR CODEX DESIGN IMPLEMENTATION (no persistence work):** Controlo, Peso, Boquilhas, Reparação Externa, História, Admin/Auth, and Pegamentos (bar minor read/DTO). Most of DES‑006/007/008/011/012(page)/013(page)/015/016 can proceed.
- **B. CODEX CAN FIX DURING DES (small query/DTO/Dapper, no schema change):**
  - Ferramentas current-location/status projection (DES-010).
  - Armazém SAP-usage alert read (DES-012).
  - Tampões `SetSaldoAsync` delta/`FOR UPDATE` (DES-013 hardening A4).
  - Armazém 1:1 `SELECT … FOR UPDATE` / `ON CONFLICT` (A3).
  - Optional typed Peso comparison DTO.
- **C. MUST FIX BEFORE A DES CAN FUNCTION (missing/incomplete repository or write path):**
  - **Job On aggregate hydration** (components/fields/CAL rows/verifications) — required for PDF, duplication, edit, Confirmar, and Peso/Pegamentos context.
  - **Job On transactional save** (revision + children + current_revision_id in one UoW) and **atomic duplication**.
  - **Reparação Interna — strip BQ** from the domain enum/codec/service/HTTP surface (CM/MF only), keeping BQ context read-only.
- **D. SCHEMA-DEPENDENT:** Job On master article/reference + reference-scoped image (Q-002); R.Interna N22 `tool_type` CHECK revert.
- **E. LATER CLEAN BASELINE:** Peso jsonb comparison → relational; Pegamentos list/measurement read DTO; Controlo MCaliper-link ledger; verification-occurrence duplication (N04/N05); Audit UI label (X12) cosmetic.

---

## 12. Known high-risk checks — explicit PASS / FAIL / PARTIAL

| CHECK | Verdict | EVIDENCE | CODEX IMPACT |
|---|---|---|---|
| 1. Job On aggregate rehydration complete | **FAIL** | `GetByIdAsync` loads header+revisions only; components/fields/rows/verifications null | must fix (C) |
| 2. Duplication receives all components/fields/rows/verifications | **FAIL** | `DuplicateAsync` reads empty current revision → copies none | must fix (C) |
| 3. Job On save transactionally safe | **FAIL** | sequential independent connections; no shared UoW | must fix (C) |
| 4. Current revision cannot become partially persisted | **FAIL** | `UpdateCurrentRevisionAsync` last, unguarded, separate connection | must fix (C) |
| 5. Controlo always uses exact `job_on_revision_id` | **PASS** | FK NOT NULL, pinned write, revision-filtered read | none |
| 6. Peso calculations server-side | **PASS** | `WeightCalculator`/`BuildMeasurementsSnapshot` C# server-side, persisted | none |
| 7. Peso previous-comparison data readable after persistence | **PASS** | `GetPreviousApprovedAsync` + loading previous control + CM matching at service | none (clean-baseline jsonb) |
| 8. Pegamentos inherits CM/BQ/MF from pinned revision | **PASS** | revision-bound FK + snapshots; not reselectable | none |
| 9. Ferramentas current location/status is only a read/DTO gap | **PASS** | data in `warehouse_stock`; not projected → read/DTO | B |
| 10. Armazém latest utilisation queryable from persisted data | **PASS** | `tool_usage_records` holds it; needs read/DTO only | B |
| 11. R.Interna has no required BQ write path | **FAIL** | app/domain/HTTP still accept & process BQ (contradicts CM/MF-only) | must fix (C) |
| 12. R.Externa pickup/return atomic with Armazém | **PASS** | one shared `uow` across repair repo + Armazém port (owner C) | none |
| 13. História reads canonical audit source + visibility | **PASS** | reads only `audit_events`; TD-24 visibility server-side | none |

---

## 13. Final verdict

**ARE THE CURRENT DAPPER / REPOSITORY PATHS COMPLETE ENOUGH FOR CODEX TO IMPLEMENT THE FINAL DESIGN?**

### ❌ NO — with two decisive must-fix items and a small set of Dapper/DTO fixes

**COMPLETE MODULES:** Controlo · Peso · Pegamentos · Boquilhas · Reparação Externa · História · Admin/Auth.

**READ/DTO GAPS:** Ferramentas current-location/status (DES-010); Armazém SAP-usage alert (DES-012); Pegamentos list measurements (minor); Peso comparison typed read (optional); Admin X12 label (cosmetic). All data exists in persistence — **query/DTO only, none block the data surface**.

**DAPPER IMPLEMENTATION GAPS:** Job On aggregate hydration + atomic save/duplication (critical); Tampões `SetSaldoAsync` lost-update (A4); Armazém 1:1 TOCTOU (A3).

**APPLICATION CONTRACT GAPS:** **Reparação Interna still models BQ as a recordable internal repair type** (contradicts the settled CM/MF-only rule and DES-014 owner decision) — must be stripped at the Application/Domain/HTTP layer.

**INCOMPLETE AGGREGATES:** **Job On** (components/fields/CAL rows/verifications not hydrated by `GetByIdAsync`).

**MISSING WRITE PATHS:** **Job On duplication** (copies no child graph); **Job On save revision not transactional** (partial-persistence risk).

**TRANSACTION/UOW GAPS:** Job On save + duplication; Tampões saldo (absolute rewrite, no `FOR UPDATE`); Armazém occupation (check-then-insert).

**SCHEMA-DEPENDENT GAPS:** Job On master reference + reference-scoped image (Q-002); R.Interna N22 `tool_type CHECK` revert to `('CM','MF')`.

**CODEX-BLOCKING ISSUES:** (1) Job On aggregate hydration; (2) Job On transactional save + duplication; (3) Reparação Interna BQ stripping.

**LATER CLEAN-BASELINE ISSUES:** Peso jsonb comparison → relational; Controlo MCaliper-link ledger; verification-occurrence duplication (N04/N05); audit/event consolidation; Job On jsonb snapshot promotion; Pegamentos list/measurement DTO.

---

### TOP 10 DAPPER RISKS BEFORE CODEX

1. **Job On `GetByIdAsync` returns revisions without components/fields/CAL rows/verifications** → every consumer (PDF, duplicate, edit, Confirmar, Peso/Pegamentos context) sees an empty aggregate after reload.
2. **Job On duplication copies the empty child graph** → duplicated production has no tool/config data.
3. **Job On save revision is not atomic** → revision + a subset of children can persist while `current_revision_id` stays stale on mid-failure.
4. **Reparação Interna BQ model leak** → domain enum/codec/service/HTTP accept BQ contradicting the settled CM/MF-only rule and DES-014.
5. **Tampões `SetSaldoAsync` absolute rewrite without row-lock/delta** → lost update on concurrent balance transformations (functional hardening A4).
6. **Armazém occupation check-then-insert (TOCTOU) + (location, tool_lote) partial index** → does not guarantee cross-tool 1:1 at a location (A3).
7. **Ferramentas current-location/status not projected** → DES-010 "current state/location" surface and Job On live-tool decorator cannot render from the Ferramentas read model.
8. **Armazém SAP-usage alert not read** → DES-012 alert card has no read projection (data exists).
9. **Job On PDF generated from unhydrated aggregate** → 4-page print omits tool sections, fields, CAL rows, verifications.
10. **Schema dependencies** (Q-002 master reference + reference image; R.Interna N22 CHECK revert) → **cannot** be solved by repository code alone; required for the designed Job On image surface and the clean CM/MF-only baseline.

---

## 14. CODEX readiness

### NOT READY

The current Dapper/repository paths are **complete and correct for 9 of 12 audited modules** (Controlo, Peso, Pegamentos, Boquilhas, Reparação Externa, História, Admin/Auth, and Ferramentas/Armazém/Tampões are complete or complete-with-read-DTO). However the two most design-critical surfaces are **not**:

1. **Job On** is broken at the repository layer (incomplete aggregate hydration; non-atomic save/duplication) — precisely the historical gap the functional rules require closed for PDF, duplication, edit, Confirmar and the Peso/Pegamentos bound context. This is a **must-fix before DES-005** can function.
2. **Reparação Interna** carries a confirmed **Application-contract/Domain contradiction** (BQ still modeled as recordable) that the final design (DES-014, CM/MF-only) requires removing.

Until the Job On hydration + atomic save/duplication and the R.Interna BQ stripping are done, Codex cannot implement the final design faithfully; the remaining module work would otherwise be safe. After those two fixes (plus the small read/DTO work and the two app-hardening Dapper fixes), the persistence/repository layer would be ready for the current test version and final design implementation.

*End of audit. No application code, test, database/schema/migration, or Git object was modified to produce this report.*