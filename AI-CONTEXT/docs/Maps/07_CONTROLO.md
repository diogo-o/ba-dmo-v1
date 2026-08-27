# BA DMO — Controlo Technical Map

MAP ID: MAP-07
Status: COMPLETE

> Reconciliation note (HEAD 8478308): per the INDEX taxonomy, **CONTROLO is ONE canonical
> module** whose internal areas are **Peso** and **Pegamentos** (plus the Resumo/Histórico
> surface). In current source these internal areas own their Domain/Application/Infrastructure/
> Web slices, so this map covers the whole vertical slice — Controlo (Folha/Resumo) +
> Peso + Pegamentos — WITHOUT promoting Peso/Pegamentos to top-level modules. They are
> mapped as internal areas under section 2 onwards; they are canonical module entries only
> as technical ids (`peso`/`pegamentos`, `isAssignable: false`) beneath the assignable
> `controlo` grant.
>
> Cross-map links: [00_INDEX](00_INDEX.md) · [01_DOMAIN](01_DOMAIN.md) · [02_DATABASE](02_DATABASE.md) ·
> [03_MIGRATIONS](03_MIGRATIONS.md) · [04_DAPPER_INFRASTRUCTURE](04_DAPPER_INFRASTRUCTURE.md) ·
> [05_TESTS](05_TESTS.md) · [06_JOB_ON](06_JOB_ON.md) · [15_ADMIN](15_ADMIN.md) ·
> [19_APPLICATION](19_APPLICATION.md) · [20_WEB](20_WEB.md).

## Navigation Index

- 1. Scope
- 2. Layer Summary
- 3. Domain Objects
- 4. Application Objects
- 5. Application Contracts / Ports
- 6. Authorization / Catalog Objects
- 7. Infrastructure Objects
- 8. Database Objects
- 9. Migration Touchpoints
- 10. Web / Routes
- 11. Static Assets
- 12. Tests
- 13. Test Doubles / Helpers
- 14. References to Job On
- 15. Direct References
- 16. External Technical References
- 17. Target-to-Layer Index
- 18. Sources Verified

## 1. Scope

Module navigation map for Controlo-specific technical objects across Domain, Application, Infrastructure, Database, Web, static assets and Tests. Per the INDEX taxonomy, CONTROL0 is ONE canonical module; Peso and Pegamentos are its internal areas with their own vertical slices in current source and are mapped here as such.

