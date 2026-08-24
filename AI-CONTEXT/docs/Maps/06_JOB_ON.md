# BA DMO — Job On Technical Map

MAP ID: MAP-06
Module map: JOB ON (first module in the sequence).
Status: COMPLETE.
Scope: module-oriented technical navigation for Job On–specific objects across Domain, Application, Infrastructure, Database, Migrations, Web/static/document and Tests.

This is a MODULE map: it inventories only technical objects that belong to or directly serve the Job On module and does not duplicate the transversal maps (`01_DOMAIN.md`, `02_DATABASE.md`, `03_MIGRATIONS.md`, `04_DAPPER_INFRASTRUCTURE.md`, `05_TESTS.md`).

## Navigation Index

1. Scope
2. Layer Summary
3. Domain Objects
4. Application Objects
5. Application Contracts / Ports
6. Infrastructure Objects
7. Database Objects
8. Migration Touchpoints
9. Web / Routes
10. Static Assets
11. PDF / Document Objects
12. User Context Objects
13. Revision / Component / Verification Objects
14. Job On Tests
15. Direct Job On References
16. External Technical References
17. Source Locations

## 1. Scope

This map covers Job On–specific technical objects:

- `src\BA.Dmo.Domain\Modules\JobOn\`
- `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`
- `src\BA.Dmo.Application\Modules\JobOn\`
- `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`
- cross-module Job On ports consumed by other modules: `IJobOnActiveContextLookup` (Reparação Interna), `IJobOnProductionContextLookup` (Pegamentos)
- `src\BA.Dmo.Infrastructure\Access\` Job On Dapper/Pdf/filesystem objects
- Job On database tables and migration touchpoints
- `src\BA.Dmo.Web\Pages\JobOn\` and Job On API endpoints in `Program.cs`
- `src\BA.Dmo.Web\wwwroot\scripts\jobon.js` and `wwwroot\styles\modules\jobon-layout.css`
- Job On test classes and doubles

It is not a transversal remap, not a functional manual, and not an end-to-end workflow narrative.

## 2. Layer Summary

| Layer | Main Job On Objects | Locations |
|---|---|---|
| Domain | `JobOn`, `JobOnRevision`, `JobOnComponent`, `JobOnComponentField`, `JobOnComponentRow`, `JobOnVerificationOccurrence`, `JobOnFieldOption`, `ComponentFamily`, `JobOnLifecycleState`(+codec), `JobOnActivityResolver`, `JobOnVerificationGenerator`, `VerificationFrequency`, `VerificationRule`, `JobOnResolutionKind`, `JobOnResolution`, `JobonModuleCatalog` | `src\BA.Dmo.Domain\Modules\JobOn\`, `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs` |
| Application | `JobOnService`, `JobOnPdfService`, `JobOnAuthorizationGate`, `JobOnExecutor`, `SnapshotJson`, request records, `HistoricalProductionSummary`, `JobOnUserCurrent`, `Unit` | `src\BA.Dmo.Application\Modules\JobOn\` |
| Application (Shared planes) | `IJobOnImageProvider`, `ImageResolution`, canonical capability ids | `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Application (ports) | `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver` | `src\BA.Dmo.Application\Modules\JobOn\` |
| Infrastructure | `DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `DapperJobOnProductionFolderResolver`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`, `JobOnPdfRenderer`, `FileSystemJobOnImageProvider` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option`, `jobon_user_current` | `database\migrations\`, `database\consolidated_clean_install.sql` |
| Web | `Index.cshtml` + `IndexModel`, `JobOnLineColor`, API endpoints | `src\BA.Dmo.Web\Pages\JobOn\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `jobon.js`, `jobon-layout.css` | `src\BA.Dmo.Web\wwwroot\scripts\`, `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Tests | 11 Job On test classes + fakes/doubles | `tests\BA.Dmo.UnitTests\Modules\JobOn\`, `tests\BA.Dmo.IntegrationTests\JobOn\`, `tests\BA.Dmo.IntegrationTests\Design\`, `tests\BA.Dmo.UnitTests\Modules\Pegamentos\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\JobOn\`; `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs` |
| Application | YES | `src\BA.Dmo.Application\Modules\JobOn\`; shared planes (`Shared\IJobOnImageProvider.cs`, `Shared\Access\CanonicalModuleCatalog.cs`) |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\` (Job On Dapper/Pdf/Filesystem classes) |
| Web | YES | `src\BA.Dmo.Web\Pages\JobOn\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N05_jobon.sql`, `N13_jobon_production_folder.sql`, `N24_jobon_user_current.sql`, `N25_remediation.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\JobOn\`, `tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `tests\BA.Dmo.IntegrationTests\JobOn\`, `tests\BA.Dmo.IntegrationTests\Design\` |

This is technical navigation only; it does not explain workflow. Present = YES/NO reflects whether the module has dedicated objects verified in this map.

## 3. Domain Objects

Namespace `BA.Dmo.Domain.Modules.JobOn`, files under `src\BA.Dmo.Domain\Modules\JobOn\`.

### Entities / records

- **`JobOn`** — aggregate root. `src\BA.Dmo.Domain\Modules\JobOn\JobOn.cs`
  - Members: `Id`, `ProductionCode`, `MachineCode`, `PlannedStartAt`, `PlannedEndAt`, `LifecycleState`, `CurrentRevisionId`, `CopiedFromJobOnId`, `ArticleReferenceId`, `ProductionFolder`, `CreatedAtUtc`, `ClosedAtUtc`, `CancelledAtUtc`, `CancelledBy`, `CancelReason`.
  - Methods: `SetId`, `FromRow`, `SaveRevision`, `DuplicateFrom` (static), `TransitionTo`, `Close`, `Cancel`.
  - Read-only navigations: `CurrentRevision`, `RevisionCount`, `Revisions`, `IsActive`.
  - `FromRow(dynamic)` maps DB row columns (`job_on_id`, `status`, `production_folder`, …) into the aggregate.

- **`JobOnRevision`** — immutable snapshot record. `src\BA.Dmo.Domain\Modules\JobOn\JobOnRevision.cs`
  - Snapshots: `ProductionSnapshot`, `ReferenceSnapshot`, `MachineSnapshot`, `DatesSnapshot`, `TypeSnapshot`, `StopSnapshot`, `WeightSnapshot`, `ProcessSnapshot`, `Sections`, `DropCount`.
  - Members: `ImageAssetId` (logical metadata), `ChangeReason`, `SavedBy`, `SavedAtUtc`.
  - Methods: `CloneWithChanges`, `CreateImageRemovalRevision`; private `CopyToNextRevision`.

- **`JobOnComponent`** — one component per family per revision. `JobOnComponent.cs`
  - Members: `SourceToolId`, `SourceLotId`, `ReferenceSnapshot`, `LotSnapshot`, `TechnicalNameSnapshot`, `PlannedQuantity`, `StockSnapshot`, `UsageSnapshot`, `Notes`, `DisplayOrder`; collections `Fields`, `Rows`, `Verifications`.

- **`JobOnComponentField`** — typed field value (`text/integer/decimal/boolean/date/select`). `JobOnComponentFields.cs`
  - Members: `FieldKey`, `ValueType`, `ValueText`, `ValueInteger`, `ValueDecimal`, `ValueBoolean`, `ValueDate`, `DisplayOrder`.

- **`JobOnComponentRow`** — CAL row entry. `JobOnComponentFields.cs`
  - Members: `ElementLabel`, `ValueDecimal`, `ValueText`, `Unit`, `MachineQuantity`, `DisplayOrder`.

- **`JobOnVerificationOccurrence`** — verification per component. `JobOnVerifications.cs`
  - Members: `SourceRuleId`, `RuleTextSnapshot`, `Status` (`pendente`/`confirmada`/`reposta`/`desativada`), `CompletionSource` (fixed `manual_job_on`), `CompletedBy`, `CompletedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`.

- **`JobOnFieldOption`** — field-options dropdown catalog. `JobOnVerifications.cs`
  - Members: `Family`, `FieldKey`, `OptionValue`, `OptionLabel`, `DisplayOrder`, `Active`.

### Enums / value types

- **`ComponentFamily`** — enum in `ComponentFamily.cs`. Values (names encode the identifiers): `MP_CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO`.
- **`JobOnLifecycleState`** — enum in `JobOnLifecycleState.cs`: `Rascunho, Planeado, EmFabrico, Fechado, Cancelado`.
- **`JobOnLifecycleStateCodec`** — static codec, `JobOnLifecycleState.cs`. Methods: `Parse(string)`, `ToStorage(JobOnLifecycleState)` mapping N05 status text (`rascunho`, `planeado`, `em_fabrico`, `fechado`, `cancelado`).
- **`VerificationFrequency`** — enum in `JobOnVerificationGenerator.cs`: `OncePerLot`, `PerProduction`.
- **`VerificationRule`** — record in `JobOnVerificationGenerator.cs`: `SourceRuleId`, `RuleText`, `Frequency`.
- **`JobOnResolutionKind`** — enum in `JobOnActivityResolver.cs`: `None`, `Single`, `Ambiguous`.
- **`JobOnResolution`** — record in `JobOnActivityResolver.cs`: `Kind`, `Candidates`; static factory `None()`.

### Helper / domain services

- **`JobOnActivityResolver`** — static class, `JobOnActivityResolver.cs`. Method: `Resolve(IReadOnlyList<JobOn> candidates, DateTimeOffset at)` → `JobOnResolution`. Filters active candidates (`IsActive` + `PlannedStartAt.HasValue`), orders by planned start, upper bound = `PlannedEndAt` else next planned start of the same sequence, else unbounded; returns `Single`/`Ambiguous`/`None`.
- **`JobOnVerificationGenerator`** — static class, `JobOnVerificationGenerator.cs`. Method: `Generate(Guid jobOnComponentId, IEnumerable<VerificationRule> rules, DateTime now)` → `IReadOnlyList<JobOnVerificationOccurrence>`; produces `pendente` occurrences with `CompletionSource = manual_job_on`.

### Domain Shared (Job On catalog)

- **`JobonModuleCatalog`** — `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`
  - Module id: `jobon`.
  - Capability ids: `jobon.view`, `jobon.edit`, `jobon.configure`, `jobon.confirmar`.
  - Family constants: `FamilyMp("MP")`, `FamilyMf("MF")`, `FamilyBq("BQ")`, `FamilyPu("PU")`, `FamilyCal("CAL")`, `FamilyAn("AN")`, `FamilyArr("ARR")`, `FamilyPi("PI")`, `FamilyCs("CS")`, `FamilyTp("TP")`, `FamilyFo("FO")`.

## 4. Application Objects

Namespace `BA.Dmo.Application.Modules.JobOn`, files under `src\BA.Dmo.Application\Modules\JobOn\`.

### `JobOnService` (`JobOnService.cs`)

Constructor dependencies: `JobOnAuthorizationGate`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IClock`.

