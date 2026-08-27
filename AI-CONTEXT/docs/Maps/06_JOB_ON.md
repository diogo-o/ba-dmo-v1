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
12. Article Reference Image Objects
13. User Context Objects
14. Revision / Component / Verification Objects
15. Job On Tests
16. Direct Job On References
17. External Technical References
18. Source Locations

Related maps: `00_INDEX.md` (registry) · `01_DOMAIN.md` · `02_DATABASE.md` · `03_MIGRATIONS.md` · `04_DAPPER_INFRASTRUCTURE.md` · `05_TESTS.md` · `19_APPLICATION.md` · `20_WEB.md` · `14_HISTORIA.md` (cross-module audit view of Job On facts) · `10_BOQUILHAS.md` (line-context endpoint consumed by `jobon.js`) · `16_USERS_ACCESS.md` (Job On module/capability grants).

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
| Application | `JobOnService`, `JobOnPdfService`, `JobOnAuthorizationGate`, `JobOnExecutor`, `SnapshotJson`, request records, `HistoricalProductionSummary`, `JobOnUserCurrent`, `Unit`; reference-image slice `ArticleReferenceImage`, `IArticleReferenceImageRepository`, `ArticleReferenceImageRules` | `src\BA.Dmo.Application\Modules\JobOn\` |
| Application (Shared planes) | `IJobOnImageProvider`, `ImageResolution`, canonical capability ids | `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Application (ports) | `IJobOnRepository`, `IJobOnUserContextRepository`, `IJobOnPdfRenderer`, `IJobOnProductionFolderResolver`, `IArticleReferenceImageRepository` | `src\BA.Dmo.Application\Modules\JobOn\` |
| Infrastructure | `DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `DapperJobOnProductionFolderResolver`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`, `DapperArticleReferenceImageRepository`, `JobOnPdfRenderer`, `FileSystemJobOnImageProvider` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option`, `jobon_user_current`, `article_reference_images` | `database\migrations\`, `database\consolidated_clean_install.sql` |
| Web | `Index.cshtml` + `IndexModel`, `JobOnLineColor`, API endpoints | `src\BA.Dmo.Web\Pages\JobOn\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `jobon.js`, `jobon-layout.css` | `src\BA.Dmo.Web\wwwroot\scripts\`, `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Tests | 13 Job On test classes + fakes/doubles | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\JobOn\`; `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs` |
| Application | YES | `src\BA.Dmo.Application\Modules\JobOn\`; shared planes (`Shared\IJobOnImageProvider.cs`, `Shared\Access\CanonicalModuleCatalog.cs`) |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\` (Job On Dapper/Pdf/Filesystem classes) |
| Web | YES | `src\BA.Dmo.Web\Pages\JobOn\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N05_jobon.sql`, `N13_jobon_production_folder.sql`, `N24_jobon_user_current.sql`, `N25_remediation.sql`, `N29_jobon_reference_images.sql`, `N30_jobon_reference_image_updated_by_index.sql` |
| Tests | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\` |

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
  - Members: `ImageAssetId` (dormant legacy column per N29 — the active image association is master-Reference owned, see §12), `ChangeReason`, `SavedBy`, `SavedAtUtc`.
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

Constructor dependencies: `JobOnAuthorizationGate`, `IJobOnRepository`, `IJobOnUserContextRepository`, `IClock`, `IArticleReferenceImageRepository?` (optional, reference-owned article images).

Public methods (each requires a capability via the gate; direct capability identifiers from `JobonModuleCatalog`):

| Method | Requires | Persists via |
|---|---|---|
| `CreateAsync(CreateJobOnRequest)` | `jobon.edit` | `IJobOnRepository.CreateAsync`, `InsertAuditEventAsync` (`jobon.criar`) |
| `DuplicateAsync(DuplicateJobOnRequest)` | `jobon.edit` | `IJobOnRepository.DuplicateAtomicallyAsync` (`jobon.duplicar`) |
| `SaveRevisionAsync(SaveJobOnRevisionRequest)` | `jobon.edit` | `IJobOnRepository.SaveRevisionGraphAsync` (`jobon.guardar`) |
| `TransitionAsync(TransitionJobOnRequest)` | `jobon.edit` | `IJobOnRepository.UpdateLifecycleStateAsync`, `InsertAuditEventAsync` (`jobon.transicao`) |
| `ResolveAsync(line, at)` | `jobon.view` | `IJobOnRepository.GetActiveAsync` → `JobOnActivityResolver.Resolve` |
| `ConfirmVerificationAsync(occurrenceId)` | `jobon.confirmar` | `IJobOnRepository.UpdateVerificationStatusAsync` |
| `AttachImageAsync` / `ReplaceImageAsync` / `RemoveImageAsync` | `jobon.edit` | `IArticleReferenceImageRepository.SetAsync` / `.RemoveAsync` (atomic with `job_on_audit_event` fact; audit codes `jobon.referencia.imagem.anexar` / `.substituir` / `.remover`; no Job On revision is created) |
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

### Reference-owned article image (N29/N30) — in `ArticleReferenceImage.cs`

The master Article/Reference image association converged by N29. Job On **consumes** the association; immutable production revisions no longer own it (`job_on_revision.image_asset_id` is dormant legacy).

- **`ArticleReferenceImage`** — record: `ReferenceCode`, `ImageAssetId`, `UpdatedBy?`, `UpdatedAtUtc?`.
- **`IArticleReferenceImageRepository`** — Application port. Methods:
  - `GetAsync(referenceCode)` → `ArticleReferenceImage?` (reads `article_reference_images`).
  - `SetAsync(association, jobOnId, jobOnRevisionId, eventType, beforeImageAssetId, actorId, occurredAtUtc)` — atomic upsert + `job_on_audit_event` fact (`DapperUnitOfWork.RunAsync`; the revision id is attribution context only, never mutated).
  - `RemoveAsync(referenceCode, jobOnId, jobOnRevisionId, eventType, beforeImageAssetId, actorId, occurredAtUtc)` — atomic delete + audit fact.
- **`ArticleReferenceImageRules`** — static parsing/validation:
  - `ExtractReferenceCode(snapshot)` — parses a `reference_snapshot` jsonb (string, or object keys `article_reference`/`reference`/`code`/`value`) → normalized reference code.
  - `NormalizeReferenceCode(code)` — trim + `ToUpperInvariant`.
  - `TryNormalizeImageAssetId(assetId, out normalized)` — rejects rooted/path-like/`..`/unsafe names; allows only `jpg/jpeg/png/gif/webp/bmp` extensions (mirrors `ck_article_reference_images_asset`).
- Error codes produced by the image use cases: `JOBON_IMAGE_STORE_UNAVAILABLE`, `JOBON_IMAGE_INVALID`, `JOBON_REFERENCE_MISSING`, `JOBON_NO_IMAGE`, `JOBON_NO_REVISION`.
- Write semantics: attach/replace/remove never create or change a Job On revision (pinned by `JobOnImageWebApiTests` fake that throws on `InsertImageMutationAsync`); binary stays in the configured company image directory; only the validated file name is persisted.

### Shared Application Plane

- **`IJobOnImageProvider`** + **`ImageResolution`** — `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`. Method: `ResolveAsync(Guid jobOnId, CancellationToken)` → `ImageResolution?` (`Bytes`, `MimeType`).
- **`CanonicalModuleCatalog`** — `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`. Declares the Job On module (`JobonModuleId = "jobon"`, order 5, route `/jobon`) and capability ids `JobonViewCapabilityId = "jobon.view"`, `JobonEditCapabilityId = "jobon.edit"`, `JobonConfigureCapabilityId = "jobon.configure"`, `JobonConfirmarCapabilityId = "jobon.confirmar"`.

## 5. Application Contracts / Ports

| Interface | Main methods | Path |
|---|---|---|
| `IJobOnRepository` | `CreateAsync`, `GetByIdAsync`, `GetActiveAsync`, `GetByProductionCodeAsync`, `UpdateLifecycleStateAsync`, `InsertRevisionAsync`, `GetRevisionsAsync`, `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`, `InsertVerificationsAsync`, `UpdateVerificationStatusAsync`, `GetCurrentRevisionIdAsync`, `UpdateCurrentRevisionAsync`, `InsertAuditEventAsync`, `InsertImageMutationAsync` (dormant — no production callers since N29), `SaveRevisionGraphAsync`, `DuplicateAtomicallyAsync`, `GetHistoricalProductionsAsync` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnRepository.cs` |
| `IArticleReferenceImageRepository` | `GetAsync`, `SetAsync`, `RemoveAsync` | `src\BA.Dmo.Application\Modules\JobOn\ArticleReferenceImage.cs` |
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
| `DapperArticleReferenceImageRepository` | `IArticleReferenceImageRepository` | `IDbConnectionFactory` | `article_reference_images` (read `GetAsync`; upsert `SetAsync`; delete `RemoveAsync`), `job_on_audit_event` (append-only audit fact in the same `DapperUnitOfWork.RunAsync` transaction) | `GetAsync`, `SetAsync`, `RemoveAsync` | `DapperArticleReferenceImageRepository.cs` |
| `JobOnPdfRenderer` | `IJobOnPdfRenderer` | none (static build) | n/a (byte output) | `RenderJobOnDocument(JobOnPdfData)` → 4-page PDF bytes; embeds the reference image on page 4 via `TryBuildPdfImage` (`BuildJpegImage`/`BuildPngImage`, `PdfImage` record) | `JobOnPdfRenderer.cs` |
| `FileSystemJobOnImageProvider` | `IJobOnImageProvider` | `IJobOnRepository`, `IArticleReferenceImageRepository`, `IAppSettingsReader` (`GetOutputRootAsync` → `main_documents_output_root` app setting) | reads `JobOn.CurrentRevision.ReferenceSnapshot` (reference code) + `article_reference_images.image_asset_id`; file resolved under the configured output root; MIME by extension | `ResolveAsync` (never throws; missing chain → null) | `FileSystemJobOnImageProvider.cs` |

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
| `article_reference_images` | table | master Article/Reference image association (N29) | PK `reference_code`; `updated_by` FK → `internal_users (actor_id)`; CHECK `ck_article_reference_images_reference` (upper-trimmed non-empty) + `ck_article_reference_images_asset` (non-empty, no path/`..`, image extension only); RLS enabled + policy `ba_dmo_app_access` + grants (N29); covering index `ix_article_reference_images_updated_by` (N30) |

