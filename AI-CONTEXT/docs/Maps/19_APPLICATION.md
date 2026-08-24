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
- It does **not** duplicate Domain internals (mapped in `01_DOMAIN.md`).
- It does **not** duplicate Infrastructure implementation detail beyond direct Application references (`04_DAPPER_INFRASTRUCTURE.md` is authoritative for Infrastructure).
- Only current source is mapped; no count is invented.

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
│  ├─ JobOn\                 (7 files)
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

`bin\` and `obj\` are build output and are excluded. `Properties\AssemblyInfo.cs` is a build-metadata file, not a source type.

**Source-file count (Application):** **96** `.cs` source files under `src\BA.Dmo.Application\`, excluding `bin\` and `obj\`. Of these: **70** are Module application objects (`Modules\*`), **25** are Shared application objects (`Shared\*`), and **1** is `Properties\AssemblyInfo.cs`.

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
| JobOn | 7 | `Modules\JobOn\` |
| Pegamentos | 7 | `Modules\Pegamentos\` |
| Peso | 4 | `Modules\Peso\` |
| ReparacaoExterna | 6 | `Modules\ReparacaoExterna\` |
| ReparacaoInterna | 5 | `Modules\ReparacaoInterna\` |
| Tampoes | 5 | `Modules\Tampoes\` |
| **Module subtotal** | **70** | `Modules\*` |
| Shared / Access | 10 | `Shared\Access\` |
| Shared / Identity | 6 | `Shared\Identity\` |
| Shared / Persistence | 6 | `Shared\Persistence\` |
| Shared / Shell | 1 | `Shared\Shell\` |
| Shared (top-level) | 2 | `Shared\IAppSettingsReader.cs`, `Shared\IJobOnImageProvider.cs` |
| **Shared subtotal** | **25** | `Shared\*` |
| Properties (build metadata) | 1 | `Properties\AssemblyInfo.cs` |
| **Total** | **96** | |

### 3.2 Type-kind inventory (source-grounded)

| Kind | Count | Where declared (representative) |
|---|---|---|
| Application services (classes named `*Service`) | 17 | `Modules\*\*Service.cs` (see §6) |
| Authorization gates (classes named `*AuthorizationGate`) | 12 | `Modules\*\*AuthorizationGate.cs` (see §9) |
| Interfaces / ports / contracts (`I*`) | 40 | `Modules\*\I*.cs`, `Shared\*\` (see §7) |
| Request/command records + filter records | present per module | `Modules\*\*Requests.cs`, `*Service.cs` (see §8) |
| DTO / model / projection records | present per module | `Modules\*\*Requests.cs`, `*Models.cs` (see §8) |
| Module catalogs / constants | present | `Modules\Historia\HistoriaModuleCatalog.cs`; Shared `Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` |
| Reusable resolver / normalizer / synchronizer helper classes | present | Shared `Access\AccessResolver.cs`, `GrantNormalizer.cs`, `NavigationService.cs`, `ModuleCatalogMirrorSynchronizer.cs`, `CatalogValidator.cs` |
| Document/application contracts (PDF renderer ports + document data records) | present | `Modules\Peso\IPdfRenderer.cs`, `Modules\Pegamentos\PegamentoPdfService.cs`, `Modules\JobOn\IJobOnPdfRenderer.cs` |
| Validators / parsers | none declared in Application | validation is delegated to Domain validators and to gates (see §9) |
| Enums (Application-only) | effectively none | enums/states live in Domain; Application reuses Domain enums |

## 4. Shared Application

Shared objects are cross-module; they are declared under `Shared\`, not by any single functional module.

### 4.1 Shared Access (`Shared\Access\`) — 10 files

| File | Type | Role |
|---|---|---|
| `CanonicalModuleCatalog.cs` | `CanonicalModuleCatalog` (static catalog) | 12 canonical module entries, capability ids, `AreaChildren` (controlo → peso, pegamentos), order/routes |
| `CanonicalPageCatalog.cs` | `CanonicalPageCatalog` (static catalog) | 12 canonical page entries (route, required capability, display order) |
| `PageCatalog.cs` | `PageCatalog` + `PageDefinition` | runtime page catalog and route grammar |
| `AccessResolver.cs` | `AccessResolver`, `EffectiveAccess`, `FirstPageResolution`, `FirstPageOutcome` | grant → effective access / first-page resolution |
| `AccessTemplateDefinition.cs` | `AccessTemplateDefinition`, `ModuleGrant` | access-template / grant model |
| `GrantNormalizer.cs` | `GrantNormalizer`, `NormalizationResult` | canonical grant validation/normalization |
| `CatalogValidator.cs` | `CatalogValidator`, `CatalogValidationException` | catalog composition validation |
| `NavigationService.cs` | `NavigationService`, `INavigationService`, shell navigation types | shell navigation derivation |
| `IModuleCatalogMirrorRepository.cs` | `IModuleCatalogMirrorRepository`, `ModuleCatalogMirrorRow` | catalog-mirror persistence port |
| `ModuleCatalogMirrorSynchronizer.cs` | `ModuleCatalogMirrorSynchronizer`, `MirrorDisplayEntry`, `MirrorValidationReport` | mirror merge/validation |

### 4.2 Shared Identity (`Shared\Identity\`) — 6 files

| File | Type | Role |
|---|---|---|
| `IInternalUserRepository.cs` | `IInternalUserRepository`, `InternalUserRecord`, `BootstrapAdminCreation` | internal-user persistence port |
| `SupabaseAuthPorts.cs` | `ISupabaseAuthAdapter`, `IAdminProvisioningAdapter`, `AuthUser`, `EnsuredAuthUser` | auth / privileged-provisioning ports |
| `IdentityResolutionService.cs` | `IdentityResolutionService`, `ResolvedIdentity` | auth-user → `CurrentUser`/access resolution |
| `AccessTemplateGrantsParser.cs` | `AccessTemplateGrantsParser` | `modules` jsonb → grants parser |
| `BootstrapAdminService.cs` | `BootstrapAdminService`, `BootstrapAdminOptions`, `BootstrapAdminOutcome` | bootstrap-admin provisioning |
| `AmbiguousIdentityException.cs` | `AmbiguousIdentityException` | ambiguous-identity data-integrity marker |

### 4.3 Shared Persistence (`Shared\Persistence\`) — 6 files

| File | Type | Role |
|---|---|---|
| `IDbConnectionFactory.cs` | `IDbConnectionFactory` | Dapper connection factory port |
| `IDbUnitOfWork.cs` | `IDbUnitOfWork` | unit-of-work port |
| `IRepairUnitOfWorkFactory.cs` | `IRepairUnitOfWorkFactory` | shared repair UoW factory port |
| `PersistenceAuthorship.cs` | `IPersistenceAuthorshipAccessor`, `PersistenceAuthorship` | actor + timestamp authorship port |
| `ConcurrencyGuard.cs` | `ConcurrencyGuard` | guarded write helper |
| `SchemaMigrationRequiredException.cs` | `SchemaMigrationRequiredException` | pending-migration marker |

### 4.4 Shared Shell + top-level Shared — 3 files

| File | Type | Role |
|---|---|---|
| `Shared\Shell\IShellService.cs` | `IShellService`, `ShellState` | per-request shell state port |
| `Shared\IAppSettingsReader.cs` | `IAppSettingsReader` | app-settings port (e.g. `main_documents_output_root`) |
| `Shared\IJobOnImageProvider.cs` | `IJobOnImageProvider`, `ImageResolution` | Job On image provider port (Shared) |

## 5. Module Application Objects

Per functional module, the module-specific Application objects and their location (`src\BA.Dmo.Application\Modules\<Module>\`). Detail per module is in each module map; this row-level table is the Application-layer navigation surface.

| Module | Path | Application files | Main Application objects |
|---|---|---|---|
| Job On | `Modules\JobOn\` | 7 | `JobOnService`, `JobOnPdfService`, `JobOnAuthorizationGate`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver` |
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