Public methods (each requires a capability via the gate; direct capability identifiers from `JobonModuleCatalog`):

| Method | Requires | Persists via |
|---|---|---|
| `CreateAsync(CreateJobOnRequest)` | `jobon.edit` | `IJobOnRepository.CreateAsync`, `InsertAuditEventAsync` (`jobon.criar`) |
| `DuplicateAsync(DuplicateJobOnRequest)` | `jobon.edit` | `IJobOnRepository.DuplicateAtomicallyAsync` (`jobon.duplicar`) |
| `SaveRevisionAsync(SaveJobOnRevisionRequest)` | `jobon.edit` | `IJobOnRepository.SaveRevisionGraphAsync` (`jobon.guardar`) |
| `TransitionAsync(TransitionJobOnRequest)` | `jobon.edit` | `IJobOnRepository.UpdateLifecycleStateAsync`, `InsertAuditEventAsync` (`jobon.transicao`) |
| `ResolveAsync(line, at)` | `jobon.view` | `IJobOnRepository.GetActiveAsync` → `JobOnActivityResolver.Resolve` |
| `ConfirmVerificationAsync(occurrenceId)` | `jobon.confirmar` | `IJobOnRepository.UpdateVerificationStatusAsync` |
| `AttachImageAsync` / `ReplaceImageAsync` / `RemoveImageAsync` | `jobon.edit` | `IJobOnRepository.InsertImageMutationAsync` (`jobon.imagem.anexar` / `.substituir` / `.remover`) |
| `SetCurrentOpenAsync(jobOnId)` | `jobon.view` | `IJobOnUserContextRepository.SetCurrentAsync` |
| `GetCurrentOpenAsync()` | `jobon.view` | `IJobOnUserContextRepository.GetCurrentAsync` |