Legacy/dormant: `job_on_revision.image_asset_id` (N05) is NOT dropped; per N29 its value is dormant — the active image association is owned by `article_reference_images`.

Non-Job-On tables with direct Job On references (listed only as external references): `audit_events.job_on_id`, `tool_check_occurrences.job_on_id/job_on_component_id`, `peso_controlos.*`, `pegamento_controlos.*`, `internal_repair_records.*`, `controlo_sheets.*`.

## 8. Migration Touchpoints

| Migration | Job On Object(s) | Technical Change |
|---|---|---|
| `N05_jobon.sql` | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option` | Table creation + constraints/indexes + `fk_job_on_current_revision` + `trg_job_on_audit_event_append_only` |
| `N13_jobon_production_folder.sql` | `job_on.production_folder` | `ADD COLUMN IF NOT EXISTS production_folder text NULL` |
| `N24_jobon_user_current.sql` | `jobon_user_current` | Table creation (actor-scoped current Job On) |
| `N25_remediation.sql` | `job_on` (partial unique index `uq_job_on_identity`, CHECK `ck_job_on_lifecycle_consistent`), `job_on_verification_occurrence` (CHECK `ck_job_on_verification_completed`), `job_on_revision` / `job_on_component` / `job_on_component_field` / `job_on_component_row` (append-only triggers `trg_*_append_only`), `jobon_user_current` (RLS enable + policy + revoke + grant statements) | Partial unique index + CHECK constraints + append-only triggers + RLS/policy/revoke/grant statements for Job On objects |
| `N29_jobon_reference_images.sql` | `article_reference_images` (new table) + dormant `job_on_revision.image_asset_id` (not dropped) | Converges the article image from per-revision metadata to a Reference-owned association: fail-closed pre-checks (unreadable Reference / unsafe file name / conflicting current images), one-time promotion of legacy current associations (`DISTINCT ON (reference_code)`), RLS enable + policy + grants; `job_on_revision.image_asset_id` left dormant |
| `N30_jobon_reference_image_updated_by_index.sql` | `article_reference_images.updated_by` | Additive covering index `ix_article_reference_images_updated_by` on the `updated_by` FK (post-N29 advisor check) |

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
| `GET /api/jobon/{jobOnId:guid}/image` | `IJobOnImageProvider.ResolveAsync` → `Results.File(bytes, mimeType)` (404 when no image) | `CapabilityPolicies.JobonView` |
| `POST /api/jobon/current` | `JobOnService.SetCurrentOpenAsync` (body `CurrentJobOnRequest`) | `CapabilityPolicies.JobonView` |
| `GET /api/jobon/current` | `JobOnService.GetCurrentOpenAsync` | `CapabilityPolicies.JobonView` |
| `POST /api/jobon/{jobOnId:guid}/document` | `JobOnPdfService.GenerateAsync(renderer, jobOnId)` → PDF file | `CapabilityPolicies.JobonView` |

### Web authorization plane

- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` — `CapabilityPolicies` constants: `JobonView = "BaDmo.Capability.jobon.view"`, `JobonEdit = "BaDmo.Capability.jobon.edit"`, `JobonConfigure = "BaDmo.Capability.jobon.configure"`, `JobonConfirmar = "BaDmo.Capability.jobon.confirmar"`. `ModulePolicies.Jobon = "BaDmo.Module.jobon"`.