### 1.1 Controlo area (Folha de Controlo / Resumo / Histórico)
- Domain: `src\BA.Dmo.Domain\Modules\Controlo\` — `ControloFolha*` types (`ControloFolha`, `ControloFolhaContext/Component`, `ControloFolhaItem`, `ControloFolhaState/Decision/codec`, `ControloSheetModuleCatalog`, `ControloUnit`).
- Application: `src\BA.Dmo.Application\Modules\Controlo\` — `ControloSheetService`, `ControloSheetAuthorizationGate`, request/DTO records, `IControloSheetRepository`, `IControloProductionContextLookup`.
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\` — `DapperControloSheetRepository`, `DapperControloProductionContextLookup`.
- Database: `database\migrations\N23_controlo_folha.sql` — `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` + trigger; RLS/grants for these tables in `N25_remediation.sql` §2 (late-table loop).
- Web: `src\BA.Dmo.Web\Pages\Controlo\` (Razor page + code-behind), `/api/controlo/*` endpoints + DI in `Program.cs` (~1179–1260).
- Static assets: `wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css`.
- Tests: `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Controlo\`.

### 1.2 Internal area — Peso
- Domain: `src\BA.Dmo.Domain\Modules\Peso\` (8 files) — `PesoControl`, `PesoControlState` (+codecs, `PesoCmDecision`), `PesoLeitura` (+comparison snapshot records), `PesoProcesso` (+codec), `PesoRecordType` (+codec), `PesoReference` (+`PesoValidator`, `ReportPathValidator`), `WeightCalculator` (single C# weight/volume engine), `PesoModuleCatalog` (+`PesoLoteRules`).
- Application: `src\BA.Dmo.Application\Modules\Peso\` (4 files) — `PesoService` (1131 lines, all use cases), `PesoAuthorizationGate`, `IPesoRepository` (+`PesoLote`/`PesoReferenceSummary`/`PesoApprovedBase` records), `IPdfRenderer` (+`PesoFolhaPdf`/`PesoCmComparisonRow`), plus `PesoFileName` (infra-adjacent helper in same file).
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\DapperPesoRepository.cs`, `PesoSingleFilePdfRenderer.cs`.
- Database: `database\migrations\N06_peso.sql` (7 `peso_*` tables); RLS for `peso_*` pre-dates N12 (see `N12_rls.sql`); approved-immutability guard trigger added in `N25_remediation.sql` (`ba_dmo_guard_peso_approved`).
- Web: `src\BA.Dmo.Web\Pages\Peso\{Index,Responsavel}.cshtml(.cs)` — two user surfaces (Operador `/peso`, Responsável `/peso/responsavel`); `/api/peso/*` endpoints + DI in `Program.cs` (192–196, 382–566); `wwwroot\scripts\peso.js`; `wwwroot\styles\modules\peso-layout.css`.
- Tests: `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Peso\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\PesoComparisonGuardTests.cs` (static guards), `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Peso\PesoPdfVisualCheck.cs` (manual visual artifact).

### 1.3 Internal area — Pegamentos
- Domain: `src\BA.Dmo.Domain\Modules\Pegamentos\` (7 files) — `PegamentoControlo` (+`PegamentoControloStatus`, `PegamentoMedicao`, `PegamentoToleranceStatus`), `PegamentoComponentKey`, `PegamentoDocumento`, `PegamentoMeasurementCalculator`, `PegamentoModuleCatalog`, `PegamentoProductionContext`, `PegamentoToolSnapshot`.
- Application: `src\BA.Dmo.Application\Modules\Pegamentos\` (7 files) — `PegamentoService`, `PegamentoRequests`, `PegamentoAuthorizationGate`, `PegamentoPdfService` (+`IPegamentoPdfRenderer`/`PegamentoPdfData`), `PegamentoPdfFilename`, `IPegamentoRepository`, `IJobOnProductionContextLookup`.
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\DapperPegamentoRepository.cs`, `PegamentoPdfRenderer.cs`, plus shared consumption of `DapperJobOnProductionContextLookup.cs`, `DapperJobOnProductionFolderResolver.cs`, `DapperAppSettingsReader.cs` (Job On / settings cross-module).
- Database: `database\migrations\N07_pegamentos.sql` (`pegamento_controlos`, `pegamento_medicoes` + append-only trigger), `N14_pegamentos_documents.sql` (`pegamento_documentos`), `N15_pegamentos_tool_number.sql`, `N16_pegamentos_component_nominals.sql`, `N17_pegamentos_notas.sql`; `pegamento_documentos` RLS in `N25_remediation.sql` §2.
- Web: `src\BA.Dmo.Web\Pages\Pegamentos\{Index,Detail}.cshtml(.cs)`; `/api/pegamentos/*` endpoints + DI in `Program.cs` (198–207, 568–694); `wwwroot\scripts\pegamentos.js`; `wwwroot\styles\modules\pegamentos-layout.css`.
- Tests: `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Pegamentos\{PegamentoWebApiTests,PegamentoPdfRendererTests}.cs`.

Shared infrastructure (`IDbConnectionFactory`, `IRepairUnitOfWorkFactory`, `IClock`, accessor interfaces, `ba_dmo_guard_append_only`) is referenced only where Controlo/Peso/Pegamentos consume it, not remapped. Content grounded in current `src\`, `database\`, `AI-CONTEXT\docs\tests\` source (HEAD 8478308).

## 2. Layer Summary

### 2.1 Controlo area (Folha de Controlo / Resumo)

| Layer | Main Controlo Objects | Locations |
|---|---|---|
| Domain | ControloFolha, ControloFolhaContext, ControloFolhaItem, ControloFolhaState, ControloFolhaStateCodec, ControloSheetModuleCatalog, ControloUnit | `src\BA.Dmo.Domain\Modules\Controlo\` |
| Application | ControloSheetService, ControloSheetServiceRequests (records), ControloSheetAuthorizationGate, IControloSheetRepository, IControloProductionContextLookup | `src\BA.Dmo.Application\Modules\Controlo\` |
| Authorization/Catalog | ControloSheetModuleCatalog, CanonicalModuleCatalog Controlo entry + controlo.* capabilities, ModulePolicies.Controlo, CapabilityPolicies.Controlo* | `src\BA.Dmo.Domain\Modules\Controlo\ControloSheetModuleCatalog.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Infrastructure | DapperControloSheetRepository, DapperControloProductionContextLookup | `src\BA.Dmo.Infrastructure\Access\` |
| Database | controlo_sheets, controlo_sheet_items, controlo_sheet_events, trg_controlo_sheet_events_append_only | `database\migrations\N23_controlo_folha.sql`, `database\consolidated_clean_install.sql` |
| Migrations | N23 (create), N25 (RLS/policy/grants) | `database\migrations\N23_controlo_folha.sql`, `database\migrations\N25_remediation.sql` |
| Web | Pages\Controlo\Index (Razor page), Program.cs controlo endpoints + DI | `src\BA.Dmo.Web\Pages\Controlo\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css` | `src\BA.Dmo.Web\wwwroot\` |
| Tests | ControloFolhaTests, ControloSheetServiceTests, ControloTestSupport | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\` |

### 2.2 Internal area — Peso

| Layer | Main Peso Objects | Locations |
|---|---|---|
| Domain | PesoControl, PesoControlState/Codec + PesoCmDecision/Codec, PesoLeitura + comparison snapshot records, PesoProcesso/Codec, PesoRecordType/Codec, PesoReference + PesoValidator + ReportPathValidator, WeightCalculator, PesoModuleCatalog + PesoLoteRules | `src\BA.Dmo.Domain\Modules\Peso\` (8 files) |
| Application | PesoService (+request/DTO records + PesoFileName), PesoAuthorizationGate, IPesoRepository (+PesoLote, PesoReferenceSummary, PesoApprovedBase), IPdfRenderer (+PesoFolhaPdf, PesoCmComparisonRow) | `src\BA.Dmo.Application\Modules\Peso\` (4 files) |
| Authorization/Catalog | PesoModuleCatalog.PesoModuleId/`peso.aprovar`, CanonicalModuleCatalog Peso entry (order 21, `isAssignable: false`), ModulePolicies.Peso, CapabilityPolicies.PesoAprovar | `src\BA.Dmo.Domain\Modules\Peso\PesoModuleCatalog.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Infrastructure | DapperPesoRepository, PesoSingleFilePdfRenderer | `src\BA.Dmo.Infrastructure\Access\` |
| Database | peso_references, peso_lotes, peso_controlos, peso_leituras, peso_comparacao_anterior, peso_day_approvals, peso_settings; trg_peso_controlos_approved_guard | `database\migrations\N06_peso.sql` (RLS pre-N12 in `N12_rls.sql`), approved-guard trigger in `N25_remediation.sql` |
| Migrations | N06 (create), N25 (approved-guard trigger + checks) | `database\migrations\N06_peso.sql`, `database\migrations\N25_remediation.sql` |
| Web | Pages\Peso\Index (Operador `/peso`), Pages\Peso\Responsavel (`/peso/responsavel`), /api/peso/* endpoints + DI | `src\BA.Dmo.Web\Pages\Peso\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `wwwroot\scripts\peso.js`, `wwwroot\styles\modules\peso-layout.css` | `src\BA.Dmo.Web\wwwroot\` |
| Tests | PesoDomainTests, PesoControlWorkflowTests, PesoServiceTests, WeightCalculatorTests, FakePesoRepository + inline doubles | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Peso\` |

### 2.3 Internal area — Pegamentos

| Layer | Main Pegamentos Objects | Locations |
|---|---|---|
| Domain | PegamentoControlo (+Status/Medicao/ToleranceStatus), PegamentoComponentKey, PegamentoDocumento, PegamentoMeasurementCalculator, PegamentoModuleCatalog, PegamentoProductionContext, PegamentoToolSnapshot | `src\BA.Dmo.Domain\Modules\Pegamentos\` (7 files) |
| Application | PegamentoService, PegamentoRequests, PegamentoAuthorizationGate, PegamentoPdfService + PegamentoPdfFilename, IPegamentoRepository, IJobOnProductionContextLookup | `src\BA.Dmo.Application\Modules\Pegamentos\` (7 files) |
| Authorization/Catalog | PegamentoModuleCatalog.ModuleId (`pegamentos`), CanonicalModuleCatalog Pegamentos entry (order 22, `isAssignable: false`, no capabilities), ModulePolicies.Pegamentos | `src\BA.Dmo.Domain\Modules\Pegamentos\PegamentoModuleCatalog.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Infrastructure | DapperPegamentoRepository, PegamentoPdfRenderer; consumes DapperJobOnProductionContextLookup, DapperJobOnProductionFolderResolver, DapperAppSettingsReader | `src\BA.Dmo.Infrastructure\Access\` |
| Database | pegamento_controlos, pegamento_medicoes, pegamento_documentos (+ tool_number, cm/bq/mf_nominal, notas columns) | `database\migrations\N07`, `N14`, `N15`, `N16`, `N17` |
| Migrations | N07 (create), N13 (job_on.production_folder prerequisite), N14–N17 (additive) | `database\migrations\N07_pegamentos.sql`, `N13_jobon_production_folder.sql`, `N14_pegamentos_documents.sql`, `N15_pegamentos_tool_number.sql`, `N16_pegamentos_component_nominals.sql`, `N17_pegamentos_notas.sql` |
| Web | Pages\Pegamentos\Index (`/pegamentos`), Pages\Pegamentos\Detail (`/pegamentos/{id:guid}`), /api/pegamentos/* endpoints + DI | `src\BA.Dmo.Web\Pages\Pegamentos\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `wwwroot\scripts\pegamentos.js`, `wwwroot\styles\modules\pegamentos-layout.css` | `src\BA.Dmo.Web\wwwroot\` |
| Tests | PegamentoServiceTests, PegamentoDocumentConfirmationTests, PegamentoHistoricalRelationshipTests, PegamentoMeasurementCalculatorTests, PegamentoPdfTests, JobOnProductionFolderResolverTests + fakes | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\` |

### 2.4 Layer Coverage

| Layer | Controlo area | Peso (internal) | Pegamentos (internal) | Primary locations |
|---|---|---|---|---|
| Domain | YES (6 files) | YES (8 files) | YES (7 files) | `src\BA.Dmo.Domain\Modules\{Controlo,Peso,Pegamentos}\` |
| Application | YES (5 files) | YES (4 files) | YES (7 files) | `src\BA.Dmo.Application\Modules\{Controlo,Peso,Pegamentos}\`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Infrastructure | YES (2) | YES (2) | YES (2 + 3 shared consumed) | `src\BA.Dmo.Infrastructure\Access\` |
| Web | YES | YES | YES | `src\BA.Dmo.Web\Pages\{Controlo,Peso,Pegamentos}\`; `src\BA.Dmo.Web\Program.cs` |
| Database | YES (3 tables + 1 trigger) | YES (7 tables + 1 trigger) | YES (3 tables + 1 trigger, 3 additive columns) | `database\migrations\N06`, `N07`, `N14`–`N17`, `N23` |
| Tests | YES | YES | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\{Controlo,Peso,Pegamentos}\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

### 3.1 Controlo area — `src\BA.Dmo.Domain\Modules\Controlo\` (6 files)

#### ControloFolha (aggregate root) — `ControloFolha.cs`
- Identifiers: `ControloSheetId` (Guid pk, default `Guid.NewGuid()`); pins `JobOnId`, `JobOnRevisionId` (Guid).
- Fields: `ProductionCode`, `Reference`, `MachineCode`, `DisplayId` (format `Controlo_<PROD>_<REF>_<MAQUINA>`), `State` (`ControloFolhaState`), `Items: IReadOnlyList<ControloFolhaItem>`, `Events: IReadOnlyList<ControloFolhaEvent>`, actors/timestamps (`CreatedBy`, `CreatedAtUtc`, `SubmittedBy/AtUtc/Note`, `DecidedBy/AtUtc/Note`, `Decision`, `DecisionNote`, `UpdatedAtUtc`).
- Computed: `HasBeenSubmitted`, `HasBeenDecided`.
- Public static factory: `Create(ControloFolhaProductionContext, string actorId, DateTimeOffset now) -> Result<ControloFolha, DomainError>`. Validation error codes: `CONTROLO_CONTEXT_REQUIRED`, `CONTROLO_ACTOR_REQUIRED` (source-verified, `ControloFolha.cs` lines 54–96).
- Mutations: `ApplyItemControls(IEnumerable<ControloFolhaItemControlEdit>, now)` (unknown item id → ignored); `Submit(actorId, note, now) -> Result<ControloUnit, DomainError>` (code `CONTROLO_DECIDED`); `Reopen(actorId, now)` (code `CONTROLO_ALREADY_DRAFT`); `Decide(ControloFolhaDecision, actorId, note, now)` (code `CONTROLO_NOT_SUBMITTED`). Edit-after-submission allowed (submission is not a permanent lock — R010).
- Eventing: `RecordEvent(ControloFolhaEvent)` (public), internal `AppendEvent`, `SetEvents`, `SetItems`; internal `BuildDisplayId(context)`.
- Records (same file): `ControloFolhaItemControlEdit(Guid ItemId, string? Result, string? Observation, string? McaliperLink)`; `ControloFolhaEvent(Guid ControloSheetEventId, Guid ControloSheetId, string EventType, string? ActorId, DateTimeOffset OccurredAtUtc, string? BeforeSummary, string? AfterSummary, string? Note)`.

#### ControloFolhaContext (immutable production context) — `ControloFolhaContext.cs`
- `ControloFolhaProductionContext(Guid JobOnId, Guid JobOnRevisionId, string ProductionCode, string Reference, string MachineCode, IReadOnlyList<ControloFolhaComponent> Components)`.
- `ControloFolhaComponent(string Family, Guid? SourceToolId, Guid? SourceLotId, string? ReferenceSnapshot, string? LotSnapshot, string? TechnicalNameSnapshot)`. Doc comment lists Resumo snapshot families `MP_CM/MF/BQ/PU/CS`.

#### ControloFolhaItem — `ControloFolhaItem.cs`
- Identifier `ControloSheetItemId`; FK `ControloSheetId`.
- Snapshot fields: `Family` (doc comment: MP_CM/MF/BQ/PU/CS), `SourceToolId`, `SourceLotId`, `ReferenceSnapshot`, `LotSnapshot`, `TechnicalNameSnapshot`.
- Control fields: `Result` (OK/NOK), `Observation`, `McaliperLink` (persisted as typed, no integration).
- Methods: `ApplyControl(result, observation, mcaliperLink)`; static `SnapshotFromComponent(...)`; private `NormalizeResult` (uppercases, returns only `OK`/`NOK`, else null).

#### ControloFolhaState — `ControloFolhaState.cs`
- Enum `ControloFolhaState`: `Rascunho`, `Submetido`, `Aprovado`, `Rejeitado`.
- Enum `ControloFolhaDecision`: `Aprovado`, `Rejeitado`.
- Codec `ControloFolhaStateCodec` (static): `ToStorage(ControloFolhaState)`, `FromStorage(string)` (text: `rascunho/submetido/aprovado/rejeitado`), `ToStorage(ControloFolhaDecision)`, `FromStorageDecision(string)`.

#### ControloSheetModuleCatalog (module constants) — `ControloSheetModuleCatalog.cs`
- `AreaId = "controlo"`; capabilities `ViewCapabilityId = "controlo.view"`, `EditCapabilityId = "controlo.edit"`, `SubmitCapabilityId = "controlo.submit"`, `ReviewCapabilityId = "controlo.review"` (R010 comment: sheet is a workflow INSIDE the Controlo area, not a separate top-level module).
- `Statuses = ["rascunho","submetido","aprovado","rejeitado"]` (matches N23 CHECK); `ComponentFamilies = ["MP_CM","MF","BQ"]`.
- ⚠️ **NEEDS REVIEW — ORPHAN CANDIDATE**: `ComponentFamilies` is never referenced in `src\` or `AI-CONTEXT\docs\tests\` (grep verified only its declaration) AND its 3-family value disagrees with the runtime projection — `DapperControloProductionContextLookup` filters `c.family IN ('MP_CM','MF','BQ','PU','CS')` (5 families, pinned by `ControloProjectionGuardTests`) and the domain doc comments list `MP_CM/MF/BQ/PU/CS`. Decide: delete the constant, or align it to the 5-family list and consume it in the SQL.

#### ControloUnit — `ControloUnit.cs`
- `readonly record struct ControloUnit` with `static ControloUnit Value`.

Domain Controlo area files: 6.

### 3.2 Internal area — Peso — `src\BA.Dmo.Domain\Modules\Peso\` (8 files)

#### PesoControl (aggregate root) — `PesoControl.cs`
- Identity: `PesoControloId`; FKs `PesoReferenceId`, `PesoLoteId`; `RecordType` (`PesoRecordType` — NovoControlo/Comparacao; "Comparação" is a record TYPE, never a status).
- Job On pinning (TD-18): `JobOnId` + `JobOnRevisionId` mandatory; `CmSnapshotJson` (inherited, non-editable, presentation/filter).
- Values: `MoldNumber/NeckringNumber`, `ProductionCode`, `Line`, `Lote`, `ControlDate`, `Status` (`PesoControlState`), `Revision` (int, starts 1), `MeasurementsSnapshotJson`, `ApprovalLogJson`, `PreviousControlJson` (comparison snapshot), `ComparisonDecisionsJson`, `ApprovedBy/AtUtc`, `CreatedAtUtc/CreatedBy`, `UpdatedAtUtc`, `Leituras: IReadOnlyList<PesoLeitura>`, `PesoNominal`, `Processo` (`PesoProcesso?`), `ConstanteGlassUsada` (OC-6 historical density).
- Derived: `PesoMedio` (glass average), `CapacidadeMedia` (volume average via `WeightCalculator`).
- Measurement snapshot fields: `TemperaturaC`, `EstadoMolde`, `FimProducaoAnteriorSap`, `PesoMedioAnteriorSap`, `Notas`, `DataRegistoComparacao`.
- Workflow (GLM-PESO-06.6): `Submit()` (rascunho→pendente; hard block `PESO_CONTROL_NO_READING`), `Approve(approvedBy, nowUtc)` (pendente→aprovado; `PESO_CONTROL_NOT_PENDING`), `Reject(justification)` (mandatory note `PESO_CONTROL_REJECT_NOTE_REQUIRED`; →nao_aprovado), `Reopen(reason, nowUtc)` (aprovado/nao_aprovado→rascunho, revision+1; `PESO_CONTROL_NOT_REOPENABLE`, `PESO_CONTROL_REOPEN_REASON`).
- `IsDeletable` — rascunho/nao_aprovado only (GLM-PESO-06.7).
- Record `PesoControloAnterior(Guid? PreviousPesoControloId, decimal? PreviousPesoMedio, decimal? PreviousCapacidadeMedia, bool Exists)` — previous-approved fact (`peso_comparacao_anterior`; same mold+neckring, strictly earlier production/date, CROSS-LINE).

#### PesoControlState — `PesoControlState.cs`
- Enum `PesoControlState`: `Rascunho`, `Pendente`, `Aprovado`, `NaoAprovado`; codec `PesoControlStateCodec` (`Parse`/`ToStorage` text `rascunho/pendente/aprovado/nao_aprovado`; `ToDisplay` for Histórico/Responsável).
- Enum `PesoCmDecision`: `None`, `Manter`, `ColocarDeParte`; codec `PesoCmDecisionCodec` (storage `manter`/`colocar_de_parte`, parse alias `aside`).

#### PesoLeitura — `PesoLeitura.cs`
- `PesoLeitura(Guid PesoLeituraId, Guid PesoControloId, string CmNumber, decimal? PesoEmAgua, decimal? PesoVidro)` — append-only reading facts (N06 `peso_leituras`, UNIQUE(control, cm_number)).
- Comparison records: `PesoComparisonCmDecision` (per-CM decision: `CmNumber`, `Decision`, `PesoAtual`), `PesoComparisonCmSnapshot` (current↔previous glass-weight association: numbers, weights, `Difference`, `DifferencePercent`), `PesoComparisonSnapshot` (immutable identity/value snapshot stored in `peso_controlos.previous_control`; pins BOTH Job On identities), `PesoComparisonDecisionSnapshot` (Responsável decisions + justification).

#### PesoProcesso — `PesoProcesso.cs`
- Enum `PesoProcesso`: `Nnpb`, `Ps`; codec `PesoProcessoCodec` (storage text `NNPB`/`PS`). Process lives on the LOT (TD-17), inherited by Job On / Novo controlo / Comparação.

#### PesoRecordType — `PesoRecordType.cs`
- Enum `PesoRecordType`: `NovoControlo`, `Comparacao`; codec `PesoRecordTypeCodec` (storage `novo_controlo`/`comparacao`; display "Registo de peso"/"Comparação").

#### PesoReference — `PesoReference.cs`
- `PesoReference(Guid PesoReferenceId, string MoldNumber, string NeckringNumber, string? CounterMold, decimal? Capacity, decimal? VolumeNeck, decimal? VolumePu, decimal? CaloteTp, string ChangeLogJson)` — N06 `peso_references`, identity UNIQUE(mold_number, neckring_number).
- `PesoValidator` — `ValidateReference` (`PESO_REF_MOLD_REQUIRED`/`PESO_REF_NECKRING_REQUIRED`), `ValidateLote` (`PESO_LOTE_REQUIRED`, `PESO_LOTE_NO_ALLOWED_LINE`, `PESO_LOTE_INVALID_LINE` (B1–C3), `PESO_LOTE_DUPLICATE_LINE`, `PESO_LOTE_SUBFOLDER_REQUIRED`, `PESO_LOTE_SUBFOLDER_ABSOLUTE`), `ValidateControlEditable` (`PESO_CONTROL_REOPEN_REASON`).
- `ReportPathValidator` — `IsAbsoluteOrTraversal`, `Resolve(mainOutputFolder, reportSubfolder)` (GLM-PESO-09/DS-08).

#### WeightCalculator — `WeightCalculator.cs`
- SINGLE authoritative weight/volume engine (GLM-PESO-05; preview JS never duplicates; constants server-injected).
- `WaterDensityByCelsius` (5–35 °C, 31 entries, TD-25); `Min/MaxTemperatureCelsius` (5/35); `LookupDensity(decimal)` (rounds AwayFromZero; `PESO_TEMPERATURE_OUT_OF_RANGE` outside 5–35); `VolumeFromWeight` (volume = weight/density); `EstimateGlassWeight` (glass = (capacity + volumeNeck − volumePu) × constant); `CaloteVolume` (π·s²·(3r−s)/3 — tampão presentation only, NOT glass weight); `GlassAverage`; `DeltaVs` ([delta, pct]); `Round2`.

#### PesoModuleCatalog — `PesoModuleCatalog.cs` + `PesoLoteRules`
- `PesoModuleId = "peso"`, `PesoAprovarCapabilityId = "peso.aprovar"`, `ConstantNnpb = 2.4027m`, `ConstantPs = 2.4231m`, `AllowedLines = ["B1","B2","B3","C1","C2","C3"]`.
- ⚠️ **NEEDS REVIEW — ORPHAN CANDIDATE**: `ReportSubfolderMinLength = 1` (line 23) is never referenced in `src\` or tests (grep verified only its declaration).
- `PesoLoteRules.MinAllowedLines = 1` — used by `PesoValidator.ValidateLote`.

Domain Peso files: 8.

### 3.3 Internal area — Pegamentos — `src\BA.Dmo.Domain\Modules\Pegamentos\` (7 files)

#### PegamentoControlo (aggregate root) — `PegamentoControlo.cs`
- Identity: `PegamentoControloId`; pins `JobOnId` + immutable `JobOnRevisionId` (private setters; exact historical anchor, never rewritten).
- Snapshots (from pinned revision only): `ProductionCode`, `MachineCode`, `ReferenceSnapshot`, `CmSnapshot/BqSnapshot/MfSnapshot` (`PegamentoToolSnapshot?`), frozen nominals `CmNominal/BqNominal/MfNominal`, `Tolerance` (default `PegamentoModuleCatalog.DefaultTolerance` 0.20), `Status` (`PegamentoControloStatus` Aberto/Fechado), `Notas`, `Measurements` (append-only facts), audit `CreatedAtUtc/CreatedBy`, `UpdatedAtUtc`.
- Factory `Create(PegamentoProductionContext, toleranceOverride, notas, nowUtc, createdBy)` — validates CM/BQ/MF snapshot keys (`PEGAMENTO_CM_SNAPSHOT_INVALID` etc. for non-matching keys, `PEGAMENTO_CONTEXT_REQUIRED` on null context); `Hydrate(...)` is the ONLY reconstruction path (includes immutable revision id).
- Mutations: `AddMeasurement(component, toolNumber, costura, contraCostura, nowUtc)` (`PEGAMENTO_CONTROL_CLOSED`, `PEGAMENTO_TOOL_NUMBER_REQUIRED`, `PEGAMENTO_COMPONENT_NOMINAL_REQUIRED`; computes `Ovalizacao`/`Media` via engine + `ToleranceStatus`); `UpdateEditableFields(tolerance, notas, nowUtc)` (tolerance/notes only; never rewrites revision anchor); `Close(nowUtc)` (`PEGAMENTO_CONTROL_NOT_OPEN`; no close reason required).
- Enums/records (same file): `PegamentoControloStatus` (Aberto/Fechado); `PegamentoMedicao` (id, controlo id, `ComponentKey`, `ToolNumber` (int?, pre-N15 rows nullable), `Costura`, `ContraCostura`, `Ovalizacao`, `Media`, `ToleranceStatus`, `CreatedAtUtc`); `PegamentoToleranceStatus` (`Ok`, `Warning`, `Exceeded`, `NotEvaluable` — NotEvaluable = legacy N16 row without historical nominal; MUST NOT be reported Ok).

#### PegamentoComponentKey — `PegamentoComponentKey.cs`
- Enum `PegamentoComponentKey`: `CM` (contra-molde), `BQ` (boquilha), `MF` (molde final). Inherited from revision components — never independently selectable.

#### PegamentoDocumento — `PegamentoDocumento.cs`
- `PegamentoDocumento(Guid PegamentoDocumentoId, Guid PegamentoControloId, string Filename, string OutputRootSnapshot, string ProductionFolderSnapshot, DateTimeOffset GeneratedAtUtc, string? GeneratedBy)` — N14 `pegamento_documentos`, one per control (enforced by UNIQUE on `pegamento_controlo_id`).

#### PegamentoMeasurementCalculator — `PegamentoMeasurementCalculator.cs`
- Pure calculation engine (TD-32; no JS duplication): `Ovalizacao(costura, contraCostura)` (difference, null when contra missing); `Media(...)` (average; single-value fallback); `CheckTolerance(measured, nominal, tolerance)` — strict corridor: inside → `Ok`; REACHING or exceeding the boundary (≤ lower or ≥ upper) → `Exceeded`.

#### PegamentoModuleCatalog — `PegamentoModuleCatalog.cs`
- `ModuleId = "pegamentos"`; `DefaultTolerance = 0.20m`. No extra capabilities in V1 (GLM-PEG-02).

#### PegamentoProductionContext — `PegamentoProductionContext.cs`
- `PegamentoProductionContext(Guid JobOnId, Guid JobOnRevisionId, string ProductionCode, string MachineCode, string Reference, PegamentoToolSnapshot CmSnapshot, PegamentoToolSnapshot BqSnapshot, PegamentoToolSnapshot MfSnapshot, decimal? CmNominal, decimal? BqNominal, decimal? MfNominal)` + `ToolSnapshots` dictionary keyed by component.

#### PegamentoToolSnapshot — `PegamentoToolSnapshot.cs`
- `PegamentoToolSnapshot(PegamentoComponentKey Key, string ReferenceSnapshot, string? LotSnapshot)` — inherited tool identity from the pinned revision only (reference + lot; no TechnicalName required).

Domain Pegamentos files: 7.

## 4. Application Objects

### 4.1 Controlo area — `src\BA.Dmo.Application\Modules\Controlo\` (5 files)

#### ControloSheetService — `ControloSheetService.cs`
Constructor dependencies: `IControloSheetRepository`, `IControloProductionContextLookup`, `IRepairUnitOfWorkFactory`, `ControloSheetAuthorizationGate`, `IClock`.
Public methods (each gates a capability via `_gate.RequireCapability(...)`):
- `CreateAsync(CreateControloSheetRequest, ct)` → edit; inserts sheet + `"criar"` event in one UoW. Error `CONTROLO_SAVE_FAILED`.
- `GetDetailAsync(Guid sheetId, ct)` → view; returns `ControloSheetDto`; error `CONTROLO_NOT_FOUND`.
- `GetForProductionAsync(Guid jobOnId, ct)` → view; create-or-load for the production.
- `GetForProductionByContextAsync(string productionCode, string? machineCode, ct)` → view; resolves Job On internally by production/machine (R012: never re-selects).
- `UpdateItemsAsync(UpdateControloSheetItemsRequest, ct)` → edit; records `"editar"` event.
- `SubmitAsync(SubmitControloSheetRequest, ct)` → submit; records `"submeter"` event.
- `ReopenAsync(ReopenControloSheetRequest, ct)` → edit; records `"reeabrir"` event.
- `DecideAsync(DecideControloSheetRequest, ct)` → review; records `"decidir"` event (note string carries decision).
- `ListSheetsAsync(from, to, machineCode, jobOnId, status, ct)` → view (R012 §22/§23 free-mode history).
- Private `PersistEditAsync`, `MapToDto`, `SerializeSummary` (JSON snapshot for event before/after).

#### ControloSheetRequests — `ControloSheetRequests.cs`
Commands (records): `CreateControloSheetRequest(Guid JobOnId)`, `UpdateControloSheetItemsRequest(Guid SheetId, IReadOnlyList<ControloFolhaItemControlEdit> Edits)`, `SubmitControloSheetRequest(Guid SheetId, string? Note)`, `ReopenControloSheetRequest(Guid SheetId)`, `DecideControloSheetRequest(Guid SheetId, ControloFolhaDecision Decision, string? Note)`.
DTOs (records): `ControloSheetDto`, `ControloSheetItemDto`, `ControloSheetEventDto`.

#### ControloSheetAuthorizationGate — `ControloSheetAuthorizationGate.cs`
Constructor: `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor`.
- `const string SurfaceModuleId = "peso"` — COMMENT: "The Folha de Controlo surface lives inside the Peso production-control area." Entry is granted by `user.HasModule("peso")`; this is CONSISTENT with the current access model because `AccessResolver.Resolve` adds `peso` (and `pegamentos`) to the effective module set whenever the assignable `controlo` grant is present (`CanonicalModuleCatalog.AreaChildren["controlo"] = ["peso","pegamentos"]`).
- `RequireSurface()` → `RequireCapability(null)`.
- `RequireCapability(string? capabilityId) -> Result<ControloSheetExecutor, DomainError>`: resolves identity, checks `user.HasModule("peso")`, optional `user.HasCapability(capabilityId)`, resolves canonical `actor_id` via authorship. Error codes `CONTROLO_FORBIDDEN`, `CONTROLO_CAPABILITY_<ID>_FORBIDDEN`.
- Record `ControloSheetExecutor(string ActorId, string DisplayName)`.

Application Controlo area files: 5.

### 4.2 Internal area — Peso — `src\BA.Dmo.Application\Modules\Peso\` (4 files + helper records)

#### PesoService — `PesoService.cs` (1131 lines)
Constructor: `PesoAuthorizationGate`, `IPesoRepository`, `IJobOnRepository`, `IClock`. Every operation re-checks the canonical capability through the gate; audit facts written via `IPesoRepository.InsertAuditEventAsync` (table `audit_events`, module_id `peso`).
Use cases (all source-verified):
- References: `SaveReferenceAsync(SaveReferenceRequest, ct)` (create or new-revision edit; `PESO_REF_CHANGE_REASON_REQUIRED`; audit `peso.referencia.criar`/`peso.referencia.editar`); `ListReferencesAsync`.
- Lots: `CreateLoteAsync`, `DuplicateLoteAsync` (audit `peso.lote.criar`/`peso.lote.duplicar`).
- Novo controlo: `CreateControlAsync(CreateControlRequest, ct)` — resolves Job On context (`ResolveJobOnContext`, errors `PESO_JOBON_NO_REVISION`/`PESO_JOBON_INVALID_REFERENCE`/`PESO_JOBON_INVALID_PROCESS`), inherits reference/production/machine/CM/lot/process, pins `JobOnRevisionId` (TD-18), computes glass weights (`PopulateGlassWeightsAsync`); `SaveControlAsync`; `SubmitControlAsync`; audit `peso.controlo.criar`/`guardar`/`submeter`.
- Approval (Responsável, `peso.aprovar`): `ApproveControlAsync` (comparison records require all CMs decided — `PESO_COMPARISON_UNDECIDED`; registers day approval), `RejectControlAsync`, `ReopenControlAsync`, `DeleteControlAsync` (`PESO_CONTROL_DELETE_STATE`/`PESO_CONTROL_DELETE_UNAUTHORIZED`; author OR aprovar role), `SaveDayApprovalAsync`, `SaveSettingsAsync` (`PESO_SETTINGS_INVALID`). Audit `peso.controlo.aprovar`/`nao_aprovar`/`reabrir`/`eliminar`.
- Comparison (GLM-PESO-06.4/5): `CreateComparisonAsync` (current Novo controlo in rascunho + previous APPROVED Novo controlo; errors `PESO_COMPARISON_CURRENT_NOT_FOUND`/`_CURRENT_NOT_DRAFT`/`_NO_APPROVED_BASE`/`_SAME_PRODUCTION`/`_REFERENCE_MISMATCH`/`_NO_GLASS_WEIGHT`/`_PAIRING_INVALID`; stores `PesoComparisonSnapshot` in `previous_control`); `ConfirmComparisonDecisionsAsync` (per-CM decisions; `PESO_NOT_COMPARISON`, `PESO_COMPARISON_SNAPSHOT_INVALID`, `PESO_COMPARISON_DECISIONS_MISMATCH`, `PESO_COMPARISON_UNDECIDED`, `PESO_COMPARISON_JUSTIFICATION_REQUIRED`; audit `peso.comparacao.criar`/`peso.comparacao.decidir`).
- Documents/email: `GenerateDocumentAsync(IPdfRenderer, GenerateDocumentRequest, ct)` (`PESO_DOC_NOT_APPROVED`; builds `PesoFolhaPdf` from approved snapshot; `PesoFileName.Builder`; audit `peso.documento.gerar`); `PrepareEmailAsync` (`PESO_EMAIL_NOT_APPROVED`, `PESO_EMAIL_NO_RECIPIENTS`; setting keys `email_recipients_linhab`/`email_recipients_linhac`).
- Queries: `SearchControlsAsync(ControlFilterRequest)` (excludes Rascunho), `GetRecordDatesAsync(year, month)`, `GetSettingAsync`, `GetControlDetailAsync`, `GetControlForCalculationAsync` (live preview result `PesoCalculationResult`; single C# engine; OC-6 uses `ConstanteGlassUsada` fallback).
- Request/dto records (same file): `SaveReferenceRequest`, `CreateLoteRequest`, `CreateControlRequest`, `PesoLeituraInput`, `SaveControlRequest`, `SubmitControlRequest`, `ApproveControlRequest`, `RejectControlRequest`, `ReopenControlRequest`, `DeleteControlRequest`, `CreateComparisonRequest`, `PesoComparisonPairRequest`, `DecideComparisonCmRequest`, `ConfirmComparisonDecisionsRequest`, `SaveDayApprovalRequest`, `SaveSettingsRequest`, `GeneratedDocument(byte[] PdfBytes, string FileName)`, `GenerateDocumentRequest`, `PrepareEmailRequest`, `ControlFilterRequest`, `PesoControlListItem`, `PesoCalculationResult`, `PesoCalculationRow`, `PreparedEmail`, `PesoFileName` (deterministic filename `{mold}{neck}__{periodo}__{line}__L{lote}.pdf`, TD-31; confirmed reference `9262T288__202604__C3__L16.pdf`).

#### PesoAuthorizationGate — `PesoAuthorizationGate.cs`
Constructor: `ICurrentUserAccessor`. `Require(params string[] anyOfCapabilityIds) -> Result<PesoExecutor, DomainError>`: fails closed (`PESO_FORBIDDEN`); module entry = `user.HasModule("peso")`; capability list optional (`peso.aprovar` for approval/decision/reopen/delete-as-Responsável); `PesoExecutor(string ActorId, string DisplayName)` with `HasAprovarRole` init flag (derived from `peso.aprovar`).

#### IPesoRepository — `IPesoRepository.cs`
Read/write port (N06; Dapper implementation). Members: references (`CreateReferenceAsync`, `GetReferenceByIdAsync`, `GetReferencesAsync(search)`, `GetReferenceByMoldNeckringAsync`, `UpdateReferenceAsync`); lots (`CreateLoteAsync`, `GetLoteByIdAsync`, `GetLotesAsync`); controls (`CreateControlAsync`, `GetControlByIdAsync`, `GetControlsAsync(filters)`, `GetApprovedControlsForJobOnAsync`, `UpdateControlAsync`, `DeleteControlAsync`); previous resolution `GetPreviousApprovedAsync` (TD-13/TD-30, cross-line); day approvals (`SaveDayApprovalAsync`, `GetRecordDatesAsync`); settings (`SaveSettingAsync`, `GetSettingAsync`); audit `InsertAuditEventAsync`.
Records (same file): `PesoLote` (admin record, N06 `peso_lotes`), `PesoReferenceSummary`, `PesoApprovedBase`.

#### IPdfRenderer — `IPdfRenderer.cs`
`byte[] RenderPesoFolha(PesoFolhaPdf data)` — concrete library is an implementation decision (QuestPDF NOT required); deterministic output. Data records: `PesoFolhaPdf` (approved-snapshot view model) and `PesoCmComparisonRow`.

Application Peso files: 4.

### 4.3 Internal area — Pegamentos — `src\BA.Dmo.Application\Modules\Pegamentos\` (7 files)

#### PegamentoService — `PegamentoService.cs`
Constructor: `IPegamentoRepository`, `IJobOnProductionContextLookup`, `PegamentoAuthorizationGate`, `IClock`, `IAppSettingsReader`, `IJobOnProductionFolderResolver`.
Use cases: `CreateControlAsync(CreatePegamentoRequest)` (`PEGAMENTO_UNAUTHORIZED`, `PEGAMENTO_REVISION_NOT_FOUND`, `PEGAMENTO_INCOMPLETE_CONTEXT` — DS-05 actionable "Corrigir ferramentas no Job On"); `GetControlDetailAsync` (reverse navigation Pegamento→production); `ListByRevisionAsync`; `ListByJobOnAsync`; `ResolveProductionContextAsync(jobOnRevisionId)`; `GetHistoryAsync(controloId)`; `SearchAsync(ControlFilterRequest)` (search by historical snapshot text, not current master IDs); `UpdateControlAsync(UpdatePegamentoRequest)` (tolerance/notes only, never rewrites revision anchor); `AddMeasurementAsync(AddMeasurementRequest)` (server-side Ovalizacao/Media); `CloseControlAsync(CloseControlRequest)`; `ConfirmDocumentSavedAsync(Guid controloId)` — document confirmation: derives filename via `PegamentoPdfFilename.Compute`, output root via `IAppSettingsReader.GetOutputRootAsync` (`PEGAMENTO_OUTPUT_ROOT_MISSING`), production folder via `IJobOnProductionFolderResolver.ResolveAsync(jobOnId)` (`PEGAMENTO_PRODUCTION_FOLDER_MISSING` — N13 `job_on.production_folder`), frozen-document guard for closed controls (`PEGAMENTO_FINAL_DOCUMENT_FROZEN`), one-per-control upsert of `PegamentoDocumento`.

#### PegamentoRequests — `PegamentoRequests.cs`
`CreatePegamentoRequest(Guid JobOnRevisionId, decimal? Tolerance, string? Notes)` (no redundant JobOnId — derived, TD-26), `UpdatePegamentoRequest`, `AddMeasurementRequest` (ToolNumber mandatory for NEW measurements), `CloseControlRequest`, `ControlFilterRequest` (historical snapshot text), DTOs `PegamentoControlDetail`, `PegamentoMeasurementDetail` (ToolNumber nullable for pre-N15 rows), `PegamentoControlItem`.

#### PegamentoAuthorizationGate — `PegamentoAuthorizationGate.cs`
Constructor: `IPersistenceAuthorshipAccessor`. `ResolveActorId() -> string?` — resolves canonical internal_users.actor_id (module admission enforced by endpoint-level `ModulePolicies.Pegamentos`); fails closed (null → Forbidden).

#### PegamentoPdfService — `PegamentoPdfService.cs`
- Port `IPegamentoPdfRenderer` (`byte[] RenderPegamento(PegamentoPdfData data)`) + `PegamentoPdfData` (frozen historical snapshot view model) + `PegamentoPdfMeasurementRow`.
- Service: constructor `IPegamentoRepository`, `PegamentoAuthorizationGate`; `GenerateAsync(IPegamentoPdfRenderer renderer, Guid controloId, ct)` → PDF bytes + `PegamentoPdfFilename.Compute` filename; directly uses the frozen snapshot — NEVER live Job On state; does NOT persist `pegamento_documentos` (persistence only after browser confirmation).

#### PegamentoPdfFilename — `PegamentoPdfFilename.cs`
`Compute(control)` → `Pegamentos_{producao}_{referencia}_{maquina}_relatorio.pdf`. Application-owned; infrastructure must not duplicate (canonical filename).

#### IPegamentoRepository — `IPegamentoRepository.cs`
Controls (`CreateAsync`, `GetByIdAsync`, `GetByRevisionAsync`, `GetByJobOnAsync`, `SearchAsync`, `UpdateAsync`); measurements (`AddMeasurementAsync`, `GetMeasurementsAsync`); document metadata (`UpsertDocumentAsync`, `GetDocumentAsync` — N14). Owns Pegamentos persistence ONLY — does NOT read Job On tables.

#### IJobOnProductionContextLookup — `IJobOnProductionContextLookup.cs`
`Task<PegamentoProductionContext?> ResolveAsync(Guid jobOnRevisionId, ct)` — explicit cross-module Application contract resolved by Infrastructure from the Job On read model.

Application Pegamentos files: 7.

## 5. Application Contracts / Ports

### 5.1 Controlo area

#### IControloSheetRepository — `IControloSheetRepository.cs`
- `InsertAsync(IDbUnitOfWork, ControloFolha, ct) -> Task<Guid>` (transactional with items).
- `GetByIdAsync(Guid, ct) -> Task<ControloFolha?>`.
- `GetForProductionAsync(Guid jobOnId, Guid? jobOnRevisionId, ct) -> Task<ControloFolha?>` (latest).
- `ListByProductionAsync(Guid jobOnId, ct) -> Task<IReadOnlyList<ControloFolha>>`.
- `ListAsync(DateTimeOffset? from, to, string? machineCode, Guid? jobOnId, string? status, ct) -> Task<IReadOnlyList<ControloFolha>>`.
- `UpdateAsync(IDbUnitOfWork, ControloFolha, IReadOnlyList<ControloFolhaItem> currentItems, ct)`.
- `InsertEventAsync(IDbUnitOfWork, ControloFolhaEvent, ct)` (append-only).
- Implemented by: `DapperControloSheetRepository`.

#### IControloProductionContextLookup — `IControloProductionContextLookup.cs`
- `ResolveAsync(Guid jobOnId, ct) -> Task<Result<ControloFolhaProductionContext, DomainError>>` (current revision).
- `ResolveByProductionAsync(string productionCode, string? machineCode, ct) -> Task<Result<ControloFolhaProductionContext, DomainError>>`.
- Implemented by: `DapperControloProductionContextLookup`.

### 5.2 Internal area — Peso
- `IPesoRepository` — see §4.2; implemented by `DapperPesoRepository`.
- `IPdfRenderer` — see §4.2; implemented by `PesoSingleFilePdfRenderer` (registered `AddSingleton<IPdfRenderer, PesoSingleFilePdfRenderer>()`, Program.cs:196).

### 5.3 Internal area — Pegamentos
- `IPegamentoRepository` — see §4.3; implemented by `DapperPegamentoRepository`.
- `IJobOnProductionContextLookup` — see §4.3; implemented by `DapperJobOnProductionContextLookup` (shared Job On read-model adapter, not Pegamentos-owned).
- Consumed shared ports (not Pegamentos-specific, not remapped): `IJobOnProductionFolderResolver` (implemented by `DapperJobOnProductionFolderResolver`, reads `job_on.production_folder` — N13), `IAppSettingsReader.GetOutputRootAsync` (implemented by `DapperAppSettingsReader`), `IPegamentoPdfRenderer` (implemented by `PegamentoPdfRenderer`, registered singleton Program.cs:207).

Shared persistence contract consumed by Controlo area (not remapped): `IDbUnitOfWork` (`IRepairUnitOfWorkFactory.BeginAsync`).

## 6. Authorization / Catalog Objects

| Identifier | Value | Source | Role |
|---|---|---|---|
| Controlo entry kind | `ModuleKind.Module` (NOT FunctionalArea) | `CanonicalModuleCatalog.cs` line 99 (Build: `ControloAreaId, "Controlo", ModuleKind.Module, 20, "/controlo"`, assignable) | Module-catalog entry for the Controlo top-level grant (order 20) |
| Controlo area id | `controlo` | `CanonicalModuleCatalog.ControloAreaId` | Canonical module id; route `/controlo`; assignable top-level grant |
| Controlo capabilities | `controlo.view`, `controlo.edit`, `controlo.submit`, `controlo.review` | `CanonicalModuleCatalog.ControloView/Edit/Submit/ReviewCapabilityId` (declared on the Controlo entry) | Gate the Folha de Controlo operations (also in `ControloSheetModuleCatalog`) |
| Area children (internal areas) | `[peso, pegamentos]` | `CanonicalModuleCatalog.AreaChildren[ControloAreaId]` (lines 57–61) | Technical child ids; never assignable |
| Peso entry | `peso`, ModuleKind.Module, order 21, `/peso`, capability `peso.aprovar`, `isAssignable: false` | `CanonicalModuleCatalog.cs` lines 107–109 | Internal area (Operador/Responsável surface); entry derived from the controlo grant |
| Pegamentos entry | `pegamentos`, ModuleKind.Module, order 22, `/pegamentos`, no capabilities, `isAssignable: false` | `CanonicalModuleCatalog.cs` lines 110–112 | Internal area; entry derived from the controlo grant |
| Derived entry semantics | granting `controlo` ⇒ effective modules also contain `peso` + `pegamentos` | `AccessResolver.Resolve` (lines 149–153) | Why `user.HasModule("peso")` passes for Controlo-granted identities (gate consistency) |
| Module policy | `ModulePolicies.Controlo = "BaDmo.Module.controlo"` (+ `ModulePolicies.Peso`, `ModulePolicies.Pegamentos`) | `ModuleAuthorizationHandler.cs` lines 55–57 | Route module policy (page/route entry) |
| Capability policies | `CapabilityPolicies.ControloView/Edit/Submit/Review` (`BaDmo.Capability.controlo.*`), `CapabilityPolicies.PesoAprovar` | `ModuleAuthorizationHandler.cs` lines 79–84 | Route-level capability policies |
| Policy registration | one `ModulePolicies.Prefix + moduleId` and one `CapabilityPolicies.Prefix + capabilityId` per canonical catalog entry | `Program.cs` lines 118–131 (loop over `CanonicalModuleCatalog.Instance.Modules`) | Composition root builds all module/capability policies |
| Sheet surface module | `SurfaceModuleId = "peso"` | `ControloSheetAuthorizationGate.cs` | Module id checked by the sheet gate via `user.HasModule("peso")` (consistent per AccessResolver above) |

Note: `CanonicalPageCatalog` (Application Shared/Access) NOW contains a `/controlo` entry: `ControloResumoPageId` ("controlo.resumo", module `controlo`, `/controlo`, required capability `controlo.view`, order 20). Peso has TWO page entries: `PesoOperadorPageId` ("peso.operador", `/peso`, no required capability, order 21) and `PesoResponsavelPageId` ("peso.responsavel", `/peso/responsavel`, `peso.aprovar`, order 21). Pegamentos: `PegamentosFolhaPageId` ("pegamentos.folha", `/pegamentos`, order 22). (13 canonical pages total; the old map text claiming "no /controlo page entry" is STALE.)

## 7. Infrastructure Objects

Location: `src\BA.Dmo.Infrastructure\Access\`

### 7.1 Controlo area

#### DapperControloSheetRepository : IControloSheetRepository — `DapperControloSheetRepository.cs`
- Constructor: `IDbConnectionFactory`.
- Methods: `InsertAsync` (INSERT `controlo_sheets` then items via `InsertItemsAsync`, one UoW), `GetByIdAsync`, `GetForProductionAsync`, `ListByProductionAsync`, `ListAsync`, `UpdateAsync` (UPDATE header + clear then UPDATE item control facts `result/observation/mcaliper_link`), `InsertEventAsync` (INSERT `controlo_sheet_events`).
- Private: `InsertItemsAsync`, `LoadItemsAndEventsAsync`, `MapHeader` (hydrates `ControloFolha` via `ControloFolhaStateCodec`), `DisposeAsync`.
- Embedded SQL tables: `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`; state mapping via `ControloFolhaStateCodec`.

#### DapperControloProductionContextLookup : IControloProductionContextLookup — `DapperControloProductionContextLookup.cs`
- Constructor: `IDbConnectionFactory`, `IJobOnRepository`.
- Methods: `ResolveAsync` (via `_jobOnRepository.GetByIdAsync`), `ResolveByProductionAsync` (via `_jobOnRepository.GetByProductionCodeAsync` + machine match; error `CONTROLO_MACHINE_MISMATCH`), private `ResolveJobOnAsync`.
- Reads Job On read model: `job_on_revision` snapshots + `job_on_component` rows. ⚠️ **FAMILY FILTER (corrected)**: the components SQL filters `c.family IN ('MP_CM', 'MF', 'BQ', 'PU', 'CS')` — FIVE families, not three (current code line 96; pinned by `ControloProjectionGuardTests`). The previous map text ("filtered to families MP_CM, MF, BQ") was STALE.
- Errors: `CONTROLO_JOBON_NOT_FOUND`, `CONTROLO_NO_REVISION`, `CONTROLO_REVISION_MISSING`, `CONTROLO_CONTEXT_INCOMPLETE`.
- JSON helpers: `ExtractString` (JSON doc property), `ExtractReference`.

Infrastructure Controlo area files: 2.

### 7.2 Internal area — Peso

#### DapperPesoRepository : IPesoRepository — `DapperPesoRepository.cs`
- Constructor: `IDbConnectionFactory`.
- References: `CreateReferenceAsync`/`GetReferenceByIdAsync`/`GetReferencesAsync(search)`/`GetReferenceByMoldNeckringAsync`/`UpdateReferenceAsync` over `peso_references`.
- Lots: `CreateLoteAsync` (serializes `allowed_lines` array)/`GetLoteByIdAsync`/`GetLotesAsync` over `peso_lotes`.
- Controls: `CreateControlAsync` (DapperUnitOfWork transaction: `peso_controlos` + `peso_leituras` rows, `readings` JSON); `GetControlByIdAsync` (LEFT JOIN `peso_references` for mold/neckring); `GetControlsAsync` (filters + ILIKE search); `GetApprovedControlsForJobOnAsync` (status `aprovado`); `UpdateControlAsync` (UPDATE header + DELETE + re-INSERT leituras); `DeleteControlAsync`.
- Previous resolution: `GetPreviousApprovedAsync` (TD-13/TD-30 — earlier production/date, `peso_controlos` only; `ExtractSnapshotAverages` from `measurements_snapshot`).
- Day approvals: `SaveDayApprovalAsync` (ON CONFLICT upsert `peso_day_approvals`); `GetRecordDatesAsync` (DISTINCT to_char).
- Settings: `SaveSettingAsync`/`GetSettingAsync` over `peso_settings`.
- Audit: `InsertAuditEventAsync` → shared `audit_events` with `module_id = 'peso'`, `entity_type = 'peso_controlo'`.
- Mappers: `MapReference`, `MapLote`, `MapControl`, `MapLeitura`, `DeserializeReadings`, `BuildMeasurementsSnapshot` (recomputes averages using `WeightCalculator` for the PDF path).

#### PesoSingleFilePdfRenderer : IPdfRenderer — `PesoSingleFilePdfRenderer.cs`
- Deterministic single-page A4 PDF (72 DPI points, 595×842) generated from the APPROVED snapshot only (historical integrity); DMO colour tokens (RGB from dmo-tokens.css); text escapes non-ASCII via `\uXXXX` for Helvetica; assembles raw PDF 1.4 bytes (`%PDF-1.4` … `%%EOF`).

Infrastructure Peso files: 2.

### 7.3 Internal area — Pegamentos

#### DapperPegamentoRepository : IPegamentoRepository — `DapperPegamentoRepository.cs`
- Constructor: `IDbConnectionFactory`.
- Status codec: `ToDbStatus/FromDbStatus` (`aberto`/`fechado`, N07 default 'aberto').
- Controls: `CreateAsync` (INSERT `pegamento_controlos`; snapshots serialized as JSON — `reference_snapshot`, `cm/bq/mf_snapshot`), `GetByIdAsync` (loads measurements then RECONSTRUCTS `Ovalizacao/Media/ToleranceStatus` via `PegamentoMeasurementCalculator` with historical nominals; legacy N16 rows without nominal → `NotEvaluable`), `GetByRevisionAsync`, `GetByJobOnAsync`, `SearchAsync` (reference ILIKE on snapshot text + production/machine/from/to), `UpdateAsync` (tolerance/status/notas only — never the revision anchor).
- Measurements: `AddMeasurementAsync` (INSERT `pegamento_medicoes` with `tool_number`, `actor_id`), `GetMeasurementsAsync`.
- Documents (N14): `UpsertDocumentAsync` (ON CONFLICT (pegamento_controlo_id) DO UPDATE — one per control), `GetDocumentAsync`.
- JSON helpers: `SerializeToolSnapshot`/`DeserializeToolSnapshot`, `DeserializeString`.

#### PegamentoPdfRenderer : IPegamentoPdfRenderer — `PegamentoPdfRenderer.cs`
- Deterministic single-page PDF from the frozen `PegamentoPdfData` snapshot ONLY (never current Job On / tool / nominal / settings state); Helvetica; strips/normalizes non-ASCII for the base font; assembly-style PDF 1.4; per-component blocks CM/BQ/MF with nominal + corridor + measurement rows; status line all-within-corridor.

Infrastructure Pegamentos files: 2 (direct) + 3 shared consumed (`DapperJobOnProductionContextLookup`, `DapperJobOnProductionFolderResolver`, `DapperAppSettingsReader`).

## 8. Database Objects

### 8.1 Controlo area — source `database\migrations\N23_controlo_folha.sql` (mirrored in `database\consolidated_clean_install.sql`, tables ~lines 1140–1204)

| Object | Kind | Main technical role | PK | Important FKs | Notes |
|---|---|---|---|---|---|
| `controlo_sheets` | Table | One production control summary sheet | `controlo_sheet_id uuid` | `job_on_id → job_on`, `job_on_revision_id → job_on_revision`, `created_by/submitted_by/decided_by → internal_users` | CHECK `ck_controlo_sheets_status` (`rascunho/submetido/aprovado/rejeitado`); CHECK `ck_controlo_sheets_decision` (decision triad consistent); `display_id` document id; indexes `ix_controlo_sheets_job_on/revision/production/status` |
| `controlo_sheet_items` | Table | Per-component/tool snapshot + control result | `controlo_sheet_item_id uuid` | `controlo_sheet_id → controlo_sheets ON DELETE CASCADE`, `source_tool_id → tool_references`, `source_lot_id → tool_lotes` | CHECK `ck_controlo_sheet_items_result` (NULL/OK/NOK); indexes `ix_controlo_sheet_items_sheet/family` |
| `controlo_sheet_events` | Table | Append-only audit of create/edit/submit/reopen/decide | `controlo_sheet_event_id uuid` | `controlo_sheet_id → controlo_sheets ON DELETE CASCADE`, `actor_id → internal_users` | CHECK `ck_controlo_sheet_events_type` (`criar/editar/submeter/reeabrir/decidir`); `before_summary/after_summary jsonb`; trigger below |
| `trg_controlo_sheet_events_append_only` | Trigger | Blocks UPDATE/DELETE on `controlo_sheet_events` | — | — | `BEFORE UPDATE OR DELETE ... EXECUTE FUNCTION ba_dmo_guard_append_only()` (shared function defined in `N01_identity.sql`) |

Controlo area DB objects: 3 tables + 1 trigger = 4.

### 8.2 Internal area — Peso — source `database\migrations\N06_peso.sql` (7 tables, 5 indexes, no triggers in-file)

| Object | Kind | Main technical role | Notes |
|---|---|---|---|
| `peso_references` | Table | Peso master reference | PK `peso_reference_id uuid`; UNIQUE `uq_peso_references_mold_neckring` (mold_number, neckring_number); `change_log jsonb DEFAULT '[]'`; FKs `created_by/updated_by → internal_users` |
| `peso_lotes` | Table | Peso lot (process NNPB/PS + allowed lines) | PK `peso_lote_id`; FK `peso_reference_id → peso_references`; UNIQUE `uq_peso_lotes_reference_lote`; CHECK `ck_peso_lotes_processo` (NNPB/PS), `ck_peso_lotes_allowed_lines` (cardinality ≥ 1); index `ix_peso_lotes_reference` |
| `peso_controlos` | Table | Control/comparison record | PK `peso_controlo_id`; FKs `peso_reference_id`, `peso_lote_id`, `job_on_id → job_on`, `job_on_revision_id → job_on_revision` (TD-18), `approved_by/created_by → internal_users`; UNIQUE `uq_peso_controlos_identity`; CHECK `ck_peso_controlos_record_type` (novo_controlo/comparacao), `ck_peso_controlos_status` (rascunho/pendente/aprovado/nao_aprovado); `measurements_snapshot jsonb`, `previous_control jsonb`, `comparison_decisions jsonb`; indexes `ix_peso_controlos_reference/job_on/job_on_revision/status_date` |
| `peso_leituras` | Table | Append-only reading facts | PK `peso_leitura_id`; FK `peso_controlo_id → peso_controlos ON DELETE CASCADE`; UNIQUE `uq_peso_leituras_controlo_cm`; `readings jsonb` |
| `peso_comparacao_anterior` | Table | Previous-approved read path (TD-13/TD-30) | PK `peso_controlo_id → peso_controlos ON DELETE CASCADE`; self-FK `previous_peso_controlo_id`; `previous_snapshot/deltas jsonb` |
| `peso_day_approvals` | Table | Day-approval facts | PK `peso_day_approval_id`; UNIQUE `uq_peso_day_approvals_identity` (mold, neckring, line, approval_date); `approved_by → internal_users` |
| `peso_settings` | Table | Editable process constants (`constant_nnpb`/`constant_ps`, email recipients) + flags | PK `setting_key text`; `setting_value jsonb`; `updated_by → internal_users` |

Peso RLS: `peso_*` tables pre-date `N12_rls.sql`; their RLS/policy/grants come from `N12_rls.sql` (consolidated mirror lists all 7 `peso_*` tables + `pegamento_controlos`/`pegamento_medicoes` in the N12-style RLS array, `database\consolidated_clean_install.sql` lines ~1240–1250) — they are NOT in N25's late-table loop.
Peso integrity: `N25_remediation.sql` adds CHECK `ck_peso_controlos_approved_consistent` (`(status='aprovado') = (approved_at_utc IS NOT NULL)`) and trigger `trg_peso_controlos_approved_guard` via new function `ba_dmo_guard_peso_approved()` (blocks DELETE and identity-changing UPDATE of approved controls).

### 8.3 Internal area — Pegamentos — sources `N07`, `N14`, `N15`, `N16`, `N17`

| Object | Kind | Main technical role | Notes |
|---|---|---|---|
| `pegamento_controlos` | Table (N07) | Pegamento control aggregate | PK `pegamento_controlo_id`; FKs `job_on_id → job_on`, `job_on_revision_id → job_on_revision` (immutable anchor); `reference_snapshot jsonb`, `cm/bq/mf_snapshot jsonb`, `nominal_average numeric(18,4)`, `tolerance numeric(6,3) DEFAULT 0.20` + CHECK `ck_pegamento_controlos_tolerance` (≥0), `status text DEFAULT 'aberto'` + CHECK `ck_pegamento_controlos_status` (N25: aberto/fechado); indexes `ix_pegamento_controlos_job_on/job_on_revision/production` |
| `pegamento_medicoes` | Table (N07) | Append-only measurement facts | PK `pegamento_medicao_id`; FK `pegamento_controlo_id → pegamento_controlos`; `component_key text`, `costura/contra_costura numeric(18,4)`, `measured_at_utc`, `actor_id → internal_users`; index `ix_pegamento_medicoes_controlo`; trigger `trg_pegamento_medicoes_append_only` (append-only via `ba_dmo_guard_append_only`) |
| `pegamento_documentos` | Table (N14) | Final-document metadata (one per control) | PK `pegamento_documento_id`; `pegamento_controlo_id UNIQUE → pegamento_controlos` (one-per-control); `filename`, `output_root_snapshot`, `production_folder_snapshot`, `generated_at_utc`, `generated_by → internal_users`; index `ix_pegamento_documentos_controlo` |
| `pegamento_medicoes.tool_number` | Column (N15) | Tool/cavity number of the measurement | `ALTER TABLE ... ADD COLUMN IF NOT EXISTS tool_number integer NULL` + index `ix_pegamento_medicoes_component_tool` (controlo_id, component_key, tool_number) |
| `pegamento_controlos.cm_nominal/bq_nominal/mf_nominal` | Columns (N16) | Frozen historical nominals | 3× `ADD COLUMN IF NOT EXISTS ... numeric(18,4) NULL` |
| `pegamento_controlos.notas` | Column (N17) | Optional notes | `ADD COLUMN IF NOT EXISTS notas text NULL` |

Pegamentos RLS: `pegamento_documentos` IS in N25's late-table loop (10 tables: `pegamento_documentos, tool_usage_records, repairer_repair_types, tampao_configuration_machines/notes/machine_event, controlo_sheets, controlo_sheet_items, controlo_sheet_events, jobon_user_current` — single policy `ba_dmo_app_access FOR ALL TO ba_dmo_app` + REVOKE from anon/authenticated + GRANT to ba_dmo_app). `pegamento_controlos`/`pegamento_medicoes` pre-date N12 → RLS via `N12_rls.sql`.

## 9. Migration Touchpoints

Current migration inventory: `database\migrations\N01_identity.sql` … `N31_template_profiles_single_assignment.sql` (31 files).

| Migration | Area | Object(s) | Technical Change |
|---|---|---|---|
| `N06_peso.sql` | Peso | 7 `peso_*` tables + 5 indexes | `CREATE TABLE IF NOT EXISTS` ×7 (peso_references, peso_lotes, peso_controlos, peso_leituras, peso_comparacao_anterior, peso_day_approvals, peso_settings); UNIQUE/CHECK constraints; indexes ×5. No RLS in-file (pre-N12) |
| `N07_pegamentos.sql` | Pegamentos | `pegamento_controlos`, `pegamento_medicoes` | `CREATE TABLE IF NOT EXISTS` ×2; CHECK `ck_pegamento_controlos_tolerance`; `ck_pegamento_controlos_status` added later (N25); trigger `trg_pegamento_medicoes_append_only` (ba_dmo_guard_append_only); indexes ×4 |
| `N12_rls.sql` | Peso/Pegamentos (pre-N12 tables) | RLS on all pre-N12 tables | Enables RLS/policy `ba_dmo_app_access`/REVOKE/GRANT for tables existing at N12 time, incl. `peso_*` and `pegamento_controlos`/`pegamento_medicoes` |
| `N13_jobon_production_folder.sql` | Pegamentos prerequisite (Job On-owned) | `job_on.production_folder` | `ALTER TABLE job_on ADD COLUMN IF NOT EXISTS production_folder text NULL` — consumed by `IJobOnProductionFolderResolver` in `ConfirmDocumentSavedAsync` |
| `N14_pegamentos_documents.sql` | Pegamentos | `pegamento_documentos` | `CREATE TABLE IF NOT EXISTS` + UNIQUE one-per-control + index `ix_pegamento_documentos_controlo`; RLS in N25 |
| `N15_pegamentos_tool_number.sql` | Pegamentos | `pegamento_medicoes.tool_number` | `ADD COLUMN IF NOT EXISTS tool_number integer NULL` + index `ix_pegamento_medicoes_component_tool` |
| `N16_pegamentos_component_nominals.sql` | Pegamentos | `pegamento_controlos.cm_nominal/bq_nominal/mf_nominal` | 3× `ADD COLUMN IF NOT EXISTS numeric(18,4) NULL` |
| `N17_pegamentos_notas.sql` | Pegamentos | `pegamento_controlos.notas` | `ADD COLUMN IF NOT EXISTS notas text NULL` |
| `N23_controlo_folha.sql` | Controlo | `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, trigger | `CREATE TABLE IF NOT EXISTS` ×3; CHECK constraints ×3; indexes ×7; `trg_controlo_sheet_events_append_only` using `ba_dmo_guard_append_only`. No RLS stanza in-file (comment: matches N18–N22 additive tables) |
| `N25_remediation.sql` | Controlo + Pegamentos + Peso | RLS late-table loop; Peso approved-guard | Adds to late-table loop (RLS/policy/grants): `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, `pegamento_documentos`, … (10 tables). Peso: CHECK `ck_peso_controlos_approved_consistent` + function `ba_dmo_guard_peso_approved()` + trigger `trg_peso_controlos_approved_guard`. Also 4× revision-family append-only triggers (job_on_revision/components) |

`database\consolidated_clean_install.sql` mirrors the same DDL: controlo tables at lines ~1140/1171/1189, N12-style RLS array (incl. `peso_*`) at lines ~1240–1250, N25-style late-table loop at lines ~1551–1572 (updated line numbers; the previous map cited 1119–1185/1537–1578 which is STALE).

## 10. Web / Routes

### 10.1 Controlo area

#### Razor Page — `src\BA.Dmo.Web\Pages\Controlo\Index.cshtml` + `Index.cshtml.cs`
- Route: `@page "/controlo"`; authorized by `ModulePolicies.Controlo` attribute (line 3). ⚠️ (previous map text said `ModulePolicies.Peso` — STALE; the page now uses the Controlo module policy, consistent with the catalog entry `controlo.resumo`).
- `IndexModel : PageModel` — constructor `ICurrentUserAccessor`; `OnGet(Guid? jobOn)` sets `ProjectedJobOnId`, `CanEdit` (`controlo.edit`), `CanSubmit` (`controlo.submit`), `CanReview` (`controlo.review`).
- Markup surfaces: active-production card (`#activeCard`), workspace tabs (`resumo/peso/comparacao/pegamentos/historico`), Resumo items/history tables, Peso/Comparação/Pegamentos embed sections (`#btnOpenPeso/#btnOpenComparacao/#btnOpenPegamentos`), Histórico list. Loads `~/scripts/controlo.js` with `defer`.

#### Page-catalog registration — `CanonicalPageCatalog.cs`
- `ControloResumoPageId` ("controlo.resumo") → module `controlo`, route `/controlo`, required capability `controlo.view`, display order 20 (source-verified lines 43–45).

#### API Endpoints — `src\BA.Dmo.Web\Program.cs` (lines 1179–1260; all `RequireAuthorization(ModulePolicies.Controlo)`)
All invoke `ControloSheetService`; operations additionally gated server-side by the `controlo.*` capability via `ControloSheetAuthorizationGate` (which also requires `user.HasModule("peso")` — derived from the controlo grant).

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `GET /api/controlo/production` | `ControloSheetService.GetForProductionAsync(jobOnId)` | ModulePolicies.Controlo | Program.cs:1179 |
| `GET /api/controlo/list` | `ControloSheetService.ListSheetsAsync(from,to,machine,jobOn,status)` | ModulePolicies.Controlo | Program.cs:1188 |
| `GET /api/controlo/by-production` | `ControloSheetService.GetForProductionByContextAsync(production,machine)` | ModulePolicies.Controlo | Program.cs:1199 |
| `POST /api/controlo` | `ControloSheetService.CreateAsync(request)` | ModulePolicies.Controlo | Program.cs:1208 |
| `GET /api/controlo/{sheetId:guid}` | `ControloSheetService.GetDetailAsync(sheetId)` | ModulePolicies.Controlo | Program.cs:1217 |
| `POST /api/controlo/{sheetId:guid}/items` | `ControloSheetService.UpdateItemsAsync(...)` | ModulePolicies.Controlo | Program.cs:1226 |
| `POST /api/controlo/{sheetId:guid}/submit` | `ControloSheetService.SubmitAsync(...)` | ModulePolicies.Controlo | Program.cs:1236 |
| `POST /api/controlo/{sheetId:guid}/reopen` | `ControloSheetService.ReopenAsync(...)` | ModulePolicies.Controlo | Program.cs:1245 |
| `POST /api/controlo/{sheetId:guid}/decide` | `ControloSheetService.DecideAsync(...)` | ModulePolicies.Controlo | Program.cs:1254 |

#### Composition Root (DI) — `src\BA.Dmo.Web\Program.cs` (lines 244–250)
- `IControloProductionContextLookup → DapperControloProductionContextLookup`
- `IControloSheetRepository → DapperControloSheetRepository`
- `ControloSheetAuthorizationGate` (Scoped)
- `ControloSheetService` (Scoped)

### 10.2 Internal area — Peso

#### Razor Pages — `src\BA.Dmo.Web\Pages\Peso\` (2 user surfaces)
- `Index.cshtml` + `Index.cshtml.cs` — route `@page "/peso"`, authorized by `ModulePolicies.Peso`; `IndexModel.OnGetAsync` returns `Forbid()` when the user lacks the peso module, and `Redirect("/peso/responsavel")` when the user holds `peso.aprovar` (Operador exclusivity, GLM-ACC-05.2/UD-06). Server-rendered references list (`Model.References`); `data-testid="page-peso-operador"`. Views: Novo controlo / Referências / Comparação / Histórico / Definições; embeds the calendar surrogate (`data-dmo-calendar`) and the controlo cross-link buttons (`#btnFolhaControlo`, `#btnFolhaControloHist`). Loads `~/scripts/peso.js`.
- `Responsavel.cshtml` + `Responsavel.cshtml.cs` — route `@page "/peso/responsavel"`, authorized by `ModulePolicies.Peso`; `ResponsavelModel.OnGetAsync` returns `Forbid()` without the peso module and `Redirect("/peso")` WITHOUT `peso.aprovar` (Responsável exclusivity). ONE approval page (calendar + day queue + detail; no second Comparações view); `Model.Pending` via `SearchControlsAsync(status=pendente)`; `data-testid="page-peso-responsavel"`. Loads `~/scripts/peso.js`.
- Page-catalog: `PesoOperadorPageId` ("peso.operador", `/peso`, order 21) and `PesoResponsavelPageId` ("peso.responsavel", `/peso/responsavel`, required `peso.aprovar`, order 21).

#### API Endpoints — `src\BA.Dmo.Web\Program.cs` (lines 382–566)
Gated by `ModulePolicies.Peso` (module entry) for Operador operations; approval/decision endpoints additionally gated by `CapabilityPolicies.PesoAprovar`; every use case also re-checks the capability via `PesoAuthorizationGate`:
`POST /api/peso/control` (396) · `POST /api/peso/{controlId}/save` (404) · `POST /api/peso/{controlId}/submit` (411) · `POST /api/peso/{controlId}/calculate` (418) · `POST /api/peso/{controlId}/approve` (427, PesoAprovar) · `POST /api/peso/{controlId}/reject` (434, PesoAprovar) · `POST /api/peso/{controlId}/reopen` (441, PesoAprovar) · `POST /api/peso/{controlId}/delete` (448) · `POST /api/peso/{controlId}/compare/decide` (455, PesoAprovar) · `GET /api/peso/control/{controlId}` (464) · `GET /api/peso/controls` (472) · `GET /api/peso/dates` (480) · `POST /api/peso/settings` (487, PesoAprovar) · `POST /api/peso/{controlId}/document` (495; injects `IPdfRenderer`) · `POST /api/peso/{controlId}/email/prepare` (505) · `GET /api/peso/references` (514) · `POST /api/peso/reference` (522) · `POST /api/peso/lote` (531) · `POST /api/peso/day-approval` (540, PesoAprovar) · `POST /api/peso/comparison` (549) · `GET /api/peso/settings/{key}` (558).

#### Composition Root (DI) — `src\BA.Dmo.Web\Program.cs` (lines 192–196)
- `IPesoRepository → DapperPesoRepository`; `PesoAuthorizationGate`; `PesoService`; `IPdfRenderer → PesoSingleFilePdfRenderer` (Singleton).

### 10.3 Internal area — Pegamentos

#### Razor Pages — `src\BA.Dmo.Web\Pages\Pegamentos\` (2 files × .cshtml/.cs)
- `Index.cshtml` + `Index.cshtml.cs` — route `@page "/pegamentos"`, authorized by `ModulePolicies.Pegamentos`; `IndexModel.OnGet` sets `CanEdit` from `jobon.edit` capability (page comment: fixing tools on a Job On / opening a control sheet requires Job On edit); views Históricos / Nova folha / Configuração; `data-testid="page-pegamentos"`; loads `~/scripts/pegamentos.js`.
- `Detail.cshtml` + `Detail.cshtml.cs` — route `@page "/pegamentos/{id:guid}"`, authorized by `ModulePolicies.Pegamentos`; `DetailModel.OnGet(Guid id)` exposes `ControloId`; displays inherited historical context + measurements; `data-testid="page-pegamentos-detail"`; loads `~/scripts/pegamentos.js`.
- Page-catalog: `PegamentosFolhaPageId` ("pegamentos.folha", `/pegamentos`, order 22).

#### API Endpoints — `src\BA.Dmo.Web\Program.cs` (lines 568–694; all `RequireAuthorization(ModulePolicies.Pegamentos)`)
`GET /api/pegamentos/context/{jobOnRevisionId:guid}` (574) · `GET /api/pegamentos/revision/{jobOnRevisionId:guid}` (583) · `GET /api/pegamentos/jobon/{jobOnId:guid}` (592) · `GET /api/pegamentos/{controloId:guid}` (601) · `POST /api/pegamentos` (610) · `POST /api/pegamentos/{controloId}/measurements` (620) · `PUT /api/pegamentos/{controloId}` (634) · `POST /api/pegamentos/{controloId}/close` (647) · `GET /api/pegamentos/{controloId}/history` (657) · `GET /api/pegamentos/search` (666) · `POST /api/pegamentos/{controloId}/document/generate` (677; PegamentoPdfService + IPegamentoPdfRenderer) · `POST /api/pegamentos/{controloId}/document/confirm` (687).

#### Composition Root (DI) — `src\BA.Dmo.Web\Program.cs` (lines 198–207)
- `IPegamentoRepository → DapperPegamentoRepository`; `IJobOnProductionContextLookup → DapperJobOnProductionContextLookup`; `PegamentoAuthorizationGate`; `PegamentoService`; `PegamentoPdfService`; `IJobOnProductionFolderResolver → DapperJobOnProductionFolderResolver`; `IAppSettingsReader → DapperAppSettingsReader`; `IPegamentoPdfRenderer → PegamentoPdfRenderer` (Singleton).

Web dedicated page files (whole slice): Controlo 2 (`Pages\Controlo\Index.cshtml(.cs)`), Peso 4 (`Pages\Peso\{Index,Responsavel}.cshtml(.cs)`), Pegamentos 4 (`Pages\Pegamentos\{Index,Detail}.cshtml(.cs)`); static asset files: 6 (see Static Assets). Shared Web files carrying wiring (`Program.cs`, `ModuleAuthorizationHandler.cs`, `CanonicalPageCatalog.cs`, `AccessResolver.cs`) are not dedicated slice files and are not counted.

## 11. Static Assets

### 11.1 Controlo area

#### `src\BA.Dmo.Web\wwwroot\scripts\controlo.js`
IIFE (`BA DMO — Controlo unified production workspace wiring`). Key facts (grep-verified):
- Selectors: `#toast`, `#canEdit/#canSubmit/#canReview`, `#btnCarregarJobOn`, `#activeCard`, `#cardDisplay/#cardSub`, `.controlo-tabs .tab`, `.controlo-tab-view`, `#controloEmpty/#controloLoading/#controloError`, `#resumoNeedsContext`, `#controloContext`, `#controloItemsCard`, `#controloHistoryCard`, `#controloItems tbody`, `#controloHistory tbody`, `#controloActions`, `#btnOpenPeso`, `#btnOpenComparacao`, `#btnOpenPegamentos`, `#controloHistoryTable tbody`, `#historyEmpty`.
- Functions: `esc`, `showToast`, `api`, `jsonPost`, `stateLabel`, `showEmpty`, `activateCard`, `detachCard`, `clearCard`, `refreshTabStates`, `loadResumo`, `renderItems`, `renderHistory`, `renderActions`, `collectEdits`, `handleAction` (save/submit/reopen/approve/reject), `loadHistoryList`, `fmtDT`, `activateFromJobOnId`, `selectSection`, `init`.
- API endpoints called: `GET /api/jobon/current`, `GET /api/controlo/production?jobOnId=`, `POST /api/controlo/{id}/items`, `POST /api/controlo/{id}/submit`, `POST /api/controlo/{id}/reopen`, `POST /api/controlo/{id}/decide`, `GET /api/controlo/list`.
- Deep-link handling: `params.get('jobOn')` + `params.get('section')` → `selectSection` (cross-module entry from Job On / Peso, pinned by `JobOnScriptSafetyGuardTests.JobOnCrossModuleLinks_ActivateRequestedControloSection`).
- Cross-route navigation: `#btnOpenPeso`/`#btnOpenComparacao` → `window.location.href = '/peso'`; `#btnOpenPegamentos` → `'/pegamentos'`.

#### `src\BA.Dmo.Web\wwwroot\styles\modules\controlo-layout.css`
Layout/composition only (uses shared `--dmo-*` tokens; allowed module-layout set per `DesignSystemGuardTests.AllowedModuleLayouts`): `.controlo-page`, `.controlo-active-card(-body)`, `.controlo-card-title/line/sub/actions`, `.controlo-hint`, `.controlo-tabs`, `.controlo-tab-view(.active)`, `.controlo-context-grid`, `.controlo-items-card`, `.controlo-history-card`, `.controlo-tab-note`, `.controlo-tab-embed`; responsive blocks at `@media (max-width: 720px)` and `520px`.

### 11.2 Internal area — Peso

#### `src\BA.Dmo.Web\wwwroot\scripts\peso.js`
Operador + Responsável wiring over `/api/peso/*` (grep-verified): `/api/peso/control` (create), `{id}/save`, `{id}/submit`, `{id}/calculate` (live preview — C#-only engine; JS never duplicates constants), `{id}/approve`, `{id}/reject`, `{id}/reopen`, `{id}/delete`, `{id}/compare/decide`, `/api/peso/control/{id}`, `/api/peso/controls`, `/api/peso/dates`, `/api/peso/settings` (+`GET /api/peso/settings/{key}` for `constant_nnpb`/`constant_ps`), `/api/peso/{id}/document`, `/api/peso/{id}/email/prepare`, `/api/peso/references`, `/api/peso/reference`, `/api/peso/lote`, `/api/peso/day-approval`, `/api/peso/comparison`.
- Controlo cross-link: `openFolhaControloForSelection(...)` → `window.location.href = "/controlo?..."` wired to `#btnFolhaControlo` (refControls) and `#btnFolhaControloHist` (historyTable) (lines ~1130–1137).

#### `src\BA.Dmo.Web\wwwroot\styles\modules\peso-layout.css`
Layout/composition only (color-free per GLM-DSN-03): Peso Operador/Responsável grids, approval layout, history/calendar layout, result summary, machine grid, queues.

### 11.3 Internal area — Pegamentos

#### `src\BA.Dmo.Web\wwwroot\scripts\pegamentos.js`
Wiring over `/api/pegamentos/*` (grep-verified): `/api/pegamentos/search`, `/api/pegamentos/{id}/history`, `/api/pegamentos/context/{jobOnRevisionId}`, `POST /api/pegamentos`, `GET /api/pegamentos/{id}`, `POST /api/pegamentos/{id}/measurements`, `PUT /api/pegamentos/{id}`, `POST /api/pegamentos/{id}/close`, `POST /api/pegamentos/{id}/document/generate`, `POST /api/pegamentos/{id}/document/confirm`. Calculations (ovalização/média/tolerance) are C#-only (`PegamentoMeasurementCalculator`).

#### `src\BA.Dmo.Web\wwwroot\styles\modules\pegamentos-layout.css`
Layout/composition only: Pegamentos tabs, filters, context grid, measurement table, sheet layout.

Static assets (whole slice): 6 files (3 scripts + 3 module layout CSS). MAPPED.

## 12. Tests

### 12.1 Controlo area — location `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\` (tests re-based from obsolete `tests\` prefix)

#### ControloFolhaTests — unit (domain invariants) — `ControloFolhaTests.cs` (7 tests)
Target: `ControloFolha`/`ControloFolhaItem`/`ControloFolhaState`. Method groups: `Create_SnapshotsComponentsAndPinsRevision` (5 families MP_CM/MF/BQ/PU/CS; DisplayId `Controlo_202601_5447T173_B1`); `Create_WithoutContext_Fails` (`CONTROLO_CONTEXT_REQUIRED`); `Submit_ThenDecide_Flow_Approved`; `Decide_WithoutSubmission_Fails` (`CONTROLO_NOT_SUBMITTED`); `Submit_AfterDecision_IsRejected_ReopenAllowsResubmit`; `EditItemsAfterSubmission_IsAllowed_AndUpdatesResults` (edit after submission allowed); `RecordEvent_IsAppendOnly`.

#### ControloSheetServiceTests — unit (application use cases) — `ControloSheetServiceTests.cs` (8 tests)
Target: `ControloSheetService` + gate + contracts. `GetForProduction_NoExistingSheet_CreatesOneFromProductionContext`; `GetForProduction_SnapshotsAllFiveResumoFamiliesFromExactJobOnRevision`; `UpdateItems_AppliesControlAndLeavesState` ("editar"); `Submit_ThenReview_Flow` ("submeter" + `controlo.review` decide); `Reopen_AfterSubmission_ReturnsToDraft` ("reeabrir"); `Create_WithoutEditCapability_Forbidden`; `GetForProductionByContext_ResolvesAndCreatesWithoutReSelection`; `ListSheets_WorksInFreeMode_NoCardRequired` (R012 §22/§23).

Controlo area unit test classes: 2 (15 tests).

#### ControloProjectionGuardTests — integration (static structure guard) — `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Controlo\ControloProjectionGuardTests.cs` (1 test)
`ProductionContextLookup_ProjectsExactlyTheFiveResumoFamilies` — asserts `DapperControloProductionContextLookup.cs` contains `c.family IN ('MP_CM', 'MF', 'BQ', 'PU', 'CS')` and NOT the 3-family clause (pins the 5-family projection).

### 12.2 Internal area — Peso — location `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Peso\`

| Test class | File | Count | Coverage |
|---|---|---|---|
| `PesoDomainTests` | PesoDomainTests.cs | 11 | `PesoValidator` reference/lote invariants, `ReportPathValidator`, codecs (`PesoProcessoCodec`/`PesoRecordTypeCodec`/`PesoControlStateCodec`); error codes `PESO_LOTE_NO_ALLOWED_LINE`, `PESO_LOTE_INVALID_LINE`, `PESO_LOTE_DUPLICATE_LINE`, `PESO_LOTE_SUBFOLDER_ABSOLUTE` |
| `PesoControlWorkflowTests` | PesoControlWorkflowTests.cs | 11 | `PesoControl` state machine: submit hard blocks (`PESO_CONTROL_NO_READING`), reject note (`PESO_CONTROL_REJECT_NOTE_REQUIRED`), approve records approver/time, reopen revision+1 (`PESO_CONTROL_REOPEN_REASON`), delete eligibility (`IsDeletable`), comparison base immutability, `PesoCmDecisionCodec` round-trip (`manter`/`colocar_de_parte`/`aside`) |
| `PesoServiceTests` | PesoServiceTests.cs | 21 | Application use cases incl. gate (`peso.aprovar` required for approve; Operador can create lote but not approve), reference save/edit (`PESO_REF_CHANGE_REASON_REQUIRED`), lot validation, control creation inheriting Job On context (TD-18), submit→approve+day-approval, reopen, delete policy, comparison creation/decisions (`PESO_COMPARISON_*`), settings OC-6 historical density, `PesoFileName` convention (`9262T288__202604__C3__L16.pdf`), document/email (`PESO_DOC_NOT_APPROVED`, `PESO_EMAIL_NO_RECIPIENTS`) |
| `WeightCalculatorTests` | WeightCalculatorTests.cs | 13 (12 Fact + 1 Theory ×31 rows) | Density table 5–35 (31 rows), rounding D1–D4, `PESO_TEMPERATURE_OUT_OF_RANGE`, glass weight formula D6–D8 (subtracts PU, adds neck), calote not in glass weight, deltas, glass average |

Also: `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\PesoComparisonGuardTests.cs` (3 static guards — Peso comparison UX contract, lives in the Design folder; see MAP-17) and `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Peso\PesoPdfVisualCheck.cs` (1 test — writes `sample_peso.pdf` for manual inspection via `PesoSingleFilePdfRenderer`).

### 12.3 Internal area — Pegamentos — location `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`

| Test class | File | Count | Coverage |
|---|---|---|---|
| `PegamentoServiceTests` | PegamentoServiceTests.cs | 7 | Create with complete context (derives JobOnId, pins revision), incomplete context block, detail reverse-navigation, list-by-revision, update never rewrites revision anchor, unauthorized fail-closed, `AddMeasurement` computes Ovalizacao/Media server-side |
| `PegamentoDocumentConfirmationTests` | PegamentoDocumentConfirmationTests.cs | 5 | `ConfirmDocumentSavedAsync`: server-derived metadata (filename `Pegamentos_202601_5447T173_B1_relatorio.pdf`, output root, production folder snapshot), `PEGAMENTO_OUTPUT_ROOT_MISSING`, `PEGAMENTO_PRODUCTION_FOLDER_MISSING`, `PEGAMENTO_FINAL_DOCUMENT_FROZEN`, one-to-one upsert |
| `PegamentoHistoricalRelationshipTests` | PegamentoHistoricalRelationshipTests.cs | 5 | Five owner-required proofs: exact revision persisted; history resolves original CM/BQ/MF; revision query returns its controls; later revision does not move old controls; two revisions each have own historically-correct rows |
| `PegamentoMeasurementCalculatorTests` | PegamentoMeasurementCalculatorTests.cs | 7 | `Ovalizacao`/`Media` formulas; tolerance corridor (inside Ok / boundary Exceeded / beyond Exceeded) |
| `PegamentoPdfTests` | PegamentoPdfTests.cs | 4 | `PegamentoPdfService.GenerateAsync`: PDF bytes + canonical filename, does not persist document row, unknown control NotFound, unauthorized Forbidden |
| `JobOnProductionFolderResolverTests` | JobOnProductionFolderResolverTests.cs | 3 | Shared `IJobOnProductionFolderResolver` consumed by `ConfirmDocumentSavedAsync`; folder from resolved Job On context, never an independent choice; later revision does not reinterpret PDF attribution |

Also integration: `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Pegamentos\PegamentoWebApiTests.cs` (PegFixture WebApplicationFactory; `ValidPegamentosUser` carries `[{"moduleId":"controlo","capabilities":[]}]` — controlo grant derives pegamentos; 2 Facts + Theory×3: anonymous denied → /login; authorized search admitted; module-less user denied → /access-denied) and `PegamentoPdfRendererTests.cs` (3 tests: valid `%PDF-1.4` header, production identity + component data, no HTML/browser-print artifacts).

## 13. Test Doubles / Helpers

### 13.1 Controlo area — `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs`
- `ControloFixedClock : IClock` (fixed UTC).
- `ControloFakeAuthorship : IPersistenceAuthorshipAccessor` (default actor `"controlo-actor"`).
- `FakeControloUowFactory : IRepairUnitOfWorkFactory`; `FakeControloUow : IDbUnitOfWork` (in-memory no-ops).
- `ControloCurrentUser : ICurrentUserAccessor` — user role array `["peso"]`; factories `View()` (`controlo.view`), `Edit()` (`view/edit/submit`), `Review()` (`view/review`), `WithoutSurface()` (null user).
- `FakeControloSheetRepository : IControloSheetRepository` (in-memory; `FailWrite` switch simulating write failure).
- `FakeControloProductionContextLookup : IControloProductionContextLookup`; static `Context(...)` builder (`202601`/`5447T173`/`B1`).
- `ControloTestBuilder.Build(...)` — builds `ControloSheetService` over the fakes.

### 13.2 Internal area — Peso — `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Peso\`
- `FakePesoRepository : IPesoRepository` (in-memory; mirrors real query semantics incl. `GetPreviousApprovedAsync`; no fail switches). Records audit events.
- `PesoServiceTests` inline doubles: `NoopPdfRenderer : IPdfRenderer`, `FakeCurrentUserAccessor : ICurrentUserAccessor` (`GrantOperador` `["peso"]`, `GrantResponsavel` `["peso","peso.aprovar"]`, `GrantNone`), `FixedClock : IClock`.
- Consumes shared `FakeJobOnRepository` from `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` (cross-test-project reuse).

### 13.3 Internal area — Pegamentos — `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`
- `FakePegamentoRepository : IPegamentoRepository` (in-memory; recomputes derived values on read to mirror Dapper hydration; documents one-per-control).
- `FakeJobOnProductionFolderResolver : IJobOnProductionFolderResolver` (per-JobOn folders + default fallback).
- `PegamentoTestSupport`: `FakeSettings : IAppSettingsReader`, `FixedClock : IClock`, `FakeAuthorshipAccessor : IPersistenceAuthorshipAccessor` (`Authorized()`/`Anonymous()`), `FakeJobOnProductionContextLookup : IJobOnProductionContextLookup`, `FakePegamentoPdfRenderer : IPegamentoPdfRenderer` (captures LastData; `NonEmpty()`), `PegamentoContextBuilder.Complete(...)` (CM/BQ/MF snapshots + nominals).

## 14. References to Job On

| Object | Area | Job On Reference | Reference Type |
|---|---|---|---|
| `ControloFolha` | Controlo | `JobOnId`, `JobOnRevisionId` | Domain identifier columns/FKs |
| `ControloFolhaProductionContext` | Controlo | `JobOnId`, `JobOnRevisionId` | Domain record fields |
| `IControloSheetRepository` | Controlo | `GetForProductionAsync(jobOnId, jobOnRevisionId)` | Application port parameter |
| `DapperControloSheetRepository` | Controlo | embeds `job_on_id`, `job_on_revision_id` columns in `controlo_sheets` SQL | Infrastructure SQL |
| `DapperControloProductionContextLookup` | Controlo | `IJobOnRepository`; reads `job_on_revision`, `job_on_component` (5 families) | Infrastructure constructor + read model SQL |
| `controlo_sheets` | Controlo | `job_on_id → job_on`, `job_on_revision_id → job_on_revision` | DB FKs (N23) |
| `CreateControloSheetRequest` | Controlo | `Guid JobOnId` | Application command field |
| `PesoControl` | Peso | `JobOnId`, `JobOnRevisionId` (mandatory, TD-18) | Domain identifier columns/FKs |
| `peso_controlos` | Peso | `job_on_id → job_on`, `job_on_revision_id → job_on_revision` | DB FKs (N06) |
| `PesoService` | Peso | `IJobOnRepository` (constructor); `ResolveJobOnContext` resolves process/reference/CM lot from the current revision's `MP_CM` component | Application service dependency (read-only) |
| `PesoComparisonSnapshot` | Peso | pins current + previous `JobOnId`/`JobOnRevisionId` | Domain snapshot record |
| `PegamentoControlo` | Pegamentos | `JobOnId`, immutable `JobOnRevisionId` | Domain identifier columns/FKs (exact historical anchor) |
| `IJobOnProductionContextLookup.ResolveAsync(jobOnRevisionId)` | Pegamentos | resolves `PegamentoProductionContext` (CM/BQ/MF snapshots + nominals) at the exact revision | Application cross-module port |
| `DapperJobOnProductionContextLookup` | Pegamentos (shared) | Job On read model (`job_on_revision` + `job_on_component`) | Infrastructure adapter behind the port |
| `ConfirmDocumentSavedAsync` | Pegamentos | `IJobOnProductionFolderResolver.ResolveAsync(jobOnId)` → `job_on.production_folder` (N13 column) | Application cross-module folder linkage |
| `pegamento_controlos` | Pegamentos | `job_on_id → job_on`, `job_on_revision_id → job_on_revision` | DB FKs (N07) |
| `PegamentoProductionContext` | Pegamentos | `JobOnId`, `JobOnRevisionId` | Domain record fields |

## 15. Direct References

Mechanical source-visible relationships (whole slice):

```
ControloSheetService
  → IControloSheetRepository
  → IControloProductionContextLookup
  → ControloSheetAuthorizationGate
  → IRepairUnitOfWorkFactory (shared)
  → IClock (shared)

IControloSheetRepository
  → DapperControloSheetRepository

IControloProductionContextLookup
  → DapperControloProductionContextLookup
  → IJobOnRepository (Job On)

ControloSheetAuthorizationGate
  → ICurrentUserAccessor (user.HasModule("peso"))
  → IPersistenceAuthorshipAccessor

DapperControloSheetRepository
  → controlo_sheets, controlo_sheet_items, controlo_sheet_events

PesoService
  → PesoAuthorizationGate (peso module; optional peso.aprovar)
  → IPesoRepository
  → IJobOnRepository (Job On context resolution, TD-18)
  → IClock
  → IPdfRenderer (method parameter for GenerateDocumentAsync)

IPesoRepository → DapperPesoRepository → peso_* tables (N06)
IPdfRenderer → PesoSingleFilePdfRenderer

PegamentoService
  → IPegamentoRepository
  → IJobOnProductionContextLookup (exact revision context)
  → PegamentoAuthorizationGate (authorship actor)
  → IClock / IAppSettingsReader / IJobOnProductionFolderResolver (document confirmation)

IPegamentoRepository → DapperPegamentoRepository → pegamento_* tables (N07/N14/N15/N16/N17)
IJobOnProductionContextLookup → DapperJobOnProductionContextLookup (Job On read model)
IPegamentoPdfRenderer → PegamentoPdfRenderer
PegamentoPdfService → IPegamentoRepository + PegamentoAuthorizationGate + IPegamentoPdfRenderer

Index.cshtml (Controlo) → controlo.js → /api/controlo/*
Peso pages → peso.js → /api/peso/* ; peso.js → /controlo (openFolhaControloForSelection)
Pegamentos pages → pegamentos.js → /api/pegamentos/*

Program.cs
  → DI registrations (Peso 193–196; Pegamentos 200–207; Controlo 247–250)
  → /api/peso/* (ModulePolicies.Peso / CapabilityPolicies.PesoAprovar)
  → /api/pegamentos/* (ModulePolicies.Pegamentos)
  → /api/controlo/* (ModulePolicies.Controlo)
  → ModulePolicies.Controlo/Peso/Pegamentos + CapabilityPolicies.Controlo*/PesoAprovar (policy loop 118–131)