### `JobOnPdfService` (`JobOnPdfService.cs`)

Constructor dependencies: `IJobOnRepository`, `JobOnAuthorizationGate`, `IJobOnImageProvider?` (optional).
- Method: `GenerateAsync(IJobOnPdfRenderer renderer, Guid jobOnId, CancellationToken)` → `Result<GeneratedJobOnDocument, DomainError>`. Requires `jobon.view`.
- `GeneratedJobOnDocument` record: `PdfBytes`, `FileName`.
- Internal helpers: `BuildPdfData`, `BuildFileName` (produces `JobOn_{production}_{reference}_{machine}.pdf`), mapping components by `ComponentFamily` into `JobOnPdfData`.

### `JobOnAuthorizationGate` (`JobOnAuthorizationGate.cs`)

Constructor dependency: `ICurrentUserAccessor`.
- Method: `Require(params string[] anyOfCapabilityIds)` → `Result<JobOnExecutor, DomainError>`. Fails closed when no resolved identity.
- `JobOnExecutor` record: `ActorId`, `DisplayName`.

### Request / result records in `JobOnService.cs`

`CreateJobOnRequest`, `DuplicateJobOnRequest`, `SaveJobOnRevisionRequest`, `TransitionJobOnRequest`, `AttachImageRequest`, `ReplaceImageRequest`, `RemoveImageRequest`, `CurrentJobOnRequest`. Also `Unit` record struct (void-like marker).

### Internal snapshot helper

- **`SnapshotJson`** — internal static class, `JobOnService.cs`. Methods: `Production(code)`, `Machine(code)`, `Dates(startAt, endAt)`; builds `{ field: value }` jsonb payloads.

### Other Application Job On objects

- **`HistoricalProductionSummary`** — record in `IJobOnRepository.cs`. Members: `JobOnId`, `ProductionCode`, `ReferenceCode`, `MachineCode`, `PlannedStartAt`, `PlannedEndAt`, `CurrentRevisionNumber`, `TotalRevisionCount`, `LifecycleState`.
- **`JobOnUserCurrent`** — record in `IJobOnUserContextRepository.cs`. Members: `JobOnId`, `ProductionCode`, `Reference`, `MachineCode`, `OpenedAtUtc`.
- **`JobOnPdfData` / `JobOnPdfComponent` / `JobOnPdfCalibreRow` / `JobOnPdfVerification`** — PDF data records in `IJobOnPdfRenderer.cs`.

### Shared Application Plane

- **`IJobOnImageProvider`** + **`ImageResolution`** — `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`. Method: `ResolveAsync(Guid jobOnId, CancellationToken)` → `ImageResolution?` (`Bytes`, `MimeType`).
- **`CanonicalModuleCatalog`** — `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`. Declares the Job On module (`JobonModuleId = "jobon"`, order 5, route `/jobon`) and capability ids `JobonViewCapabilityId = "jobon.view"`, `JobonEditCapabilityId = "jobon.edit"`, `JobonConfigureCapabilityId = "jobon.configure"`, `JobonConfirmarCapabilityId = "jobon.confirmar"`.

## 5. Application Contracts / Ports