## 10. Static Assets

- **`src\BA.Dmo.Web\wwwroot\scripts\jobon.js`**
  - Top-level/local functions: `syncCapabilityAttributes`, `esc`, `openView`, `rowsForDate`, `resolveOpenUrl`, `setEditing`, `getCurrentJobOnId`, `persistImageAction`, `showEmptyImage`, `showServerImage`, `loadSidepanel`, plus an IIFE (PDF "Exportar").
  - DOM targets/selectors: `.jobon-tabs .tab[data-view]`, `#calendar`/`#jobList`, `#editSheet`/`#saveSheet`/`#sheetMode`/`#jobSheet`, `#inventoryPicker`, `#calRows`/`#addCalRow`, `#catalogRows`/`#addCatalogOption`/`#newCatalogOption`/`#editCatalogOption`/`#disableCatalogOption`, `#imagePreview`/`#article-reference-image`/`#article-image-empty`/`#image-directory-status`/`#job-image-input`/`#link-image-dir-btn`/`#replace-image-btn`/`#remove-image-btn`, `#linePanel`, `meta[name="jobon-id"]`, `#piClampMaterial`.
  - API endpoints called: `/api/jobon/{id}/image/{attach|replace|remove}`, `/api/jobon/{id}/document`, `/api/boquilhas/production-context` (side panel). The preview `<img id="article-reference-image">` is server-rendered against `GET /api/jobon/{id}/image`.
  - Image handling (post-N29): plain `<input type="file">` selection (`#job-image-input`, `accept="image/*"`); the browser selects a file from the configured company image directory, only its validated file name is persisted; NO IndexedDB (`ba-dmo-jobon`/`imageDirectories`) and NO File System Access API (`showDirectoryPicker`) remain — removed since the 2026-08-23 verification.