Application use-case services (classes named `*Service`, plus PDF services). Each is a Domain-dependent use-case surface; constructor dependencies and method surfaces are inventoried per module map.

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

## 7. Interfaces / Ports / Contracts

Application declares ports/contracts that Infrastructure implements (`04_DAPPER_INFRASTRUCTURE.md`). Navigation-level inventory (exact file each).

### 7.1 Module ports (`Modules\*\`) per module

| Module | Interfaces / Ports | File(s) |
|---|---|---|
| Job On | `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver` | `Modules\JobOn\IJobOnRepository.cs`, `IJobOnUserContextRepository.cs`, `IJobOnPdfRenderer.cs`, `IJobOnProductionFolderResolver.cs` |
| Peso | `IPesoRepository`, `IPdfRenderer` | `Modules\Peso\IPesoRepository.cs`, `IPdfRenderer.cs` |
| Pegamentos | `IPegamentoRepository`, `IPegamentoPdfRenderer`, `IJobOnProductionContextLookup` | `Modules\Pegamentos\IPegamentoRepository.cs`, `PegamentoPdfService.cs`, `IJobOnProductionContextLookup.cs` |
| Ferramentas | `IFerramentasRepository`, `IFerramentasRuleLookup`, `IFerramentasIdentityLookup`, `IFerramentasPieceLookup` | `Modules\Ferramentas\IFerramentas*.cs` |
| Armazem | `IArmazemRepository`, `IArmazemRepairMovementPort`, `IToolIdentityResolver` | `Modules\Armazem\IArmazem*.cs`, `IToolIdentityResolver.cs` |
| ReparacaoExterna | `IRepairRepository`, `IToolPieceResolver` | `Modules\ReparacaoExterna\IRepairRepository.cs`, `IToolPieceResolver.cs` |
| ReparacaoInterna | `IReparacaoInternaRepository`, `IJobOnActiveContextLookup` | `Modules\ReparacaoInterna\IReparacaoInternaRepository.cs`, `IJobOnActiveContextLookup.cs` |
| Controlo | `IControloSheetRepository`, `IControloProductionContextLookup` | `Modules\Controlo\IControloSheetRepository.cs`, `IControloProductionContextLookup.cs` |
| Tampoes | `ITampaoRepository`, `ITampoesUnitOfWorkFactory` | `Modules\Tampoes\ITampaoRepository.cs`, `ITampoesUnitOfWorkFactory.cs` |
| Historia | `IHistoriaRepository` | `Modules\Historia\IHistoriaRepository.cs` |
| Boquilhas | `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory` | `Modules\Boquilhas\IBoquilhasRepository.cs`, `IBoquilhasUnitOfWorkFactory.cs` |
| Admin | `IAdminRepository` | `Modules\Admin\IAdminRepository.cs` |

