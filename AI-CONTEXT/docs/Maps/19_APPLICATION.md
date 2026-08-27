# BA DMO — Application Technical Map

MAP ID: MAP-19
Status: COMPLETE

## Navigation Index

- [1. Scope](#1-scope)
- [2. Project / Folder Structure](#2-project--folder-structure)
- [3. Application Inventory](#3-application-inventory)
- [4. Shared Application](#4-shared-application)
- [5. Module Application Objects](#5-module-application-objects)
- [6. Services](#6-services)
- [7. Interfaces / Ports / Contracts](#7-interfaces--ports--contracts)
- [8. Models / DTOs / Projections](#8-models--dtos--projections)
- [9. Validators / Parsers / Gates](#9-validators--parsers--gates)
- [10. Direct Application References](#10-direct-application-references)
- [11. Target-to-Location Index](#11-target-to-location-index)
- [12. Sources Verified](#12-sources-verified)

## Counts

## 1. Scope

Pure transversal technical inventory/navigation of the **Application layer** (`src\BA.Dmo.Application\`). This map catalogues what Application source declares and where each object lives: use-case services, module application objects, shared application planes, interfaces/contracts/ports, DTOs/models/projections, commands/queries (request records), validators/parsers where present, authorization gates in Application, shared Access/Identity Application objects, document/application contracts, exact paths, and direct Application-layer references.

Rules respected:

- It does **not** explain end-to-end workflows.
- It does **not** duplicate Domain internals (mapped in [01_DOMAIN.md](01_DOMAIN.md)).
- It does **not** duplicate Infrastructure implementation detail beyond direct Application references ([04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) is authoritative for Infrastructure).
- Only current source is mapped; no count is invented.

Cross-references: repository index [00_INDEX.md](00_INDEX.md); Domain [01_DOMAIN.md](01_DOMAIN.md); Dapper Infrastructure [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md); tests [05_TESTS.md](05_TESTS.md) (test sources live under `AI-CONTEXT\docs\tests\`); Web [20_WEB.md](20_WEB.md); module maps [06_JOB_ON.md](06_JOB_ON.md), [07_CONTROLO.md](07_CONTROLO.md) (Controlo area — Peso and Pegamentos Application objects are covered by 07 and this map; there is no dedicated Peso/Pegamentos map file), [08_FERRAMENTAS.md](08_FERRAMENTAS.md), [09_ARMAZEM.md](09_ARMAZEM.md), [10_BOQUILHAS.md](10_BOQUILHAS.md), [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md), [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md), [13_TAMPOES.md](13_TAMPOES.md), [14_HISTORIA.md](14_HISTORIA.md), [15_ADMIN.md](15_ADMIN.md).

## 2. Project / Folder Structure

Project: `src\BA.Dmo.Application\BA.Dmo.Application.csproj` — references **only** `BA.Dmo.Domain` (use cases, DTOs, ports, module catalog services). It does not reference Infrastructure or Web.

```
src\BA.Dmo.Application\
├─ Modules\                         ← module application objects (use cases, ports, gates, DTOs)
│  ├─ Admin\                 (7 files)
│  ├─ Armazem\               (7 files)
│  ├─ Boquilhas\             (5 files)
│  ├─ Controlo\              (5 files)
│  ├─ Ferramentas\           (7 files)
│  ├─ Historia\              (5 files)
│  ├─ JobOn\                 (8 files)   ← +1 since previous revision: ArticleReferenceImage.cs
│  ├─ Pegamentos\            (7 files)
│  ├─ Peso\                  (4 files)
│  ├─ ReparacaoExterna\      (6 files)
│  ├─ ReparacaoInterna\      (5 files)
│  └─ Tampoes\               (5 files)
├─ Shared\                          ← shared application planes
│  ├─ Access\               (10 files — catalogs, resolver, normalizer, navigation, mirror)
│  ├─ Identity\             ( 6 files — internal-user port, auth ports, resolution, bootstrap)
│  ├─ Persistence\          ( 6 files — IDbConnectionFactory, IDbUnitOfWork, authorship)
│  ├─ Shell\                ( 1 file  — IShellService)
│  ├─ IAppSettingsReader.cs ( 1 file  — shared app-settings port)
│  └─ IJobOnImageProvider.cs( 1 file  — shared image provider port)
└─ Properties\
   └─ AssemblyInfo.cs       ( 1 file)
```

`bin\` and `obj\` are build output and are excluded. `Properties\AssemblyInfo.cs` is a build-metadata file (single `InternalsVisibleTo("BA.Dmo.UnitTests")`), not a source type.

**Source-file count (Application):** **97** `.cs` source files under `src\BA.Dmo.Application\`, excluding `bin\` and `obj\`. Of these: **71** are Module application objects (`Modules\*`), **25** are Shared application objects (`Shared\*`), and **1** is `Properties\AssemblyInfo.cs`.

## 3. Application Inventory

### 3.1 Count of Application files by folder (exact)

| Area | Files | Path (under `src\BA.Dmo.Application\`) |
|---|---|---|
| Admin | 7 | `Modules\Admin\` |
| Armazem | 7 | `Modules\Armazem\` |
| Boquilhas | 5 | `Modules\Boquilhas\` |
| Controlo | 5 | `Modules\Controlo\` |
| Ferramentas | 7 | `Modules\Ferramentas\` |
| Historia | 5 | `Modules\Historia\` |
| JobOn | 8 | `Modules\JobOn\` |
| Pegamentos | 7 | `Modules\Pegamentos\` |
| Peso | 4 | `Modules\Peso\` |
| ReparacaoExterna | 6 | `Modules\ReparacaoExterna\` |
| ReparacaoInterna | 5 | `Modules\ReparacaoInterna\` |
| Tampoes | 5 | `Modules\Tampoes\` |
| **Module subtotal** | **71** | `Modules\*` |
| Shared / Access | 10 | `Shared\Access\` |
| Shared / Identity | 6 | `Shared\Identity\` |
| Shared / Persistence | 6 | `Shared\Persistence\` |
| Shared / Shell | 1 | `Shared\Shell\` |
| Shared (top-level) | 2 | `Shared\IAppSettingsReader.cs`, `Shared\IJobOnImageProvider.cs` |
| **Shared subtotal** | **25** | `Shared\*` |
| Properties (build metadata) | 1 | `Properties\AssemblyInfo.cs` |
| **Total** | **97** | |

### 3.2 Type-kind inventory (source-grounded)

| Kind | Count | Where declared (representative) |
|---|---|---|
| Application services (classes named `*Service`) | 17 | `Modules\*\*Service.cs` (see §6) |
| Authorization gates (classes named `*AuthorizationGate`) | 12 | `Modules\*\*AuthorizationGate.cs` (see §9) |
| Interfaces / ports / contracts (`I*`) | 41 | `Modules\*\I*.cs`, `Modules\JobOn\ArticleReferenceImage.cs`, `Modules\Pegamentos\PegamentoPdfService.cs`, `Shared\*\` (see §7) |
| Request/command records + filter records | present per module | `Modules\*\*Requests.cs`, `*Service.cs` (see §8) |
| DTO / model / projection records | present per module | `Modules\*\*Requests.cs`, `*Models.cs` (see §8) |
| Module catalogs / constants | present | `Modules\Historia\HistoriaModuleCatalog.cs`; Shared `Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` |
| Reusable resolver / normalizer / synchronizer helper classes | present | Shared `Access\AccessResolver.cs`, `GrantNormalizer.cs`, `NavigationService.cs`, `ModuleCatalogMirrorSynchronizer.cs`, `CatalogValidator.cs` |
| Module-port adapters implemented in Application (Application-internal) | 2 | `Modules\Armazem\FerramentasArmazemToolIdentityResolver.cs`, `Modules\ReparacaoExterna\FerramentasRepairToolPieceResolver.cs` |
| Document/application contracts (PDF renderer ports + document data records) | present | `Modules\Peso\IPdfRenderer.cs`, `Modules\Pegamentos\PegamentoPdfService.cs`, `Modules\JobOn\IJobOnPdfRenderer.cs` |
| Parsers / rules helpers | 2 | `Shared\Identity\AccessTemplateGrantsParser.cs`, `Modules\JobOn\ArticleReferenceImage.cs` (`ArticleReferenceImageRules`) |
| Validators | none declared in Application | value validation is delegated to Domain validators and to gates + shared normalizer (see §9) |
| Enums (Application-only) | effectively none | enums/states live in Domain; Application reuses Domain enums (`FirstPageOutcome` and `BootstrapAdminOutcome` are the only Application-declared enums, both in Shared) |

## 4. Shared Application

Shared objects are cross-module; they are declared under `Shared\`, not by any single functional module.

### 4.1 Shared Access (`Shared\Access\`) — 10 files

| File | Type | Role |
|---|---|---|
| `CanonicalModuleCatalog.cs` | `CanonicalModuleCatalog` (static catalog) | 12 canonical module entries, capability ids, `AreaChildren` (`controlo` → `peso`, `pegamentos`), order/routes, PT-PT display `Descriptions` |
| `CanonicalPageCatalog.cs` | `CanonicalPageCatalog` (static catalog) | 13 canonical page entries (route, required capability, display order; `jobon.folha` is the single landing), page id constants |
| `PageCatalog.cs` | `PageCatalog` + `PageDefinition` | runtime page catalog and route grammar (`^/[a-z][a-z0-9-]*(?:/…)*$`) |
| `AccessResolver.cs` | `AccessResolver`, `EffectiveAccess`, `FirstPageResolution`, `FirstPageOutcome`, `NavigationItem`-adjacent access surface | template grants → effective access / first-page resolution; profile→capability projection (admin/operador/responsável rules); Peso Operador/Responsável exclusivity |
| `AccessTemplateDefinition.cs` | `AccessTemplateDefinition`, `ModuleGrant` | access-template / grant model (`PreferredFirstPageId` read-only, unused in V1) |
| `GrantNormalizer.cs` | `GrantNormalizer`, `NormalizationResult` | canonical grant validation/normalization with explicit discard report |
| `CatalogValidator.cs` | `CatalogValidator`, `CatalogValidationException` | composition-time catalog validation (uniqueness, route grammar, landings, area children) |
| `NavigationService.cs` | `NavigationService`, `INavigationService`, `NavigationItem`/`NavigationTab`/`NavigationArea`/`ShellNavigation` | shell navigation derivation (GLM-SHL-03) |
| `IModuleCatalogMirrorRepository.cs` | `IModuleCatalogMirrorRepository`, `ModuleCatalogMirrorRow` | catalog-mirror persistence port (Admin display only) |
| `ModuleCatalogMirrorSynchronizer.cs` | `ModuleCatalogMirrorSynchronizer`, `MirrorDisplayEntry`, `MirrorValidationReport` | mirror merge/validation (`BuildSyncRows`, `ValidateMirrorRows`, `MergeForDisplay`) |

### 4.2 Shared Identity (`Shared\Identity\`) — 6 files

| File | Type | Role |
|---|---|---|
| `IInternalUserRepository.cs` | `IInternalUserRepository`, `InternalUserRecord`, `InternalUserAccessTemplateRecord`, `BootstrapAdminCreation` | internal-user persistence port; user → template(s) (N27) resolution with `ModulesOverrideJson`; `AdminExistsAsync`/`CreateBootstrapAdminAsync` |
| `SupabaseAuthPorts.cs` | `ISupabaseAuthAdapter`, `IAdminProvisioningAdapter`, `AuthUser`, `EnsuredAuthUser` | auth / privileged-provisioning ports (service-role isolated) |
| `IdentityResolutionService.cs` | `IdentityResolutionService`, `ResolvedIdentity` | auth-user → `CurrentUser`/`EffectiveAccess`/first-page resolution; exactly-one-active-template fail-closed rule; per-request cache |
| `AccessTemplateGrantsParser.cs` | `AccessTemplateGrantsParser` (static) | `modules` jsonb → `ModuleGrant` list parser (fail closed on structural defects) |
| `BootstrapAdminService.cs` | `BootstrapAdminService`, `BootstrapAdminOptions`, `BootstrapAdminOutcome` | CLI-only bootstrap-admin provisioning (idempotent; `PreExistedRecovered` path HI-4) |
| `AmbiguousIdentityException.cs` | `AmbiguousIdentityException` | ambiguous-identity data-integrity marker (typed diagnostic) |

### 4.3 Shared Persistence (`Shared\Persistence\`) — 6 files

| File | Type | Role |
|---|---|---|
| `IDbConnectionFactory.cs` | `IDbConnectionFactory` | Dapper connection factory port |
| `IDbUnitOfWork.cs` | `IDbUnitOfWork` | unit-of-work port (explicit transaction scope) |
| `IRepairUnitOfWorkFactory.cs` | `IRepairUnitOfWorkFactory` | shared UoW factory port for coordinated multi-module writes (U-15/U-16/Controlo) |
| `PersistenceAuthorship.cs` | `IPersistenceAuthorshipAccessor`, `PersistenceAuthorship` | actor + UTC timestamp authorship port |
| `ConcurrencyGuard.cs` | `ConcurrencyGuard` (static), `ConcurrencyConflictException` | guarded-write helper (`EnsureSingleRowUpdated`) |
| `SchemaMigrationRequiredException.cs` | `SchemaMigrationRequiredException` | pending-migration marker (N26) with user-safe handling contract |

### 4.4 Shared Shell + top-level Shared — 3 files

| File | Type | Role |
|---|---|---|
| `Shared\Shell\IShellService.cs` | `IShellService`, `ShellState` | per-request shell state port (Web implements `RequestShellService`) |
| `Shared\IAppSettingsReader.cs` | `IAppSettingsReader` | app-settings port (`GetOutputRootAsync` → `main_documents_output_root`); consumed by `PegamentoService` |
| `Shared\IJobOnImageProvider.cs` | `IJobOnImageProvider`, `ImageResolution` | Job On Article/Reference image provider port (Shared); consumed by `JobOnPdfService` |

## 5. Module Application Objects

Per functional module, the module-specific Application objects and their location (`src\BA.Dmo.Application\Modules\<Module>\`). Detail per module is in each module map; this row-level table is the Application-layer navigation surface.

| Module | Path | Application files | Main Application objects |
|---|---|---|---|
| Job On | `Modules\JobOn\` | 8 | `JobOnService`, `JobOnPdfService`, `JobOnAuthorizationGate`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver`, **`ArticleReferenceImage` + `IArticleReferenceImageRepository` + `ArticleReferenceImageRules`** (new since previous revision) |
| Peso | `Modules\Peso\` | 4 | `PesoService`, `PesoAuthorizationGate`, `IPesoRepository`, `IPdfRenderer` |
| Pegamentos | `Modules\Pegamentos\` | 7 | `PegamentoService`, `PegamentoPdfService`, `PegamentoAuthorizationGate`, `IPegamentoRepository`, `IPegamentoPdfRenderer`, `IJobOnProductionContextLookup`, `PegamentoPdfFilename` |
| Ferramentas | `Modules\Ferramentas\` | 7 | `FerramentasService`, `FerramentasAuthorizationGate`, `IFerramentasRepository`, `IFerramentasRuleLookup`, `IFerramentasIdentityLookup`, `IFerramentasPieceLookup` |
| Armazem | `Modules\Armazem\` | 7 | `ArmazemService`, `ArmazemAuthorizationGate`, `IArmazemRepository`, `IArmazemRepairMovementPort`, `IToolIdentityResolver`, `FerramentasArmazemToolIdentityResolver` |
| ReparacaoExterna | `Modules\ReparacaoExterna\` | 6 | `ReparacaoExternaService`, `ReparacaoExternaAuthorizationGate`, `IRepairRepository`, `IToolPieceResolver`, `FerramentasRepairToolPieceResolver` |
| ReparacaoInterna | `Modules\ReparacaoInterna\` | 5 | `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate`, `IReparacaoInternaRepository`, `IJobOnActiveContextLookup` |
| Controlo | `Modules\Controlo\` | 5 | `ControloSheetService`, `ControloSheetAuthorizationGate`, `IControloSheetRepository`, `IControloProductionContextLookup` |
| Tampoes | `Modules\Tampoes\` | 5 | `TampaoService`, `TampaoAuthorizationGate`, `ITampaoRepository`, `ITampoesUnitOfWorkFactory` |
| Historia | `Modules\Historia\` | 5 | `HistoriaService`, `HistoriaAuthorizationGate`, `IHistoriaRepository`, `HistoriaModuleCatalog` |
| Boquilhas | `Modules\Boquilhas\` | 5 | `BoquilhasService`, `BqAuthorizationGate`, `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory` |
| Admin | `Modules\Admin\` | 7 | `AdminUserService`, `AdminTemplateService`, `AdminAuditService`, `AdminMirrorService`, `AdminAuthorizationGate`, `IAdminRepository` |

## 6. Services

Application use-case services (classes named `*Service`, plus PDF services). Constructor-level dependency surfaces are inventoried in §6.3; method surfaces per module are inventoried in each module map.

### 6.1 Module services (17)

| Service | Module | File |
|---|---|---|
| `JobOnService` | Job On | `Modules\JobOn\JobOnService.cs` |
| `JobOnPdfService` | Job On | `Modules\JobOn\JobOnPdfService.cs` |
| `PesoService` | Peso | `Modules\Peso\PesoService.cs` |
| `PegamentoService` | Pegamentos | `Modules\Pegamentos\PegamentoService.cs` |
| `PegamentoPdfService` | Pegamentos | `Modules\Pegamentos\PegamentoPdfService.cs` |
| `FerramentasService` | Ferramentas | `Modules\Ferramentas\FerramentasService.cs` |
| `ArmazemService` | Armazém | `Modules\Armazem\ArmazemService.cs` |
| `ReparacaoExternaService` | Reparação Externa | `Modules\ReparacaoExterna\ReparacaoExternaService.cs` |
| `ReparacaoInternaService` | Reparação Interna | `Modules\ReparacaoInterna\ReparacaoInternaService.cs` |
| `ControloSheetService` | Controlo | `Modules\Controlo\ControloSheetService.cs` |
| `TampaoService` | Tampões | `Modules\Tampoes\TampaoService.cs` |
| `HistoriaService` | História | `Modules\Historia\HistoriaService.cs` |
| `BoquilhasService` | Boquilhas | `Modules\Boquilhas\BoquilhasService.cs` |
| `AdminUserService` | Admin | `Modules\Admin\AdminUserService.cs` |
| `AdminTemplateService` | Admin | `Modules\Admin\AdminTemplateService.cs` |
| `AdminAuditService` | Admin | `Modules\Admin\AdminAuditService.cs` |
| `AdminMirrorService` | Admin | `Modules\Admin\AdminMirrorService.cs` |

### 6.2 Shared application helper/query types (not `*Service`)

| Type | File |
|---|---|
| `HistoriaAuthorizationGate` + `HistoriaScope` | `Modules\Historia\HistoriaAuthorizationGate.cs` |
| `GrantNormalizer` (shared validation helper) | `Shared\Access\GrantNormalizer.cs` |
| `AccessResolver` (shared resolution surface) | `Shared\Access\AccessResolver.cs` |
| `NavigationService` (shared shell navigation) | `Shared\Access\NavigationService.cs` |
| `IdentityResolutionService` (shared identity resolution) | `Shared\Identity\IdentityResolutionService.cs` |
| `ModuleCatalogMirrorSynchronizer` (shared mirror) | `Shared\Access\ModuleCatalogMirrorSynchronizer.cs` |
| `BootstrapAdminService` (shared bootstrap) | `Shared\Identity\BootstrapAdminService.cs` |
| `AccessTemplateGrantsParser` (shared template JSON parser) | `Shared\Identity\AccessTemplateGrantsParser.cs` |

### 6.3 Service dependency surfaces (constructor evidence)

For each major service: PATH / INPUT-OUTPUT MODELS / REPOSITORY DEPENDENCIES (ports injected) / AUTHORIZATION-GATES / CROSS-MODULE + SHARED DEPENDENCIES. `Result<T, DomainError>` is the universal output model; typical `T` is listed.

| Service | Constructor dependencies (ports/gates/helpers) | Gate methods used | Cross-module / shared port consumption (owner of the port) | Typical output models |
|---|---|---|---|---|
| `JobOnService` | `JobOnAuthorizationGate`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IClock`, `IArticleReferenceImageRepository?` (optional) | `Require(jobon.edit \| jobon.view \| jobon.confirmar)` | `IJobOnUserContextRepository` (own), `IArticleReferenceImageRepository` (own, `ArticleReferenceImage.cs`) | `Guid`, `JobOnResolution`, `Published ArticleReferenceImage`, `JobOnUserCurrent`, `Unit` |
| `JobOnPdfService` | `IJobOnRepository`, `JobOnAuthorizationGate`, `IJobOnImageProvider?` (optional) | `Require(jobon.view)` | `IJobOnImageProvider` (Shared) | `GeneratedJobOnDocument` (bytes + filename) |
| `PesoService` | `PesoAuthorizationGate`, `IPesoRepository`, `IJobOnRepository`, `IClock` | `Require()` module entry; `Require(peso.aprovar)` for approval/decision/reopen/delete-as-responsável/settings | **`IJobOnRepository` (Modules\JobOn)** — full read/write JobOn port injected; used only via `GetByIdAsync` | `Guid`, `bool`, `PesoCalculationResult`, `GeneratedDocument`, `PreparedEmail`, `PesoControlListItem`, `PesoReferenceSummary`, `PesoControl?` |
| `PegamentoService` | `IPegamentoRepository`, `IJobOnProductionContextLookup`, `PegamentoAuthorizationGate`, `IClock`, `IAppSettingsReader`, `IJobOnProductionFolderResolver` | `ResolveActorId()` (gate has no `Result<Executor>` — see §9.1) | `IJobOnProductionContextLookup` (own module), **`IJobOnProductionFolderResolver` (Modules\JobOn)**, **`IAppSettingsReader` (Shared)** | `Guid`, `bool`, `PegamentoControlDetail`, `PegamentoProductionContext`, `PegamentoMeasurementDetail`, `PegamentoControlItem` |
| `PegamentoPdfService` | `IPegamentoRepository`, `PegamentoAuthorizationGate` | `ResolveActorId()` | — | `GeneratedDocument` (bytes + filename) |
| `FerramentasService` | `IFerramentasRepository`, `IFerramentasRuleLookup`, `FerramentasAuthorizationGate`, `IClock` | `Require()` module entry; `Require(ferramentas.configure)` for check-rule configuration | `IFerramentasRuleLookup` (own; `ResolveActiveRulesAsync` documented as "consumed by Job On") | `Guid`, `bool`, `FerramentasReferenceDetail`, `FerramentasReferenceItem`, `FerramentasLoteItem`, `VerificationRule` |
| `ArmazemService` | `IArmazemRepository`, `IToolIdentityResolver`, `ArmazemAuthorizationGate`, `IClock` | `Require()` module entry | `IToolIdentityResolver` (own module; impl `FerramentasArmazemToolIdentityResolver` adapts `IFerramentasIdentityLookup` from Ferramentas) | `Guid`, `bool`, `IReadOnlyList<ArmazemConsultationRow>`, `IReadOnlyList<ArmazemMovementRow>`, `CorrigirLocalizacaoResult` |
| `ReparacaoExternaService` | `IRepairRepository`, `IToolPieceResolver`, `IArmazemRepairMovementPort`, `IRepairUnitOfWorkFactory`, `ReparacaoExternaAuthorizationGate`, `IClock` | `Require()` module entry (CM/MF only; BQ deferred) | `IToolPieceResolver` (own module; impl `FerramentasRepairToolPieceResolver` adapts `IFerramentasPieceLookup`), **`IArmazemRepairMovementPort` (Modules\Armazem)** — same-UoW coordinated writes, **`IRepairUnitOfWorkFactory` (Shared)** | `Guid`, `bool`, `RepairExitDto`, `RepairerDto`, `LineRepairerDefaultDto`, `RepairHistoryRow`, `RepairToolIdentity` |
| `ReparacaoInternaService` | `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup`, `IRepairUnitOfWorkFactory`, `ReparacaoInternaAuthorizationGate`, `IClock` | `Require()` module entry; `RequireCorrigir(actorId)` for corrections | `IJobOnActiveContextLookup` (own module), **`IFerramentasPieceLookup` (Modules\Ferramentas)**, **`IRepairUnitOfWorkFactory` (Shared)** | `IReadOnlyList<Guid>`, `IReadOnlyList<InternalRepairLineCard>`, `InternalRepairContextDto`, `InternalRepairHistoryRow`, `InternalRepairDetailDto` |
| `ControloSheetService` | `IControloSheetRepository`, `IControloProductionContextLookup`, `IRepairUnitOfWorkFactory`, `ControloSheetAuthorizationGate`, `IClock` | `RequireCapability(controlo.view \| controlo.edit \| controlo.submit \| controlo.review)` (surface grant = `peso` module) | `IControloProductionContextLookup` (own module), **`IRepairUnitOfWorkFactory` (Shared)** | `Guid`, `bool`, `ControloSheetDto`, `ControloUnit` |
| `TampaoService` | `ITampaoRepository`, `ITampoesUnitOfWorkFactory`, `TampaoAuthorizationGate`, `IClock` | `Require()` module entry | `ITampoesUnitOfWorkFactory` (own module) | `Guid`, `bool`, `TampaoConfigurationDto`, `TampaoConfigurationDetailDto`, `TampaoMovimentoDto`, `TampaoPlanoDto`, `TampaoFieldDefDto` |
| `HistoriaService` | `HistoriaAuthorizationGate`, `IHistoriaRepository` | `Require()` (module grant + TD-24 scope) | — | `HistoriaScope`, `HistoriaQueryResult`, `IReadOnlyList<HistoriaEntryRow>` |
| `BoquilhasService` | `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory`, `BqAuthorizationGate`, `IClock` | `Require()` module entry | `IBoquilhasUnitOfWorkFactory` (own module) | `Guid`, `bool`, `BqMovementRowDto`, `BqLotSummaryDto`, `BqLoteDto`, `BqDiscrepancyDto`, `BqRepairerDto` |
| `AdminUserService` | `AdminAuthorizationGate`, `IAdminRepository`, `IAdminProvisioningAdapter`, `IClock` | `Require(admin.gerir)` | `IAdminProvisioningAdapter` (Shared Identity) | `AdminUserRow` (list/single), `bool`, `Result<IReadOnlyList<AdminUserRow>, DomainError>` |
| `AdminTemplateService` | `AdminAuthorizationGate`, `IAdminRepository`, `GrantNormalizer`, `IClock` | `Require(admin.gerir)` | `GrantNormalizer` (Shared Access) | `AdminTemplateRow` (list/single), `string` (canonical modules JSON) |
| `AdminAuditService` | `AdminAuthorizationGate`, `IAdminRepository` | `Require(audit.view)`; `Require(audit.export)` for export | — | `AuditQueryResult`, `string` (CSV export) |
| `AdminMirrorService` | `AdminAuthorizationGate`, `ModuleCatalog` (Domain), `IModuleCatalogMirrorRepository`, `IAdminRepository`, `IClock` | `Require(admin.gerir)` | `IModuleCatalogMirrorRepository` (Shared Access), `ModuleCatalogMirrorSynchronizer` (Shared Access, constructed internally), Domain `ModuleCatalog` | `IReadOnlyList<MirrorDisplayEntry>` |

## 7. Interfaces / Ports / Contracts

Application declares ports/contracts that Infrastructure implements (see [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md)). Navigation-level inventory (exact file each).

### 7.1 Module ports (`Modules\*\`) per module

| Module | Interfaces / Ports | File(s) |
|---|---|---|
| Job On | `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver`, **`IArticleReferenceImageRepository`** | `Modules\JobOn\IJobOnRepository.cs`, `IJobOnUserContextRepository.cs`, `IJobOnPdfRenderer.cs`, `IJobOnProductionFolderResolver.cs`, **`ArticleReferenceImage.cs`** |
| Peso | `IPesoRepository`, `IPdfRenderer` | `Modules\Peso\IPesoRepository.cs`, `IPdfRenderer.cs` |
| Pegamentos | `IPegamentoRepository`, `IPegamentoPdfRenderer`, `IJobOnProductionContextLookup` | `Modules\Pegamentos\IPegamentoRepository.cs`, **`PegamentoPdfService.cs`** (declares `IPegamentoPdfRenderer`), `IJobOnProductionContextLookup.cs` |
| Ferramentas | `IFerramentasRepository`, `IFerramentasRuleLookup`, `IFerramentasIdentityLookup`, `IFerramentasPieceLookup` | `Modules\Ferramentas\IFerramentas*.cs` |
| Armazem | `IArmazemRepository`, `IArmazemRepairMovementPort`, `IToolIdentityResolver` | `Modules\Armazem\IArmazem*.cs`, `IToolIdentityResolver.cs` |
| ReparacaoExterna | `IRepairRepository`, `IToolPieceResolver` | `Modules\ReparacaoExterna\IRepairRepository.cs`, `IToolPieceResolver.cs` |
| ReparacaoInterna | `IReparacaoInternaRepository`, `IJobOnActiveContextLookup` | `Modules\ReparacaoInterna\IReparacaoInternaRepository.cs`, `IJobOnActiveContextLookup.cs` |
| Controlo | `IControloSheetRepository`, `IControloProductionContextLookup` | `Modules\Controlo\IControloSheetRepository.cs`, `IControloProductionContextLookup.cs` |
| Tampoes | `ITampaoRepository`, `ITampoesUnitOfWorkFactory` | `Modules\Tampoes\ITampaoRepository.cs`, `ITampoesUnitOfWorkFactory.cs` |
| Historia | `IHistoriaRepository` | `Modules\Historia\IHistoriaRepository.cs` |
| Boquilhas | `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory` | `Modules\Boquilhas\IBoquilhasRepository.cs`, `IBoquilhasUnitOfWorkFactory.cs` |
| Admin | `IAdminRepository` | `Modules\Admin\IAdminRepository.cs` |

Module ports total: **29** public `I*` interfaces across `Modules\*` (2 are declared inside multi-type files: `IArticleReferenceImageRepository` in `ArticleReferenceImage.cs`, `IPegamentoPdfRenderer` in `PegamentoPdfService.cs`).

### 7.2 Shared ports (`Shared\`) — 12

| Port | File |
|---|---|
| `IDbConnectionFactory` | `Shared\Persistence\IDbConnectionFactory.cs` |
| `IDbUnitOfWork` | `Shared\Persistence\IDbUnitOfWork.cs` |
| `IRepairUnitOfWorkFactory` | `Shared\Persistence\IRepairUnitOfWorkFactory.cs` |
| `IPersistenceAuthorshipAccessor` | `Shared\Persistence\PersistenceAuthorship.cs` |
| `IInternalUserRepository` | `Shared\Identity\IInternalUserRepository.cs` |
| `ISupabaseAuthAdapter`, `IAdminProvisioningAdapter` | `Shared\Identity\SupabaseAuthPorts.cs` |
| `IModuleCatalogMirrorRepository` | `Shared\Access\IModuleCatalogMirrorRepository.cs` |
| `INavigationService` | `Shared\Access\NavigationService.cs` |
| `IShellService` | `Shared\Shell\IShellService.cs` |
| `IAppSettingsReader` | `Shared\IAppSettingsReader.cs` |
| `IJobOnImageProvider` | `Shared\IJobOnImageProvider.cs` |

### 7.3 Document / application contracts

| Contract | Kind | File |
|---|---|---|
| `IPdfRenderer` + `PesoFolhaPdf` / `PesoCmComparisonRow` data | port + document data records | `Modules\Peso\IPdfRenderer.cs` |
| `IPegamentoPdfRenderer` + `PegamentoPdfData` / `PegamentoPdfMeasurementRow` data | port + document data records | `Modules\Pegamentos\PegamentoPdfService.cs` |
| `IJobOnPdfRenderer` + `JobOnPdfData/Component/CalibreRow/Verification` records | port + data records | `Modules\JobOn\IJobOnPdfRenderer.cs` |
| `IArticleReferenceImageRepository` + `ArticleReferenceImage` / `ArticleReferenceImageRules` | port + data record + rules helper (new) | `Modules\JobOn\ArticleReferenceImage.cs` |
| `HistoriaQueryResult` / `HistoriaEntryRow` / `HistoriaGroupRow` | projection records | `Modules\Historia\HistoriaModels.cs` |

## 8. Models / DTOs / Projections

Request/command records, filter records, DTO/models and projection records are declared in Application per module (see each module map for full member detail). Navigation-level:

| Module | File | Content kind |
|---|---|---|
| Job On | `Modules\JobOn\JobOnService.cs` | request records (Create/Duplicate/SaveRevision/Transition/Image: Attach/Replace/Remove/Current) + `Unit` + internal `SnapshotJson` helper |
| Job On (reference images) | `Modules\JobOn\ArticleReferenceImage.cs` | `ArticleReferenceImage` record + `ArticleReferenceImageRules` (reference-code extraction, image-asset-id validation) |
| Peso | `Modules\Peso\PesoService.cs` | request records + filters (`ControlFilterRequest` etc.) + `PesoCalculationResult`/`PesoCalculationRow` + `GeneratedDocument` + `PreparedEmail` + `PesoFileName` filename helper |
| Pegamentos | `Modules\Pegamentos\PegamentoRequests.cs` | request records (Create/Update/AddMeasurement/Close/Filter) + detail/item/measurement DTOs |
| Pegamentos (filename) | `Modules\Pegamentos\PegamentoPdfFilename.cs` | canonical PDF filename helper |
| Ferramentas | `Modules\Ferramentas\FerramentasRequests.cs`, `FerramentasService.cs` | request records + DTO rows + search/utilisation requests (`FerramentasSearchRequest`, `RecordToolUtilisationRequest`) |
| Armazem | `Modules\Armazem\ArmazemRequests.cs` | request records + DTO rows (search/consultation/history/entry, `WarehouseMovementFact` also in `IArmazemRepository.cs`) |
| ReparacaoExterna | `Modules\ReparacaoExterna\ReparacaoExternaRequests.cs` | request records + DTOs (exits/items/repairer/line default/history) + `RepairToolIdentity` (in `IToolPieceResolver.cs`) |
| ReparacaoInterna | `Modules\ReparacaoInterna\ReparacaoInternaRequests.cs` | request records + filter + DTOs (line card/context/history/detail) |
| Controlo | `Modules\Controlo\ControloSheetRequests.cs` | request records (Create/UpdateItems/Submit/Reopen/Decide) + sheet/item/event DTOs |
| Tampoes | `Modules\Tampoes\TampaoRequests.cs` | request records + filters + DTOs (field/config/saldo/movement/plano/machine/note) |
| Historia | `Modules\Historia\HistoriaModels.cs` | filter/row/group/query-result records |
| Boquilhas | `Modules\Boquilhas\BqRequests.cs`, `IBoquilhasRepository.cs` | request records + DTOs (lote/movement/saldo/discrepancy/repairer/trace) + filters (`BqLoteFilter`, `BqHistoryFilter`) |
| Admin | `Modules\Admin\AdminModels.cs` | Admin user/template/audit rows, requests (user/template/mirror), audit filter/result records |

There is no dedicated `Models` / `DTO` project; DTOs/models/projections are per-module records co-located with the service/port files.

## 9. Validators / Parsers / Gates

### 9.1 Authorization gates (12)

Each gate enforces the module/capability grant server-side and fails closed. **Correction: not every gate returns `Result<Executor, DomainError>`** — `PegamentoAuthorizationGate` deviates (see note below).

| Gate | Module | File |
|---|---|---|
| `JobOnAuthorizationGate` (`Require(params capabilityIds)`) | Job On | `Modules\JobOn\JobOnAuthorizationGate.cs` |
| `PesoAuthorizationGate` (`Require`; `peso.aprovar` for approver ops; `PesoExecutor.HasAprovarRole`) | Peso | `Modules\Peso\PesoAuthorizationGate.cs` |
| `PegamentoAuthorizationGate` (**`ResolveActorId()` only — no `Result<Executor>`**) | Pegamentos | `Modules\Pegamentos\PegamentoAuthorizationGate.cs` |
| `FerramentasAuthorizationGate` (`Require`; `ferramentas.configure`; `FerramentasExecutor.CanConfigure`) | Ferramentas | `Modules\Ferramentas\FerramentasAuthorizationGate.cs` |
| `ArmazemAuthorizationGate` (`Require`, module entry) | Armazém | `Modules\Armazem\ArmazemAuthorizationGate.cs` |
| `ReparacaoExternaAuthorizationGate` (`Require`, module entry) | Reparação Externa | `Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGate.cs` |
| `ReparacaoInternaAuthorizationGate` (`Require`; `RequireCorrigir` → `reparacao_interna.corrigir`) | Reparação Interna | `Modules\ReparacaoInterna\ReparacaoInternaAuthorizationGate.cs` |
| `ControloSheetAuthorizationGate` (`RequireSurface`/`RequireCapability`; surface grant = `peso` module + `controlo.*` capability) | Controlo | `Modules\Controlo\ControloSheetAuthorizationGate.cs` |
| `TampaoAuthorizationGate` (`Require`, module entry) | Tampões | `Modules\Tampoes\TampaoAuthorizationGate.cs` |
| `HistoriaAuthorizationGate` (`Require` → `HistoriaScope`; TD-24 origin-scope + audit.view for admin events) | História | `Modules\Historia\HistoriaAuthorizationGate.cs` |
| `BqAuthorizationGate` (`Require`, module entry) | Boquilhas | `Modules\Boquilhas\BqAuthorizationGate.cs` |
| `AdminAuthorizationGate` (`Require(admin.gerir \| audit.view \| audit.export)`) | Admin | `Modules\Admin\AdminAuthorizationGate.cs` |

Notes (source-grounded):

- **`PegamentoAuthorizationGate` is the only gate that does not produce an executor `Result`** — it exposes `string? ResolveActorId()` via `IPersistenceAuthorshipAccessor` only; `PegamentoService`/`PegamentoPdfService` check `actorId is null` and return `PEGAMENTO_UNAUTHORIZED` themselves. Claim "each gate fails closed (`Result<Executor, DomainError>`)" from the previous revision is therefore inaccurate; updated here.
- `ControloSheetAuthorizationGate` gates on the surrounding **`peso` module surface** (`SurfaceModuleId = "peso"`) plus `controlo.view/edit/submit/review` capabilities (owner decision R010).
- `HistoriaAuthorizationGate` and `AdminAuthorizationGate` consume `CanonicalCapabilities.AuditView` (declared in `Modules\Admin\AdminUserService.cs`); `HistoriaService` uses the resolved `HistoriaScope` to restrict queries.

### 9.2 Validators / parsers

Application declares **no validator classes**; value validation is delegated to Domain validators (`PesoValidator`, `ReportPathValidator`, `BqRules`, `TampaoRules`, `TampaoMachine`, `ArticleReferenceImageRules`, `WarehouseLocation` — see [01_DOMAIN.md](01_DOMAIN.md)) consumed by the services, and to gates + the shared normalizer. Parser / rules helpers **are** declared in Application:

- `AccessTemplateGrantsParser` (static) — `Shared\Identity\AccessTemplateGrantsParser.cs`: parses `access_templates.modules` jsonb into `ModuleGrant`s (consumed by `IdentityResolutionService` and `AdminUserService.ValidateProfileTemplatesAsync`).
- `ArticleReferenceImageRules` — `Modules\JobOn\ArticleReferenceImage.cs`: reference-code extraction from revision snapshot + image-asset-id normalization/validation.
- Shared `GrantNormalizer` / `AccessResolver` — canonical grant validation and access projection (`Shared\Access\`).

### 9.3 Shared Access/Identity Application objects

- `AccessResolver`, `EffectiveAccess`, `FirstPageResolution`/`FirstPageOutcome` — `Shared\Access\AccessResolver.cs`.
- `GrantNormalizer`, `NormalizationResult` — `Shared\Access\GrantNormalizer.cs`.
- `AccessTemplateGrantsParser` — `Shared\Identity\AccessTemplateGrantsParser.cs`.
- `ModuleCatalogMirrorSynchronizer`, `MirrorDisplayEntry`, `MirrorValidationReport` — `Shared\Access\ModuleCatalogMirrorSynchronizer.cs`.

## 10. Direct Application References

Mechanical, source-visible references **inside** Application (references to Domain, and to Application ports governing the Web/Infrastructure surface).

### 10.1 Application → Domain

`BA.Dmo.Application.csproj` references `BA.Dmo.Domain` (single `ProjectReference`). Services consume Domain aggregate roots / value objects / services and `Shared.Kernel.Result<T, Error>` / `DomainError` / `IClock`. Representative (not exhaustive):

- `JobOnService` → `JobOn`, `JobOnRevision`, `JobOnActivityResolver`, `JobonModuleCatalog`, `Result<T, DomainError>`; PDF path uses `ArticleReferenceImageRules` (Application).
- `PesoService` → `PesoControl`, `PesoValidator`, `WeightCalculator`, `PesoModuleCatalog`, `PesoProcesso`.
- `AdminMirrorService` → Domain `ModuleCatalog` (injected) + Shared `ModuleCatalogMirrorSynchronizer`.
- `IdentityResolutionService` → `CurrentUser`, `FunctionalProfileNames`, `EffectiveAccess` (Application) + `IInternalUserRepository` (Shared port).
- every gate → `Domain.Shared.Access.CurrentUser` (`ICurrentUserAccessor`) + `DomainError`; the module-entry gates additionally use `IPersistenceAuthorshipAccessor` (Shared) for the canonical `actor_id`.

### 10.2 Application ports → implementations (composition root, `src\BA.Dmo.Web\Program.cs`)

Each port listed in §7 has an implementation registered in `Program.cs` (Web composition root; verified lines 133–275). The port→implementation edges are:

```
IAdminRepository             → DapperAdminRepository
IAdminProvisioningAdapter    → SupabaseAdminProvisioningAdapter
IModuleCatalogMirrorRepository → DapperModuleCatalogMirrorRepository
IInternalUserRepository      → DapperInternalUserRepository
ISupabaseAuthAdapter         → SupabaseAuthAdapter
IJobOnRepository             → DapperJobOnRepository
IJobOnUserContextRepository  → DapperJobOnUserContextRepository
IArticleReferenceImageRepository → DapperArticleReferenceImageRepository   (new)
IJobOnImageProvider          → FileSystemJobOnImageProvider
IJobOnPdfRenderer            → JobOnPdfRenderer
IJobOnProductionFolderResolver → DapperJobOnProductionFolderResolver
IPesoRepository              → DapperPesoRepository
IPdfRenderer                 → PesoSingleFilePdfRenderer
IPegamentoRepository         → DapperPegamentoRepository
IPegamentoPdfRenderer        → PegamentoPdfRenderer
IJobOnProductionContextLookup → DapperJobOnProductionContextLookup
IAppSettingsReader           → DapperAppSettingsReader
IFerramentasRepository       → DapperFerramentasRepository
IFerramentasRuleLookup       → DapperFerramentasRuleLookup
IFerramentasIdentityLookup   → DapperFerramentasIdentityLookup
IFerramentasPieceLookup      → DapperFerramentasPieceLookup
IArmazemRepository           → DapperArmazemRepository
IArmazemRepairMovementPort   → DapperArmazemRepairMovementRepository
IToolIdentityResolver        → FerramentasArmazemToolIdentityResolver   (Application adapter, Modules\Armazem)
IRepairRepository            → DapperRepairRepository
IToolPieceResolver           → FerramentasRepairToolPieceResolver       (Application adapter, Modules\ReparacaoExterna)
IRepairUnitOfWorkFactory     → DapperRepairUnitOfWorkFactory
IReparacaoInternaRepository  → DapperReparacaoInternaRepository
IJobOnActiveContextLookup    → DapperJobOnActiveContextLookup
IControloSheetRepository     → DapperControloSheetRepository
IControloProductionContextLookup → DapperControloProductionContextLookup
ITampaoRepository            → DapperTampaoRepository
ITampoesUnitOfWorkFactory    → DapperTampoesUnitOfWorkFactory
IHistoriaRepository          → DapperHistoriaRepository
IBoquilhasRepository         → DapperBoquilhasRepository
IBoquilhasUnitOfWorkFactory  → DapperBoquilhasUnitOfWorkFactory
```

`IDbConnectionFactory` → `AppDbConnectionFactory` (Infrastructure `Persistence`) and the shared `DapperUnitOfWork`/`ConcurrencyGuard` (`Shared\Persistence`) also live in Infrastructure (see [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md)). Services, gates and shared helper services (`JobOnService`, `AdminUserService`, `PesoService`, `NavigationService`, `IdentityResolutionService`, etc.) are registered scoped/singleton alongside the ports.

### 10.3 Cross-module Application dependency wires (consumer-side, constructor evidence)

Which services depend on which ports declared **outside their own module** (or on the Shared plane); also recorded where one module's port is implemented by an Application adapter over another module's port:

| Consumer (Application) | Port consumed | Port declared in | Direction / evidence |
|---|---|---|---|
| `PesoService` | `IJobOnRepository` | `Modules\JobOn\` | Peso → JobOn module port (full read/write repo used read-only via `GetByIdAsync`). **NEEDS REVIEW — port breadth**: Peso injects the write-capable JobOn repository rather than a narrow read-only context lookup. |
| `PegamentoService` | `IJobOnProductionFolderResolver` | `Modules\JobOn\` | Pegamentos → JobOn module port (`ResolveAsync(jobOnId)` for the production folder). Interface doc names Peso and Pegamentos as consumers; only Pegamentos currently consumes it. |
| `PegamentoService` | `IAppSettingsReader` | `Shared\` (top-level) | Pegamentos → Shared port (document output root). |
| `ReparacaoExternaService` | `IArmazemRepairMovementPort` | `Modules\Armazem\` | Reparação Externa → Armazém module port (physical pickup/return in the SAME `IDbUnitOfWork`; owner decisions B/C). |
| `ReparacaoInternaService` | `IFerramentasPieceLookup` | `Modules\Ferramentas\` | Reparação Interna → Ferramentas module port (read-only piece lookup, lot resolution). |
| `FerramentasRepairToolPieceResolver` (`IToolPieceResolver` impl) | `IFerramentasPieceLookup` | `Modules\Ferramentas\` | Reparação Externa adapter → Ferramentas module port (Application-internal adapter). |
| `FerramentasArmazemToolIdentityResolver` (`IToolIdentityResolver` impl) | `IFerramentasIdentityLookup` | `Modules\Ferramentas\` | Armazém adapter → Ferramentas module port (Application-internal adapter). |
| `ArmazemService` | `IToolIdentityResolver` | `Modules\Armazem\` | same-module port, but the registered implementation is the Ferramentas-adapting resolver (see row above). |
| `ReparacaoExternaService` | `IRepairUnitOfWorkFactory` | `Shared\Persistence\` | Shared UoW factory (also consumed by `ReparacaoInternaService` and `ControloSheetService`). |
| `ControloSheetService` | `IRepairUnitOfWorkFactory` | `Shared\Persistence\` | Controlo → Shared UoW factory. |
| `ReparacaoInternaService` | `IRepairUnitOfWorkFactory` | `Shared\Persistence\` | Reparação Interna → Shared UoW factory. |
| `JobOnPdfService` | `IJobOnImageProvider` | `Shared\` (top-level) | JobOn PDF → Shared image provider (optional injection). |
| `JobOnService` | `IArticleReferenceImageRepository` | `Modules\JobOn\ArticleReferenceImage.cs` | same-module port (optional injection; attach/replace/remove reference image). |
| `FerramentasService` | `IFerramentasRuleLookup` | `Modules\Ferramentas\` | same-module port; `ResolveActiveRulesAsync` documented as the "rule lookup consumed by Job On" surface. |
| Module-local read-only context lookups (implemented by Infrastructure against the Job On read model, not by another module) | `IJobOnActiveContextLookup` (`Modules\ReparacaoInterna\`), `IJobOnProductionContextLookup` (`Modules\Pegamentos\`), `IControloProductionContextLookup` (`Modules\Controlo\`) | own modules | Job-On-derived context ports; each declared on the consuming module and implemented in Infrastructure. |

### 10.4 Web direct use of Application ports — bypass observation

- **Normal composition (not bypass):** `Pages\Login.cshtml.cs` injects `ISupabaseAuthAdapter` directly (Application port); `Pages\JobOn\Index.cshtml.cs` injects `IJobOnRepository` (Application port) directly for `GetByIdAsync`/`GetHistoricalProductionsAsync` (bypasses `JobOnService` for read surfaces — port-level composition, not an Application-layer bypass on its own).
- **NEEDS REVIEW — Web bypasses Application with Infrastructure persistence:** `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` (used by Admin Users Create/Edit and Templates Index/Edit pages) injects `IDbConnectionFactory` (port) **and uses `BA.Dmo.Infrastructure.Persistence` `Db.*` static helpers to run raw SQL** against `access_template_profiles` and to **UPDATE `internal_users.profile_title`** (`UpsertAsync`, lines 98–136). No Application service or port other than the connection factory mediates these reads/writes — the Admin template-profile workflow bypasses `AdminTemplateService`/`AdminUserService` for this persistence. This matches the flag criteria (Web PageModel-adjacent class calls Infrastructure directly). Owner decision required: promote to an Application service/port (e.g. IAdminRepository or a dedicated port) or document the intentional exception.

## 11. Target-to-Location Index

| Technical object (kind) | Location (`src\BA.Dmo.Application\`) |
|---|---|
| Application service classes (`*Service`) | `Modules\*\*Service.cs` |
| Authorization gates (`*AuthorizationGate`) | `Modules\*\*AuthorizationGate.cs` |
| Module ports (`I*Repository`, lookups, renderers, UoW factories, image repo) | `Modules\*\I*.cs`, `Modules\JobOn\ArticleReferenceImage.cs`, `Modules\Pegamentos\PegamentoPdfService.cs` |
| Module-port adapters (Application-internal) | `Modules\Armazem\FerramentasArmazemToolIdentityResolver.cs`, `Modules\ReparacaoExterna\FerramentasRepairToolPieceResolver.cs` |
| Request/command + DTO records | `Modules\*\*Requests.cs`, `*Service.cs`, `*Models.cs` |
| Module catalog constants (História) | `Modules\Historia\HistoriaModuleCatalog.cs` |
| Canonical module / page catalogs | `Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` |
| Access resolution / normalizer / navigation / mirror | `Shared\Access\` |
| Identity resolution / auth ports / template parser / bootstrap | `Shared\Identity\` |
| Persistence ports (connection/UoW/authorship/concurrency/migration marker) | `Shared\Persistence\` |
| Shell port | `Shared\Shell\IShellService.cs` |
| Top-level shared ports | `Shared\IAppSettingsReader.cs`, `Shared\IJobOnImageProvider.cs` |
| Build metadata | `Properties\AssemblyInfo.cs` |

## 12. Sources Verified

- `src\BA.Dmo.Application\BA.Dmo.Application.csproj` (single `Domain` `ProjectReference`).
- Full recursive listing of `src\BA.Dmo.Application\` (**97** `.cs` files, `bin\`/`obj\` excluded) — every file read or member-inventoried (services, gates, ports, Shared files and request/DTO files fully read; the 8 `*Requests.cs` files' record inventories confirmed by declaration scan).
- `src\BA.Dmo.Application\Modules\*` — all 71 module files (12 folders) read/scanned; all 17 `*Service` constructors and public method surfaces read.
- `src\BA.Dmo.Application\Shared\*` (Access 10, Identity 6, Persistence 6, Shell 1, top-level Shared 2) all read.
- `src\BA.Dmo.Web\Program.cs` DI registrations (lines 133–275) — port → implementation mapping verified (§10.2).
- `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` + `Pages\Admin\Users\Create.cshtml.cs` — Web-bypass evidence (§10.4); `Pages\JobOn\Index.cshtml.cs` direct port use noted.
- Per-module type roles cross-checked against sibling module maps (`06..15_*.md` in `AI-CONTEXT\docs\Maps\`); Peso/Pegamentos Application objects covered by [07_CONTROLO.md](07_CONTROLO.md) and this map.
- Tests are NOT used as evidence; test locations are covered by [05_TESTS.md](05_TESTS.md) (test sources under `AI-CONTEXT\docs\tests\`).

## Counts

- Total Application `.cs` files: **97** (was 96)
- Module application files: **71** (`Modules\*`, 12 module folders) (was 70)
- Shared application files: **25** (`Shared\*`)
- Build-metadata file: **1** (`Properties\AssemblyInfo.cs`)
- Application service classes: **17** (`*Service`, incl. 4 Admin services + 3 PDF services)
- Authorization gates: **12** (`*AuthorizationGate`)
- Interfaces / ports / contracts: **41** (29 module `I*` + 12 shared `I*`) (was 40 — +`IArticleReferenceImageRepository`)
- Module-by-module Application file counts: JobOn **8** (was 7), Peso 4, Pegamentos 7, Ferramentas 7, Armazem 7, ReparacaoExterna 6, ReparacaoInterna 5, Controlo 5, Tampoes 5, Historia 5, Boquilhas 5, Admin 7
- Shared per-plane counts: Access 10, Identity 6, Persistence 6, Shell 1, top-level Shared 2
- Validators in Application: **0** (delegated to Domain validators + shared `GrantNormalizer`/gates); parsers/rules helpers in Application: **2** (`AccessTemplateGrantsParser`, `ArticleReferenceImageRules`)
- Document/application contracts: **3** PDF renderer ports (`IPdfRenderer`, `IPegamentoPdfRenderer`, `IJobOnPdfRenderer`) + `IArticleReferenceImageRepository` (reference-image port) + per-module document data records in the same files
- Application-internal module-port adapters: **2** (`FerramentasArmazemToolIdentityResolver`, `FerramentasRepairToolPieceResolver`)
- NEEDS REVIEW items: **3** (PesoService consumes write-capable `IJobOnRepository`; Web `TemplateProfileStore` bypasses Application with Infrastructure raw SQL; `PegamentoAuthorizationGate` deviates from the executor-Result gate pattern — recorded for accuracy, no defect asserted)