| Interface | Main methods | Path |
|---|---|---|
| `IJobOnRepository` | `CreateAsync`, `GetByIdAsync`, `GetActiveAsync`, `GetByProductionCodeAsync`, `UpdateLifecycleStateAsync`, `InsertRevisionAsync`, `GetRevisionsAsync`, `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`, `InsertVerificationsAsync`, `UpdateVerificationStatusAsync`, `GetCurrentRevisionIdAsync`, `UpdateCurrentRevisionAsync`, `InsertAuditEventAsync`, `InsertImageMutationAsync`, `SaveRevisionGraphAsync`, `DuplicateAtomicallyAsync`, `GetHistoricalProductionsAsync` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnRepository.cs` |
| `IJobOnUserContextRepository` | `SetCurrentAsync`, `GetCurrentAsync` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnUserContextRepository.cs` |
| `IJobOnPdfRenderer` | `RenderJobOnDocument(JobOnPdfData)` → `byte[]` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnPdfRenderer.cs` |
| `IJobOnProductionFolderResolver` | `ResolveAsync(Guid jobOnId)` → `string?` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnProductionFolderResolver.cs` |
| `IJobOnImageProvider` | `ResolveAsync(Guid jobOnId)` → `ImageResolution?` | `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs` |
| `IJobOnActiveContextLookup` (Reparação Interna consumer) | `ResolveActiveAsync(line, at)` | `src\BA.Dmo.Application\Modules\ReparacaoInterna\IJobOnActiveContextLookup.cs` |
| `IJobOnProductionContextLookup` (Pegamentos consumer) | `ResolveAsync(Guid jobOnRevisionId)` → `PegamentoProductionContext?` | `src\BA.Dmo.Application\Modules\Pegamentos\IJobOnProductionContextLookup.cs` |

## 6. Infrastructure Objects

All under `src\BA.Dmo.Infrastructure\Access\`.

| Class | Implements | Constructor dependencies | Job On DB objects referenced | Key public methods | Path |
|---|---|---|---|---|---|
| `DapperJobOnRepository` | `IJobOnRepository` | `IDbConnectionFactory` | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event` | full `IJobOnRepository` surface; atomic `SaveRevisionGraphAsync`, `DuplicateAtomicallyAsync`, `InsertImageMutationAsync` via `DapperUnitOfWork.RunAsync`; hydration helpers `GetHydratedRevisionContent`, `MapRevision`, `MapField`, `MapComponentRow`, `MapVerificationOccurrence`, `ParseComponentFamily`, `SerializeWeight`/`ParseWeight` | `DapperJobOnRepository.cs` |
| `DapperJobOnUserContextRepository` | `IJobOnUserContextRepository` | `IDbConnectionFactory` | `jobon_user_current` (upsert via `ON CONFLICT (actor_id)`) | `SetCurrentAsync`, `GetCurrentAsync` | `DapperJobOnUserContextRepository.cs` |
| `DapperJobOnProductionFolderResolver` | `IJobOnProductionFolderResolver` | `IDbConnectionFactory` | `job_on.production_folder` | `ResolveAsync` | `DapperJobOnProductionFolderResolver.cs` |
| `DapperJobOnActiveContextLookup` | `IJobOnActiveContextLookup` | `IDbConnectionFactory`, `IJobOnRepository` | `job_on_revision`, `job_on_component` (MP_CM/MF/BQ lots), `ReparacaoInternaProductionProjection` | `ResolveActiveAsync` | `DapperJobOnActiveContextLookup.cs` |
| `DapperJobOnProductionContextLookup` | `IJobOnProductionContextLookup` | `IDbConnectionFactory` | `job_on_revision`, `job_on_component`, `job_on_component_field` (`field_key = 'nominal'`) for MP_CM/BQ/MF | `ResolveAsync` (fail-closed) | `DapperJobOnProductionContextLookup.cs` |
| `JobOnPdfRenderer` | `IJobOnPdfRenderer` | none (static build) | n/a (byte output) | `RenderJobOnDocument(JobOnPdfData)` → 4-page PDF bytes | `JobOnPdfRenderer.cs` |
| `FileSystemJobOnImageProvider` | `IJobOnImageProvider` | `IJobOnRepository`, `IAppSettingsReader` (`main_documents_output_root`), `IDbConnectionFactory` | reads `job_on.production_folder` + `job_on_revision.image_asset_id`; MIME by extension | `ResolveAsync` | `FileSystemJobOnImageProvider.cs` |

`DapperJobOnRepository` internal accumulated holder `HydratedRevisionChildren` (`Components`, `Verifications`) is confined to that file.

## 7. Database Objects

Source of authority for object shapes: `database\consolidated_clean_install.sql` and `database\migrations\*`. Column/constraint detail: `02_DATABASE.md` (MAP-02).