### 7.2 Shared ports (`Shared\`)

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
| `IPdfRenderer` (Peso render port) | port | `Modules\Peso\IPdfRenderer.cs` |
| `IPegamentoPdfRenderer` + document data | port | `Modules\Pegamentos\PegamentoPdfService.cs` |
| `IJobOnPdfRenderer` + `JobOnPdfData/Component/CalibreRow/Verification` records | port + data records | `Modules\JobOn\IJobOnPdfRenderer.cs` |
| `HistoriaQueryResult` / `HistoriaEntryRow` / `HistoriaGroupRow` | projection records | `Modules\Historia\HistoriaModels.cs` |

## 8. Models / DTOs / Projections

Request/command records, filter records, DTO/models and projection records are declared in Application per module (see each module map for full member detail). Navigation-level:

| Module | File | Content kind |
|---|---|---|
| Job On | `Modules\JobOn\JobOnService.cs` | request records (Create/Duplicate/SaveRevision/Transition/Image/Current) + `Unit`, `SnapshotJson` helper |
| Peso | `Modules\Peso\PesoService.cs` | request records + filters (`ControlFilterRequest` etc.) |
| Pegamentos | `Modules\Pegamentos\PegamentoRequests.cs` | request records (Create/Update/AddMeasurement/Close/Filter) |
| Ferramentas | `Modules\Ferramentas\FerramentasRequests.cs`, `FerramentasService.cs` | request records + DTO rows + search/utilisation requests |
| Armazem | `Modules\Armazem\ArmazemRequests.cs` | request records + DTO rows (search/consultation/history/entry) |
| ReparacaoExterna | `Modules\ReparacaoExterna\ReparacaoExternaRequests.cs` | request records + DTOs (exits/items/repairer/line default/history) |
| ReparacaoInterna | `Modules\ReparacaoInterna\ReparacaoInternaRequests.cs` | request records + filter + DTOs (line card/context/history/detail) |
| Controlo | `Modules\Controlo\ControloSheetRequests.cs` | request records (Create/UpdateItems/Submit/Reopen/Decide) + DTOs |
| Tampoes | `Modules\Tampoes\TampaoRequests.cs` | request records + filters + DTOs (field/config/saldo/movement/plano/machine/note) |
| Historia | `Modules\Historia\HistoriaModels.cs` | filter/row/group/query-result records |
| Boquilhas | `Modules\Boquilhas\BqRequests.cs` | request records + DTOs (lote/movement/saldo/discrepancy/repairer/trace) |
| Admin | `Modules\Admin\AdminModels.cs` | Admin user/template/audit rows, requests, audit filter/result records |