- **`src\BA.Dmo.Web\wwwroot\styles\modules\jobon-layout.css`**
  - Job On module layout/classes: `.jobon-page`, `.jobon-tabs`, `.jobon-view`, `.planner`, `.calendar`, `.day-card`, `.filters`, `.create-panel`, `.sheet`/`.sheet-head`/`.sheet-body`, `.tool-grid`/`.tool`/`.tool-title`, `.mini-grid`, `.measure-table`, `.quantity`, `.lower-grid`/`.checks`/`.check`, `.history-box`, `.inventory-picker`, `.dmo-line-b1..c3`, `.dmo-line-swatch`, `.dmo-line-chip`, edit-mode gating (`.edit-only`, `#jobSheet.editing`, `body[data-can-edit-jobon]`, `body[data-can-confirm-verifications]`).
  - References shared `--dmo-*` design tokens (dmo-tokens.css) for colours/spacing; does not redefine canonical components.

## 11. PDF / Document Objects

| Object | Kind | Role | Key members/methods | Path |
|---|---|---|---|---|
| `IJobOnPdfRenderer` | Application contract | renderer port | `RenderJobOnDocument(JobOnPdfData)` → `byte[]` | `src\BA.Dmo.Application\Modules\JobOn\IJobOnPdfRenderer.cs` |
| `JobOnPdfData` | record | document data model | header context, `ImageBytes`/`ImageMimeType`, per-family `JobOnPdfComponent` (Cm, Mf, Tp, Bq, An, Pu, Arr, Pi, Cs, Fo), `CalibreRows`, `Verifications` | same file |
| `JobOnPdfComponent` | record | one tool component | `Reference`, `Lot`, `TechnicalName`, `Usage`, `Notes`, `Stock`, `MachineQuantity`, `Fields` | same file |
| `JobOnPdfCalibreRow` | record | CAL row | `Element`, `Value`, `Quantity` | same file |
| `JobOnPdfVerification` | record | verification display | `RuleText`, `IsChecked`, `StatusText` | same file |
| `JobOnPdfService` | Application service | generation (view gate) | `GenerateAsync(renderer, jobOnId)` → `GeneratedJobOnDocument`; consumes `IJobOnImageProvider?` when present and fills `JobOnPdfData.ImageBytes`/`ImageMimeType` (current master Reference association — a historical revision does not own an image snapshot) | `src\BA.Dmo.Application\Modules\JobOn\JobOnPdfService.cs` |
| `GeneratedJobOnDocument` | record | byte output | `PdfBytes`, `FileName` | same file |
| `JobOnPdfRenderer` | Infrastructure implementation | renders 4 PDF pages | `RenderJobOnDocument(JobOnPdfData)` → PDF bytes; page helpers `RenderFichaDeArtigo`, `RenderJobOnMoldes`, `RenderTrabalhoDeEquipa`, `Escape`, `EncodeStreamContent`; image helpers `TryBuildPdfImage` (`BuildJpegImage`/`BuildPngImage`, `PdfImage` record) embed the Reference image on page 4 only (object-fit-contain) | `src\BA.Dmo.Infrastructure\Access\JobOnPdfRenderer.cs` |

## 12. Article Reference Image Objects

Reference-owned article image slice (N29/N30). Job On revisions never own the active image; the master association is keyed by normalized Article/Reference.