AccessResolver.Resolve → controlo grant ⇒ modules += peso, pegamentos
```

## 16. External Technical References

| Object | Area | External Technical Reference | Reference Type |
|---|---|---|---|
| `ControloFolha` | Controlo | `Domain.Shared.Kernel.Result<T,E>` / `DomainError` | Domain shared kernel usage |
| `ControloFolha.Item.SourceToolId` | Controlo | `tool_references` (FK on `controlo_sheet_items.source_tool_id`) | DB FK |
| `ControloFolha.Item.SourceLotId` | Controlo | `tool_lotes` (FK on `controlo_sheet_items.source_lot_id`) | DB FK |
| `controlo_sheets` | Controlo | `internal_users` (created_by/submitted_by/decided_by FKs) | DB FK |
| `controlo_sheet_events.trigger` | Controlo | `ba_dmo_guard_append_only` (shared function, N01) | DB function reference |
| `DapperControloSheetRepository.UpdateAsync` | Controlo | clears/sets item columns; shared `IDbConnectionFactory`/`Db` (Dapper) | Infrastructure dependency |
| `ControloSheetAuthorizationGate` | Controlo | checks `user.HasModule("peso")`; `AccessResolver` derives peso+pegamentos from the controlo grant; `CanonicalModuleCatalog.AreaChildren[controlo] = [peso, pegamentos]` | Authorization cross-module |
| `ControloSheetService.GetForProductionByContextAsync` | Controlo | `IControloProductionContextLookup.ResolveByProductionAsync` reads Job On production code/machine | Application port cross-module (Job On) |
| Controlo web tabs | Controlo | `/peso`, `/pegamentos` navigation targets (`controlo.js` `window.location.href`) | Static asset route references |
| `PesoControl` | Peso | `internal_users` (approved_by/created_by FKs), `job_on`/`job_on_revision` (pinned context FKs) | DB FKs |
| `PesoService` | Peso | `IJobOnRepository` read-only; `audit_events` (module_id `peso`) | Application service cross-module |
| `PesoReference` | Peso | `peso_validator` rules → DB CHECK constraints (N06) | Domain/DB invariant pairing |
| `PesoSingleFilePdfRenderer` | Peso | DMO colour tokens (`--dmo-brand-*`, success) from `dmo-tokens.css` | Design-system static reference |
| `PesoFileName` | Peso | TD-31 convention `{mold}{neck}__{periodo}__{line}__L{lote}.pdf` | Application convention |
| `PegamentoControlo` | Pegamentos | `internal_users` (created_by/actor_id FKs), `job_on`/`job_on_revision` (exact anchor FKs) | DB FKs |
| `PegamentoPdfService` | Pegamentos | `PegamentoPdfFilename` canonical name; `IPegamentoPdfRenderer` infra choice | Application/infrastructure seam |
| `ConfirmDocumentSavedAsync` | Pegamentos | `IJobOnProductionFolderResolver` → `job_on.production_folder` (N13); `IAppSettingsReader.GetOutputRootAsync` | Cross-module document linkage (Job On / shared settings) |
| `pegamento_medicoes` | Pegamentos | `ba_dmo_guard_append_only` trigger | DB function reference |

## 17. Target-to-Layer Index

| Technical Object | Area | Layer | Location |
|---|---|---|---|
| ControloFolha | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolha.cs` |
| ControloFolhaContext | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaContext.cs` |
| ControloFolhaItem | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaItem.cs` |
| ControloFolhaState / Decision / Codec | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaState.cs` |
| ControloSheetModuleCatalog | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloSheetModuleCatalog.cs` |
| ControloUnit | Controlo | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloUnit.cs` |
| ControloSheetService | Controlo | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetService.cs` |
| ControloSheetRequests (commands/DTOs) | Controlo | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetRequests.cs` |
| ControloSheetAuthorizationGate | Controlo | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetAuthorizationGate.cs` |
| IControloSheetRepository | Controlo | Application | `src\BA.Dmo.Application\Modules\Controlo\IControloSheetRepository.cs` |
| IControloProductionContextLookup | Controlo | Application | `src\BA.Dmo.Application\Modules\Controlo\IControloProductionContextLookup.cs` |
| PesoControl / PesoControlState / PesoLeitura / PesoProcesso / PesoRecordType / PesoReference / WeightCalculator / PesoModuleCatalog | Peso | Domain | `src\BA.Dmo.Domain\Modules\Peso\` (8 files) |
| PesoService (+records, PesoFileName) | Peso | Application | `src\BA.Dmo.Application\Modules\Peso\PesoService.cs` |
| PesoAuthorizationGate | Peso | Application | `src\BA.Dmo.Application\Modules\Peso\PesoAuthorizationGate.cs` |
| IPesoRepository (+PesoLote/…) | Peso | Application | `src\BA.Dmo.Application\Modules\Peso\IPesoRepository.cs` |
| IPdfRenderer (+PesoFolhaPdf) | Peso | Application | `src\BA.Dmo.Application\Modules\Peso\IPdfRenderer.cs` |
| PegamentoControlo / PegamentoComponentKey / PegamentoDocumento / PegamentoMeasurementCalculator / PegamentoModuleCatalog / PegamentoProductionContext / PegamentoToolSnapshot | Pegamentos | Domain | `src\BA.Dmo.Domain\Modules\Pegamentos\` (7 files) |
| PegamentoService / PegamentoRequests / PegamentoAuthorizationGate / PegamentoPdfService / PegamentoPdfFilename / IPegamentoRepository / IJobOnProductionContextLookup | Pegamentos | Application | `src\BA.Dmo.Application\Modules\Pegamentos\` (7 files) |
| CanonicalModuleCatalog controlo/peso/pegamentos entries + AreaChildren | Slice | Application (shared catalog) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| DapperControloSheetRepository / DapperControloProductionContextLookup | Controlo | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| DapperPesoRepository / PesoSingleFilePdfRenderer | Peso | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| DapperPegamentoRepository / PegamentoPdfRenderer | Pegamentos | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| DapperJobOnProductionContextLookup / DapperJobOnProductionFolderResolver (shared consumed) | Pegamentos | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| controlo_sheets / _items / _events / trigger | Controlo | Database | `database\migrations\N23_controlo_folha.sql` |
| peso_* (7 tables) | Peso | Database | `database\migrations\N06_peso.sql` + `N25_remediation.sql` (guard) |
| pegamento_* (3 tables + 3 additive columns) | Pegamentos | Database | `database\migrations\N07`, `N14`–`N17` |
| Pages\Controlo\Index + /api/controlo/* + DI | Controlo | Web | `src\BA.Dmo.Web\Pages\Controlo\`, `src\BA.Dmo.Web\Program.cs` |
| Pages\Peso\{Index,Responsavel} + /api/peso/* + DI | Peso | Web | `src\BA.Dmo.Web\Pages\Peso\`, `src\BA.Dmo.Web\Program.cs` |
| Pages\Pegamentos\{Index,Detail} + /api/pegamentos/* + DI | Pegamentos | Web | `src\BA.Dmo.Web\Pages\Pegamentos\`, `src\BA.Dmo.Web\Program.cs` |
| ModulePolicies/CapabilityPolicies Controlo/Peso/Pegamentos | Slice | Web (authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| controlo.js / peso.js / pegamentos.js | Slice | Static asset | `src\BA.Dmo.Web\wwwroot\scripts\` |
| controlo-layout.css / peso-layout.css / pegamentos-layout.css | Slice | Static asset | `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Controlo unit/guard tests | Controlo | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\`, `IntegrationTests\Controlo\` |
| Peso unit/guard/visual tests | Peso | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Peso\`, `IntegrationTests\Design\PesoComparisonGuardTests.cs`, `IntegrationTests\Peso\PesoPdfVisualCheck.cs` |
| Pegamentos unit + integration tests | Pegamentos | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `IntegrationTests\Pegamentos\` |

## 18. Sources Verified

- `maps\00_INDEX.md` (mapping contract/registry; INDEX taxonomy: Controlo = one module, Peso/Pegamentos internal).
- `src\BA.Dmo.Domain\Modules\Controlo\` (6 files), `Modules\Peso\` (8 files), `Modules\Pegamentos\` (7 files) — read completely.
- `src\BA.Dmo.Domain\Shared\Access\Capability.cs`, `ModuleCatalog.cs`, `ModuleKind.cs`, `CurrentUser.cs` (referenced types).
- `src\BA.Dmo.Application\Modules\Controlo\` (5 files), `Modules\Peso\` (4 files incl. full `PesoService.cs` 1131 lines), `Modules\Pegamentos\` (7 files) — read completely.
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`, `AccessResolver.cs` — read completely.
- `src\BA.Dmo.Infrastructure\Access\DapperControloSheetRepository.cs`, `DapperControloProductionContextLookup.cs`, `DapperPesoRepository.cs`, `DapperPegamentoRepository.cs`, `PesoSingleFilePdfRenderer.cs`, `PegamentoPdfRenderer.cs`, `DapperJobOnProductionFolderResolver.cs`, `DapperAppSettingsReader.cs` (+`IAppSettingsReader`) — read/grep.
- `src\BA.Dmo.Web\Program.cs` (fallback policy 96–132; DI 192–250; Peso endpoints 382–566; Pegamentos endpoints 568–694; Controlo endpoints 1179–1260; Run wiring).
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`, `AuthenticatedSessionHandler.cs` (referenced), `Pages\Shared\_Layout.cshtml`, `Pages\Shared\_Header.cshtml`/`_Navigation.cshtml` (via ShellAndCalendarGuardTests assertions).
- `src\BA.Dmo.Web\Pages\Controlo\Index.cshtml(.cs)`, `Pages\Peso\Index.cshtml(.cs)`, `Pages\Peso\Responsavel.cshtml(.cs)`, `Pages\Pegamentos\Index.cshtml(.cs)`, `Pages\Pegamentos\Detail.cshtml(.cs)` — read.
- `src\BA.Dmo.Web\wwwroot\scripts\controlo.js`, `peso.js`, `pegamentos.js` (grep-verified endpoints/selectors); `wwwroot\styles\modules\{controlo-layout,peso-layout,pegamentos-layout}.css` (existence + DesignSystemGuardTests AllowedModuleLayouts pinning).
- `database\migrations\N06_peso.sql`, `N07_pegamentos.sql`, `N13_jobon_production_folder.sql`, `N14_pegamentos_documents.sql`, `N15_pegamentos_tool_number.sql`, `N16_pegamentos_component_nominals.sql`, `N17_pegamentos_notas.sql`, `N23_controlo_folha.sql`, `N25_remediation.sql` (subagent inventory, cross-checked line/anchor claims), `N01_identity.sql` (`ba_dmo_guard_append_only` function reference), `consolidated_clean_install.sql` (tables ~1140–1204; N12 RLS array ~1240–1250; N25 loop ~1551–1572).
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Controlo\` (3 files), `Modules\Peso\` (5 files), `Modules\Pegamentos\` (9 files) — full inventories (102 test methods).
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Controlo\ControloProjectionGuardTests.cs`, `Design\PesoComparisonGuardTests.cs`, `Pegamentos\PegamentoWebApiTests.cs`, `Pegamentos\PegamentoPdfRendererTests.cs`, `Peso\PesoPdfVisualCheck.cs` — read; `Design\*` guard files cross-referenced via MAP-17.