There is no dedicated `Models` / `DTO` project; DTOs/models/projections are per-module records co-located with the service/port files.

## 9. Validators / Parsers / Gates

### 9.1 Authorization gates (12)

Each gate enforces the module/capability grant server-side; each fails closed (`Result<Executor, DomainError>`).

| Gate | Module | File |
|---|---|---|
| `JobOnAuthorizationGate` (+ `JobOnExecutor`) | Job On | `Modules\JobOn\JobOnAuthorizationGate.cs` |
| `PesoAuthorizationGate` | Peso | `Modules\Peso\PesoAuthorizationGate.cs` |
| `PegamentoAuthorizationGate` | Pegamentos | `Modules\Pegamentos\PegamentoAuthorizationGate.cs` |
| `FerramentasAuthorizationGate` (+ `FerramentasExecutor`) | Ferramentas | `Modules\Ferramentas\FerramentasAuthorizationGate.cs` |
| `ArmazemAuthorizationGate` (+ `ArmazemExecutor`) | Armazém | `Modules\Armazem\ArmazemAuthorizationGate.cs` |
| `ReparacaoExternaAuthorizationGate` (+ `ReparacaoExternaExecutor`) | Reparação Externa | `Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGate.cs` |
| `ReparacaoInternaAuthorizationGate` (+ `ReparacaoInternaExecutor`) | Reparação Interna | `Modules\ReparacaoInterna\ReparacaoInternaAuthorizationGate.cs` |
| `ControloSheetAuthorizationGate` (+ `ControloSheetExecutor`) | Controlo | `Modules\Controlo\ControloSheetAuthorizationGate.cs` |
| `TampaoAuthorizationGate` (+ `TampaoExecutor`) | Tampões | `Modules\Tampoes\TampaoAuthorizationGate.cs` |
| `HistoriaAuthorizationGate` (+ `HistoriaScope`) | História | `Modules\Historia\HistoriaAuthorizationGate.cs` |
| `BqAuthorizationGate` (+ `BqExecutor`) | Boquilhas | `Modules\Boquilhas\BqAuthorizationGate.cs` |
| `AdminAuthorizationGate` (+ `AdminExecutor`) | Admin | `Modules\Admin\AdminAuthorizationGate.cs` |