| Object | Kind | Role | Key members/methods | Path |
|---|---|---|---|---|
| `ArticleReferenceImage` | Application record | master association | `ReferenceCode`, `ImageAssetId`, `UpdatedBy`, `UpdatedAtUtc` | `src\BA.Dmo.Application\Modules\JobOn\ArticleReferenceImage.cs` |
| `IArticleReferenceImageRepository` | Application port | read/write port | `GetAsync`, `SetAsync`, `RemoveAsync` | same file |
| `ArticleReferenceImageRules` | Application helper | parsing/validation | `ExtractReferenceCode`, `NormalizeReferenceCode`, `TryNormalizeImageAssetId` | same file |
| `JobOnService.AttachImageAsync` / `ReplaceImageAsync` / `RemoveImageAsync` | Application use cases | require `jobon.edit` | audit events `jobon.referencia.imagem.anexar` / `.substituir` / `.remover` | `src\BA.Dmo.Application\Modules\JobOn\JobOnService.cs` |
| `JobOnPdfService` / `IJobOnImageProvider` | Application consumers | PDF print + UI preview | `GenerateAsync` fills `ImageBytes`/`ImageMimeType`; `ResolveAsync(jobOnId)` | `JobOnPdfService.cs`, `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs` |
| `DapperArticleReferenceImageRepository` | Infrastructure | Dapper persistence | reads/writes `article_reference_images`; atomic audit fact into `job_on_audit_event` via `DapperUnitOfWork.RunAsync` | `src\BA.Dmo.Infrastructure\Access\DapperArticleReferenceImageRepository.cs` |
| `FileSystemJobOnImageProvider` | Infrastructure | `IJobOnImageProvider` impl | Job On → Reference → association → file under `main_documents_output_root`; MIME by extension; never throws | `src\BA.Dmo.Infrastructure\Access\FileSystemJobOnImageProvider.cs` |
| `article_reference_images` | database table | persistence | PK `reference_code`; CHECKs; RLS `ba_dmo_app_access`; index `ix_article_reference_images_updated_by` | `database\migrations\N29_jobon_reference_images.sql`, `N30_jobon_reference_image_updated_by_index.sql` |
| `GET /api/jobon/{jobOnId:guid}/image` | Web endpoint | binary preview | `IJobOnImageProvider.ResolveAsync` → `Results.File`; `CapabilityPolicies.JobonView` | `src\BA.Dmo.Web\Program.cs` |
| `JobOnImageWebApiTests` | integration tests | reference-image API | `AttachAndRemove_ChangeReferenceAssociation_WithoutAddingRevision`, `UnsafePath_IsRejected_AndWritesNothing` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\JobOnImageWebApiTests.cs` |

## 13. User Context Objects

Per-actor current/open Job On context.

| Object | Kind | Role | Path |
|---|---|---|---|
| `IJobOnUserContextRepository` | Application contract | read/write port for per-actor current-open context | `src\BA.Dmo.Application\Modules\JobOn\IJobOnUserContextRepository.cs` |
| `JobOnUserCurrent` | record | projection of the per-actor current-open context | same file |
| `JobOnService.SetCurrentOpenAsync` / `GetCurrentOpenAsync` | Application methods | stores/reads current Job On context per actor (requires `jobon.view`) | `src\BA.Dmo.Application\Modules\JobOn\JobOnService.cs` |
| `DapperJobOnUserContextRepository` | Infrastructure | upsert/read over `jobon_user_current` | `src\BA.Dmo.Infrastructure\Access\DapperJobOnUserContextRepository.cs` |
| `jobon_user_current` | table | persistence (per-`actor_id` row) | N24 |

## 14. Revision / Component / Verification Objects

### Revision

- `JobOnRevision` (Domain record) — `JobOnRevision.cs`; `JobOn.SaveRevision`, `JobOnRevision.CloneWithChanges`, `JobOnRevision.CreateImageRemovalRevision`.
- Persistence: `job_on_revision` table; repository methods `InsertRevisionAsync`, `SaveRevisionGraphAsync`, `DuplicateAtomicallyAsync`, `GetRevisionsAsync`.
- `IJobOnRepository.InsertImageMutationAsync` (and its `DapperJobOnRepository` implementation) persists a revision + current-revision advance + audit in one transaction (TD-23) but has NO production callers since N29 — the image use cases route through `IArticleReferenceImageRepository` (see §12). `JobOnImageWebApiTests.FakeJobOnRepository` throws on it to pin the no-revision-created contract. `LEGACY CANDIDATE — NEEDS AUDIT`.

### Component

- `JobOnComponent`, `JobOnComponentField`, `JobOnComponentRow` (Domain records) — `JobOnComponent.cs`, `JobOnComponentFields.cs`.
- `ComponentFamily` enum — `ComponentFamily.cs`.
- Persistence: `job_on_component`, `job_on_component_field`, `job_on_component_row`; repository `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`.

### Verification

- `JobOnVerificationOccurrence`, `JobOnFieldOption` (Domain records) — `JobOnVerifications.cs`.
- Generator: `JobOnVerificationGenerator.Generate` (Domain), plus `VerificationFrequency`/`VerificationRule`.
- Persistence: `job_on_verification_occurrence`, `job_on_field_option`; repository `InsertVerificationsAsync`, `UpdateVerificationStatusAsync`.
- Application: `JobOnService.ConfirmVerificationAsync` (requires `jobon.confirmar`).

## 15. Job On Tests

### Unit tests — `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\`

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnServiceTests` | unit (fakes) | `JobOnService` use cases (create/duplicate/save-revision/transition/resolve/confirm, reference-image attach/replace/remove via `IArticleReferenceImageRepository`, gates, snapshot completeness, legacy per-revision `image_asset_id` NOT persisted) | `JobOnServiceTests.cs` |
| `JobOnDomainTests` | unit | `JobOn` domain (transitions, cancellation, duplication, revision immutability), `JobOnLifecycleStateCodec` | `JobOnDomainTests.cs` |
| `JobOnActivityResolverTests` | unit | `JobOnActivityResolver.Resolve` (single/none/ambiguous/end-boundary/null-end) | `JobOnActivityResolverTests.cs` |
| `JobOnVerificationGeneratorTests` | unit | `JobOnVerificationGenerator.Generate` (occurrence materialization, frequency, invalid rules) | `JobOnVerificationGeneratorTests.cs` |
| `JobOnUserContextTests` | unit (fakes) | `JobOnService.SetCurrentOpenAsync`/`GetCurrentOpenAsync`, per-actor context, view-only open, canonical six-line support | `JobOnUserContextTests.cs` |
| `JobOnPdfTests` | unit (fakes + `TestPdfRenderer`) | `JobOnPdfService.GenerateAsync` (4 pages, family grouping, CAL rows, ports, notes), `BuildFileName`, image-provider consumption (`ImageProvider_ResolvesNull_WhenNoImage`, `GenerateAsync_ConsumesReferenceImageProvider_IntoPrintProjection`) | `JobOnPdfTests.cs` |
| `JobOnRevisionImmutabilityIntegrationTests` | service-level integration (in-memory) | Job On → Peso → Pegamentos revision immutability (rev B does not move rev A) | `JobOnRevisionImmutabilityIntegrationTests.cs` |

