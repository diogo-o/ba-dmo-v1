# BA DMO — Domain Technical Map

Pure technical domain map (MAP-01R5; final purity cleanup MAP-01R5.1).
Scope: what exists in the **Domain layer** (`src\BA.Dmo.Domain\`) and where it exists.

- This map states only what the **Domain source** establishes: types, identifiers, relationships, enums, states, domain rules, factories, invariants, module boundaries, and cross-module references.
- It does **not** explain how the business works, how another layer resolves a value, how persistence implements it, how repositories use it, or how tests validate it. Those belong to each layer's own map.

---

## Navigation Index

### Sections

- [1. Purpose](#1-purpose)
- [2. Domain Project Structure](#2-domain-project-structure)
- [3. Domain Inventory](#3-domain-inventory)
- [4. Shared Domain / Kernel](#4-shared-domain--kernel)
- [5. Job On](#5-job-on)
- [6. Controlo](#6-controlo)
- [7. Ferramentas](#7-ferramentas)
- [8. Armazém](#8-armazém)
- [9. Boquilhas](#9-boquilhas)
- [10. Reparação Interna](#10-reparação-interna)
- [11. Peso](#11-peso)
- [12. Pegamentos](#12-pegamentos)
- [13. Reparação Externa](#13-reparação-externa)
- [14. Tampões](#14-tampões)
- [15. Modules / Surfaces With No Dedicated Domain Types](#15-modules--surfaces-with-no-dedicated-domain-types)

### Global Technical Indexes

- [Identifiers](#identifiers)
- [Entities / Aggregate Roots](#entities--aggregate-roots)
- [Value Objects / Enums / States](#value-objects--enums--states)
- [Direct Cross-Module Domain References](#direct-cross-module-domain-references)
- [Domain Module Boundaries](#domain-module-boundaries)
- [Domain Technical Overlaps](#domain-technical-overlaps)
- [Sources Verified](#sources-verified)

---

## 1. Purpose

Answer: *"Which Domain type am I looking for, what is it, what does it contain, which Domain module contains it, which Domain types reference it directly, and which file do I open?"*

- `01_DOMAIN.md` is a global transversal inventory of the single Domain project: types, identifiers, relationships, module placement, and cross-module references visible in Domain source.
- It is technical navigation only. Functional flow, persistence, repository, and test behaviour belong to their own layer maps.

### Related maps (relative links, same folder)

- Registry: [00_INDEX.md](00_INDEX.md)
- Technical layers: [02_DATABASE.md](02_DATABASE.md) · [03_MIGRATIONS.md](03_MIGRATIONS.md) · [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) · [05_TESTS.md](05_TESTS.md) · [19_APPLICATION.md](19_APPLICATION.md) · [20_WEB.md](20_WEB.md)
- Module maps: [06_JOB_ON.md](06_JOB_ON.md) · [07_CONTROLO.md](07_CONTROLO.md) · [08_FERRAMENTAS.md](08_FERRAMENTAS.md) · [09_ARMAZEM.md](09_ARMAZEM.md) · [10_BOQUILHAS.md](10_BOQUILHAS.md) · [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md) · [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md) · [13_TAMPOES.md](13_TAMPOES.md) · [14_HISTORIA.md](14_HISTORIA.md) · [15_ADMIN.md](15_ADMIN.md)

---

## 2. Domain Project Structure

Project: `src\BA.Dmo.Domain\BA.Dmo.Domain.csproj` — references nothing outside itself (pure Domain).

```
src\BA.Dmo.Domain\
├─ Modules\                       ← functional module domains
│  ├─ Armazem\                    (8 files)
│  ├─ Boquilhas\                  (10 files)
│  ├─ Controlo\                   (6 files)
│  ├─ Ferramentas\                (8 files)
│  ├─ JobOn\                      (9 files)
│  ├─ Pegamentos\                 (7 files)
│  ├─ Peso\                       (8 files)
│  ├─ ReparacaoExterna\           (10 files)
│  ├─ ReparacaoInterna\           (6 files)
│  └─ Tampoes\                    (11 files)
└─ Shared\
   ├─ Access\                     (8 files — module catalog / identity / profiles / grants)
   └─ Kernel\                     (5 files — Result, DomainError, Clock)
```

`bin\` and `obj\` are build output (ignored). `Properties\AssemblyInfo.cs` declares `InternalsVisibleTo` for Infrastructure and UnitTests.

**Source-file count (Domain):** **96** `.cs` source files under `src\BA.Dmo.Domain\`, excluding `bin\`, `obj\` and `Properties\AssemblyInfo.cs` (the folder also contains the project file `BA.Dmo.Domain.csproj`). Module files: 83; Shared files: 13. *(+1 vs the previous map's 95: `Shared\Access\FunctionalProfile.cs`, see §4 and [Sources Verified](#sources-verified).)*

---

## 3. Domain Inventory

| Module / Area | Path (under `src\BA.Dmo.Domain\`) | Main Types |
|---|---|---|
| Job On | `Modules\JobOn\` | `JobOn`, `JobOnRevision`, `JobOnComponent`, `JobOnComponentField`, `JobOnComponentRow`, `JobOnVerificationOccurrence`, `JobOnFieldOption`, `ComponentFamily`, `JobOnLifecycleState(+Codec)`, `JobOnActivityResolver`, `JobOnResolution`, `JobOnResolutionKind`, `JobOnVerificationGenerator`, `VerificationFrequency`, `VerificationRule` |
| Controlo | `Modules\Controlo\` | `ControloFolha`, `ControloFolhaItem`, `ControloFolhaEvent`, `ControloFolhaProductionContext`, `ControloFolhaComponent`, `ControloFolhaItemControlEdit`, `ControloFolhaState`, `ControloFolhaDecision`, `ControloUnit`, `ControloSheetModuleCatalog` |
| Ferramentas | `Modules\Ferramentas\` | `ToolReference`, `ToolLote`, `PhysicalPiece`, `ToolCheckRule`, `ToolCheckOccurrence`, `ToolUtilisationReading`, `ToolUtilisationStatus`, `FerramentasToolType(+Codec)`, `ToolCondition(+Codec)`, `FerramentasCheckFrequency(+Codec)`, `FerramentasModuleCatalog` |
| Armazém | `Modules\Armazem\` | `WarehouseLocation`, `WarehouseMovement`, `WarehouseStock`, `WarehouseToolIdentity`, `WarehouseStockRules`, `WarehouseMovementDirection(+Codec)`, `WarehouseToolDomain`, `ArmazemLocationOccupiedException`, `ArmazemModuleCatalog` |
| Boquilhas | `Modules\Boquilhas\` | `BqLote`, `BqTrace`, `BqMovement`, `BqSaldos`, `BqDiscrepancy`, `BqLifecycleEvent`, `BqRepairer`, `BqLineRepairerDefault`, `BqUtilisationReading`, `BqCloseSnapshot`, enums/codecs, `BqRules`, `BqInventoryCalculator`, `BoquilhasModuleCatalog` |
| Reparação Interna | `Modules\ReparacaoInterna\` | `InternalRepairRecord`, `InternalRepairContext`, `InternalRepairContextResolution(+Candidate)`, `InternalRepairResolutionKind`, `InternalRepairToolType(+Codec)`, `InternalRepairRules`, `ReparacaoInternaProductionProjection`, `ReparacaoInternaModuleCatalog`, `Unit` |
| Peso | `Modules\Peso\` | `PesoControl`, `PesoLeitura`, `PesoReference`, `PesoControloAnterior`, `PesoComparisonCmDecision`, `PesoComparisonCmSnapshot`, `PesoComparisonSnapshot`, `PesoComparisonDecisionSnapshot`, `PesoValidationError`, `PesoControlState(+Codec)`, `PesoCmDecision(+Codec)`, `PesoRecordType(+Codec)`, `PesoProcesso(+Codec)`, `PesoValidator`, `ReportPathValidator`, `WeightCalculator`, `PesoModuleCatalog`, `PesoLoteRules` |
| Pegamentos | `Modules\Pegamentos\` | `PegamentoControlo`, `PegamentoMedicao`, `PegamentoDocumento`, `PegamentoProductionContext`, `PegamentoToolSnapshot`, `PegamentoComponentKey`, `PegamentoControloStatus`, `PegamentoToleranceStatus`, `PegamentoMeasurementCalculator`, `PegamentoModuleCatalog` |
| Reparação Externa | `Modules\ReparacaoExterna\` | `RepairExit`, `RepairExitItem`, `Repairer`, `RepairerSnapshot`, `LineRepairerDefault`, `RepairExitStatus(+Codec)`, `RepairType(+Codec)`, `RepairExitRules`, `RepairExitStatusMachine`, `ReparacaoExternaModuleCatalog` |
| Tampões | `Modules\Tampoes\` | `TampaoConfiguration`, `TampaoConfigurationNote`, `TampaoConfigurationKey`, `TampaoPlano`, `TampaoSaldo`, `TampaoMovement`, `TampaoFieldDef`, `TampaoFieldValue`, `TampaoMachine`, `TampaoMachineEvent`, `TampaoMovementType(+Codec)`, `TampaoBalanceKind(+Codec)`, `TampaoRules`, `TampoesModuleCatalog` |
| Shared / Access | `Shared\Access\` | `ModuleCatalog`, `ModuleDefinition`, `ModuleKind`, `Capability`, `CurrentUser`, `FunctionalProfile(+Names)`, `ICurrentUserAccessor`, `JobonModuleCatalog` |
| Shared / Kernel | `Shared\Kernel\` | `Result<TSuccess,TError>` (+`Result` factories), `DomainError`, `ErrorCategory`, `IClock`, `SystemClock` |

---

## 4. Shared Domain / Kernel

Shared types are cross-module — they are declared under `Shared\`, not by any single functional module.

### Shared Kernel (`Shared\Kernel\`)
- `Result.cs` → `Result<TSuccess, TError>` + `Result` static factories — the discriminated outcome used by every module's domain factories.
- `DomainError.cs` → `DomainError` record (Category + stable Code + Message) with factories: `Validation`, `DomainConflict`, `NotFound`, `Unauthorized`, `Forbidden`, `ConcurrencyConflict`, `BackendUnavailable`, `Unexpected`.
- `ErrorCategory.cs` → `ErrorCategory` enum.
- `IClock.cs` → `IClock` time abstraction (`UtcNow`). `SystemClock.cs` → `SystemClock.Instance`.

### Shared Access (`Shared\Access\`)
Declares access/identity Domain concepts (Admin / Users / Access / Login surface).
- `ModuleCatalog.cs` → catalog of `ModuleDefinition`; `IsCapabilityKnown`, `ContainsModule`, ordered by `CanonicalOrder`.
- `ModuleDefinition.cs` → record: `ModuleId`, `DisplayName`, `ModuleKind`, `CanonicalOrder`, `InitialRoute`, `Capabilities`.
- `ModuleKind.cs` → enum `Module` / `FunctionalArea`.
- `Capability.cs` → `"{moduleId}.{ação}"` record; `ModuleSegment` parse.
- `CurrentUser.cs` → user identity projection (`InternalUserId` + granted Modules/Capabilities).
- `ICurrentUserAccessor.cs` → per-request access point to `CurrentUser?`.
- `FunctionalProfile.cs` → ENUM `FunctionalProfile` (`Admin` / `OperatorController` / `Responsible`) + static `FunctionalProfileNames` (canonical display names `Admin` / `Operador / Controlador` / `Responsável`; `TryParse` + `DisplayName` extension). Declared as "The three and only three functional profiles in BA DMO". **CONFIRMED CURRENT — ADDED since the previous map:** absent from this map's prior verification; introduced by commit `91e049f` (2026-08-27, "Complete BA DMO restructuring and UI convergence"). USED BY `Application\Modules\Admin\AdminUserService.cs`, `Application\Shared\Access\AccessResolver.cs`, `Web\Pages\Admin\TemplateProfileStore.cs`, `Web\Pages\Admin\Templates\Edit.cshtml.cs` (Admin user/template-profile surface). Related ADMIN map: [15_ADMIN.md](15_ADMIN.md).
- `JobonModuleCatalog.cs` → Job On module id (`jobon`) + capability ids (`jobon.view/.edit/.configure/.confirmar`) + family constants (MP/MF/BQ/PU/CAL/AN/ARR/PI/CS/TP/FO). A Domain-side copy of the canonical Job On catalog constants; USED BY `Application\Modules\JobOn\JobOnService.cs`, `Application\Modules\JobOn\JobOnPdfService.cs`.

Per-module catalog constants live in each module's `*ModuleCatalog.cs` (see module sections); these mirror the shared catalog. The Domain project declares **no** `admin` / `historia` module catalogs — those ids live in the application layer: the canonical registry `CanonicalModuleCatalog` (`Application\Shared\Access\CanonicalModuleCatalog.cs`, builds the `ModuleCatalog` instance with all module ids incl. `historia`/`admin`) and `HistoriaModuleCatalog` (`Application\Modules\Historia\HistoriaModuleCatalog.cs`, História module + origin-module ids). See [19_APPLICATION.md](19_APPLICATION.md).

---

## 5. Job On

Path: `src\BA.Dmo.Domain\Modules\JobOn\`

### Types

- `JobOn`
  - Role: aggregate root; `JobOn.Id` (Guid). Fields: `ProductionCode`, `MachineCode`, `PlannedStartAt/EndAt`, `LifecycleState`, `CurrentRevisionId`, `CopiedFromJobOnId`, `ArticleReferenceId`, `ProductionFolder`, timestamps, `CancelReason`. Lifecycle methods: `TransitionTo`, `Close`, `Cancel`, `DuplicateFrom`, `IsActive`. `SaveRevision` sets `CurrentRevisionId`.
  - File: `Modules\JobOn\JobOn.cs`

- `JobOnRevision`
  - Role: immutable snapshot record. Identity `JobOnRevisionId` (Guid); parent `JobOnId`. Snapshot fields (Production/Reference/Machine/Dates/Type/Stop/Weight/Process, `Sections`, `DropCount` — serialized string snapshots in Domain code), `ImageAssetId`, `ChangeReason`, `SavedBy`, `SavedAtUtc`. `CopyToNextRevision` / `CloneWithChanges` / `CreateImageRemovalRevision` create NEW numbered revisions (saved revisions are not mutated). Holds `Components` + `Verifications`.
  - File: `Modules\JobOn\JobOnRevision.cs`

- `JobOnComponent`
  - Role: record, component per family per revision. Identity `JobOnComponentId`; parent `JobOnRevisionId`; `Family`; physical links `SourceToolId`→`ToolReference`, `SourceLotId`→`ToolLote`; reference/lot/technical-name/stock/usage snapshots; `Fields`, `Rows`, `Verifications`.
  - File: `Modules\JobOn\JobOnComponent.cs`

- `JobOnComponentField` / `JobOnComponentRow`
  - Role: typed field (`ValueText/ValueInteger/ValueDecimal/ValueBoolean/ValueDate` by `FieldKey`/`ValueType`) and CAL row (ElementLabel/Value/Unit/MachineQuantity).
  - File: `Modules\JobOn\JobOnComponentFields.cs`

- `JobOnVerificationOccurrence` / `JobOnFieldOption`
  - Role: verification occurrence (status pendente/confirmada/reposta/desativada; `CompletionSource` fixed `manual_job_on`; `SourceRuleId`→rule) and dropdown option catalog.
  - File: `Modules\JobOn\JobOnVerifications.cs`

- `VerificationFrequency` / `VerificationRule` / `JobOnVerificationGenerator`
  - Role: `VerificationFrequency` enum (OncePerLot/PerProduction) + `VerificationRule` record + `JobOnVerificationGenerator` static (materializes verification occurrences).
  - File: `Modules\JobOn\JobOnVerificationGenerator.cs`

- `JobOnLifecycleState`
  - Role: STATE enum `Rascunho/Planeado/EmFabrico/Fechado/Cancelado` + `JobOnLifecycleStateCodec` (storage text).
  - File: `Modules\JobOn\JobOnLifecycleState.cs`

- `ComponentFamily`
  - Role: ENUM `MP_CM/MF/BQ/PU/CAL/AN/ARR/PI/CS/TP/FO`.
  - File: `Modules\JobOn\ComponentFamily.cs`

- `JobOnActivityResolver` / `JobOnResolutionKind` / `JobOnResolution`
  - Role: Domain service `Resolve(candidates, at)` selects candidates with `IsActive` covering `at` — 0 → None, 1 → Single, several overlapping → Ambiguous (no auto-selection). `JobOnResolution` record (None/Single/Ambiguous) + `JobOnResolutionKind` enum.
  - File: `Modules\JobOn\JobOnActivityResolver.cs`

### Direct Domain relationships

`JobOn` → owns `JobOnRevision[]{CurrentRevision}` → owns `JobOnComponent[]` → owns `JobOnComponentField[]`, `JobOnComponentRow[]`, `JobOnVerificationOccurrence[]`. Revisions are immutable snapshots managed as a single snapshot set through the aggregate.

**Cross-module:** `JobOnComponent.SourceToolId`/`SourceLotId` → Ferramentas `ToolReference`/`ToolLote`. `JobOnVerificationOccurrence.SourceRuleId` → Ferramentas `ToolCheckRule`. `JobOnRevisionId` is referenced by **Controlo** (`ControloFolha`), **Peso** (`PesoControl`), **Pegamentos** (`PegamentoControlo`) and **Reparação Interna** (`InternalRepairRecord`, nullable).

---

## 6. Controlo

Path: `src\BA.Dmo.Domain\Modules\Controlo\`

### Types

- `ControloFolha`
  - Role: aggregate root. Identity `ControloSheetId` (Guid); `JobOnId` + `JobOnRevisionId`; `ProductionCode`, `Reference`, `MachineCode`, `DisplayId` (generated doc id `Controlo_<PROD>_<REF>_<MÁQUINA>`); `State`, decision audit fields, `Items` + `Events` collections. Methods: `Create`, `ApplyItemControls`, `Submit`, `Reopen`, `Decide`. Also defines records `ControloFolhaItemControlEdit`, `ControloFolhaEvent` (append-only history).
  - File: `Modules\Controlo\ControloFolha.cs`

- `ControloFolhaItem`
  - Role: sheet item. Identity `ControloSheetItemId`; snapshot of family (MP_CM/MF/BQ) + `SourceToolId`/`SourceLotId` + Reference/Lot/TechnicalName snapshots; control fields `Result` (OK/NOK), `Observation`, `McaliperLink` (manual). `SnapshotFromComponent`.
  - File: `Modules\Controlo\ControloFolhaItem.cs`

- `ControloFolhaProductionContext` / `ControloFolhaComponent`
  - Role: records — production context (JobOnId + exact JobOnRevisionId + ProductionCode/Reference/MachineCode + Components) and component (Family + `SourceToolId`/`SourceLotId` + snapshots).
  - File: `Modules\Controlo\ControloFolhaContext.cs`

- `ControloFolhaState` / `ControloFolhaDecision`
  - Role: STATE enum `Rascunho/Submetido/Aprovado/Rejeitado` + `ControloFolhaDecision` enum (Aprovado/Rejeitado) + `ControloFolhaStateCodec`.
  - File: `Modules\Controlo\ControloFolhaState.cs`

- `ControloUnit`
  - Role: readonly record struct (unit/no-payload result).
  - File: `Modules\Controlo\ControloUnit.cs`

- `ControloSheetModuleCatalog`
  - Role: constants: `AreaId="controlo"`, capability ids `controlo.view/.edit/.submit/.review`, standard statuses, allowed families.
  - File: `Modules\Controlo\ControloSheetModuleCatalog.cs`

### Direct Domain relationships

`ControloFolha` → owns `ControloFolhaItem[]`, `ControloFolhaEvent[]`. References `JobOnId` + `JobOnRevisionId` (Job On); items carry `SourceToolId`/`SourceLotId` snapshots.

---

## 7. Ferramentas

Path: `src\BA.Dmo.Domain\Modules\Ferramentas\`

### Types

- `ToolReference`
  - Role: master-reference Domain entity. Identity `ToolReferenceId` (Guid); `TechnicalName`, `OwnerPlant` (default `MG — Marinha Grande`). Static `Create` + `EditEditableFields` (Result-based). Holds no processo (processo belongs to the lot).
  - File: `Modules\Ferramentas\ToolReference.cs`

- `ToolLote`
  - Role: lot Domain entity. Identity `ToolLoteId` (Guid); `ToolReferenceId` parent; fields: `Qty`, `AllowedLines` (plain string-list field; the current factories require non-empty), `DrawingCode/Revision`, `Processo` (NNPB/PS), `CopiedFromToolLoteId`. Methods: `CreateInitial`, `CreateFromBase` (duplication), `EditEditableFields`.
  - File: `Modules\Ferramentas\ToolLote.cs`

- `PhysicalPiece`
  - Role: individually identified piece associated with `ToolLote`. Identity `PhysicalPieceId`; `ToolLoteId`; `Sequence`, `Number`; `Status` (operational), `Condition` (`ToolCondition` enum New/Repaired/NotRepaired/Sucatado). Methods: `Register`, `SetCondition` (reason required). Also `ToolConditionCodec`.
  - File: `Modules\Ferramentas\PhysicalPiece.cs`

- `ToolCheckRule`
  - Role: verification rule on a lot card. Identity `ToolCheckRuleId`; `ToolLoteId`; `RuleText`, `Frequency`, `Active`, `CopiedFromRuleId`. Also `FerramentasCheckFrequency` enum + codec (OncePerLot/PerProduction).
  - File: `Modules\Ferramentas\ToolCheckRule.cs`

- `ToolCheckOccurrence`
  - Role: materialized occurrence. Identity `ToolCheckOccurrenceId`; contains `ToolCheckRuleId`, `JobOnId`, `JobOnComponentId`, `Status` (default `pendente`), `CompletionSource="manual_job_on"` (fixed constant).
  - File: `Modules\Ferramentas\ToolCheckOccurrence.cs`

- `ToolUtilisationReading` / `ToolUtilisationStatus`
  - Role: append-only utilisation/life reading. Identity `ToolUsageRecordId`; `ToolLoteId`; `SapStart`, `SapEnd`, `PercentUsed` (manual from SAP), `ValueAdded`, `ValueCumulative`. Also record `ToolUtilisationStatus` (History + Latest + PercentUsed).
  - File: `Modules\Ferramentas\ToolUtilisationReading.cs`

- `FerramentasToolType`
  - Role: ENUM `CM/MF/BQ/PU/CS` + `FerramentasToolTypeCodec`. The flow types (`ToolReference`/`ToolLote`/`PhysicalPiece`) are generic over this enum.
  - File: `Modules\Ferramentas\FerramentasToolType.cs`

- `FerramentasModuleCatalog`
  - Role: constants: `ModuleId`, `DefaultOwnerPlant`.
  - File: `Modules\Ferramentas\FerramentasModuleCatalog.cs`

### Direct Domain relationships

`ToolReference` 1→N `ToolLote` 1→N `PhysicalPiece`; `ToolLote` 1→N `ToolCheckRule` 1→N `ToolCheckOccurrence`; `ToolLote` 1→N `ToolUtilisationReading`.

**Cross-module:** `ToolReferenceId`/`ToolLoteId` referenced by `JobOnComponent.SourceToolId/SourceLotId` and `JobOnVerificationOccurrence.SourceRuleId`→`ToolCheckRule`. `PhysicalPieceId` referenced by `RepairExitItem` (Reparação Externa). `ToolLote.Processo` (NNPB/PS) shares the `PesoProcesso` value set (Peso).

---

## 8. Armazém

Path: `src\BA.Dmo.Domain\Modules\Armazem\`

### Types

- `WarehouseLocation`
  - Role: physical-position Domain entity. Identity `WarehouseLocationId`; `Code` (exactly 4 digits via `PositionCodeRegex`), `Kind`. Static `IsValidPositionCode`, `NormalizePositionCode`.
  - File: `Modules\Armazem\WarehouseLocation.cs`

- `WarehouseMovement`
  - Role: append-only in/out fact. Identity `WarehouseMovementId`; `WarehouseStockId`, `Direction` (`WarehouseMovementDirection` In/Out + codec), `Qty`, `Destination` (optional), `ActorId`, `OccurredAtUtc`.
  - File: `Modules\Armazem\WarehouseMovement.cs`

- `WarehouseStock`
  - Role: occupation-fact Domain entity. Identity `WarehouseStockId`; `WarehouseLocationId`, `ToolId` (domain-generic stable tool identity), `OccupiedSinceUtc/By`, `ReleasedAtUtc/By`, `IsActive`.
  - File: `Modules\Armazem\WarehouseStock.cs`

- `WarehouseToolIdentity`
  - Role: record projection `WarehouseToolIdentity(Guid ToolId, WarehouseToolDomain Domain, string Type, string Reference, string Lot, string? TechnicalName)` declared in the Armazém module. The `Domain` field discriminates the owning tool domain.
  - File: `Modules\Armazem\WarehouseToolIdentity.cs`

- `WarehouseStockRules`
  - Role: Domain service: `IsPositionOccupied`, `IsFora` (derived, never stored), `HasReferenceConflict`.
  - File: `Modules\Armazem\WarehouseStockRules.cs`

- `WarehouseToolDomain`
  - Role: ENUM `Ferramentas` / `Boquilhas`.
  - File: `Modules\Armazem\WarehouseToolDomain.cs`

- `ArmazemLocationOccupiedException`
  - Role: Domain exception (`ARMZ_POSITION_OCCUPIED`).
  - File: `Modules\Armazem\ArmazemLocationOccupiedException.cs`

- `ArmazemModuleCatalog`
  - Role: constants: `ModuleId`, `PositionCodePattern`.
  - File: `Modules\Armazem\ArmazemModuleCatalog.cs`

### Direct Domain relationships

`WarehouseLocation` 1→N `WarehouseStock` 1→N `WarehouseMovement`; 1:1 active occupation per position.

**Cross-module:** `WarehouseStock.ToolId` is a domain-generic stable tool identity; `WarehouseToolDomain` discriminates the owning tool domain (`Ferramentas` / `Boquilhas`). Reparação Externa item location uses `WarehouseLocation.Code` (position) via `RepairExitRules.HasUnknownLocation`.

---

## 9. Boquilhas

Path: `src\BA.Dmo.Domain\Modules\Boquilhas\`

### Types

- `BqLote`
  - Role: master-lot Domain entity (separate from Ferramentas `ToolLote`). Identity `BqLoteId` (Guid); `AllowedLines`, `LifecycleState`. Also `BqCloseSnapshot` (immutable final snapshot at close).
  - File: `Modules\Boquilhas\BqLote.cs`

- `BqTrace`
  - Role: production/repair trace of one lot. Identity `BqTraceId` (Guid); `BqLoteId`; `Status` (Active/Closed), `Purpose` (Production/Repair), `StartLine` (mandatory for production), `SapStart/End`, `ReopenHistory`/`DeletedMovements` (serialized history arms). Only one active trace per lot. Also `BqTraceStatus`/`BqTracePurpose` enums + codecs.
  - File: `Modules\Boquilhas\BqTrace.cs`

- `BqMovement`
  - Role: append-only fact. `Qty` null only for `linha`; `RepairerId` chosen at movement; `ExceptionalReceivedQty`. Identity `BqMovementId`.
  - File: `Modules\Boquilhas\BqMovementType.cs`

- `BqMovementType`
  - Role: ENUM `Inicio/Saida/Entrada/Irreparavel/Linha/Contagem/Fim` + codec.
  - File: `Modules\Boquilhas\BqMovementType.cs`

- `BqSaldos`
  - Role: value object / projection (Prod/Repair/Irreparable/ExceptionalReceived/TransactionalBalance + derived `PhysicalInventory`). The record keeps physical inventory and transactional balances as distinct fields.
  - File: `Modules\Boquilhas\BqSaldos.cs`

- `BqRules` / `BqInventoryCalculator`
  - Role: Domain services. `BqRules` (hard blocks BQ-RULE-001/003/005/006/007/008; quantity/utilisation validation); `BqInventoryCalculator` (return reconciliation Matched/Unmatched + `Apply` state machine).
  - File: `Modules\Boquilhas\BqRules.cs`

- `BqDiscrepancy` / `BqLifecycleEvent`
  - Role: `BqDiscrepancy` (return-excess; Expected/Actual/ExcessQty + Status/resolution) + `BqDiscrepancyStatus` enum; `BqLifecycleEvent`/`BqLifecycleEventKind` (audit: archived/scrapped/restored/retired).
  - File: `Modules\Boquilhas\BqDiscrepancy.cs`

- `BqRepairer` / `BqLineRepairerDefault`
  - Role: `BqRepairer` ENTITY (deactivated preserved) + `BqLineRepairerDefault` (Line + DefaultRepairerId + AllowedRepairerIds).
  - File: `Modules\Boquilhas\BqRepairer.cs`

- `BqUtilisationReading` / `BqUtilisationReadingKind`
  - Role: utilisation reading ENTITY + enum (Initial/Final).
  - File: `Modules\Boquilhas\BqUtilisationReading.cs`

- `BqLifecycleState`
  - Role: STATE enum `Available/Archived/Scrapped` + codec (active/preparing is DERIVED from traces, not a stored Domain state field).
  - File: `Modules\Boquilhas\BqLifecycleState.cs`

- `BoquilhasModuleCatalog`
  - Role: constants: `ModuleId`, `Lines` (B1–C3), `ReferencePattern`, `CanonicalPageSizes`, `ReferenceInvalidCode`.
  - File: `Modules\Boquilhas\BoquilhasModuleCatalog.cs`

### Direct Domain relationships

`BqLote` 1→N `BqTrace` 1→N `BqMovement` + 1→N `BqUtilisationReading`; `BqLote` 1→N `BqDiscrepancy` + `BqLifecycleEvent`; `BqRepairer` ↔ `BqLineRepairerDefault`.

**Cross-module (boundary):** `BqLote` / `BqTrace` are distinct Domain representations from Ferramentas `ToolLote`. BQ is represented by JobOn `ComponentFamily.BQ` and referenced by Reparação Externa `RepairExitItem.BqLoteId` and Pegamentos `BqSnapshot`.

---

## 10. Reparação Interna

Path: `src\BA.Dmo.Domain\Modules\ReparacaoInterna\`

### Types

- `InternalRepairRecord`
  - Role: aggregate root. Identity `InternalRepairRecordId` (Guid); `Line`, nullable production context (`JobOnId`, `JobOnRevisionId`, `ProductionCode`, `Reference`, `LotId`), `ToolType` (CM/MF), `IndividualNumber` (repeated numbers are separate records), `OperatorId`, `OccurredAtUtc`, `CorrectionOfId`/`BeforeSnapshot`/`CorrectionReason` (corrections are NEW records). Methods: `Create`, `CreateCorrection`, `IsCorrection`.
  - File: `Modules\ReparacaoInterna\InternalRepairRecord.cs`

- `InternalRepairContext` / `InternalRepairContextResolution` / `InternalRepairContextCandidate` / `InternalRepairResolutionKind`
  - Role: records/enum — `InternalRepairContext` (production context record incl. `CmLotIds`/`MfLotIds`/`BqLotIds`), `InternalRepairContextResolution` (None/Single/Ambiguous), `InternalRepairContextCandidate`, `InternalRepairResolutionKind` enum.
  - File: `Modules\ReparacaoInterna\InternalRepairContext.cs`

- `InternalRepairToolType`
  - Role: ENUM `CM`/`MF` + `InternalRepairToolTypeCodec` (BQ rejected at storage parse).
  - File: `Modules\ReparacaoInterna\InternalRepairToolType.cs`

- `InternalRepairRules` / `Unit`
  - Role: Domain service. Current code: `EvalCollectibleWhen` unconditionally returns success; `NumberInContextLot` is a non-blocking informational check. `Unit` struct.
  - File: `Modules\ReparacaoInterna\InternalRepairRules.cs`

- `ReparacaoInternaProductionProjection`
  - Role: Domain service: activation projection (most recent planned start activated at 09:00 local via `FactoryLocalOffsetUtc = +1h`). Consumes JobOn `JobOn` types (`IsActive` + `PlannedStartAt`).
  - File: `Modules\ReparacaoInterna\ReparacaoInternaProductionProjection.cs`

- `ReparacaoInternaModuleCatalog`
  - Role: constants: `ModuleId`, `CorrigirCapabilityId`, `Lines`.
  - File: `Modules\ReparacaoInterna\ReparacaoInternaModuleCatalog.cs`

### Direct Domain relationships

`InternalRepairRecord` references nullable `JobOnId`/`JobOnRevisionId`; `InternalRepairContext` carries `CmLotIds`/`MfLotIds`/`BqLotIds` lists.

---

## 11. Peso

Path: `src\BA.Dmo.Domain\Modules\Peso\`

### Types

- `PesoControl`
  - Role: aggregate root. Identity `PesoControloId`; `PesoReferenceId`, `PesoLoteId`, `RecordType`, `MoldNumber`, `NeckringNumber`, `ProductionCode`, `Line`, `Lote`, `ControlDate`, `JobOnId` + `JobOnRevisionId`, snapshot fields as named in Domain code: `CmSnapshotJson`, `MeasurementsSnapshotJson`, `ApprovalLogJson`, `PreviousControlJson`, `ComparisonDecisionsJson`; `Status`, `Revision`, approving audit, `Leituras`, `PesoNominal`, `Processo`, `ConstanteGlassUsada`, derived `PesoMedio`/`CapacidadeMedia`. Methods: `Submit`, `Approve`, `Reject`, `Reopen`, `IsDeletable`. The `...Json` suffix is the Domain property name only. Also record `PesoControloAnterior`.
  - File: `Modules\Peso\PesoControl.cs`

- `PesoLeitura` / `PesoComparisonCmDecision` / `PesoComparisonCmSnapshot` / `PesoComparisonSnapshot` / `PesoComparisonDecisionSnapshot`
  - Role: records — `PesoLeitura` (append-only reading: `CmNumber`, `PesoEmAgua`, computed `PesoVidro`) and `PesoComparisonCmDecision` (per-CM keeper/aside decision), plus the comparison-snapshot chain: `PesoComparisonCmSnapshot` (explicit current-CM ↔ previous-CM glass-weight association; `CurrentGlassWeight`/`PreviousGlassWeight`/`Difference`/`DifferencePercent`), `PesoComparisonSnapshot` (immutable identity+value snapshot persisted in `peso_controlos.previous_control`; pins `CurrentJobOnId`/`CurrentJobOnRevisionId` and `PreviousJobOnId`/`PreviousJobOnRevisionId` — reference text is never used as identity; carries `Rows: PesoComparisonCmSnapshot[]`), `PesoComparisonDecisionSnapshot` (Responsável decisions + mandatory `Justification` bound to every current CM).
  - **CONFIRMED CURRENT — ADDED since the previous map** (introduced by commit `91e049f`, 2026-08-27): the three `PesoComparison*Snapshot` records were absent from the previous verification. USED BY `Application\Modules\Peso\PesoService.cs` (comparison record type, `PesoRecordType.Comparacao`).
  - File: `Modules\Peso\PesoLeitura.cs`

- `PesoReference` / `PesoValidationError` / `PesoValidator` / `ReportPathValidator`
  - Role: `PesoReference` record (master reference; `Capacity`, `VolumeNeck`, `VolumePu`, `CaloteTp`, `ChangeLogJson`). `PesoValidationError`, `PesoValidator` (ValidateReference/ValidateLote/ValidateControlEditable), `ReportPathValidator` (rejects absolute/traversal names — the report subfolder must be a relative name).
  - File: `Modules\Peso\PesoReference.cs`

- `PesoProcesso`
  - Role: ENUM `Nnpb/Ps` + codec (process at the lot, not reference).
  - File: `Modules\Peso\PesoProcesso.cs`

- `PesoRecordType`
  - Role: ENUM `NovoControlo/Comparacao` + codec/display.
  - File: `Modules\Peso\PesoRecordType.cs`

- `PesoControlState` / `PesoCmDecision`
  - Role: STATE enum `Rascunho/Pendente/Aprovado/NaoAprovado` + codec; `PesoCmDecision` enum (None/Manter/ColocarDeParte) + codec.
  - File: `Modules\Peso\PesoControlState.cs`

- `WeightCalculator`
  - Role: Domain service / pure engine — single Peso calculation engine: water-density table 5–35°C, `VolumeFromWeight`, `EstimateGlassWeight`, `CaloteVolume`, `GlassAverage`, `DeltaVs`, `Round2`.
  - File: `Modules\Peso\WeightCalculator.cs`

- `PesoModuleCatalog` / `PesoLoteRules`
  - Role: constants: `PesoModuleId`, `PesoAprovarCapabilityId`, `ConstantNnpb`/`ConstantPs` (editable defaults), `AllowedLines`; `PesoLoteRules` (`MinAllowedLines`).
  - File: `Modules\Peso\PesoModuleCatalog.cs`

### Direct Domain relationships

`PesoControl` 1→N `PesoLeitura`. The Domain project contains no `PesoLote` type — the lot is referenced by the `PesoControl.PesoLoteId` identifier field (plus the `PesoLoteRules.MinAllowedLines` constant); `PesoControl.Processo` carries the lot process (NNPB/PS).

**Cross-module:** `PesoControl.JobOnId` + `JobOnRevisionId` → Job On. `PesoControl.Processo` mirrors the `ToolLote.Processo` value set (Ferramentas). The aggregate carries `CmSnapshotJson` and `PesoLoteId`.

---

## 12. Pegamentos

Path: `src\BA.Dmo.Domain\Modules\Pegamentos\`

### Types

- `PegamentoControlo`
  - Role: aggregate root. Identity `PegamentoControloId`; `JobOnId` + immutable `JobOnRevisionId`; `ProductionCode`, `MachineCode`, `ReferenceSnapshot`; `CmSnapshot`/`BqSnapshot`/`MfSnapshot` (`PegamentoToolSnapshot`); `CmNominal`/`BqNominal`/`MfNominal`; `Tolerance` (default 0.20); `Status` (Aberto/Fechado); `Measurements` collection. Methods: `Create`, `Hydrate`, `AddMeasurement`, `UpdateEditableFields`, `Close`. Also `PegamentoControloStatus` enum, `PegamentoMedicao` entity, `PegamentoToleranceStatus` enum (Ok/Warning/Exceeded/NotEvaluable).
  - File: `Modules\Pegamentos\PegamentoControlo.cs`

- `PegamentoProductionContext`
  - Role: record (JobOnId + JobOnRevisionId + ProductionCode/MachineCode/Reference + CM/BQ/MF snapshots + nominals; `ToolSnapshots` map).
  - File: `Modules\Pegamentos\PegamentoProductionContext.cs`

- `PegamentoToolSnapshot`
  - Role: record `PegamentoToolSnapshot(Key, ReferenceSnapshot, LotSnapshot)`.
  - File: `Modules\Pegamentos\PegamentoToolSnapshot.cs`

- `PegamentoComponentKey`
  - Role: ENUM `CM/BQ/MF`.
  - File: `Modules\Pegamentos\PegamentoComponentKey.cs`

- `PegamentoMeasurementCalculator`
  - Role: Domain service / pure engine — `Ovalizacao`, `Media`, `CheckTolerance` (boundary = Exceeded).
  - File: `Modules\Pegamentos\PegamentoMeasurementCalculator.cs`

- `PegamentoDocumento`
  - Role: metadata per control (one-to-one); Filename, `OutputRootSnapshot`, `ProductionFolderSnapshot`. Identity `PegamentoDocumentoId`.
  - File: `Modules\Pegamentos\PegamentoDocumento.cs`

- `PegamentoModuleCatalog`
  - Role: constants: `ModuleId`, `DefaultTolerance`.
  - File: `Modules\Pegamentos\PegamentoModuleCatalog.cs`

### Direct Domain relationships

`PegamentoControlo.JobOnId` + `JobOnRevisionId` → Job On. The aggregate carries `CmSnapshot`/`BqSnapshot`/`MfSnapshot` (CM/MF correspond to Ferramentas `ToolLote` identity, BQ to Boquilhas `BqLote` identity). `PegamentoDocumento.OutputRootSnapshot`/`ProductionFolderSnapshot` relate to Job On `ProductionFolder`.

---

## 13. Reparação Externa

Path: `src\BA.Dmo.Domain\Modules\ReparacaoExterna\`

### Types

- `RepairExit`
  - Role: aggregate root. Identity `RepairExitId`; `RepairType`, `RepairerId` + `RepairerSnapshot`, `PlannedDate`, `Status`, `Items`. Methods: `Create`, `IsPreparing`, `IsOpen`, static `ValidateNotAlreadyInOpenExit` (hard block).
  - File: `Modules\ReparacaoExterna\RepairExit.cs`

- `RepairExitItem`
  - Role: exit item. Identity `RepairExitItemId`; `RepairExitId`; CM/MF → `PhysicalPieceId`+`IndividualNumber`; BQ → `BqLoteId`+`Qty`; out/in facts (`OutAtUtc`/`OutOperatorId`, `InAtUtc`/`InOperatorId`, `Picked`, `Status`). Methods: `CreateCmMf`, `ConfirmPickedOut`, `ConfirmReturned` (idempotent).
  - File: `Modules\ReparacaoExterna\RepairExitItem.cs`

- `Repairer`
  - Role: repairer Domain entity. Members: `RepairerId`, `Name`, `Active`, `SupportedTypes`.
  - File: `Modules\ReparacaoExterna\Repairer.cs`

- `RepairerSnapshot`
  - Role: record `RepairerSnapshot(RepairerId, Name, Active)` (immutable per-send snapshot).
  - File: `Modules\ReparacaoExterna\RepairerSnapshot.cs`

- `LineRepairerDefault`
  - Role: default repairer per line + tool type. Natural identity is `Line` + `ToolType`; the type has no GUID identifier.
  - File: `Modules\ReparacaoExterna\LineRepairerDefault.cs`

- `RepairExitStatus`
  - Role: STATE enum `Preparacao/ARetirar/Enviado/RetornoParcial/Concluido/Cancelado` + codec.
  - File: `Modules\ReparacaoExterna\RepairExitStatus.cs`

- `RepairType`
  - Role: ENUM `BQ/CM/MF` + codec (CM and MF distinct).
  - File: `Modules\ReparacaoExterna\RepairType.cs`

- `RepairExitStatusMachine`
  - Role: Domain service — methods `ConfirmPickup`/`ConfirmReturn`.
  - File: `Modules\ReparacaoExterna\RepairExitStatusMachine.cs`

- `RepairExitRules`
  - Role: Domain service — duplicate-in-open-exit and return-without-exit hard blocks; unknown-location warning.
  - File: `Modules\ReparacaoExterna\RepairExitRules.cs`

- `ReparacaoExternaModuleCatalog`
  - Role: constants: `ModuleId`, `RepairTypes`.
  - File: `Modules\ReparacaoExterna\ReparacaoExternaModuleCatalog.cs`

### Direct Domain relationships

`RepairExit` 1→N `RepairExitItem`; `Repairer` / `RepairerSnapshot`; `LineRepairerDefault` per line+type.

**Cross-module:** CM/MF items reference Ferramentas `PhysicalPiece`; BQ items reference Boquilhas `BqLoteId`; item location uses Armazém `WarehouseLocation.Code` (informational); repairer representation parallels Boquilhas `BqRepairer`.

---

## 14. Tampões

Path: `src\BA.Dmo.Domain\Modules\Tampoes\`

### Types

- `TampaoConfiguration` / `TampaoConfigurationKey`
  - Role: configuration entity. Identity `TampaoConfigurationId` (Guid); `Values` (ordered field-name→normalized-value map; `TampaoConfigurationKey.Serialize` builds a deterministic canonical key), `Active`. `DiffersFrom`. Also `TampaoConfigurationKey` (canonical key serializer).
  - File: `Modules\Tampoes\TampaoConfiguration.cs`

- `TampaoSaldo`
  - Role: value object (Enchidos/PorEncher balances, both ≥ 0; `Get`, `IsNonNegative`).
  - File: `Modules\Tampoes\TampaoSaldo.cs`

- `TampaoMovement`
  - Role: immutable append-only fact (Adicionar/Remover/AlterarEstado/AlterarConfiguracao; origin/destination config ids, `BalancesBefore/After`, `ActorId`, `OccurredAtUtc`, `IsSingleBalance`). Identity `TampaoMovementId`.
  - File: `Modules\Tampoes\TampaoMovement.cs`

- `TampaoMovementType`
  - Role: ENUM `Adicionar/Remover/AlterarEstado/AlterarConfiguracao` + codec.
  - File: `Modules\Tampoes\TampaoMovementType.cs`

- `TampaoBalanceKind`
  - Role: ENUM `Enchidos/PorEncher` + codec.
  - File: `Modules\Tampoes\TampaoBalanceKind.cs`

- `TampaoFieldDef` / `TampaoFieldValue`
  - Role: `TampaoFieldDef` (configurable comparable field) + `TampaoFieldValue` (normalized available value; natural identity `TampaoFieldDefId` + `ValueNumeric`).
  - File: `Modules\Tampoes\TampaoFieldDef.cs` / `Modules\Tampoes\TampaoFieldValue.cs`

- `TampaoPlano`
  - Role: planned need; optional unambiguous `JobOnId`; `Canceled` preserves the record. Identity `TampaoPlanoId`.
  - File: `Modules\Tampoes\TampaoPlano.cs`

- `TampaoMachine` / `TampaoConfigurationNote` / `TampaoMachineEvent`
  - Role: `TampaoMachine` constants/validator (B1–C3) + `TampaoConfigurationNote` (append-only note) + `TampaoMachineEvent` (append-only machine-association audit).
  - File: `Modules\Tampoes\TampaoMachine.cs`

- `TampaoRules`
  - Role: Domain service (hard blocks: negative balance, destination=origin, invalid quantity, insufficient origin, no characteristic changed; `ValidateQuantity`, `ApplySingleBalanceChange`, `ResolveStateOrigin`, `ApplyBalanceTransfer`, `ValidateConfigurationTransform`, `NormalizeValue`).
  - File: `Modules\Tampoes\TampaoRules.cs`

- `TampoesModuleCatalog`
  - Role: constants: `ModuleId`, default fields (`Diâmetro`, `Profundidade/Calote`).
  - File: `Modules\Tampoes\TampoesModuleCatalog.cs`

### Direct Domain relationships

`TampaoConfiguration` ↔ `TampaoSaldo` (1:N) + `TampaoMovement` (immutable history) + `TampaoConfigurationNote` + `TampaoMachineEvent`; `TampaoFieldDef` 1→N `TampaoFieldValue`; `TampaoPlano` → `TampaoConfigurationId` (+ optional `JobOnId`).

**Cross-module:** `TampaoPlano.JobOnId` is an optional unambiguous Job On link (read-only reference). `TampaoRules` applies balance changes through `ApplySingleBalanceChange` / `ApplyBalanceTransfer`.

---

## 15. Modules / Surfaces With No Dedicated Domain Types

Checked against the current Domain tree for every canonical functional module (10) and transversal/system surface (3) declared in [00_INDEX.md](00_INDEX.md):

| Module / Surface | Category | Dedicated Domain folder/types? | Notes |
|---|---|---|---|
| Job On | Canonical functional module | YES | `Modules\JobOn\` |
| Controlo | Canonical functional module | YES | `Modules\Controlo\` |
| Ferramentas | Canonical functional module | YES | `Modules\Ferramentas\` |
| Armazém | Canonical functional module | YES | `Modules\Armazem\` |
| Boquilhas | Canonical functional module | YES | `Modules\Boquilhas\` |
| Reparação Interna | Canonical functional module | YES | `Modules\ReparacaoInterna\` |
| Reparação Externa | Canonical functional module | YES | `Modules\ReparacaoExterna\` |
| Tampões | Canonical functional module | YES | `Modules\Tampoes\` |
| História | Canonical functional module | **NO DEDICATED DOMAIN TYPES FOUND** | No `audit`/event Domain entity exists in the Domain project; no `Modules\Historia\`. Mapped in [14_HISTORIA.md](14_HISTORIA.md); its module constants live in `src\BA.Dmo.Application\Modules\Historia\HistoriaModuleCatalog.cs` (**Application, not Domain**). |
| Admin | Canonical functional module | **NO DEDICATED DOMAIN TYPES FOUND** | Access/identity surface is served by `Shared\Access\` (ModuleCatalog, CurrentUser, Capability, FunctionalProfile, …) and module `*ModuleCatalog` constants. No `Modules\Admin\`. Mapped in [15_ADMIN.md](15_ADMIN.md). |
| Users / Access | Transversal / system surface | **NO DEDICATED DOMAIN TYPES FOUND** | Identity/grants served by `Shared\Access\`. No `Modules\UsersAccess\`. NOT a canonical functional module. Mapped in [16_USERS_ACCESS.md](16_USERS_ACCESS.md). |
| Design Laboratório | Transversal / system surface | **NO DEDICATED DOMAIN TYPES FOUND** | No `Modules\DesignLaboratorio\`. NOT a canonical functional module. Mapped in [17_DESIGN_LABORATORIO.md](17_DESIGN_LABORATORIO.md). |
| Login | Transversal / system surface | **NO DEDICATED DOMAIN TYPES FOUND** | Identity/auth surface served by `Shared\Access\` (CurrentUser/ICurrentUserAccessor). No `Modules\Login\`. NOT a canonical functional module. Mapped in [18_LOGIN.md](18_LOGIN.md). |

The table covers the 10 canonical functional modules plus the 3 transversal/system surfaces (Users / Access, Design Laboratório, Login); the surfaces are NOT canonical functional modules. Per the binding INDEX, **História (#9) and Admin (#10) are canonical modules with no Domain module** — the consequence is documented by their module maps [14_HISTORIA.md](14_HISTORIA.md) and [15_ADMIN.md](15_ADMIN.md) (and the application layers: História/Admin constants in Application, not Domain).

**Controlo internal areas with dedicated Domain types:** In the Domain project source, the Controlo internal
areas **Peso** and **Pegamentos** each have their own dedicated Domain module folder and types —
`Modules\Peso\` (`PesoControl`, `PesoLeitura`, `PesoReference`, `WeightCalculator`, `PesoRecordType` incl.
`Comparacao`, …) and `Modules\Pegamentos\` (`PegamentoControlo`, `PegamentoMedicao`, …). Per the binding INDEX,
these are **internal areas of the Controlo canonical module** — NOT canonical mapping modules and NOT top-level
mapping entries. They are inventoried here as Controlo-internal Domain technical detail (see §6/§11/§12 and the
navigation index); they do not appear in the canonical-module inventory above.

---

## Identifiers

Global index of important Domain identifiers. Unless noted, each is a **Guid-based identifier**; IDs are described in Domain terms (what they identify), not as database PK/FK facts.

| ID Type | Identifies | Path | Direct Domain References |
|---|---|---|---|
| `JobOn.Id` | Job On aggregate root | `Modules\JobOn\JobOn.cs` | JobOnRevision, ControloFolha, PesoControl, PegamentoControlo, InternalRepairRecord, TampaoPlano, ToolCheckOccurrence |
| `JobOnRevisionId` | Immutable Job On revision snapshot | `Modules\JobOn\JobOnRevision.cs` | JobOn (CurrentRevisionId), JobOnComponent, ControloFolha, PesoControl, PegamentoControlo, InternalRepairRecord (nullable) |
| `JobOnComponentId` | Component per family per revision | `Modules\JobOn\JobOnComponent.cs` | JobOnComponentField, JobOnComponentRow, JobOnVerificationOccurrence, ToolCheckOccurrence |
| `JobOnComponentFieldId` | Typed field value | `Modules\JobOn\JobOnComponentFields.cs` | — |
| `JobOnComponentRowId` | CAL row entry | `Modules\JobOn\JobOnComponentFields.cs` | — |
| `JobOnVerificationOccurrenceId` | Verification occurrence | `Modules\JobOn\JobOnVerifications.cs` | — |
| `JobOnFieldOptionId` | Dropdown option catalog | `Modules\JobOn\JobOnVerifications.cs` | — |
| `SourceToolId` | Tool reference (Ferramentas) link | `JobOnComponent.cs` | JobOnComponent → ToolReference |
| `SourceLotId` | Tool lot (Ferramentas) link | `JobOnComponent.cs` | JobOnComponent → ToolLote |
| `SourceRuleId` | Verification rule (Ferramentas) link | `JobOnVerifications.cs` | JobOnVerificationOccurrence → ToolCheckRule |
| `ControloSheetId` | Folha de Controlo aggregate | `Modules\Controlo\ControloFolha.cs` | ControloFolhaItem, ControloFolhaEvent |
| `ControloSheetItemId` | Control sheet item | `ControloFolhaItem.cs` | — |
| `ControloSheetEventId` | Append-only event | `ControloFolha.cs` | — |
| `ToolReferenceId` | Tool reference (master) | `Modules\Ferramentas\ToolReference.cs` | ToolLote |
| `ToolLoteId` | Tool lot (physical) | `Modules\Ferramentas\ToolLote.cs` | PhysicalPiece, ToolCheckRule, ToolUtilisationReading, ToolCheckOccurrence |
| `PhysicalPieceId` | Individual numbered piece | `Modules\Ferramentas\PhysicalPiece.cs` | RepairExitItem (CM/MF) |
| `ToolCheckRuleId` | Verification rule | `ToolCheckRule.cs` | ToolCheckOccurrence, JobOnVerificationOccurrence.SourceRuleId |
| `ToolCheckOccurrenceId` | Materialized occurrence | `ToolCheckOccurrence.cs` | — (carries `JobOnId` / `JobOnComponentId`) |
| `ToolUsageRecordId` | Utilisation/life reading | `ToolUtilisationReading.cs` | — |
| `WarehouseLocationId` | Physical warehouse position | `Modules\Armazem\WarehouseLocation.cs` | WarehouseStock |
| `WarehouseMovementId` | Movement fact | `WarehouseMovement.cs` | — |
| `WarehouseStockId` | Occupation fact | `WarehouseStock.cs` | WarehouseMovement |
| `ToolId` (Warehouse) | Domain-generic stable tool identity (discriminated by `WarehouseToolDomain`) | `WarehouseStock.cs` | WarehouseStock, WarehouseToolIdentity |
| `BqLoteId` | BQ lot identity | `Modules\Boquilhas\BqLote.cs` | BqTrace, BqDiscrepancy, BqLifecycleEvent, BqCloseSnapshot, RepairExitItem.BqLoteId |
| `BqTraceId` | BQ trace | `BqTrace.cs` | BqMovement, BqUtilisationReading, BqDiscrepancy |
| `BqMovementId` | BQ movement fact | `BqMovementType.cs` | — |
| `BqDiscrepancyId` | Return-excess record | `BqDiscrepancy.cs` | — |
| `BqLifecycleEventId` | Lifecycle audit event | `BqDiscrepancy.cs` | — |
| `RepairerId` (BQ) | BQ repairer | `BqRepairer.cs` | BqMovement.RepairerId, BqLineRepairerDefault |
| `BqUtilisationReadingId` | BQ utilisation reading | `BqUtilisationReading.cs` | — |
| `InternalRepairRecordId` | Internal repair record | `Modules\ReparacaoInterna\InternalRepairRecord.cs` | CorrectionOfId (self) |
| `PesoReferenceId` | Peso master reference | `PesoReference.cs` | PesoControl |
| `PesoLoteId` | Peso lot | `PesoControl.cs` | PesoControl |
| `PesoControloId` | Peso control aggregate | `PesoControl.cs` | PesoLeitura |
| `PesoLeituraId` | Peso reading | `PesoLeitura.cs` | — |
| `PegamentoControloId` | Pegamentos aggregate | `PegamentoControlo.cs` | PegamentoMedicao, PegamentoDocumento |
| `PegamentoMedicaoId` | Measurement fact | `PegamentoControlo.cs` | — |
| `PegamentoDocumentoId` | Generated document metadata | `PegamentoDocumento.cs` | — |
| `RepairExitId` | External repair exit list | `RepairExit.cs` | RepairExitItem |
| `RepairExitItemId` | Exit list item | `RepairExitItem.cs` | — |
| `RepairerId` (RE) | Reparação Externa `Repairer` entity | `Repairer.cs` | RepairExit, RepairerSnapshot, LineRepairerDefault |
| `TampaoConfigurationId` | Tampões configuration | `TampaoConfiguration.cs` | TampaoSaldo, TampaoMovement, TampaoPlano, TampaoConfigurationNote/MachineEvent |
| `TampaoSaldoId` | Balance | `TampaoSaldo.cs` | — |
| `TampaoMovementId` | Quantity movement | `TampaoMovement.cs` | — |
| `TampaoFieldDefId` | Comparable field def | `TampaoFieldDef.cs` | TampaoFieldValue |
| `TampaoFieldValueId` | Normalized value | `TampaoFieldValue.cs` | — |
| `TampaoPlanoId` | Planned need | `TampaoPlano.cs` | — |
| `TampaoConfigurationMachineEventId` | Machine-association audit | `TampaoMachine.cs` | — |
| `TampaoConfigurationNoteId` | Append-only note | `TampaoMachine.cs` | — |
| `InternalUserId` (CurrentUser) | Authenticated internal user | `Shared\Access\CurrentUser.cs` | ICurrentUserAccessor |

---

## Entities / Aggregate Roots

Navigation table of identified entities / aggregate roots. Classification basis: `AGGREGATE ROOT` is used only where the type's own comment/factory-workflow explicitly presents it as an aggregate root. `ENTITY` and other labels are **MAPPER CLASSIFICATION** where the code does not itself declare the DDD role.

| Type | Classification | Module | Path |
|---|---|---|---|
| `JobOn` | AGGREGATE ROOT | Job On | `Modules\JobOn\JobOn.cs` |
| `JobOnRevision` | ENTITY (immutable snapshot record) | Job On | `Modules\JobOn\JobOnRevision.cs` |
| `JobOnComponent` | ENTITY (record) | Job On | `Modules\JobOn\JobOnComponent.cs` |
| `JobOnComponentField` / `JobOnComponentRow` | VALUE OBJECTS (records) | Job On | `JobOnComponentFields.cs` |
| `JobOnVerificationOccurrence` / `JobOnFieldOption` | ENTITY (records) | Job On | `JobOnVerifications.cs` |
| `ControloFolha` | AGGREGATE ROOT | Controlo | `ControloFolha.cs` |
| `ControloFolhaItem` | ENTITY | Controlo | `ControloFolhaItem.cs` |
| `ControloFolhaEvent` | VALUE / append-only fact (record) | Controlo | `ControloFolha.cs` |
| `ToolReference` | ENTITY | Ferramentas | `ToolReference.cs` |
| `ToolLote` | ENTITY | Ferramentas | `ToolLote.cs` |
| `PhysicalPiece` | ENTITY | Ferramentas | `PhysicalPiece.cs` |
| `ToolCheckRule` / `ToolCheckOccurrence` / `ToolUtilisationReading` | ENTITY | Ferramentas | `ToolCheckRule.cs` / `ToolCheckOccurrence.cs` / `ToolUtilisationReading.cs` |
| `WarehouseLocation`, `WarehouseMovement`, `WarehouseStock` | ENTITY | Armazém | `Warehouse*.cs` |
| `BqLote`, `BqTrace`, `BqMovement`, `BqDiscrepancy`, `BqLifecycleEvent`, `BqRepairer`, `BqUtilisationReading` | ENTITY | Boquilhas | `Bq*.cs` |
| `BqCloseSnapshot` | VALUE / snapshot (record) | Boquilhas | `BqLote.cs` |
| `BqSaldos` | VALUE OBJECT / projection | Boquilhas | `BqSaldos.cs` |
| `InternalRepairRecord` | AGGREGATE ROOT | Reparação Interna | `InternalRepairRecord.cs` |
| `PesoControl` | AGGREGATE ROOT | Peso | `PesoControl.cs` |
| `PesoLeitura` / `PesoReference` / `PesoComparisonCmDecision` | VALUE OBJECTS (records) | Peso | `PesoLeitura.cs` / `PesoReference.cs` |
| `PegamentoControlo` | AGGREGATE ROOT | Pegamentos | `PegamentoControlo.cs` |
| `PegamentoMedicao` | ENTITY (append-only fact) | Pegamentos | `PegamentoControlo.cs` |
| `PegamentoDocumento` | ENTITY | Pegamentos | `PegamentoDocumento.cs` |
| `RepairExit` | AGGREGATE ROOT | Reparação Externa | `RepairExit.cs` |
| `RepairExitItem` | ENTITY | Reparação Externa | `RepairExitItem.cs` |
| `Repairer` | ENTITY | Reparação Externa | `Repairer.cs` |
| `LineRepairerDefault` | ENTITY | Reparação Externa | `LineRepairerDefault.cs` |
| `RepairerSnapshot` | VALUE (record) | Reparação Externa | `RepairerSnapshot.cs` |
| `TampaoConfiguration` | ENTITY | Tampões | `TampaoConfiguration.cs` |
| `TampaoSaldo` | VALUE OBJECT | Tampões | `TampaoSaldo.cs` |
| `TampaoMovement` | ENTITY (append-only) | Tampões | `TampaoMovement.cs` |
| `TampaoFieldDef` / `TampaoFieldValue` / `TampaoPlano` / `TampaoConfigurationNote` / `TampaoMachineEvent` | ENTITY | Tampões | `Tampao*.cs` / `TampaoMachine.cs` |

---

## Value Objects / Enums / States

Global navigation table of value objects, enums and state types (codecs co-located).

| Type | Classification | Module | Path |
|---|---|---|---|
| `ComponentFamily` | ENUM | Job On | `JobOnComponent.cs` (file `ComponentFamily.cs`) |
| `JobOnLifecycleState` | STATE (enum) | Job On | `JobOnLifecycleState.cs` |
| `JobOnResolutionKind` | STATE (enum) | Job On | `JobOnActivityResolver.cs` |
| `JobOnResolution` | VALUE (record) | Job On | `JobOnActivityResolver.cs` |
| `VerificationFrequency` | ENUM | Job On | `JobOnVerificationGenerator.cs` |
| `VerificationRule` | VALUE (record) | Job On | `JobOnVerificationGenerator.cs` |
| `ControloFolhaState` | STATE (enum) | Controlo | `ControloFolhaState.cs` |
| `ControloFolhaDecision` | ENUM | Controlo | `ControloFolhaState.cs` |
| `ControloFolhaProductionContext` / `ControloFolhaComponent` / `ControloFolhaItemControlEdit` | VALUE (records) | Controlo | `ControloFolhaContext.cs` / `ControloFolha.cs` |
| `ControloUnit` | VALUE (struct) | Controlo | `ControloUnit.cs` |
| `FerramentasToolType` | ENUM | Ferramentas | `FerramentasToolType.cs` |
| `ToolCondition` | STATE (enum) | Ferramentas | `PhysicalPiece.cs` |
| `FerramentasCheckFrequency` | ENUM | Ferramentas | `ToolCheckRule.cs` |
| `ToolUtilisationStatus` | VALUE (record) | Ferramentas | `ToolUtilisationReading.cs` |
| `WarehouseMovementDirection` | ENUM | Armazém | `WarehouseMovement.cs` |
| `WarehouseToolDomain` | ENUM | Armazém | `WarehouseToolDomain.cs` |
| `WarehouseToolIdentity` | VALUE (record) | Armazém | `WarehouseToolIdentity.cs` |
| `BqLifecycleState` | STATE (enum) | Boquilhas | `BqLifecycleState.cs` |
| `BqTraceStatus` / `BqTracePurpose` | STATE / ENUM | Boquilhas | `BqTrace.cs` |
| `BqMovementType` | ENUM | Boquilhas | `BqMovementType.cs` |
| `BqDiscrepancyStatus` | STATE (enum) | Boquilhas | `BqDiscrepancy.cs` |
| `BqLifecycleEventKind` | ENUM | Boquilhas | `BqDiscrepancy.cs` |
| `BqUtilisationReadingKind` | ENUM | Boquilhas | `BqUtilisationReading.cs` |
| `InternalRepairToolType` | ENUM | Reparação Interna | `InternalRepairToolType.cs` |
| `InternalRepairResolutionKind` | STATE (enum) | Reparação Interna | `InternalRepairContext.cs` |
| `InternalRepairContext` / `InternalRepairContextResolution` / `InternalRepairContextCandidate` | VALUE (records) | Reparação Interna | `InternalRepairContext.cs` |
| `Unit` | VALUE (struct) | Reparação Interna | `InternalRepairRules.cs` |
| `PesoControlState` | STATE (enum) | Peso | `PesoControlState.cs` |
| `PesoCmDecision` | ENUM | Peso | `PesoControlState.cs` |
| `PesoRecordType` | ENUM | Peso | `PesoRecordType.cs` |
| `PesoProcesso` | ENUM | Peso | `PesoProcesso.cs` |
| `PesoValidationError` | VALUE (record) | Peso | `PesoReference.cs` |
| `PesoControloAnterior` | VALUE (record) | Peso | `PesoControl.cs` |
| `PesoComparisonCmSnapshot` / `PesoComparisonSnapshot` / `PesoComparisonDecisionSnapshot` | VALUE (records) | Peso | `PesoLeitura.cs` |
| `PegamentoComponentKey` | ENUM | Pegamentos | `PegamentoComponentKey.cs` |
| `PegamentoControloStatus` | STATE (enum) | Pegamentos | `PegamentoControlo.cs` |
| `PegamentoToleranceStatus` | ENUM | Pegamentos | `PegamentoControlo.cs` |
| `PegamentoProductionContext` / `PegamentoToolSnapshot` | VALUE (records) | Pegamentos | `PegamentoProductionContext.cs` / `PegamentoToolSnapshot.cs` |
| `RepairExitStatus` | STATE (enum) | Reparação Externa | `RepairExitStatus.cs` |
| `RepairType` | ENUM | Reparação Externa | `RepairType.cs` |
| `TampaoMovementType` | ENUM | Tampões | `TampaoMovementType.cs` |
| `TampaoBalanceKind` | ENUM | Tampões | `TampaoBalanceKind.cs` |
| `ErrorCategory` | ENUM | Shared Kernel | `Shared\Kernel\ErrorCategory.cs` |
| `DomainError` | VALUE (record) | Shared Kernel | `Shared\Kernel\DomainError.cs` |
| `Result<TSuccess,TError>` | VALUE (struct) | Shared Kernel | `Shared\Kernel\Result.cs` |
| `ModuleKind` | ENUM | Shared Access | `Shared\Access\ModuleKind.cs` |
| `FunctionalProfile` (+ `FunctionalProfileNames` static helper) | ENUM | Shared Access | `Shared\Access\FunctionalProfile.cs` |
| `ModuleDefinition` / `Capability` / `CurrentUser` | VALUE (records) | Shared Access | `Shared\Access\ModuleDefinition.cs` / `Capability.cs` / `CurrentUser.cs` |

---

## Direct Cross-Module Domain References

Direct references visible in Domain source between modules.

**Job On → Ferramentas**
- `JobOnComponent.SourceToolId` / `SourceLotId` → `ToolReference` / `ToolLote`.
- `JobOnVerificationOccurrence.SourceRuleId` → `ToolCheckRule`.

**Downstream modules → Job On (revision identifier)**
- `ControloFolha.JobOnRevisionId` → Job On revision identifier.
- `PesoControl.JobOnRevisionId` → Job On revision identifier.
- `PegamentoControlo.JobOnRevisionId` → Job On revision identifier.
- `InternalRepairRecord.JobOnRevisionId` → Job On revision identifier (nullable).
- `JobOnActivityResolver.Resolve(candidates, at)` selects candidates whose `IsActive` covers `at`, returning None / Single / Ambiguous.
- `ReparacaoInternaProductionProjection` references Job On `JobOn.IsActive` / `PlannedStartAt`.

**Job On → Boquilhas**
- `ComponentFamily.BQ` — BQ is the `BQ` value of the Job On `ComponentFamily` enum. BQ identity appears in downstream snapshot fields: `PegamentoControlo.BqSnapshot`, `InternalRepairContext.BqLotIds`, `ControloFolhaComponent` (family `BQ`).

**Ferramentas → Reparação Externa**
- `RepairExitItem.PhysicalPieceId` / `IndividualNumber` → `PhysicalPiece`.

**Ferramentas ↔ Peso**
- `ToolLote.Processo` (NNPB/PS) mirrors the `PesoProcesso` value set; `PesoControl.Processo` carries the process at the lot.

**Armazém ↔ tool domain**
- `WarehouseStock.ToolId` is a domain-generic stable tool identity; `WarehouseToolDomain` is an enum with values `Ferramentas` / `Boquilhas`.

**Boquilhas → Reparação Externa**
- `RepairExitItem.BqLoteId` → `BqLote` (`RepairType` includes `BQ` and the item type carries `BqLoteId`).

**Boquilhas ↔ Reparação Externa — parallel repairer vocabulary**
- Boquilhas and Reparação Externa contain parallel repairer representations: `BqRepairer` / `BqLineRepairerDefault` and `Repairer` / `LineRepairerDefault`. Each is a distinct Domain type set in its own module.

**Controlo / Peso / Pegamentos → Job On snapshots**
- Controlo: `ControloFolha.JobOnRevisionId`; snapshot fields on `ControloFolhaItem`. File: `Modules\Controlo\ControloFolha.cs`, `ControloFolhaItem.cs`.
- Peso: `PesoControl.JobOnRevisionId`; `CmSnapshotJson`. File: `Modules\Peso\PesoControl.cs`.
- Peso comparison: `PesoComparisonSnapshot` pins `CurrentJobOnId`/`CurrentJobOnRevisionId` and `PreviousJobOnId`/`PreviousJobOnRevisionId` (both Job On identities immutable in the snapshot). File: `Modules\Peso\PesoLeitura.cs`.
- Pegamentos: `PegamentoControlo.JobOnRevisionId`; `CmSnapshot` / `BqSnapshot` / `MfSnapshot`. File: `Modules\Pegamentos\PegamentoControlo.cs`.

---

## Application / Web Consumers (selected)

Whole-word usage of Domain main types across `src\BA.Dmo.Application\` + `src\BA.Dmo.Web\` (grep at HEAD `8478308`; file counts are approximate usage evidence, not exhaustiveness):

| Domain type | Application/Web files (count) | Main consumers |
|---|---|---|
| `Result<TSuccess,TError>` / `DomainError` | `Result` 53 · `DomainError` 34 | foundation of every Application service result |
| `JobOn` | 24 | Job On services/pages (e.g. `Application\Modules\JobOn\JobOnService.cs`) |
| `ModuleCatalog` | 7 | `Application\Shared\Access\CanonicalModuleCatalog.cs` + access resolution |
| `FunctionalProfile` | 4 | `Application\Modules\Admin\AdminUserService.cs`, `Application\Shared\Access\AccessResolver.cs`, `Web\Pages\Admin\TemplateProfileStore.cs`, Web Admin templates |
| `PegamentoControlo` | 4 | `Application\Modules\Pegamentos\PegamentoService.cs` (+ repo/PDF) |
| `CurrentUser` | 3 | access resolution / Web |
| `ControloFolha`, `ToolReference`, `ToolLote`, `BqLote`, `InternalRepairRecord`, `PesoControl`, `RepairExit`, `RepairExitItem`, `TampaoConfiguration`, `TampaoPlano`, `WarehouseLocation`, `WarehouseStock` | 2 each | per-module Application service + repository/interface |
| `JobonModuleCatalog` | 2 | `Application\Modules\JobOn\JobOnService.cs`, `JobOnPdfService.cs` |
| `PesoComparisonCmSnapshot` / `PesoComparisonSnapshot` / `PesoComparisonDecisionSnapshot` | 1 each | `Application\Modules\Peso\PesoService.cs` |

(*`ControloFolhaState` has 0 exact-word matches in Application/Web — the enum surfaces through the aggregate's `State` property and serialized JSON, not by type name.*)

---

## Domain Module Boundaries

Technical separation visible in Domain source: distinct types, enums and module placement.

- **Ferramentas vs Boquilhas:** `ToolReference` / `ToolLote` and `BqLote` / `BqTrace` are distinct Domain representations; `FerramentasToolType` admits `CM/MF/BQ/PU/CS`, while Boquilhas declares its own `BqLote`/`BqTrace` types.
- **Reparação Interna CM/MF only:** `InternalRepairToolType` admits `CM`/`MF` only (factory + codec rejection); BQ appears only as production/reference context values (e.g. `InternalRepairContext.BqLotIds`).
- **Reparação Externa vs Reparação Interna:** distinct modules and distinct aggregate roots (`RepairExit` vs `InternalRepairRecord`).
- **Armazém decoupling:** `WarehouseToolIdentity` / `WarehouseToolDomain` are declared in the Armazém module and carry no typed Ferramentas/Boquilhas reference.
- **Controlo vs Job On:** Controlo is a `FunctionalArea` (Shared/Access `ModuleKind`); `ControloFolha` carries a `JobOnRevisionId` reference and `ControloFolhaItem` snapshot fields.
- **Job On and downstream references:** **Controlo**, **Peso**, **Pegamentos**, **Reparação Interna** aggregates reference `JobOnRevisionId` and carry snapshot fields from it.

---

## Domain Technical Overlaps

Domain-only technical overlaps, recorded for navigation (not refactored).

1. **Parallel verification-frequency enums (Job On vs Ferramentas).** `VerificationFrequency` (Job On) and `FerramentasCheckFrequency` (Ferramentas) both contain `OncePerLot` and `PerProduction`. Paths: `Modules\JobOn\JobOnVerificationGenerator.cs`, `Modules\Ferramentas\ToolCheckRule.cs`.
2. **Two Domain types for the same materialized occurrence.** `JobOnVerificationOccurrence` (Job On) and `ToolCheckOccurrence` (Ferramentas) are separate types. The Job On type is produced by `JobOnVerificationGenerator`; the Ferramentas type carries `ToolCheckRuleId`, `JobOnId`, `JobOnComponentId`, `Status`, `CompletionSource`. Paths: `Modules\JobOn\JobOnVerifications.cs`, `Modules\Ferramentas\ToolCheckOccurrence.cs`.
3. **Parallel repairer types (Boquilhas vs Reparação Externa).** `BqRepairer` (Boquilhas) and `Repairer` (Reparação Externa) are separate types; both contain `SupportedTypes`. `BqLineRepairerDefault` (Boquilhas) and `LineRepairerDefault` (Reparação Externa) are separate types. Paths: `Modules\Boquilhas\BqRepairer.cs`, `Modules\ReparacaoExterna\Repairer.cs`, `LineRepairerDefault.cs`.

---

## Sources Verified

**Primary Domain source — 96 mapped `.cs` files under the module/shared Domain tree, excluding `bin\`, `obj\`, and `Properties\AssemblyInfo.cs` from the 96-file inventory**
- `Modules\Armazem\` — all 8 files.
- `Modules\Boquilhas\` — all 10 files.
- `Modules\Controlo\` — all 6 files.
- `Modules\Ferramentas\` — all 8 files.
- `Modules\JobOn\` — all 9 files.
- `Modules\Pegamentos\` — all 7 files.
- `Modules\Peso\` — all 8 files.
- `Modules\ReparacaoExterna\` — all 10 files.
- `Modules\ReparacaoInterna\` — all 6 files.
- `Modules\Tampoes\` — all 11 files.
- `Shared\Access\` — all 8 files.
- `Shared\Kernel\` — all 5 files.

**Additional project metadata belonging to the Domain project**
- `Properties\AssemblyInfo.cs`
- `BA.Dmo.Domain.csproj`

**Reconciliation delta (map verified 2026-08-23; map file last changed `f514031` 2026-08-24; current HEAD `8478308` 2026-08-27):**
- ADDED `Shared\Access\FunctionalProfile.cs` (`FunctionalProfile` enum + `FunctionalProfileNames` helper) — introduced by commit `91e049f` (2026-08-27, "Complete BA DMO restructuring and UI convergence"); absent from the previous map.
- ADDED Peso comparison snapshot records `PesoComparisonCmSnapshot`, `PesoComparisonSnapshot`, `PesoComparisonDecisionSnapshot` (inside `Modules\Peso\PesoLeitura.cs`) — same commit.
- No Domain type was renamed, moved, or removed since the previous verification; the prior 95-file inventory maps 1:1 onto the current 96-file tree plus the additions above.
- Confirmed with concrete evidence: `ComponentFamily` includes `FO` (11 members); enum member lists and aggregate members re-extracted from source.

Domain source is the sole authority for Domain facts; `Application\` / `Web\` paths are cited only as consumer (USED BY) evidence. No domain source, tests, database, or other layer file was modified.

---

*End of 01_DOMAIN.md — pure technical domain map (MAP-01R5.1). No domain source, tests, database, or other layer file was modified.*