| Table | Kind | Main technical role | Key references for navigation |
|---|---|---|---|
| `job_on` | table | Job On aggregate root / production sheet | PK `job_on_id`; FK `copied_from_job_on_id` → `job_on`; circular FK `current_revision_id` → `job_on_revision`; unique `uq_job_on_identity (production_code, machine_code) WHERE canceled_at_utc IS NULL` (N25) |
| `job_on_revision` | table | immutable revision snapshot | PK `job_on_revision_id`; FK `job_on_id` → `job_on`; `uq_job_on_revision_number`; append-only trigger `trg_job_on_revision_append_only` (N25) |
| `job_on_component` | table | one component per family per revision | PK `job_on_component_id`; FK `job_on_revision_id` → `job_on_revision`; append-only trigger (N25) |
| `job_on_component_field` | table | typed field values | PK; FK `job_on_component_id` → `job_on_component`; `uq_job_on_component_field`; append-only trigger (N25) |
| `job_on_component_row` | table | CAL row entries | PK; FK `job_on_component_id` → `job_on_component`; append-only trigger (N25) |
| `job_on_verification_occurrence` | table | verification checks | PK; FK `job_on_component_id` → `job_on_component`; `ck_job_on_verification_completed` (N25); `completion_source = 'manual_job_on'` |
| `job_on_audit_event` | table | module audit facts | PK; FK `job_on_id` → `job_on`, FK `job_on_revision_id` → `job_on_revision`; append-only trigger `trg_job_on_audit_event_append_only` |
| `job_on_field_option` | table | field dropdown catalog | PK; `uq_job_on_field_option (family, field_key, option_value)` |
| `jobon_user_current` | table | per-actor current-open Job On context | PK `actor_id` → `internal_users`; FK `job_on_id` → `job_on` (N24; RLS policy `ba_dmo_app_access` in N25) |

Non-Job-On tables with direct Job On references (listed only as external references): `audit_events.job_on_id`, `tool_check_occurrences.job_on_id/job_on_component_id`, `peso_controlos.*`, `pegamento_controlos.*`, `internal_repair_records.*`, `controlo_sheets.*`.

## 8. Migration Touchpoints

| Migration | Job On Object(s) | Technical Change |
|---|---|---|
| `N05_jobon.sql` | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option` | Table creation + constraints/indexes + `fk_job_on_current_revision` + `trg_job_on_audit_event_append_only` |
| `N13_jobon_production_folder.sql` | `job_on.production_folder` | `ADD COLUMN IF NOT EXISTS production_folder text NULL` |
| `N24_jobon_user_current.sql` | `jobon_user_current` | Table creation (actor-scoped current Job On) |
| `N25_remediation.sql` | `job_on` (partial unique index `uq_job_on_identity`, CHECK `ck_job_on_lifecycle_consistent`), `job_on_verification_occurrence` (CHECK `ck_job_on_verification_completed`), `job_on_revision` / `job_on_component` / `job_on_component_field` / `job_on_component_row` (append-only triggers `trg_*_append_only`), `jobon_user_current` (RLS enable + policy + revoke + grant statements) | Partial unique index + CHECK constraints + append-only triggers + RLS/policy/revoke/grant statements for Job On objects |

## 9. Web / Routes

### Page

- `src\BA.Dmo.Web\Pages\JobOn\Index.cshtml` — Razor page, `@page "/jobon"`, `@attribute [Authorize(Policy = CapabilityPolicies.JobonView)]`. Loads `~/scripts/jobon.js` and `~/styles/modules/jobon-layout.css`. Exposes views: `#planeamento` (calendar + list), `#folha` (Job On sheet), `#historico`, `#definicoes` (when `CanConfigure`). Emits `meta[name="jobon-id"]`, calendar `data-record-dates` / `data-record-lines`.
- `src\BA.Dmo.Web\Pages\JobOn\Index.cshtml.cs` — `IndexModel : PageModel`. Constructor deps: `ICurrentUserAccessor`, `IJobOnRepository`, `JobOnService?` (optional). Handler: `OnGetAsync(Guid? id, string? date)`. Builds `PlaneamentoItem` / `VerificationItem` projections and records current-open context. Records: `PlaneamentoItem`, `VerificationItem` (same file).
- `src\BA.Dmo.Web\Pages\JobOn\JobOnLineColor.cs` — static class. Members: `Lines` (B1..C3), `GetColorKey`, `GetColorToken`, `GetLineClass`, `IsValid`. Deterministic machine/line → color-key mapping.

### API endpoints (in `src\BA.Dmo.Web\Program.cs`)

| Route | Technical entry point | Authorization |
|---|---|---|
| `POST /api/jobon/{jobOnId:guid}/image/attach` | `JobOnService.AttachImageAsync` | `CapabilityPolicies.JobonEdit` |
| `POST /api/jobon/{jobOnId:guid}/image/replace` | `JobOnService.ReplaceImageAsync` | `CapabilityPolicies.JobonEdit` |
| `POST /api/jobon/{jobOnId:guid}/image/remove` | `JobOnService.RemoveImageAsync` | `CapabilityPolicies.JobonEdit` |
| `POST /api/jobon/current` | `JobOnService.SetCurrentOpenAsync` (body `CurrentJobOnRequest`) | `CapabilityPolicies.JobonView` |
| `GET /api/jobon/current` | `JobOnService.GetCurrentOpenAsync` | `CapabilityPolicies.JobonView` |
| `POST /api/jobon/{jobOnId:guid}/document` | `JobOnPdfService.GenerateAsync(renderer, jobOnId)` → PDF file | `CapabilityPolicies.JobonView` |

### Web authorization plane

- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` — `CapabilityPolicies` constants: `JobonView = "BaDmo.Capability.jobon.view"`, `JobonEdit = "BaDmo.Capability.jobon.edit"`, `JobonConfigure = "BaDmo.Capability.jobon.configure"`, `JobonConfirmar = "BaDmo.Capability.jobon.confirmar"`. `ModulePolicies.Jobon = "BaDmo.Module.jobon"`.

## 10. Static Assets

- **`src\BA.Dmo.Web\wwwroot\scripts\jobon.js`**
  - Top-level/local functions: `syncCapabilityAttributes`, `esc`, `openDirectoryDb`, `saveDirectoryHandle`, `readDirectoryHandle`, `openView`, `rowsForDate`, `resolveOpenUrl`, `setEditing`, `getCurrentJobOnId`, `persistImageAction`, `showPreviewMessage`, `loadSidepanel`, plus an IIFE (PDF "Exportar").
  - DOM targets/selectors: `.jobon-tabs .tab[data-view]`, `#calendar`/`#jobList`, `#editSheet`/`#saveSheet`/`#sheetMode`/`#jobSheet`, `#inventoryPicker`, `#calRows`/`#addCalRow`, `#catalogRows`/`#addCatalogOption`/`#newCatalogOption`/`#editCatalogOption`/`#disableCatalogOption`, `#imagePreview`/`#job-image-input`/`#link-image-dir-btn`/`#replace-image-btn`/`#remove-image-btn`, `#linePanel`, `meta[name="jobon-id"]`, `#piClampMaterial`.
  - API endpoints called: `/api/jobon/{id}/image/{attach|replace|remove}`, `/api/jobon/{id}/document`, `/api/boquilhas/production-context` (side panel).
  - Uses IndexedDB (`ba-dmo-jobon` / `imageDirectories`) and File System Access API (`showDirectoryPicker`) for image handling.
- **`src\BA.Dmo.Web\wwwroot\styles\modules\jobon-layout.css`**
  - Job On module layout/classes: `.jobon-page`, `.jobon-tabs`, `.jobon-view`, `.planner`, `.calendar`, `.day-card`, `.filters`, `.create-panel`, `.sheet`/`.sheet-head`/`.sheet-body`, `.tool-grid`/`.tool`/`.tool-title`, `.mini-grid`, `.measure-table`, `.quantity`, `.lower-grid`/`.checks`/`.check`, `.history-box`, `.inventory-picker`, `.dmo-line-b1..c3`, `.dmo-line-swatch`, `.dmo-line-chip`, edit-mode gating (`.edit-only`, `.sheet.editing`, `body[data-can-edit-jobon]`, `body[data-can-confirm-verifications]`).
  - References shared `--dmo-*` design tokens (dmo-tokens.css) for colours/spacing; does not redefine canonical components.

## 11. PDF / Document Objects

| Object | Kind | Role | Key members/methods | Path |
|---|---|---|---|---|
| `IJobOnPdfRenderer` | Application contract | renderer port | `RenderJobOnDocument(JobOnPdfData)` → `byte[]` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnPdfRenderer.cs` |
| `JobOnPdfData` | record | document data model | header context, `ImageBytes`/`ImageMimeType`, per-family `JobOnPdfComponent` (Cm, Mf, Tp, Bq, An, Pu, Arr, Pi, Cs, Fo), `CalibreRows`, `Verifications` | same file |
| `JobOnPdfComponent` | record | one tool component | `Reference`, `Lot`, `TechnicalName`, `Usage`, `Notes`, `Stock`, `MachineQuantity`, `Fields` | same file |
| `JobOnPdfCalibreRow` | record | CAL row | `Element`, `Value`, `Quantity` | same file |
| `JobOnPdfVerification` | record | verification display | `RuleText`, `IsChecked`, `StatusText` | same file |
| `JobOnPdfService` | Application service | generation (view gate) | `GenerateAsync(renderer, jobOnId)` → `GeneratedJobOnDocument` | `src\BA.Dmo.Application\Modules\JobOn\JobOnPdfService.cs` |
| `GeneratedJobOnDocument` | record | byte output | `PdfBytes`, `FileName` | same file |
| `JobOnPdfRenderer` | Infrastructure implementation | renders 4 PDF pages | `RenderJobOnDocument(JobOnPdfData)` → PDF bytes; page helpers `RenderFichaDeArtigo`, `RenderJobOnMoldes`, `RenderTrabalhoDeEquipa`, `Escape`, `EncodeStreamContent` | `src\BA.Dmo.Infrastructure\Access\JobOnPdfRenderer.cs` |

## 12. User Context Objects

Per-actor current/open Job On context.

| Object | Kind | Role | Path |
|---|---|---|---|
| `IJobOnUserContextRepository` | Application contract | read/write port for per-actor current-open context | `src\BA.Dmo.Application\Modules\JobOn\IJobOnUserContextRepository.cs` |
| `JobOnUserCurrent` | record | projection of the per-actor current-open context | same file |
| `JobOnService.SetCurrentOpenAsync` / `GetCurrentOpenAsync` | Application methods | stores/reads current Job On context per actor (requires `jobon.view`) | `src\BA.Dmo.Application\Modules\JobOn\JobOnService.cs` |
| `DapperJobOnUserContextRepository` | Infrastructure | upsert/read over `jobon_user_current` | `src\BA.Dmo.Infrastructure\Access\DapperJobOnUserContextRepository.cs` |
| `jobon_user_current` | table | persistence (per-`actor_id` row) | N24 |

## 13. Revision / Component / Verification Objects

### Revision

- `JobOnRevision` (Domain record) — `JobOnRevision.cs`; `JobOn.SaveRevision`, `JobOnRevision.CloneWithChanges`, `JobOnRevision.CreateImageRemovalRevision`.
- Persistence: `job_on_revision` table; repository methods `InsertRevisionAsync`, `SaveRevisionGraphAsync`, `InsertImageMutationAsync`, `DuplicateAtomicallyAsync`, `GetRevisionsAsync`.

### Component

- `JobOnComponent`, `JobOnComponentField`, `JobOnComponentRow` (Domain records) — `JobOnComponent.cs`, `JobOnComponentFields.cs`.
- `ComponentFamily` enum — `ComponentFamily.cs`.
- Persistence: `job_on_component`, `job_on_component_field`, `job_on_component_row`; repository `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`.

### Verification

- `JobOnVerificationOccurrence`, `JobOnFieldOption` (Domain records) — `JobOnVerifications.cs`.
- Generator: `JobOnVerificationGenerator.Generate` (Domain), plus `VerificationFrequency`/`VerificationRule`.
- Persistence: `job_on_verification_occurrence`, `job_on_field_option`; repository `InsertVerificationsAsync`, `UpdateVerificationStatusAsync`.
- Application: `JobOnService.ConfirmVerificationAsync` (requires `jobon.confirmar`).

## 14. Job On Tests

### Unit tests — `tests\BA.Dmo.UnitTests\Modules\JobOn\`

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnServiceTests` | unit (fakes) | `JobOnService` use cases (create/duplicate/save-revision/transition/resolve/confirm/image ops, gates, snapshot completeness) | `JobOnServiceTests.cs` |
| `JobOnDomainTests` | unit | `JobOn` domain (transitions, cancellation, duplication, revision immutability), `JobOnLifecycleStateCodec` | `JobOnDomainTests.cs` |
| `JobOnActivityResolverTests` | unit | `JobOnActivityResolver.Resolve` (single/none/ambiguous/end-boundary/null-end) | `JobOnActivityResolverTests.cs` |
| `JobOnVerificationGeneratorTests` | unit | `JobOnVerificationGenerator.Generate` (occurrence materialization, frequency, invalid rules) | `JobOnVerificationGeneratorTests.cs` |
| `JobOnUserContextTests` | unit (fakes) | `JobOnService.SetCurrentOpenAsync`/`GetCurrentOpenAsync`, per-actor context | `JobOnUserContextTests.cs` |
| `JobOnPdfTests` | unit (fakes + `TestPdfRenderer`) | `JobOnPdfService.GenerateAsync`, `JobOnPdfService.BuildFileName` | `JobOnPdfTests.cs` |
| `JobOnRevisionImmutabilityIntegrationTests` | service-level integration (in-memory) | Job On → Peso → Pegamentos revision immutability (rev B does not move rev A) | `JobOnRevisionImmutabilityIntegrationTests.cs` |