### 9.2 Validators / parsers

Application declares no dedicated validator/parser class. Validation and value normalization are delegated to:

- Domain validators consumed by Application services (e.g. `PesoValidator`, `ReportPathValidator` in Domain — `01_DOMAIN.md`, §11);
- the shared `GrantNormalizer` / `AccessResolver` (canonical grant validation, `Shared\Access\`);
- the gates above (capability/module authorization).

### 9.3 Shared Access/Identity Application objects

- `AccessResolver`, `EffectiveAccess`, `FirstPageResolution`/`FirstPageOutcome` — `Shared\Access\AccessResolver.cs`.
- `GrantNormalizer` — `Shared\Access\GrantNormalizer.cs`.
- `AccessTemplateGrantsParser` — `Shared\Identity\AccessTemplateGrantsParser.cs`.
- `ModuleCatalogMirrorSynchronizer` — `Shared\Access\ModuleCatalogMirrorSynchronizer.cs`.

## 10. Direct Application References

Mechanical, source-visible references **inside** Application (references to Domain, and to Application ports governing the Web/Infrastructure surface).

### 10.1 Application → Domain

`BA.Dmo.Application.csproj` references `BA.Dmo.Domain`. Services consume Domain aggregate roots / value objects / services and `Shared.Kernel.Result<T, Error>` / `DomainError`. Representative (not exhaustive):

- `JobOnService` → `JobOn`, `JobOnRevision`, `JobOnActivityResolver`, `JobonModuleCatalog`, `Result<T, DomainError>`.
- `PesoService` → `PesoControl`, `PesoValidator`, `WeightCalculator`.
- every gate → `Domain.Shared.Access.CurrentUser` (`ICurrentUserAccessor`) + `DomainError`.

### 10.2 Application ports → Infrastructure implementations (direct reference boundary)

Each Application port listed in §7 has an Infrastructure implementation registered in `src\BA.Dmo.Web\Program.cs` (Web composition root). The port→implementation edges are:

```
IJobOnRepository             → DapperJobOnRepository
IJobOnUserContextRepository  → DapperJobOnUserContextRepository
IJobOnPdfRenderer            → JobOnPdfRenderer
IJobOnProductionFolderResolver → DapperJobOnProductionFolderResolver
IJobOnImageProvider          → FileSystemJobOnImageProvider
IPesoRepository              → DapperPesoRepository
IPdfRenderer                 → PesoSingleFilePdfRenderer
IPegamentoRepository         → DapperPegamentoRepository
IPegamentoPdfRenderer        → PegamentoPdfRenderer
IJobOnProductionContextLookup → DapperJobOnProductionContextLookup
IFerramentasRepository       → DapperFerramentasRepository
IFerramentasRuleLookup       → DapperFerramentasRuleLookup
IFerramentasIdentityLookup   → DapperFerramentasIdentityLookup
IFerramentasPieceLookup      → DapperFerramentasPieceLookup
IArmazemRepository           → DapperArmazemRepository
IArmazemRepairMovementPort   → DapperArmazemRepairMovementRepository
IToolIdentityResolver        → FerramentasArmazemToolIdentityResolver
IRepairRepository            → DapperRepairRepository
IToolPieceResolver           → FerramentasRepairToolPieceResolver
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
IAdminRepository             → DapperAdminRepository
IAdminProvisioningAdapter    → SupabaseAdminProvisioningAdapter
IModuleCatalogMirrorRepository → DapperModuleCatalogMirrorRepository
IInternalUserRepository      → DapperInternalUserRepository
ISupabaseAuthAdapter         → SupabaseAuthAdapter
```

`IDbConnectionFactory` → `AppDbConnectionFactory` (Infrastructure `Persistence`) and the shared `DapperUnitOfWork`/`ConcurrencyGuard` (`Shared\Persistence`) are the persistence ports implemented in Infrastructure.

### 10.3 Cross-module Application ports (consumer-side)

Application also declares consumer-side cross-module ports implemented by the owning module's adapter:

- `IJobOnActiveContextLookup` (in `Modules\ReparacaoInterna\`) → implemented by Infrastructure, consumed by Reparação Interna.
- `IJobOnProductionContextLookup` (in `Modules\Pegamentos\`) → consumed by Pegamentos.
- `IToolPieceResolver` (in `Modules\ReparacaoExterna\`) → implemented by `FerramentasRepairToolPieceResolver`.
- `IToolIdentityResolver` (in `Modules\Armazem\`) → implemented by `FerramentasArmazemToolIdentityResolver`.

## 11. Target-to-Location Index

| Technical object (kind) | Location (`src\BA.Dmo.Application\`) |
|---|---|
| Application service classes (`*Service`) | `Modules\*\*Service.cs` |
| Authorization gates (`*AuthorizationGate`) | `Modules\*\*AuthorizationGate.cs` |
| Module ports (`I*Repository`, lookups, renderers, UoW factories) | `Modules\*\I*.cs` |
| Request/command + DTO records | `Modules\*\*Requests.cs`, `*Service.cs`, `*Models.cs` |
| Module catalog constants (História) | `Modules\Historia\HistoriaModuleCatalog.cs` |
| Canonical module / page catalogs | `Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` |
| Access resolution / normalizer / navigation / mirror | `Shared\Access\` |
| Identity resolution / auth ports / template parser / bootstrap | `Shared\Identity\` |
| Persistence ports (connection/UoW/authorship) | `Shared\Persistence\` |
| Shell port | `Shared\Shell\IShellService.cs` |
| Top-level shared ports | `Shared\IAppSettingsReader.cs`, `Shared\IJobOnImageProvider.cs` |
| Build metadata | `Properties\AssemblyInfo.cs` |

## 12. Sources Verified

- `src\BA.Dmo.Application\BA.Dmo.Application.csproj` (single `Domain` reference).
- Full recursive listing of `src\BA.Dmo.Application\` (96 `.cs` files, `bin\`/`obj\` excluded) — folder/file enumeration via directory scan.
- `src\BA.Dmo.Application\Shared\*` (Access 10, Identity 6, Persistence 6, Shell 1, top-level Shared 2).
- Per-module type roles cross-checked against `maps\06..15_*.md` (each module map inventories its Application objects and locations).
- `src\BA.Dmo.Web\Program.cs` DI registrations (Application port → Infrastructure implementation mapping).
- No Design/SOT, AI-CONTEXT, or implementation files used as evidence; source-inspection only.

## Counts

- Total Application `.cs` files: **96**
- Module application files: **70** (`Modules\*`, 12 module folders)
- Shared application files: **25** (`Shared\*`)
- Build-metadata file: **1** (`Properties\AssemblyInfo.cs`)
- Application service classes: **17** (`*Service`, incl. 4 Admin services + 3 PDF services)
- Authorization gates: **12** (`*AuthorizationGate`)
- Interfaces / ports / contracts: **40** (public `I*` interfaces across `Modules\*\I*.cs` and `Shared\*\I*.cs`)
- Module-by-module Application file counts: JobOn 7, Peso 4, Pegamentos 7, Ferramentas 7, Armazem 7, ReparacaoExterna 6, ReparacaoInterna 5, Controlo 5, Tampoes 5, Historia 5, Boquilhas 5, Admin 7
- Shared per-plane counts: Access 10, Identity 6, Persistence 6, Shell 1, top-level Shared 2
- Validators/parsers in Application: **0** (delegated to Domain validators + shared `GrantNormalizer`/gates)
- Document/application contracts: **3** PDF renderer ports (`IPdfRenderer`, `IPegamentoPdfRenderer`, `IJobOnPdfRenderer`) + per-module document data records that live in the same files.