### Unit tests — `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\` (Job On production-folder)

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnProductionFolderResolverTests` | unit | `IJobOnProductionFolderResolver` consumption by Pegamentos | `JobOnProductionFolderResolverTests.cs` |

### Integration tests — `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\`

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnImageWebApiTests` | integration (WebApplicationFactory `ImageFixture`; fake `IArticleReferenceImageRepository` + `IJobOnRepository`) | reference-image API: attach/remove change the Reference association without adding a revision; unsafe path rejected and writes nothing | `JobOnImageWebApiTests.cs` |
| `JobOnPdfRendererTests` | integration | `JobOnPdfRenderer` raw bytes: reference image drawn exactly once on the required page, no image object without bytes, PNG embedded with PDF-compatible filter | `JobOnPdfRendererTests.cs` |
| `JobOnLandingTests` | integration (WebApplicationFactory, fake `IJobOnRepository`) | `/jobon` planeamento calendar + list + colour keys + current-open context | `JobOnLandingTests.cs` |
| `JobOnLineColorMappingTests` | integration | `JobOnLineColor` machine/line → colour key mapping | `JobOnLineColorMappingTests.cs` |

### Integration tests — `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\`

| Test class | Kind | Direct target | Path |
|---|---|---|---|
| `JobOnScriptSafetyGuardTests` | static-file integration | `jobon.js` `esc()` guard on catalog-label interpolation, article-image file-selection (no persisted browser directory handles), cross-module links | `JobOnScriptSafetyGuardTests.cs` |

### Test doubles / helpers

| Double | Role | Path |
|---|---|---|
| `FakeJobOnRepository` | in-memory fake of `IJobOnRepository` (tracks JobOns, Revisions, Components, Fields, Rows, Verifications, AuditEvents, LifecycleUpdates, CurrentRevisionUpdates, VerificationUpdates) | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnRepository.cs` |
| `FakeJobOnUserContextRepository` | in-memory fake of `IJobOnUserContextRepository` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\FakeJobOnUserContextRepository.cs` |
| `FakeArticleReferenceImageRepository` | in-memory fake of `IArticleReferenceImageRepository` (tracks `Associations` + `AuditFacts`) | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\FakeArticleReferenceImageRepository.cs` |
| `FakeJobOnProductionFolderResolver` | in-memory fake of `IJobOnProductionFolderResolver` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\FakeJobOnProductionFolderResolver.cs` |
| `TestPdfRenderer` | captures `JobOnPdfData`, returns minimal PDF | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| `NullJobOnImageProvider` | always-null `IJobOnImageProvider` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\JobOnPdfTests.cs` |
| `JobOnImageWebApiTests.ImageFixture` / `FakeArticleImageRepository` / `FakeJobOnRepository` (nested) | `WebApplicationFactory<Program>` replacing auth/identity + reference-image + Job On repositories | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\JobOnImageWebApiTests.cs` |
| Local identity/clock accessors | `FakeCurrentUserAccessor`, `PdfTestIdentityAccessor`, `LocalFakeCurrentUserAccessor`, `FixedClock`, `PdfTestClock`, `LocalFixedClock`, `TestClock` | within respective test files |