## Counts

- Domain Controlo area files: 6; Peso (internal): 8; Pegamentos (internal): 7 (slice total 21)
- Application Controlo area files: 5; Peso (internal): 4; Pegamentos (internal): 7 (slice total 16)
- Infrastructure Controlo area files: 2; Peso (internal): 2; Pegamentos (internal): 2 + 3 shared consumed (slice total 6 direct)
- Web dedicated page files (slice): 10 (`Pages\Controlo\Index` ×2, `Pages\Peso\{Index,Responsavel}` ×4, `Pages\Pegamentos\{Index,Detail}` ×4)
- Static asset files (slice): 6 (`wwwroot\scripts\{controlo,peso,pegamentos}.js`, `wwwroot\styles\modules\{controlo-layout,peso-layout,pegamentos-layout}.css`)
- Shared Web files with slice wiring (not counted as dedicated): `Program.cs`, `ModuleAuthorizationHandler.cs`, `CanonicalPageCatalog.cs`, `AccessResolver.cs`
- DB objects: Controlo area 4 (3 tables + 1 trigger) · Peso 7 tables + 1 approved-guard trigger · Pegamentos 3 tables + 1 trigger + 3 additive columns (+ 1 index)
- Migration touchpoints: N06, N07, N12, N13, N14, N15, N16, N17, N23, N25 (10 files)
- Unit test classes: Controlo 2 (15 tests) · Peso 4 (56 tests) · Pegamentos 6 (31 tests) = 12 classes, 102 tests; support/fake files: Controlo 1, Peso 1 + inline doubles, Pegamentos 2 fakes + 1 support
- Integration test classes (slice): ControloProjectionGuardTests (1), PesoComparisonGuardTests (3, static; Design folder), PesoPdfVisualCheck (1, manual artifact), PegamentoWebApiTests (2 Facts + Theory×3), PegamentoPdfRendererTests (3)