### Unit tests — `tests\BA.Dmo.UnitTests\Modules\Pegamentos\` (Job On production-folder)

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnProductionFolderResolverTests` | unit | `IJobOnProductionFolderResolver` consumption by Pegamentos | `JobOnProductionFolderResolverTests.cs` |

### Integration tests

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnLandingTests` | integration (WebApplicationFactory, fake `IJobOnRepository`) | `/jobon` planeamento calendar + list + colour keys + current-open context | `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLandingTests.cs` |
| `JobOnLineColorMappingTests` | integration | `JobOnLineColor` machine/line → colour key mapping | `tests\BA.Dmo.IntegrationTests\JobOn\JobOnLineColorMappingTests.cs` |
| `JobOnScriptSafetyGuardTests` | static-file integration | `jobon.js` `esc()` guard on catalog-label interpolation | `tests\BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs` |

### Test doubles / helpers

| Double | Role | Path |
|---|---|---|
| `FakeJobOnRepository` | in-memory fake of `IJobOnRepository` (tracks JobOns, Revisions, Components, Fields, Rows, Verifications, AuditEvents, LifecycleUpdates, CurrentRevisionUpdates, VerificationUpdates) | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` |
| `FakeJobOnUserContextRepository` | in-memory fake of `IJobOnUserContextRepository` | `tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| `FakeJobOnProductionFolderResolver` | in-memory fake of `IJobOnProductionFolderResolver` | `tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakeJobOnProductionFolderResolver.cs` |
| `TestPdfRenderer` | captures `JobOnPdfData`, returns minimal PDF | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| `NullJobOnImageProvider` | always-null `IJobOnImageProvider` | `tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| Local identity/clock accessors | `FakeCurrentUserAccessor`, `PdfTestIdentityAccessor`, `LocalFakeCurrentUserAccessor`, `FixedClock`, `PdfTestClock`, `LocalFixedClock`, `TestClock` | within respective test files |

## 15. Direct Job On References

Mechanical, source-visible relationships only.

```
JobOnService → IJobOnRepository, IJobOnUserContextRepository, JobOnAuthorizationGate, IClock
JobOnService → JobonModuleCatalog (capability ids), SnapshotJson, JobOnActivityResolver, JobOnEntity(Domain)
JobOnService.ResolveAsync → IJobOnRepository.GetActiveAsync → JobOnActivityResolver.Resolve
JobOnPdfService → IJobOnRepository, JobOnAuthorizationGate, IJobOnImageProvider?
JobOnPdfService.GenerateAsync → IJobOnPdfRenderer.RenderJobOnDocument

IJobOnRepository → DapperJobOnRepository (implementation)
DapperJobOnRepository → job_on, job_on_revision, job_on_component, job_on_component_field,
                        job_on_component_row, job_on_verification_occurrence, job_on_audit_event
DapperJobOnRepository → DapperUnitOfWork, IDbConnectionFactory