## 16. Direct Job On References

Mechanical, source-visible relationships only.

```
JobOnService → IJobOnRepository, IJobOnUserContextRepository, IArticleReferenceImageRepository?, JobOnAuthorizationGate, IClock
JobOnService → JobonModuleCatalog (capability ids), SnapshotJson, JobOnActivityResolver, JobOnEntity(Domain), ArticleReferenceImageRules
JobOnService.ResolveAsync → IJobOnRepository.GetActiveAsync → JobOnActivityResolver.Resolve
JobOnService.{Attach,Replace,Remove}ImageAsync → IArticleReferenceImageRepository.{SetAsync,RemoveAsync}
JobOnPdfService → IJobOnRepository, JobOnAuthorizationGate, IJobOnImageProvider?
JobOnPdfService.GenerateAsync → IJobOnPdfRenderer.RenderJobOnDocument

IJobOnRepository → DapperJobOnRepository (implementation)
DapperJobOnRepository → job_on, job_on_revision, job_on_component, job_on_component_field,
                        job_on_component_row, job_on_verification_occurrence, job_on_audit_event
DapperJobOnRepository → DapperUnitOfWork, IDbConnectionFactory

IArticleReferenceImageRepository → DapperArticleReferenceImageRepository (implementation)
DapperArticleReferenceImageRepository → article_reference_images, job_on_audit_event

IJobOnUserContextRepository → DapperJobOnUserContextRepository (implementation)
DapperJobOnUserContextRepository → jobon_user_current

IJobOnProductionFolderResolver → DapperJobOnProductionFolderResolver (implementation)
DapperJobOnProductionFolderResolver → job_on.production_folder

IJobOnActiveContextLookup → DapperJobOnActiveContextLookup (implementation)
DapperJobOnActiveContextLookup → IJobOnRepository, job_on_revision, job_on_component, ReparacaoInternaProductionProjection

IJobOnProductionContextLookup → DapperJobOnProductionContextLookup (implementation)
DapperJobOnProductionContextLookup → job_on_revision, job_on_component, job_on_component_field (nominal)

IJobOnImageProvider → FileSystemJobOnImageProvider (implementation)
FileSystemJobOnImageProvider → IJobOnRepository, IArticleReferenceImageRepository, IAppSettingsReader,
                                job_on.current revision reference_snapshot, article_reference_images.image_asset_id

IJobOnPdfRenderer → JobOnPdfRenderer (implementation)
JobOnPdfRenderer → JobOnPdfData (embeds reference image on page 4)

CapabilityPolicies.JobonX → CanonicalModuleCatalog.JobonXCapabilityId
CanonicalModuleCatalog (Job On) → JobonModuleCatalog constants (capability strings)
```

## 17. External Technical References

Mechanical direct references from Job On source into other technical scopes.

| Job On Object | External Technical Reference | Reference Type |
|---|---|---|
| `JobOn` / `JobOnComponent` | `SourceToolId`, `SourceLotId` (logical links) | FK-like logical reference (snapshot-carrying) |
| `JobOnVerificationOccurrence` | `SourceRuleId` (`tool_check_rules`) | logical rule reference |
| `JobOnComponentField` | `field_key = 'nominal'` read by Pegamentos context lookup | field-key contract |
| `IJobOnActiveContextLookup` (consumer-side) | `ReparacaoInternaProductionProjection`, `InternalRepairContextResolution` | resolved by Reparação Interna module |
| `IJobOnProductionContextLookup` (consumer-side) | `PegamentoProductionContext`, `PegamentoComponentKey` | resolved by Pegamentos module |
| `DapperJobOnActiveContextLookup` | `job_on_component.family IN ('MP_CM','MF','BQ')` with `source_lot_id` (tool lot identity) | tool-consumption reference |
| `FileSystemJobOnImageProvider` | `IAppSettingsReader` → `GetOutputRootAsync` (`main_documents_output_root` app setting, N11 `app_settings`) | application setting reference |
| `ArticleReferenceImageRules.TryNormalizeImageAssetId` | `ck_article_reference_images_asset` CHECK (N29) | DB constraint mirror (application-side validation duplicates the DB guard) |
| `jobon_user_current` / `article_reference_images` | `internal_users.actor_id` FK | shared identity reference |
| `CanonicalModuleCatalog` / `CapabilityPolicies` / `CanonicalPageCatalog` | module/capability/page catalog entries for `jobon` | catalog reference |

## 18. Source Locations

