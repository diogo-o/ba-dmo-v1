# BA DMO — Ferramentas Technical Map

MAP ID: MAP-08
Status: COMPLETE

## Navigation Index

- [1. Scope](#1-scope)
- [2. Layer Summary](#2-layer-summary)
- [3. Domain Objects](#3-domain-objects)
- [4. Application Objects](#4-application-objects)
- [5. Application Contracts / Ports](#5-application-contracts--ports)
- [6. Authorization / Catalog Objects](#6-authorization--catalog-objects)
- [7. Infrastructure Objects](#7-infrastructure-objects)
- [8. Database Objects](#8-database-objects)
- [9. Migration Touchpoints](#9-migration-touchpoints)
- [10. Web / Routes](#10-web--routes)
- [11. Static Assets](#11-static-assets)
- [12. Tests](#12-tests)
- [13. Test Doubles / Helpers](#13-test-doubles--helpers)
- [14. Direct Ferramentas References](#14-direct-ferramentas-references)
- [15. External Technical References](#15-external-technical-references)
- [16. Target-to-Layer Index](#16-target-to-layer-index)
- [17. Sources Verified](#17-sources-verified)
- [Counts](#counts)

## 1. Scope

Technical inventory of Ferramentas-specific objects across Domain, Application, Infrastructure, Database, Migrations, Web, Static Assets and Tests. Cross-layer navigation only; no end-to-end flow.

## 2. Layer Summary

| Layer | Main Ferramentas Objects | Locations |
|---|---|---|
| Domain | `ToolReference`, `ToolLote`, `PhysicalPiece`, `ToolCheckRule`, `ToolCheckOccurrence`, `ToolUtilisationReading`, `FerramentasToolType`, `FerramentasModuleCatalog` (+ codecs) | `src\BA.Dmo.Domain\Modules\Ferramentas\` |
| Application | `FerramentasService`, `FerramentasAuthorizationGate`, `FerramentasRequests`, `IFerramentasRepository`, `IFerramentasRuleLookup`, `IFerramentasIdentityLookup`, `IFerramentasPieceLookup` | `src\BA.Dmo.Application\Modules\Ferramentas\` |
| Infrastructure | `DapperFerramentasRepository`, `DapperFerramentasRuleLookup`, `DapperFerramentasIdentityLookup`, `DapperFerramentasPieceLookup` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records` (+ indexes/trigger) | `database\migrations\N04_ferramentas.sql`, `N19_tool_usage.sql` |
| Migrations | Ferramentas objects created/altered in N04, N12, N19, N25 | `database\migrations\` |
| Web | Index, Criar, Ficha Razor Pages (CS/HTML 7 files); minimal API endpoints in `Program.cs` | `src\BA.Dmo.Web\Pages\Ferramentas\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `ferramentas.js`, `ferramentas-layout.css` | `src\BA.Dmo.Web\wwwroot\scripts\`, `wwwroot\styles\modules\` |
| Tests | `FerramentasDomainTests`, `FerramentasServiceTests`, `FerramentasUtilisationServiceTests` (unit); `FerramentasWebApiTests` (integration) | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\`, `tests\BA.Dmo.IntegrationTests\Ferramentas\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\Ferramentas\` |
| Application | YES | `src\BA.Dmo.Application\Modules\Ferramentas\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperFerramentas*` |
| Web | YES | `src\BA.Dmo.Web\Pages\Ferramentas\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N04_ferramentas.sql`, `N19_tool_usage.sql`, `N25_remediation.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\`, `tests\BA.Dmo.IntegrationTests\Ferramentas\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

Namespace `BA.Dmo.Domain.Modules.Ferramentas`. All files under `src\BA.Dmo.Domain\Modules\Ferramentas\`.

| Type | Kind | Key Members | File |
|---|---|---|---|
| `FerramentasToolType` | enum | `CM`, `MF`, `BQ`, `PU`, `CS` | `FerramentasToolType.cs` |
| `FerramentasToolTypeCodec` | static codec | `ToStorage(FerramentasToolType): string`, `FromStorage(string?): FerramentasToolType` (maps `"CM"|"MF"|"BQ"|"PU"|"CS"`) | `FerramentasToolType.cs` |
| `FerramentasModuleCatalog` | static constants | `ModuleId = "ferramentas"`, `DefaultOwnerPlant = "MG — Marinha Grande"` | `FerramentasModuleCatalog.cs` |
| `ToolReference` | entity (master identity) | `ToolReferenceId:Guid`, `ToolType`, `RefCode`, `TechnicalName`, `OwnerPlant`, `CreatedAtUtc/CreatedBy`, `UpdatedAtUtc/UpdatedBy`; `Create(...)`, `EditEditableFields(...)` | `ToolReference.cs` |
| `ToolLote` | entity (lot occurrence) | `ToolLoteId:Guid`, `ToolReferenceId`, `Lote`, `Qty:int?`, `AllowedLines:IReadOnlyList<string>`, `DrawingCode`, `DrawingRevision`, `Processo`, `CopiedFromToolLoteId:Guid?`, timestamps; `CreateInitial(...)`, `CreateFromBase(...)`, `EditEditableFields(...)` | `ToolLote.cs` |
| `PhysicalPiece` | entity (numbered piece) | `PhysicalPieceId:Guid`, `ToolLoteId`, `Sequence:int`, `Number`, `Status="operational"`, `Condition:ToolCondition`, timestamps; `Register(...)`, `SetCondition(...)` | `PhysicalPiece.cs` |
| `ToolCondition` | enum | `New`, `Repaired`, `NotRepaired`, `Sucatado` | `PhysicalPiece.cs` |
| `ToolConditionCodec` | static codec | maps `new/repaired/not_repaired/sucatado`; `"operational"` → `ToolCondition.New` | `PhysicalPiece.cs` |
| `ToolCheckRule` | entity (verification rule per lot) | `ToolCheckRuleId:Guid`, `ToolLoteId`, `RuleText`, `Frequency:FerramentasCheckFrequency`, `Active=true`, `CopiedFromRuleId:Guid?`, timestamps; `Create(...)`, `Edit(...)` | `ToolCheckRule.cs` |
| `FerramentasCheckFrequency` | enum | `OncePerLot`, `PerProduction` | `ToolCheckRule.cs` |
| `FerramentasCheckFrequencyCodec` | static codec | maps `"uma_vez_no_lote"`, `"por_fabrico"` | `ToolCheckRule.cs` |
| `ToolCheckOccurrence` | read-model entity | `ToolCheckOccurrenceId`, `ToolCheckRuleId`, `JobOnId:Guid?`, `JobOnComponentId:Guid?`, `Status="pendente"`, `CompletionSource="manual_job_on"`, `CompletedBy`, `CompletedAtUtc`, timestamps | `ToolCheckOccurrence.cs` |
| `ToolUtilisationReading` | entity (append-only reading) | `ToolUsageRecordId:Guid`, `ToolLoteId`, `SapStart:decimal?`, `SapEnd:decimal?`, `PercentUsed:decimal?`, `ValueAdded:decimal?`, `ValueCumulative:decimal`, `Notes`, `ActorId`, `ReadingAtUtc` | `ToolUtilisationReading.cs` |
| `ToolUtilisationStatus` | record (history + latest) | `History:IReadOnlyList<ToolUtilisationReading>`, `Latest`, `PercentUsed:decimal?` | `ToolUtilisationReading.cs` |

Domain factory methods return `Result<T, DomainError>`. Validation error codes observed: `FERRAMENTAS_REFCODE_REQUIRED`, `FERRAMENTAS_LOTE_REQUIRED`, `FERRAMENTAS_LINES_REQUIRED`, `FERRAMENTAS_QTY_INVALID`, `FERRAMENTAS_PIECE_SEQUENCE_INVALID`, `FERRAMENTAS_PIECE_NUMBER_REQUIRED`, `FERRAMENTAS_CONDITION_REASON_REQUIRED`, `FERRAMENTAS_RULE_TEXT_REQUIRED`.

## 4. Application Objects

Namespace `BA.Dmo.Application.Modules.Ferramentas`. All files under `src\BA.Dmo.Application\Modules\Ferramentas\`.

| Type | Kind | Key Public Members | File |
|---|---|---|---|
| `FerramentasService` | application service | `CreateReferenceWithFirstLoteAsync`, `EditReferenceAsync`, `EditLoteAsync`, `CreateLoteFromBaseAsync`, `RegisterPieceAsync`, `SetConditionAsync`, `AddCheckRuleAsync`, `UpdateCheckRuleAsync`, `ToggleCheckRuleAsync`, `DeleteCheckRuleAsync`, `ListReferencesAsync`, `GetReferenceDetailAsync`, `ListLotesByReferenceAsync`, `ListPiecesByLoteAsync`, `ListCheckRulesByLoteAsync`, `ResolveActiveRulesAsync`, `RecordUtilisationReadingAsync`, `GetUtilisationAsync`. Constructor deps: `IFerramentasRepository`, `IFerramentasRuleLookup`, `FerramentasAuthorizationGate`, `IClock`. | `FerramentasService.cs` |
| `FerramentasAuthorizationGate` | capability gate | `Require(params string[] anyOfCapabilityIds): Result<FerramentasExecutor, DomainError>`. Deps: `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor`. Checks `FerramentasModuleCatalog.ModuleId` module grant + optional capability. Fails closed. | `FerramentasAuthorizationGate.cs` |
| `FerramentasExecutor` | record | `ActorId`, `DisplayName`, `CanConfigure` | `FerramentasAuthorizationGate.cs` |
| `CreateFerramentasRequest` | record | `ToolType`, `RefCode`, `TechnicalName`, `OwnerPlant`, `Lote`, `Qty`, `AllowedLines`, `DrawingCode`, `DrawingRevision`, `Processo` | `FerramentasRequests.cs` |
| `CreateLoteFromBaseRequest` | record | `BaseLoteId`, `Lote`, `Qty`, `AllowedLines`, `DrawingCode`, `DrawingRevision` | `FerramentasRequests.cs` |
| `EditFerramentasRequest` | record | `ReferenceId`, `TechnicalName`, `OwnerPlant` | `FerramentasRequests.cs` |
| `EditLoteRequest` | record | `LoteId`, `Qty`, `AllowedLines`, `DrawingCode`, `DrawingRevision` | `FerramentasRequests.cs` |
| `RegisterPieceRequest` | record | `LoteId`, `Sequence`, `Number` | `FerramentasRequests.cs` |
| `SetConditionRequest` | record | `LoteId`, `Number`, `Condition:ToolCondition`, `Reason` | `FerramentasRequests.cs` |
| `CheckRuleRequest` | record | `LoteId`, `RuleText`, `Frequency` | `FerramentasRequests.cs` |
| `ToggleRuleRequest` | record | `RuleId`, `Active` | `FerramentasRequests.cs` |
| `RecordToolUtilisationRequest` | record | `ToolLoteId`, `SapStart`, `SapEnd`, `PercentUsed`, `ValueAdded`, `ValueCumulative`, `Notes` | `FerramentasService.cs` |
| `FerramentasSearchRequest` | record | `Reference`, `TechnicalName`, `Lote`, `Drawing`, `Line`, `Processo`, `OwnerPlant` (all nullable) | `FerramentasService.cs` |
| `FerramentasReferenceItem` | record DTO | `ReferenceId`, `ToolType`, `RefCode`, `TechnicalName`, `OwnerPlant`, `Processo`, `AllowedLinesCsv`, `LotesCount` | `FerramentasRequests.cs` |
| `FerramentasReferenceDetail` | record DTO | `ReferenceId`, `ToolType`, `RefCode`, `TechnicalName`, `OwnerPlant`, `Lotes` | `FerramentasRequests.cs` |
| `FerramentasLoteItem` | record DTO | `LoteId`, `ReferenceId`, `Lote`, `Qty`, `AllowedLines`, `DrawingCode`, `DrawingRevision`, `Processo`, `CopiedFromToolLoteId` | `FerramentasRequests.cs` |
| `FerramentasPieceItem` | record DTO | `PieceId`, `LoteId`, `Sequence`, `Number`, `Status`, `Condition` | `FerramentasRequests.cs` |
| `FerramentasCheckRuleItem` | record DTO | `RuleId`, `LoteId`, `RuleText`, `Frequency`, `Active`, `CopiedFromRuleId` | `FerramentasRequests.cs` |
| `FerramentasOccurrenceItem` | record DTO | `OccurrenceId`, `RuleId`, `JobOnId`, `Status`, `CompletionSource`, `CompletedBy`, `CompletedAtUtc` | `FerramentasRequests.cs` |

## 5. Application Contracts / Ports

All under `src\BA.Dmo.Application\Modules\Ferramentas\`.

| Interface | Main Methods | Path | Implementation(s) |
|---|---|---|---|
| `IFerramentasRepository` | `CreateReferenceAsync`, `GetReferenceByIdAsync`, `GetReferenceByTypeAndCodeAsync`, `UpdateReferenceAsync`, `SearchReferencesAsync`; `CreateLoteAsync`, `GetLoteByIdAsync`, `UpdateLoteAsync`, `GetLotesByReferenceAsync`, `LoteExistsInReferenceAsync`; `RegisterPieceAsync`, `UpdatePieceAsync`, `GetPiecesByLoteAsync`; `AddCheckRuleAsync`, `UpdateCheckRuleAsync`, `ToggleCheckRuleActiveAsync`, `DeleteCheckRuleAsync`, `CopyCheckRuleAsync`, `GetCheckRulesByLoteAsync`, `GetCheckRuleByIdAsync`; `GetOccurrencesByRuleAsync`; `RecordUtilisationReadingAsync`, `ListUtilisationReadingsAsync`; `CreateReferenceWithFirstLoteAsync`; `InsertAuditEventAsync` | `IFerramentasRepository.cs` | `DapperFerramentasRepository` |
| `IFerramentasRuleLookup` | `ResolveActiveRulesAsync(Guid toolLoteId, ct): IReadOnlyList<VerificationRule>` | `IFerramentasRuleLookup.cs` | `DapperFerramentasRuleLookup` |
| `IFerramentasIdentityLookup` | `SearchAsync(FerramentasToolType, reference?, lot?, ct): IReadOnlyList<FerramentasIdentityHit>`; `ResolveAsync(toolLoteId, ct): FerramentasIdentityHit?` | `IFerramentasIdentityLookup.cs` | `DapperFerramentasIdentityLookup` |
| `IFerramentasPieceLookup` | `SearchAsync(FerramentasToolType, reference?, lot?, number?, ct): IReadOnlyList<FerramentasPieceHit>`; `ResolveAsync(physicalPieceId, ct): FerramentasPieceHit?` | `IFerramentasPieceLookup.cs` | `DapperFerramentasPieceLookup` |

Port projection records:

| Record | Members | File |
|---|---|---|
| `FerramentasIdentityHit` | `ToolReferenceId`, `ToolLoteId`, `Type:FerramentasToolType`, `Reference`, `Lot`, `TechnicalName` | `IFerramentasIdentityLookup.cs` |
| `FerramentasPieceHit` | `PhysicalPieceId`, `ToolLoteId`, `ToolReferenceId`, `Type:FerramentasToolType`, `Reference`, `Lot`, `Number`, `TechnicalName` | `IFerramentasPieceLookup.cs` |

## 6. Authorization / Catalog Objects

Literal source identifiers (module and capability ids from `CanonicalModuleCatalog`, policy names from `ModuleAuthorizationHandler`, module catalog entry):

| Constant / Identifier | Value | Source |
|---|---|---|
| `CanonicalModuleCatalog.FerramentasModuleId` | `"ferramentas"` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| `CanonicalModuleCatalog.FerramentasConfigureCapabilityId` | `"ferramentas.configure"` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| `ModulePolicies.Ferramentas` | `"BaDmo.Module.ferramentas"` | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `CapabilityPolicies.FerramentasConfigure` | `"BaDmo.Capability.ferramentas.configure"` | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `FerramentasModuleCatalog.ModuleId` | `"ferramentas"` | `src\BA.Dmo.Domain\Modules\Ferramentas\FerramentasModuleCatalog.cs` |
| Module definition (kind Module, order 40, route `/ferramentas`, capability `ferramentas.configure`) | — | `CanonicalModuleCatalog.Build()` |

Gate behavior: `FerramentasAuthorizationGate.Require()` requires the `ferramentas` module grant; rule-configuration use cases pass `CanonicalModuleCatalog.FerramentasConfigureCapabilityId` to require `ferramentas.configure`. Endpoints using module policy vs capability policy are listed in section 10.

## 7. Infrastructure Objects

Namespace `BA.Dmo.Infrastructure.Access`. All under `src\BA.Dmo.Infrastructure\Access\`. Dapper repositories consume `IDbConnectionFactory`; atomic writes use `DapperUnitOfWork`. Embedded SQL objects per class:

| Class | Implements | Constructor Deps | Embedded SQL Objects (tables) | Mapping/Hydration | Path |
|---|---|---|---|---|---|
| `DapperFerramentasRepository` | `IFerramentasRepository` | `IDbConnectionFactory` | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records`, `audit_events` | `MapReference`, `MapLote`, `MapPiece`, `MapCheckRule`, `MapOccurrence`, `MapUtilisation`, `ToReferenceParams`, `ToLoteParams` | `DapperFerramentasRepository.cs` |
| `DapperFerramentasRuleLookup` | `IFerramentasRuleLookup` | `IDbConnectionFactory` | `tool_check_rules` (active filter) | `MapFrequency` → `VerificationFrequency` | `DapperFerramentasRuleLookup.cs` |
| `DapperFerramentasIdentityLookup` | `IFerramentasIdentityLookup` | `IDbConnectionFactory` | `tool_references` join `tool_lotes` | `Map` → `FerramentasIdentityHit` | `DapperFerramentasIdentityLookup.cs` |
| `DapperFerramentasPieceLookup` | `IFerramentasPieceLookup` | `IDbConnectionFactory` | `physical_pieces` join `tool_lotes` join `tool_references` | `Map` → `FerramentasPieceHit` | `DapperFerramentasPieceLookup.cs` |

`DapperFerramentasRepository.CreateReferenceWithFirstLoteAsync` runs `INSERT tool_references` + `INSERT tool_lotes` inside one `DapperUnitOfWork.RunAsync` transaction. `DeleteCheckRuleAsync` soft-deactivates (`active = FALSE`). `InsertAuditEventAsync` writes `audit_events` with fixed `module_id='ferramentas'`, `entity_type='ferramenta'`.

## 8. Database Objects

Created by `database\migrations\N04_ferramentas.sql` and the extended later migrations. Navigation-level only.

| Object | Kind | Main Technical Role | PK / FKs / Constraints / Indexes |
|---|---|---|---|
| `tool_references` | table | master tool identity | PK `tool_reference_id`; UNIQUE `uq_tool_references_type_code (tool_type, ref_code)`; CHECK `ck_tool_references_type (tool_type IN ('CM','MF','BQ','PU','CS'))`; FK `created_by → internal_users(actor_id)` |
| `tool_lotes` | table | lot per reference | PK `tool_lote_id`; FK `tool_reference_id → tool_references`; FK `created_by → internal_users`; UNIQUE `uq_tool_lotes_reference_lote (tool_reference_id, lote)`; CHECK `ck_tool_lotes_qty (qty IS NULL OR qty >= 0)`; index `ix_tool_lotes_reference` |
| `physical_pieces` | table | numbered pieces per lot | PK `physical_piece_id`; FK `tool_lote_id → tool_lotes`; FK `created_by → internal_users`; UNIQUE `uq_physical_pieces_lote_number (tool_lote_id, number)`; CHECK `ck_physical_pieces_sequence (sequence >= 1)`; index `ix_physical_pieces_lote` |
| `tool_check_rules` | table | verification rules per lot | PK `tool_check_rule_id`; FK `tool_lote_id → tool_lotes`; FK `copied_from_rule_id → tool_check_rules`; FK `created_by → internal_users`; CHECK `ck_tool_check_rules_frequency (frequency IN ('uma_vez_no_lote','por_fabrico'))`; index `ix_tool_check_rules_lote` |
| `tool_check_occurrences` | table | rules materialized in Job On (read) | PK `tool_check_occurrence_id`; FK `tool_check_rule_id → tool_check_rules`; FK `completed_by/created_by → internal_users`; CHECK `ck_tool_check_occurrences_status (pending/confirmed/reposta/desativada)`, `ck_tool_check_occurrences_source (completion_source='manual_job_on')`, `ck_tool_check_occurrences_completed`; indexes `ix_tool_check_occurrences_rule`, `ix_tool_check_occurrences_job_on` |
| `tool_usage_records` | table (N19) | append-only utilisation readings | PK `tool_usage_record_id`; FK `tool_lote_id → tool_lotes`; FK `actor_id → internal_users`; CHECKs `ck_tool_usage_records_sap_start/sap_end/percent (0..100)`, `ck_tool_usage_records_cumulative (value_cumulative >= 0)`; index `ix_tool_usage_records_lote`; trigger `trg_tool_usage_records_append_only` → `ba_dmo_guard_append_only()` |

## 9. Migration Touchpoints

Literal SQL operations touching Ferramentas objects.

| Migration | Ferramentas Object(s) | Technical Change |
|---|---|---|
| `N04_ferramentas.sql` | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` | Creates the 5 tables + PK/UNIQUE/CHECK/FK constraints + indexes |
| `N12_rls.sql` | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` | Enables RLS; creates `ba_dmo_app_access` policy; grants CRUD to `ba_dmo_app`; revokes anon/authenticated |
| `N19_tool_usage.sql` | `tool_usage_records` | Creates table + CHECK constraints + index; creates `trg_tool_usage_records_append_only` → `ba_dmo_guard_append_only()` |
| `N25_remediation.sql` | `tool_usage_records` | Enables RLS + creates `ba_dmo_app_access` policy; revokes anon/authenticated; grants CRUD to `ba_dmo_app` |

Referenced-from (FK targets into Ferramentas tables, defined in other modules' migrations — external technical references, not Ferramentas-object changes): `N05_jobon.sql` (`source_tool_id → tool_references`, `source_lot_id → tool_lotes`, `source_rule_id → tool_check_rules`), `N08_reparacoes.sql` (`physical_piece_id → physical_pieces`), `N09_armazem.sql` (`tool_lote_id → tool_lotes`), `N23_controlo_folha.sql` (`source_tool_id → tool_references`, `source_lot_id → tool_lotes`).

## 10. Web / Routes

### Razor Pages

Namespace `BA.Dmo.Web.Pages.Ferramentas`. All under `src\BA.Dmo.Web\Pages\Ferramentas\`.

| Page | Model Class | Route | Handler | Authorization | Deps | Model Members | Files |
|---|---|---|---|---|---|---|---|
| Index (landing + CM/MF reference list) | `IndexModel` | `/ferramentas` | `OnGet()` | `ModulePolicies.Ferramentas` | `ICurrentUserAccessor` | `CanConfigure`, `FerramentasListModel(ToolType)` | `Index.cshtml`, `Index.cshtml.cs` |
| Criar (create reference + first lot) | `CriarModel` | `/ferramentas/criar` | `OnGet(string type="CM")` | `ModulePolicies.Ferramentas` | — | `ToolType` | `Criar.cshtml`, `Criar.cshtml.cs` |
| Ficha (reference card) | `FichaModel` | `/ferramentas/{id:guid}` | `OnGet(Guid id)` | `ModulePolicies.Ferramentas` | `ICurrentUserAccessor` | `ReferenceId`, `CanConfigure` | `Ficha.cshtml`, `Ficha.cshtml.cs` |
| Partial | `FerramentasListModel` | — | — | — | — | `ToolType` | `_ReferenceList.cshtml` |

All three pages render `ferramentas-layout.css` and load `ferramentas.js`; pages are data/API-driven.

### Minimal API Endpoints (`src\BA.Dmo.Web\Program.cs`)

| Route | Method | Handler target | Authorization | Program.cs line |
|---|---|---|---|---|
| `/api/ferramentas/references` | GET | `ListReferencesAsync` | `ModulePolicies.Ferramentas` | 687 |
| `/api/ferramentas/references/{referenceId:guid}` | GET | `GetReferenceDetailAsync` | `ModulePolicies.Ferramentas` | 698 |
| `/api/ferramentas/reference` | POST | `CreateReferenceWithFirstLoteAsync` | `ModulePolicies.Ferramentas` | 707 |
| `/api/ferramentas/references/{referenceId:guid}` | PUT | `EditReferenceAsync` | `ModulePolicies.Ferramentas` | 716 |
| `/api/ferramentas/references/{referenceId:guid}/lotes` | GET | `ListLotesByReferenceAsync` | `ModulePolicies.Ferramentas` | 727 |
| `/api/ferramentas/lotes/{loteId:guid}/duplicate` | POST | `CreateLoteFromBaseAsync` | `ModulePolicies.Ferramentas` | 736 |
| `/api/ferramentas/lotes/{loteId:guid}` | PUT | `EditLoteAsync` | `ModulePolicies.Ferramentas` | 745 |
| `/api/ferramentas/lotes/{loteId:guid}/pieces` | GET | `ListPiecesByLoteAsync` | `ModulePolicies.Ferramentas` | 756 |
| `/api/ferramentas/lotes/{loteId:guid}/pieces` | POST | `RegisterPieceAsync` | `ModulePolicies.Ferramentas` | 764 |
| `/api/ferramentas/lotes/{loteId:guid}/condition` | POST | `SetConditionAsync` | `ModulePolicies.Ferramentas` | 773 |
| `/api/ferramentas/lotes/{loteId:guid}/rules` | GET | `ListCheckRulesByLoteAsync` | `ModulePolicies.Ferramentas` | 782 |
| `/api/ferramentas/lotes/{loteId:guid}/rules` | POST | `AddCheckRuleAsync` | `CapabilityPolicies.FerramentasConfigure` | 790 |
| `/api/ferramentas/rules/{ruleId:guid}` | PUT | `UpdateCheckRuleAsync` | `CapabilityPolicies.FerramentasConfigure` | 798 |
| `/api/ferramentas/rules/{ruleId:guid}/toggle` | POST | `ToggleCheckRuleAsync` | `CapabilityPolicies.FerramentasConfigure` | 806 |
| `/api/ferramentas/rules/{ruleId:guid}` | DELETE | `DeleteCheckRuleAsync` | `CapabilityPolicies.FerramentasConfigure` | 814 |
| `/api/ferramentas/lotes/{loteId:guid}/rules/active` | GET | `ResolveActiveRulesAsync` | `ModulePolicies.Ferramentas` | 823 |
| `/api/ferramentas/lotes/{loteId:guid}/utilizacao` | POST | `RecordUtilisationReadingAsync` | `ModulePolicies.Ferramentas` | 1609 |
| `/api/ferramentas/lotes/{loteId:guid}/utilizacao` | GET | `GetUtilisationAsync` | `ModulePolicies.Ferramentas` | 1618 |

### Route Index

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `/ferramentas` | `IndexModel.OnGet` | `BaDmo.Module.ferramentas` | `Index.cshtml` |
| `/ferramentas/criar` | `CriarModel.OnGet` | `BaDmo.Module.ferramentas` | `Criar.cshtml` |
| `/ferramentas/{id:guid}` | `FichaModel.OnGet` | `BaDmo.Module.ferramentas` | `Ficha.cshtml` |
| `/api/ferramentas/*` | minimal API endpoints | `BaDmo.Module.ferramentas` / `BaDmo.Capability.ferramentas.configure` | `Program.cs` |

### DI Registration (`Program.cs`)

`FerramentasService`, `FerramentasAuthorizationGate` scoped; `IFerramentasRepository → DapperFerramentasRepository`, `IFerramentasRuleLookup → DapperFerramentasRuleLookup`, `IFerramentasIdentityLookup → DapperFerramentasIdentityLookup`, `IFerramentasPieceLookup → DapperFerramentasPieceLookup`; cross-module `IToolIdentityResolver → FerramentasArmazemToolIdentityResolver`, `IToolPieceResolver → FerramentasRepairToolPieceResolver` (Program.cs lines 202–228).

## 11. Static Assets

| File | Principal Functions | API Routes Called | Location |
|---|---|---|---|
| `ferramentas.js` | CM/MF tab switching; reference search/load; reference row render + selection; create (`/ferramentas/criar`); rule load/render/edit/toggle/save; utilisation guards via `#canConfigure` | `/api/ferramentas/references`, `/api/ferramentas/reference`, `/api/ferramentas/references/{id}`, `/api/ferramentas/lotes/{id}/rules`, `/api/ferramentas/rules/{id}` (+ `/toggle`) | `src\BA.Dmo.Web\wwwroot\scripts\ferramentas.js` |
| `ferramentas-layout.css` | Ferramentas page layout: `.ferramentas-page`, `.ferramentas-tabs`, `.ferramentas-view`, `.ferramentas-filters`, `.ferramentas-create-grid`, `.ferramentas-machine-grid/.machine-choice`, `.ferramentas-context-grid`, `.ferramentas-rule-editor`; responsive breakpoints | — | `src\BA.Dmo.Web\wwwroot\styles\modules\ferramentas-layout.css` |

No Ferramentas navigation wiring found in shared layout/navigation partials (`_Layout.cshtml`, `_Navigation.cshtml`).

## 12. Tests

### Unit Tests — `tests\BA.Dmo.UnitTests\Modules\Ferramentas\`

| Test Class | Kind | Direct Target | Main Groups | Path |
|---|---|---|---|---|
| `FerramentasDomainTests` | Unit (xunit) | Domain entities/codecs | reference create validation; `OwnerPlant` default; lot lines/qty validation; CM/MF distinct; rule text required; piece sequence/number validation; condition requires reason | `FerramentasDomainTests.cs` |
| `FerramentasServiceTests` | Unit (xunit) | `FerramentasService` | atomic reference+lote; processo-on-lote; duplicate-reference block; duplicate-lote config-only; rule configure gate; piece register + condition; list mapping; rule lookup | `FerramentasServiceTests.cs` |
| `FerramentasUtilisationServiceTests` | Unit (xunit) | `FerramentasService` | append-only history; manual percent (no formula); invalid percent reject; negative cumulative reject | `FerramentasUtilisationServiceTests.cs` |

### Integration Tests — `tests\BA.Dmo.IntegrationTests\Ferramentas\`

| Test Class | Kind | Direct Target | Main Groups | Path |
|---|---|---|---|---|
| `FerramentasWebApiTests` | Integration (`WebApplicationFactory<Program>`) | `/api/ferramentas/*` endpoints + authorization guards | anonymous denied → login; authorized ferramentas admitted; user without module denied → access-denied; rules endpoint requires/excludes `ferramentas.configure` | `FerramentasWebApiTests.cs` |

## 13. Test Doubles / Helpers

Ferramentas-scope test support under `tests\BA.Dmo.UnitTests\Modules\Ferramentas\`:

| File | Doubles/Helpers | Role |
|---|---|---|
| `FerramentasTestSupport.cs` | `FixedClock(IClock)`, `FakeAuthorshipAccessor(IPersistenceAuthorshipAccessor)`, `FakeCurrentUser(ICurrentUserAccessor)` (+ `Authorized`, `Configurator`, `WithoutModule` static factories), `FakeRuleLookup(IFerramentasRuleLookup)` | clocks/identity/rule-lookup fakes for service tests |
| `FakeFerramentasRepository.cs` | `FakeFerramentasRepository(IFerramentasRepository)` in-memory fake (`References`, `Lotes`, `Pieces`, `CheckRules`, `AuditEvents`, `UtilisationReadings`, `FailAtomicCreate`) | persistence-port fake |

## 14. Direct Ferramentas References

One edge per relationship.

- `ToolReference.Create` → `FerramentasModuleCatalog.DefaultOwnerPlant`
- `ToolReference` → `Result<T, DomainError>` (`Domain.Shared.Kernel`)
- `ToolLote.CreateFromBase` → `CopiedFromToolLoteId` (self-reference of duplication origin)
- `PhysicalPiece.SetCondition` → `ToolConditionCodec.ToStorage`
- `ToolCheckRule.Create` → `CopiedFromRuleId` (self-reference of duplication origin)
- `FerramentasService` → `IFerramentasRepository`
- `FerramentasService` → `IFerramentasRuleLookup`
- `FerramentasService` → `FerramentasAuthorizationGate`
- `FerramentasService` → `IClock`
- `FerramentasService` → `CanonicalModuleCatalog.FerramentasConfigureCapabilityId`
- `FerramentasService.ResolveActiveRulesAsync` → `VerificationRule` / `VerificationFrequency` (`Domain.Modules.JobOn`)
- `FerramentasAuthorizationGate` → `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor`, `FerramentasModuleCatalog.ModuleId`
- `IFerramentasRepository` → `DapperFerramentasRepository`
- `IFerramentasRuleLookup` → `DapperFerramentasRuleLookup`
- `IFerramentasIdentityLookup` → `DapperFerramentasIdentityLookup`
- `IFerramentasPieceLookup` → `DapperFerramentasPieceLookup`
- `DapperFerramentasRepository` → `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records`, `audit_events` (embedded SQL)
- `DapperFerramentasRepository` → `IDbConnectionFactory`, `DapperUnitOfWork`
- `DapperFerramentasRuleLookup` → `tool_check_rules` (embedded SQL)
- `DapperFerramentasIdentityLookup` → `tool_references`, `tool_lotes` (join)
- `DapperFerramentasPieceLookup` → `physical_pieces`, `tool_lotes`, `tool_references` (join)
- `ModulePolicies.Ferramentas` → `CanonicalModuleCatalog.FerramentasModuleId`
- `CapabilityPolicies.FerramentasConfigure` → `CanonicalModuleCatalog.FerramentasConfigureCapabilityId`
- `Program.cs` minimal API endpoints → `FerramentasService`
- `Program.cs` DI → `DapperFerramentas*` implementations
- `Index/Ficha.cshtml` → `ferramentas-layout.css`, `ferramentas.js`
- `ferramentas.js` → `/api/ferramentas/*` endpoints

## 15. External Technical References

Source-visible references from other modules/tables into Ferramentas objects. Recorded as literal technical facts only; module classification is not inferred.

| Ferramentas Object | External Technical Reference | Reference Type |
|---|---|---|
| `tool_references` | `N05_jobon.sql` `job_on_component.source_tool_id` | DB FK |
| `tool_lotes` | `N05_jobon.sql` `job_on_component.source_lot_id` | DB FK |
| `tool_check_rules` | `N05_jobon.sql` `job_on_verification_occurrence.source_rule_id` | DB FK |
| `tool_lotes` | `N09_armazem.sql` `warehouse_stock.tool_lote_id` | DB FK |
| `physical_pieces` | `N08_reparacoes.sql` `repair_exit_items.physical_piece_id` | DB FK |
| `tool_references` / `tool_lotes` | `N23_controlo_folha.sql` `controlo_sheet_items.source_tool_id` / `source_lot_id` | DB FK |
| `VerificationRule` / `VerificationFrequency` | `JobOnVerificationGenerator` (`Domain.Modules.JobOn`) | application port / shared contract |
| `IFerramentasRuleLookup` | `FerramentasService.ResolveActiveRulesAsync` (consumed by Job On) | application port |
| `IFerramentasIdentityLookup` | `FerramentasArmazemToolIdentityResolver` (Armazem) | application port (read-only) |
| `IFerramentasPieceLookup` | `FerramentasRepairToolPieceResolver` (Reparação Externa) | application port (read-only) |
| `FerramentasToolType` | `FerramentasToolType.BQ` enum literal; `FerramentasToolTypeCodec.ToStorage/FromStorage` map `"BQ"`; `tool_references` CHECK `ck_tool_references_type` allows `'BQ'` | enum/storage/reference literal |
| `FerramentasArmazemToolIdentityResolver` | `FerramentasArmazemToolIdentityResolverTests` (`tests\BA.Dmo.UnitTests\Modules\Armazem\`) | test class targeting Ferramentas port adapter |

## 16. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `ToolReference`, `ToolLote`, `PhysicalPiece`, `ToolCheckRule`, `ToolCheckOccurrence`, `ToolUtilisationReading` + codecs/enums | Domain | `src\BA.Dmo.Domain\Modules\Ferramentas\` |
| `FerramentasToolType`, `FerramentasModuleCatalog` | Domain | `src\BA.Dmo.Domain\Modules\Ferramentas\` |
| `FerramentasService`, `FerramentasAuthorizationGate`, `FerramentasRequests` | Application | `src\BA.Dmo.Application\Modules\Ferramentas\` |
| `IFerramentasRepository`, `IFerramentasRuleLookup`, `IFerramentasIdentityLookup`, `IFerramentasPieceLookup` | Application (ports) | `src\BA.Dmo.Application\Modules\Ferramentas\` |
| `DapperFerramentasRepository`, `DapperFerramentasRuleLookup`, `DapperFerramentasIdentityLookup`, `DapperFerramentasPieceLookup` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records` | Database | `database\migrations\N04_ferramentas.sql`, `N19_tool_usage.sql` |
| `ModulePolicies.Ferramentas`, `CapabilityPolicies.FerramentasConfigure` | Web (Authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `/api/ferramentas/*` minimal API endpoints + DI | Web (Program) | `src\BA.Dmo.Web\Program.cs` |
| Index, Criar, Ficha Razor Pages + partial | Web | `src\BA.Dmo.Web\Pages\Ferramentas\` |
| `IndexModel`, `CriarModel`, `FichaModel`, `FerramentasListModel` | Web | `src\BA.Dmo.Web\Pages\Ferramentas\*.cshtml.cs` |
| `ferramentas.js`, `ferramentas-layout.css` | Static Assets | `src\BA.Dmo.Web\wwwroot\scripts\`, `wwwroot\styles\modules\` |
| Ferramentas unit/integration test classes + support | Tests | `tests\BA.Dmo.UnitTests\Modules\Ferramentas\`, `tests\BA.Dmo.IntegrationTests\Ferramentas\` |

## 17. Sources Verified

- `src\BA.Dmo.Domain\Modules\Ferramentas\` (8 files)
- `src\BA.Dmo.Application\Modules\Ferramentas\` (7 files)
- `src\BA.Dmo.Application\Modules\Armazem\FerramentasArmazemToolIdentityResolver.cs` (external reference source)
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\FerramentasRepairToolPieceResolver.cs` (external reference source)
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperFerramentas{PieceLookup,IdentityLookup,RuleLookup,Repository}.cs`
- `src\BA.Dmo.Web\Program.cs` (DI + minimal API)
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Pages\Ferramentas\*.cshtml` / `*.cshtml.cs`
- `src\BA.Dmo.Web\wwwroot\scripts\ferramentas.js`, `wwwroot\styles\modules\ferramentas-layout.css`
- `database\migrations\N04_ferramentas.sql`, `N12_rls.sql`, `N19_tool_usage.sql`, `N25_remediation.sql`
- `database\migrations\N05_jobon.sql`, `N08_reparacoes.sql`, `N09_armazem.sql`, `N23_controlo_folha.sql` (FK reference points)
- `tests\BA.Dmo.UnitTests\Modules\Ferramentas\*`, `tests\BA.Dmo.UnitTests\Modules\Armazem\FerramentasArmazemToolIdentityResolverTests.cs`
- `tests\BA.Dmo.IntegrationTests\Ferramentas\FerramentasWebApiTests.cs`

Design/SOT not used as evidence. Source-inspection only.

## Counts

- Domain Ferramentas files: 8
- Application Ferramentas files (dedicated `Modules\Ferramentas\`): 7
- Infrastructure Ferramentas files: 4
- Web dedicated page files (pages + code-behind + partial): 7
- Static asset files: 2 (`ferramentas.js`, `ferramentas-layout.css`)
- Shared Web wiring files: 2 (`Program.cs`, `ModuleAuthorizationHandler.cs`)
- Ferramentas DB objects (tables): 6 (plus associated indexes/triggers)
- Ferramentas migration touchpoints: 4 (N04, N12, N19, N25)
- Ferramentas test classes: 4 (3 unit + 1 integration)
- Ferramentas test support/helper files: 2