IJobOnUserContextRepository → DapperJobOnUserContextRepository (implementation)
DapperJobOnUserContextRepository → jobon_user_current

IJobOnProductionFolderResolver → DapperJobOnProductionFolderResolver (implementation)
DapperJobOnProductionFolderResolver → job_on.production_folder

IJobOnActiveContextLookup → DapperJobOnActiveContextLookup (implementation)
DapperJobOnActiveContextLookup → IJobOnRepository, job_on_revision, job_on_component, ReparacaoInternaProductionProjection

IJobOnProductionContextLookup → DapperJobOnProductionContextLookup (implementation)
DapperJobOnProductionContextLookup → job_on_revision, job_on_component, job_on_component_field (nominal)

IJobOnImageProvider → FileSystemJobOnImageProvider (implementation)
FileSystemJobOnImageProvider → IJobOnRepository, IAppSettingsReader, job_on.production_folder, job_on_revision.image_asset_id

IJobOnPdfRenderer → JobOnPdfRenderer (implementation)
JobOnPdfRenderer → JobOnPdfData

CapabilityPolicies.JobonX → CanonicalModuleCatalog.JobonXCapabilityId
CanonicalModuleCatalog (Job On) → JobonModuleCatalog constants (capability strings)
```

## 16. External Technical References

Mechanical direct references from Job On source into other technical scopes.

| Job On Object | External Technical Reference | Reference Type |
|---|---|---|
| `JobOn` / `JobOnComponent` | `SourceToolId`, `SourceLotId` (logical links) | FK-like logical reference (snapshot-carrying) |
| `JobOnVerificationOccurrence` | `SourceRuleId` (`tool_check_rules`) | logical rule reference |
| `JobOnComponentField` | `field_key = 'nominal'` read by Pegamentos context lookup | field-key contract |
| `IJobOnActiveContextLookup` (consumer-side) | `ReparacaoInternaProductionProjection`, `InternalRepairContextResolution` | resolved by Reparação Interna module |
| `IJobOnProductionContextLookup` (consumer-side) | `PegamentoProductionContext`, `PegamentoComponentKey` | resolved by Pegamentos module |
| `DapperJobOnActiveContextLookup` | `job_on_component.family IN ('MP_CM','MF','BQ')` with `source_lot_id` (tool lot identity) | tool-consumption reference |
| `FileSystemJobOnImageProvider` | `IAppSettingsReader` → `main_documents_output_root` | application setting reference |
| `CanonicalModuleCatalog` / `CapabilityPolicies` / `CanonicalPageCatalog` | module/capability/page catalog entries for `jobon` | catalog reference |

## 17. Source Locations

- Domain: `src\BA.Dmo.Domain\Modules\JobOn\`; `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`
- Application: `src\BA.Dmo.Application\Modules\JobOn\`; `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`; consumer-side ports `src\BA.Dmo.Application\Modules\ReparacaoInterna\IJobOnActiveContextLookup.cs`, `src\BA.Dmo.Application\Modules\Pegamentos\IJobOnProductionContextLookup.cs`
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\` (Job On Dapper/Pdf/filesystem classes)
- Database: `database\migrations\N05_jobon.sql`, `N13_jobon_production_folder.sql`, `N24_jobon_user_current.sql`, `N25_remediation.sql`; consolidated `database\consolidated_clean_install.sql`
- Web: `src\BA.Dmo.Web\Pages\JobOn\`; `src\BA.Dmo.Web\Program.cs` (Job On API endpoints); `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- Static assets: `src\BA.Dmo.Web\wwwroot\scripts\jobon.js`; `src\BA.Dmo.Web\wwwroot\styles\modules\jobon-layout.css`
- Tests: `tests\BA.Dmo.UnitTests\Modules\JobOn\`, `tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `tests\BA.Dmo.IntegrationTests\JobOn\`, `tests\BA.Dmo.IntegrationTests\Design\`

## Counts (confidently derivable)

- Domain Job On files: 9 (`ComponentFamily.cs`, `JobOn.cs`, `JobOnActivityResolver.cs`, `JobOnComponent.cs`, `JobOnComponentFields.cs`, `JobOnLifecycleState.cs`, `JobOnRevision.cs`, `JobOnVerificationGenerator.cs`, `JobOnVerifications.cs`) + `JobonModuleCatalog.cs` (Shared).
- Application Job On files: 7 under `Modules\JobOn\` + `IJobOnImageProvider.cs` (Shared) + 2 consumer-side Job On ports.
- Infrastructure Job On files: 7 (`DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `DapperJobOnProductionFolderResolver`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`, `JobOnPdfRenderer`, `FileSystemJobOnImageProvider`).
- Job On DB objects: 9 tables.
- Job On migration touchpoints: 4 files (N05, N13, N24, N25).
- Job On test classes: 11 (7 under `Modules\JobOn`, 1 under `Modules\Pegamentos`, 3 under `IntegrationTests\JobOn` and `IntegrationTests\Design`) targeting Job On objects; Job On-specific doubles: `FakeJobOnRepository`, `FakeJobOnUserContextRepository`, `FakeJobOnProductionFolderResolver`.

## Sources Verified

Primary evidence: current Job On-specific source from `src\`, `database\`, `tests\`, plus `maps\00_INDEX.md` as mapping contract/registry. No historical AI-CONTEXT, Design/SOT, screenshots, or audit reports were used as evidence.