- Domain: `src\BA.Dmo.Domain\Modules\JobOn\`; `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`
- Application: `src\BA.Dmo.Application\Modules\JobOn\` (incl. `ArticleReferenceImage.cs`); `src\BA.Dmo.Application\Shared\IJobOnImageProvider.cs`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`; consumer-side ports `src\BA.Dmo.Application\Modules\ReparacaoInterna\IJobOnActiveContextLookup.cs`, `src\BA.Dmo.Application\Modules\Pegamentos\IJobOnProductionContextLookup.cs`
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\` (Job On Dapper/Pdf/filesystem classes, incl. `DapperArticleReferenceImageRepository.cs`, `FileSystemJobOnImageProvider.cs`)
- Database: `database\migrations\N05_jobon.sql`, `N13_jobon_production_folder.sql`, `N24_jobon_user_current.sql`, `N25_remediation.sql`, `N29_jobon_reference_images.sql`, `N30_jobon_reference_image_updated_by_index.sql`; consolidated `database\consolidated_clean_install.sql`
- Web: `src\BA.Dmo.Web\Pages\JobOn\`; `src\BA.Dmo.Web\Program.cs` (Job On API endpoints); `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- Static assets: `src\BA.Dmo.Web\wwwroot\scripts\jobon.js`; `src\BA.Dmo.Web\wwwroot\styles\modules\jobon-layout.css`
- Tests: `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Pegamentos\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\`

## Counts (confidently derivable)

- Domain Job On files: 9 (`ComponentFamily.cs`, `JobOn.cs`, `JobOnActivityResolver.cs`, `JobOnComponent.cs`, `JobOnComponentFields.cs`, `JobOnLifecycleState.cs`, `JobOnRevision.cs`, `JobOnVerificationGenerator.cs`, `JobOnVerifications.cs`) + `JobonModuleCatalog.cs` (Shared).
- Application Job On files: 8 under `Modules\JobOn\` (incl. `ArticleReferenceImage.cs`) + `IJobOnImageProvider.cs` (Shared) + 2 consumer-side Job On ports.
- Infrastructure Job On files: 8 (`DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `DapperJobOnProductionFolderResolver`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`, `DapperArticleReferenceImageRepository`, `JobOnPdfRenderer`, `FileSystemJobOnImageProvider`).
- Job On DB objects: 10 tables.
- Job On migration touchpoints: 6 files (N05, N13, N24, N25, N29, N30).
- Job On test classes: 13 (7 under `Modules\JobOn`, 1 under `Modules\Pegamentos`, 4 under `IntegrationTests\JobOn`, 1 under `IntegrationTests\Design`) targeting Job On objects; Job On-specific doubles: `FakeJobOnRepository`, `FakeJobOnUserContextRepository`, `FakeArticleReferenceImageRepository`, `FakeJobOnProductionFolderResolver`.

## Sources Verified

Primary evidence: current Job On-specific source from `src\`, `database\`, `AI-CONTEXT\docs\tests\`, plus `maps\00_INDEX.md` as mapping contract/registry. No historical AI-CONTEXT, Design/SOT, screenshots, or audit reports were used as evidence.

Reconciled IN PLACE at HEAD `8478308` (2026-08-27): reference-image slice (N29/N30) mapped; image use cases re-routed from `IJobOnRepository.InsertImageMutationAsync` to `IArticleReferenceImageRepository`; `FileSystemJobOnImageProvider` dependencies corrected; `jobon.js` IndexedDB / File System Access API claims removed; PDF image embedding documented; new tests (`JobOnImageWebApiTests`, `JobOnPdfRendererTests`) and `FakeArticleReferenceImageRepository` added; test paths moved to `AI-CONTEXT\docs\tests\`.

Files inspected for this pass: `src\BA.Dmo.Domain\Modules\JobOn\*.cs`; `src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`; `src\BA.Dmo.Application\Modules\JobOn\{JobOnService,JobOnPdfService,ArticleReferenceImage,IJobOnRepository,IJobOnUserContextRepository,JobOnAuthorizationGate,IJobOnPdfRenderer}.cs`; `src\BA.Dmo.Application\Shared\{IJobOnImageProvider,IAppSettingsReader}.cs`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`; `src\BA.Dmo.Infrastructure\Access\{DapperJobOnRepository,DapperJobOnUserContextRepository,DapperJobOnProductionFolderResolver,DapperJobOnActiveContextLookup,DapperJobOnProductionContextLookup,DapperArticleReferenceImageRepository,JobOnPdfRenderer,FileSystemJobOnImageProvider}.cs`; `src\BA.Dmo.Web\Program.cs`; `src\BA.Dmo.Web\Pages\JobOn\{Index.cshtml,Index.cshtml.cs,JobOnLineColor.cs}`; `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`; `src\BA.Dmo.Web\wwwroot\scripts\jobon.js`; `src\BA.Dmo.Web\wwwroot\styles\modules\jobon-layout.css`; `database\migrations\{N05,N13,N24,N25,N29,N30}*.sql`; `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\JobOn\*.cs`; `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\JobOn\*.cs`; `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs`.